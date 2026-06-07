using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Personliggjør en <see cref="ExerciseTargetProfile"/> (og forsidens pitch-målsone)
    /// mot brukerens egen <see cref="UserVoiceProfile"/> — stilmål, kalibrert komfortsone
    /// og recovery-status. Erstatter ikke <see cref="PitchTargetZonePolicy"/>: policyen
    /// gir den statiske per-difficulty-sonen som denne adapteren personlig-tilpasser
    /// (eller faller tilbake til når brukeren ikke er kalibrert).
    ///
    /// KLINISKE PRINSIPPER som er hardkodet inn i justeringene:
    ///   • Resonans er primærdimensjonen for feminisering — IKKE pitch-jag. Stilmål
    ///     justerer derfor resonansmål, ALDRI faste Hz-mål. DarkFeminine senker
    ///     resonansmålet (mørk feminin stemme skal ikke presses mot lys resonans).
    ///   • Personlig kalibrert komfortsone overstyrer profilens generiske pitch-grenser
    ///     (brukerens egen anatomi > generisk default).
    ///   • Recovery > Progression: når recovery er aktiv KRYMPER vi alle krav og kan
    ///     aldri øke noe. Recovery-justering anvendes ETTER stiljustering.
    ///
    /// Ren og testbar (ingen DB-/IO-avhengigheter) — registreres som DI-singleton.
    /// </summary>
    public sealed class TargetProfileAdapter
    {
        // ── Resonans-clamps (0–1 normalisert) ────────────────────────────────────
        private const double ResonanceCeiling      = 0.85;  // tak for elevasjon av min
        private const double ResonanceFloorStyle   = 0.20;  // gulv ved stil-senking
        private const double ResonanceFloorRecovery = 0.15; // gulv ved recovery-senking

        private const double FeminineMinDelta      = 0.03;
        private const double DarkFeminineMinDelta  = -0.08;
        private const double DarkFeminineMaxDelta  = -0.05;
        private const double AndrogynousMinDelta   = -0.04;

        // ── Recovery-clamps ──────────────────────────────────────────────────────
        private const double RecoveryResonanceMinDelta = -0.06;
        private const double RecoveryStabilityDelta    = -0.08;
        private const double RecoveryStabilityFloor    = 0.20;
        private const double RecoveryHoldDelta         = -2.0;
        private const double RecoveryPitchShrinkFactor = 0.15; // 15 % innkrymping mot midten

        /// <summary>
        /// Personliggjør en øvelses-målprofil mot brukerens stilmål, kalibrerte
        /// komfortsone og recovery-status. Returnerer en NY validert profil; muterer
        /// aldri inn-profilen.
        ///
        /// <paramref name="user"/> == null ⇒ <paramref name="baseProfile"/> returneres
        /// uendret (bakoverkompatibelt — uregistrerte brukere mister ingenting).
        /// </summary>
        public ExerciseTargetProfile Personalize(
            ExerciseTargetProfile baseProfile,
            UserVoiceProfile? user,
            bool recoveryActive)
        {
            if (baseProfile == null) throw new ArgumentNullException(nameof(baseProfile));
            if (user == null) return baseProfile;   // bakoverkompatibelt

            // Start fra base-verdiene; juster i normaliserte mellomvariabler.
            double resonanceMin = baseProfile.TargetResonanceMin;
            double resonanceMax = baseProfile.TargetResonanceMax;
            double stability    = baseProfile.StabilityThreshold;
            double holdSeconds  = baseProfile.RequiredHoldSeconds;
            double? minPitch    = baseProfile.MinPitch;
            double? maxPitch    = baseProfile.MaxPitch;

            // ── 1) Stilbasert resonansjustering (resonans = primærdimensjon) ────────
            switch (user.PreferredVoiceStyle)
            {
                case VoiceStyleGoal.Feminine:
                    // Behold/eleviér lett — clamp slik at vi aldri presser over taket.
                    resonanceMin = Math.Min(ResonanceCeiling, resonanceMin + FeminineMinDelta);
                    break;

                case VoiceStyleGoal.DarkFeminine:
                    // Mørk feminin stemme skal IKKE presses mot lys resonans: senk målet.
                    resonanceMin = Math.Max(ResonanceFloorStyle, resonanceMin + DarkFeminineMinDelta);
                    resonanceMax = resonanceMax + DarkFeminineMaxDelta;
                    break;

                case VoiceStyleGoal.Androgynous:
                    resonanceMin = Math.Max(ResonanceFloorStyle, resonanceMin + AndrogynousMinDelta);
                    break;

                case VoiceStyleGoal.Situational:
                case VoiceStyleGoal.Custom:
                default:
                    // Uendret — ingen kanonisk resonans-retning.
                    break;
            }

            // Hold max ≥ min selv etter en mulig nedjustering av max (DarkFeminine).
            if (resonanceMax < resonanceMin)
                resonanceMax = resonanceMin;

            // ── 2) Personlig kalibrert komfortsone overstyrer generiske pitch-grenser ─
            // Kun for pitch-baserte øvelser, og kun når begge grensene er kalibrert.
            if (baseProfile.UsesPitch
                && user.ComfortZoneMinPitch.HasValue
                && user.ComfortZoneMaxPitch.HasValue)
            {
                minPitch = user.ComfortZoneMinPitch.Value;
                maxPitch = user.ComfortZoneMaxPitch.Value;
            }

            // ── 3) Recovery: krymp ALLE krav (etter stil; kan aldri øke noe) ────────
            if (recoveryActive)
            {
                resonanceMin = Math.Max(ResonanceFloorRecovery, resonanceMin + RecoveryResonanceMinDelta);
                stability    = Math.Max(RecoveryStabilityFloor, stability + RecoveryStabilityDelta);
                holdSeconds  = Math.Max(0, holdSeconds + RecoveryHoldDelta);

                // resonanceMax aldri øket; hold konsistens om min nå overstiger max.
                if (resonanceMax < resonanceMin)
                    resonanceMax = resonanceMin;

                if (baseProfile.UsesPitch && minPitch.HasValue && maxPitch.HasValue)
                {
                    var shrunk = ShrinkZoneTowardCenter(minPitch.Value, maxPitch.Value, RecoveryPitchShrinkFactor);
                    minPitch = shrunk.min;
                    maxPitch = shrunk.max;
                }
            }

            // ── Bygg ny profil à la ClampToRecoveryFloor-mønsteret (kopier alle nøkler) ─
            var personalized = new ExerciseTargetProfile
            {
                UsesResonance = baseProfile.UsesResonance,
                UsesPitch     = baseProfile.UsesPitch,
                UsesStability = baseProfile.UsesStability,
                UsesIntensity = baseProfile.UsesIntensity,

                ClinicalPurposeKey         = baseProfile.ClinicalPurposeKey,
                PhysicalFocusKey           = baseProfile.PhysicalFocusKey,
                CommonMistakesKey          = baseProfile.CommonMistakesKey,
                SafetyInfoKey              = baseProfile.SafetyInfoKey,
                FeedbackModeKey            = baseProfile.FeedbackModeKey,
                ThresholdStrategyKey       = baseProfile.ThresholdStrategyKey,
                IndicatorPackageSummaryKey = baseProfile.IndicatorPackageSummaryKey,

                MinPitch            = minPitch,
                MaxPitch            = maxPitch,
                TargetResonanceMin  = resonanceMin,
                TargetResonanceMax  = resonanceMax,
                StabilityThreshold  = stability,
                RequiredHoldSeconds = holdSeconds
            };

            personalized.Validate();
            return personalized;
        }

        /// <summary>
        /// Personliggjør forsidens pitch-målsone. Samme regler som <see cref="Personalize"/>
        /// for pitch: personlig kalibrert sone hvis tilgjengelig, ellers
        /// <paramref name="baseZone"/>; recovery ⇒ 15 % innkrymping mot midten.
        ///
        /// Ingen stil-basert pitch-endring — det finnes ingen faste Hz-mål per stil
        /// (klinisk prinsipp: resonans, ikke pitch, bærer feminiseringen).
        /// Statisk fordi den ikke har noen tilstand og kalles fra hot paths i MainViewModel.
        /// </summary>
        public static (double min, double max) PersonalizePitchZone(
            (double min, double max) baseZone,
            UserVoiceProfile? user,
            bool recoveryActive)
        {
            double min = baseZone.min;
            double max = baseZone.max;

            // Personlig kalibrert sone overstyrer generisk default.
            if (user?.ComfortZoneMinPitch is double calibratedMin
                && user.ComfortZoneMaxPitch is double calibratedMax
                && calibratedMax > calibratedMin)
            {
                min = calibratedMin;
                max = calibratedMax;
            }

            if (recoveryActive)
            {
                var shrunk = ShrinkZoneTowardCenter(min, max, RecoveryPitchShrinkFactor);
                min = shrunk.min;
                max = shrunk.max;
            }

            return (min, max);
        }

        /// <summary>
        /// Krymper et [min, max]-intervall <paramref name="factor"/> (f.eks. 0.15 = 15 %)
        /// inn mot midtpunktet — symmetrisk. Reduserer alltid bredden; øker aldri sonen.
        /// </summary>
        private static (double min, double max) ShrinkZoneTowardCenter(double min, double max, double factor)
        {
            var center = (min + max) / 2.0;
            var newMin = min + (center - min) * factor;
            var newMax = max - (max - center) * factor;
            return (newMin, newMax);
        }
    }
}
