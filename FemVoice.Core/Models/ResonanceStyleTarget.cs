using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Stil-bevisst resonansmål-sett. Eier per-<see cref="VoiceStyleGoal"/> formant-,
    /// spacing- og spektral-tyngdepunkt-optima (samt F1/F2-bånd) som
    /// resonans-scoringen sikter MOT — ikke bare tersklene den måles mot.
    ///
    /// ROTPROBLEM dette løser (spec forbyr «universal female target»):
    /// ResonanceProxyEngine og ResonansScoringService scoret tidligere ALLTID mot et
    /// lyst/fremre feminint klangideal og tok aldri inn brukerens stilmål. En
    /// DarkFeminine-bruker ble derfor presset mot feminin klang i selve feedback-
    /// RETNINGEN, ikke bare i terskelbåndet. Ved å gjøre selve målpunktene
    /// stil-avhengige flytter vi scoringsretningen, ikke bare terskelen.
    ///
    /// KRITISK BAKOVERKOMPATIBILITET: <see cref="VoiceStyleGoal.Feminine"/> (og
    /// Situational/Custom/default) gir NØYAKTIG de historiske konstantene for begge
    /// konsumenter — ingen atferdsendring for det vanlige tilfellet. Alle eksisterende
    /// ResonanceProxyEngine-/ResonansScoringService-tester forblir grønne.
    ///
    /// Klinisk sikkerhetshierarki: dette er beskrivende klang-mål (Coaching/UI-nivå),
    /// aldri noe som kan overstyre Safety/Health/Recovery. Mørkere mål gjør KUN klangen
    /// mindre fremre/lys; de utvider ikke noe helse- eller sikkerhetskrav.
    /// </summary>
    public sealed class ResonanceStyleTarget
    {
        // ── Felles formant-/spektral-optima (delt semantikk på tvers av konsumenter) ──

        /// <summary>Optimal F1 (Hz). ResonanceProxyEngine-default = 320; scoring-default = 330.</summary>
        public double F1Optimal { get; }

        /// <summary>Optimal F2 (Hz). Lavere = mindre fremre/lys klang.</summary>
        public double F2Optimal { get; }

        /// <summary>Optimal F3 (Hz). Brukes av ResonanceProxyEngine.</summary>
        public double F3Optimal { get; }

        /// <summary>Optimal F2−F1-spacing (Hz). Brukes av ResonanceProxyEngine.</summary>
        public double SpacingOptimal { get; }

        /// <summary>Optimalt spektralt tyngdepunkt (Hz). Lavere = mørkere/mindre lys klang.</summary>
        public double CentroidOptimal { get; }

        // ── F1/F2-bånd (brukes av ResonansScoringService for «innenfor band»-bonus) ──

        /// <summary>Nedre F1-bånd (Hz).</summary>
        public double F1Min { get; }

        /// <summary>Øvre F1-bånd (Hz).</summary>
        public double F1Max { get; }

        /// <summary>Nedre F2-bånd (Hz).</summary>
        public double F2Min { get; }

        /// <summary>Øvre F2-bånd (Hz).</summary>
        public double F2Max { get; }

        private ResonanceStyleTarget(
            double f1Optimal, double f2Optimal, double f3Optimal,
            double spacingOptimal, double centroidOptimal,
            double f1Min, double f1Max, double f2Min, double f2Max)
        {
            F1Optimal = f1Optimal;
            F2Optimal = f2Optimal;
            F3Optimal = f3Optimal;
            SpacingOptimal = spacingOptimal;
            CentroidOptimal = centroidOptimal;
            F1Min = f1Min;
            F1Max = f1Max;
            F2Min = f2Min;
            F2Max = f2Max;
        }

        // ── Historiske ResonanceProxyEngine-konstanter (rør IKKE — bakoverkompatibilitet) ──
        private const double EngineF1Optimal = 320.0;
        private const double EngineF2Optimal = 2300.0;
        private const double EngineF3Optimal = 2900.0;
        private const double EngineSpacingOptimal = 1900.0;
        private const double EngineCentroidOptimal = 2500.0;

        // ── Historiske ResonansScoringService-konstanter (rør IKKE — bakoverkompatibilitet) ──
        private const double ScoringF1Optimal = 330.0;
        private const double ScoringF2Optimal = 2200.0;
        private const double ScoringCentroidOptimal = 2500.0;
        private const double ScoringF1Min = 280.0;
        private const double ScoringF1Max = 450.0;
        private const double ScoringF2Min = 1800.0;
        private const double ScoringF2Max = 2600.0;

        // ── Stil-deltaer (Hz). Mørkere mål = lavere F2/F3/centroid (mindre fremre/lys). ──
        // Konservative skift som matcher retningen i TargetProfileAdapter (DarkFeminine
        // sterkere enn Androgynous), men her på formant-/centroid-nivå i stedet for
        // normaliserte terskelbånd.
        private const double DarkFeminineF2Delta       = -250.0;
        private const double DarkFeminineF3Delta       = -200.0;
        private const double DarkFeminineCentroidDelta = -300.0;
        private const double DarkFeminineSpacingDelta  = -250.0; // F2 senkes ⇒ spacing ned
        private const double DarkFeminineBandDelta     = -150.0; // F2-båndet skyves ned

        private const double AndrogynousF2Delta        = -120.0;
        private const double AndrogynousF3Delta        = -90.0;
        private const double AndrogynousCentroidDelta  = -140.0;
        private const double AndrogynousSpacingDelta   = -120.0;
        private const double AndrogynousBandDelta       = -70.0;

        /// <summary>
        /// Stil-bevisst mål-sett for <see cref="ResonanceProxyEngine"/>-semantikk
        /// (F1/F2/F3/Spacing/Centroid-optima). F1/F2-båndene speiler scoring-defaulten,
        /// men brukes ikke av motoren.
        /// </summary>
        /// <remarks>
        /// Feminine/Situational/Custom/ukjent ⇒ NØYAKTIG de historiske motor-konstantene.
        /// </remarks>
        public static ResonanceStyleTarget ForEngine(VoiceStyleGoal style)
        {
            // Feminin/neutral default = eksakte historiske motor-konstanter.
            switch (style)
            {
                case VoiceStyleGoal.DarkFeminine:
                    return new ResonanceStyleTarget(
                        f1Optimal:    EngineF1Optimal,
                        f2Optimal:    EngineF2Optimal + DarkFeminineF2Delta,
                        f3Optimal:    EngineF3Optimal + DarkFeminineF3Delta,
                        spacingOptimal: EngineSpacingOptimal + DarkFeminineSpacingDelta,
                        centroidOptimal: EngineCentroidOptimal + DarkFeminineCentroidDelta,
                        f1Min: ScoringF1Min, f1Max: ScoringF1Max,
                        f2Min: ScoringF2Min + DarkFeminineBandDelta,
                        f2Max: ScoringF2Max + DarkFeminineBandDelta);

                case VoiceStyleGoal.Androgynous:
                    return new ResonanceStyleTarget(
                        f1Optimal:    EngineF1Optimal,
                        f2Optimal:    EngineF2Optimal + AndrogynousF2Delta,
                        f3Optimal:    EngineF3Optimal + AndrogynousF3Delta,
                        spacingOptimal: EngineSpacingOptimal + AndrogynousSpacingDelta,
                        centroidOptimal: EngineCentroidOptimal + AndrogynousCentroidDelta,
                        f1Min: ScoringF1Min, f1Max: ScoringF1Max,
                        f2Min: ScoringF2Min + AndrogynousBandDelta,
                        f2Max: ScoringF2Max + AndrogynousBandDelta);

                case VoiceStyleGoal.Feminine:
                case VoiceStyleGoal.Situational:
                case VoiceStyleGoal.Custom:
                default:
                    return new ResonanceStyleTarget(
                        f1Optimal:    EngineF1Optimal,
                        f2Optimal:    EngineF2Optimal,
                        f3Optimal:    EngineF3Optimal,
                        spacingOptimal: EngineSpacingOptimal,
                        centroidOptimal: EngineCentroidOptimal,
                        f1Min: ScoringF1Min, f1Max: ScoringF1Max,
                        f2Min: ScoringF2Min, f2Max: ScoringF2Max);
            }
        }

        /// <summary>
        /// Stil-bevisst mål-sett for <see cref="ResonansScoringService"/>-semantikk
        /// (F1/F2-optima + bånd + centroid). F3/Spacing speiler motor-defaulten, men
        /// brukes ikke av scoringen.
        /// </summary>
        /// <remarks>
        /// Feminine/Situational/Custom/ukjent ⇒ NØYAKTIG de historiske scoring-konstantene.
        /// </remarks>
        public static ResonanceStyleTarget ForScoring(VoiceStyleGoal style)
        {
            switch (style)
            {
                case VoiceStyleGoal.DarkFeminine:
                    return new ResonanceStyleTarget(
                        f1Optimal:    ScoringF1Optimal,
                        f2Optimal:    ScoringF2Optimal + DarkFeminineF2Delta,
                        f3Optimal:    EngineF3Optimal + DarkFeminineF3Delta,
                        spacingOptimal: EngineSpacingOptimal + DarkFeminineSpacingDelta,
                        centroidOptimal: ScoringCentroidOptimal + DarkFeminineCentroidDelta,
                        f1Min: ScoringF1Min, f1Max: ScoringF1Max,
                        f2Min: ScoringF2Min + DarkFeminineBandDelta,
                        f2Max: ScoringF2Max + DarkFeminineBandDelta);

                case VoiceStyleGoal.Androgynous:
                    return new ResonanceStyleTarget(
                        f1Optimal:    ScoringF1Optimal,
                        f2Optimal:    ScoringF2Optimal + AndrogynousF2Delta,
                        f3Optimal:    EngineF3Optimal + AndrogynousF3Delta,
                        spacingOptimal: EngineSpacingOptimal + AndrogynousSpacingDelta,
                        centroidOptimal: ScoringCentroidOptimal + AndrogynousCentroidDelta,
                        f1Min: ScoringF1Min, f1Max: ScoringF1Max,
                        f2Min: ScoringF2Min + AndrogynousBandDelta,
                        f2Max: ScoringF2Max + AndrogynousBandDelta);

                case VoiceStyleGoal.Feminine:
                case VoiceStyleGoal.Situational:
                case VoiceStyleGoal.Custom:
                default:
                    return new ResonanceStyleTarget(
                        f1Optimal:    ScoringF1Optimal,
                        f2Optimal:    ScoringF2Optimal,
                        f3Optimal:    EngineF3Optimal,
                        spacingOptimal: EngineSpacingOptimal,
                        centroidOptimal: ScoringCentroidOptimal,
                        f1Min: ScoringF1Min, f1Max: ScoringF1Max,
                        f2Min: ScoringF2Min, f2Max: ScoringF2Max);
            }
        }
    }
}
