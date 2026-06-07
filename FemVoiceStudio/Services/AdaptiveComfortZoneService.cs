using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using Range = FemVoiceStudio.Models.Range;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Service for adaptive comfort zone calculation.
    /// Adjusts target zones based on user baseline and session stability.
    ///
    /// Komfortsonen som beregnes her er IKKE bare UI-pynt: når den faktisk er
    /// kalibrert (ekte SmartCoach-baseline, ikke fallback-default) persisteres den
    /// til <see cref="UserVoiceProfile"/> via <see cref="PersistCalibratedProfile"/>
    /// slik at TargetProfileAdapter kan personliggjøre forsidens pitch-målsone på
    /// tvers av økter/restart. UserVoiceProfile er et PERSISTERT SNAPSHOT av de
    /// allerede beregnede kildene (SmartCoach-baseline + øktens komfort/helse) — ikke
    /// en parallell baseline-tilstand.
    /// </summary>
    public class AdaptiveComfortZoneService
    {
        private readonly SmartCoachEngine _smartCoach;

        // Valgfri DB-tilgang. Brukes til (1) å persistere en kalibrert komfortsone +
        // baseline-snapshot til UserVoiceProfile, og (2) baseline-fallback-lesing når
        // SmartCoach-baselinen mangler/er umoden (kaldstart etter DB-bytte). null i
        // rene beregnings-/test-kontekster — da degraderer vi til ren default-oppførsel.
        private readonly IDatabaseService? _database;

        // Base comfort zone values (defaults)
        private const double DefaultMinPitch = 165;
        private const double DefaultMaxPitch = 255;
        private const double DefaultOptimalPitch = 200;

        // Adaptive ranges
        private const double MinComfortRange = 30;   // Minimum range width
        private const double MaxComfortRange = 60;    // Maximum range width
        private const double RangeExpansionRate = 5;  // Hz per stable session

        // Progressive session parameters
        private const double ProgressivePitchStep = 5;  // Hz increase per progressive session
        private const double MaintenancePitchReduction = 10;  // Hz reduction for maintenance
        private const double RecoveryPitchReduction = 30;    // Hz reduction for recovery

        // Requirements for pitch progression
        private const double MinResonanceForProgression = 60;
        private const double MinHealthForProgression = 70;
        private const int MinStableSessionsForProgression = 3;

        // Persisterings-terskler. En kalibrert sone må ha minst denne bredden for å
        // regnes som meningsfull, og må avvike minst denne Hz-en fra det som alt er
        // lagret før vi skriver (unngår en DB-skriving per økt på uendret kalibrering).
        private const double MinCalibratedZoneWidth = 5.0;
        private const double MeaningfulZoneDeltaHz = 2.0;

        /// <summary>
        /// Constructor with dependency injection (recommended).
        /// <paramref name="database"/> er valgfri: med den persisterer/leser vi
        /// UserVoiceProfile-snapshotet; uten den degraderer tjenesten til ren
        /// beregning (bakoverkompatibelt med eksisterende kallsteder/tester).
        /// </summary>
        public AdaptiveComfortZoneService(SmartCoachEngine smartCoach, IDatabaseService? database = null)
        {
            _smartCoach = smartCoach ?? throw new ArgumentNullException(nameof(smartCoach));
            _database = database;
        }

        /// <summary>
        /// Calculate adaptive comfort zone based on user baseline.
        /// </summary>
        public Range CalculateComfortZone(int userId = 1, SessionType sessionType = SessionType.Progressive)
        {
            return ComputeComfortZone(userId, sessionType).Zone;
        }

        /// <summary>
        /// Som <see cref="CalculateComfortZone"/>, men oppgir om sonen faktisk er
        /// kalibrert fra ekte baseline-data (true) eller er en generisk fallback-default
        /// (false). Persisterings-stien bruker dette for å aldri lagre defaults som om
        /// de var brukerens egen kalibrering.
        /// </summary>
        public (Range Zone, bool Calibrated) ComputeComfortZone(
            int userId = 1, SessionType sessionType = SessionType.Progressive)
        {
            var baseline = _smartCoach.GetOrCalculateBaseline(userId);

            // Effektiv baseline-pitch: ekte SmartCoach-baseline når den er moden, ellers
            // et persistert UserVoiceProfile-snapshot (kaldstart-fallback, Oppgave B).
            // null ⇒ vi har ingen kalibrert kilde og må bruke generisk default.
            double? calibratedPitch = ResolveCalibratedBaselinePitch(baseline, userId);

            if (calibratedPitch == null)
            {
                // No calibrated baseline - use defaults (IKKE kalibrert).
                return (GetDefaultComfortZone(sessionType), false);
            }

            double baselinePitch = calibratedPitch.Value;
            double minPitch, maxPitch, optimalPitch;

            // Calculate based on session type
            switch (sessionType)
            {
                case SessionType.Recovery:
                    minPitch = Math.Max(140, baselinePitch - RecoveryPitchReduction);
                    // Ensure recovery sessions reduce the maximum pitch below baseline
                    maxPitch = Math.Min(baselinePitch - 1, minPitch + MinComfortRange);
                    optimalPitch = (minPitch + maxPitch) / 2;
                    break;

                case SessionType.Maintenance:
                    minPitch = Math.Max(150, baselinePitch - MaintenancePitchReduction);
                    maxPitch = minPitch + MinComfortRange;
                    optimalPitch = (minPitch + maxPitch) / 2;
                    break;

                case SessionType.Progressive:
                default:
                    // Progressive: expand based on stability
                    var stabilityFactor = CalculateStabilityFactor(userId);

                    // Calculate range based on baseline and stability
                    double range = Math.Min(MaxComfortRange, MinComfortRange + stabilityFactor * RangeExpansionRate);

                    minPitch = baselinePitch - 10;  // Slightly below baseline
                    maxPitch = minPitch + range;

                    // Cap at safe maximum
                    if (maxPitch > 280)
                    {
                        maxPitch = 280;
                        minPitch = maxPitch - range;
                    }

                    optimalPitch = (minPitch + maxPitch) / 2;
                    break;
            }

            return (new Range(minPitch, maxPitch, optimalPitch), true);
        }

        /// <summary>
        /// Finner en kalibrert baseline-pitch å beregne komfortsonen fra:
        ///   1) ekte SmartCoach-baseline når den finnes og ikke er "low" confidence, ellers
        ///   2) persistert UserVoiceProfile.BaselinePitch (snapshot fra forrige kalibrering —
        ///      bevarer kalibrering på tvers av DB-bytte/kaldstart, Oppgave B), ellers
        ///   3) null ⇒ ingen kalibrert kilde (kall faller tilbake til generisk default).
        /// Ingen parallell baseline-tilstand: UserVoiceProfile er kun et snapshot av den
        /// samme beregnede SmartCoach-kilden.
        /// </summary>
        private double? ResolveCalibratedBaselinePitch(SmartCoachBaseline? baseline, int userId)
        {
            if (baseline != null
                && baseline.ConfidenceLevel != "low"
                && baseline.BaselinePitch > 0)
            {
                return baseline.BaselinePitch;
            }

            // SmartCoach-baselinen mangler/er umoden — bruk persistert snapshot om vi har DB.
            if (_database != null)
            {
                try
                {
                    var profile = _database.GetUserVoiceProfile(userId);
                    if (profile != null && profile.BaselinePitch > 0)
                        return profile.BaselinePitch;
                }
                catch
                {
                    // DB utilgjengelig ⇒ ingen fallback; behandle som ikke-kalibrert.
                }
            }

            return null;
        }

        /// <summary>
        /// Persisterer en KALIBRERT komfortsone + baseline-snapshot til UserVoiceProfile.
        /// Skriver KUN når
        ///   • DB er tilgjengelig,
        ///   • sonen faktisk er kalibrert (ekte baseline, ikke fallback-default),
        ///   • sonen har gyldig bredde, og
        ///   • sonen avviker meningsfullt fra det som alt er lagret (unngår skriving per tick).
        /// Bevarer ALLE andre felter på profilen (hent-eller-opprett).
        ///
        /// <paramref name="sessionComfortRatio"/> (0-1) og <paramref name="sessionHealthScore"/>
        /// (0-100) er øktens beregnede verdier fra øktslutt-stien — snapshotes til
        /// BaselineComfort/BaselineHealth. Returnerer den persisterte (eller uendrede)
        /// profilen, eller null hvis ingenting ble lagret.
        /// </summary>
        public UserVoiceProfile? PersistCalibratedProfile(
            int userId,
            SessionType sessionType,
            double? sessionComfortRatio = null,
            double? sessionHealthScore = null)
        {
            if (_database == null)
                return null;

            var (zone, calibrated) = ComputeComfortZone(userId, sessionType);
            if (!calibrated)
                return null;   // aldri persister en generisk default som om den var kalibrert

            if (zone.Max - zone.Min < MinCalibratedZoneWidth)
                return null;   // degenerert/ugyldig bredde — ikke skriv

            var baseline = _smartCoach.GetOrCalculateBaseline(userId);

            UserVoiceProfile profile;
            try
            {
                profile = _database.GetUserVoiceProfile(userId) ?? new UserVoiceProfile { UserId = userId };
            }
            catch
            {
                return null;
            }

            // Avviks-terskel: skriv kun når sonen flytter seg meningsfullt, ELLER når et
            // baseline-snapshot-felt faktisk endres. Uten dette ville hver økt skrevet DB
            // selv om kalibreringen sto stille.
            bool zoneChanged =
                !WithinTolerance(profile.ComfortZoneMinPitch, zone.Min)
                || !WithinTolerance(profile.ComfortZoneMaxPitch, zone.Max)
                || !WithinTolerance(profile.ComfortZoneOptimalPitch, zone.Optimal);

            // Baseline-snapshot fra de eksisterende beregnede kildene.
            double newBaselinePitch = baseline?.BaselinePitch is > 0
                ? baseline!.BaselinePitch
                : profile.BaselinePitch;
            double newBaselineResonance = baseline?.BaselineResonanceScore is > 0
                ? baseline!.BaselineResonanceScore
                : profile.BaselineResonance;
            double newBaselineComfort = sessionComfortRatio ?? profile.BaselineComfort;
            double newBaselineHealth = sessionHealthScore ?? profile.BaselineHealth;

            bool baselineChanged =
                Math.Abs(newBaselinePitch - profile.BaselinePitch) > MeaningfulZoneDeltaHz
                || Math.Abs(newBaselineResonance - profile.BaselineResonance) > 0.5
                || Math.Abs(newBaselineComfort - profile.BaselineComfort) > 0.01
                || Math.Abs(newBaselineHealth - profile.BaselineHealth) > 0.5;

            if (!zoneChanged && !baselineChanged)
                return null;   // ingenting meningsfullt endret — ikke skriv

            // Bevar alle andre felter; oppdater kun sone + baseline-snapshot.
            profile.ComfortZoneMinPitch = zone.Min;
            profile.ComfortZoneMaxPitch = zone.Max;
            profile.ComfortZoneOptimalPitch = zone.Optimal;
            profile.BaselinePitch = newBaselinePitch;
            profile.BaselineResonance = newBaselineResonance;
            profile.BaselineComfort = newBaselineComfort;
            profile.BaselineHealth = newBaselineHealth;

            try
            {
                _database.SaveUserVoiceProfile(profile);   // setter LastUpdated
            }
            catch
            {
                return null;
            }

            return profile;
        }

        /// <summary>
        /// True når en lagret (nullbar) verdi er innenfor avviks-terskelen av den nye
        /// verdien. En ikke-lagret verdi (null) regnes ALDRI som innenfor — første
        /// kalibrering må alltid skrive.
        /// </summary>
        private static bool WithinTolerance(double? stored, double candidate)
            => stored.HasValue && Math.Abs(stored.Value - candidate) <= MeaningfulZoneDeltaHz;

        /// <summary>
        /// Get default comfort zone for new users
        /// </summary>
        private Range GetDefaultComfortZone(SessionType sessionType)
        {
            return sessionType switch
            {
                SessionType.Recovery => new Range(140, 180, 160),
                SessionType.Maintenance => new Range(155, 195, 175),
                _ => new Range(DefaultMinPitch, DefaultMaxPitch, DefaultOptimalPitch)
            };
        }

        /// <summary>
        /// Calculate stability factor based on recent sessions
        /// </summary>
        private double CalculateStabilityFactor(int userId)
        {
            // Get recent sessions for stability analysis
            // This would normally come from database - simplified here
            return 1.0; // Base stability factor
        }

        /// <summary>
        /// Determine if user can progress to higher pitch targets
        /// </summary>
        public bool CanProgress(int userId)
        {
            var baseline = _smartCoach.GetOrCalculateBaseline(userId);

            if (baseline == null)
                return false;

            // Check resonance requirement
            if (baseline.BaselineResonanceScore < MinResonanceForProgression)
                return false;

            // Helse-gate: sone-progresjon krever at den ukentlige helsestatusen tillater
            // det. Vi leser SAMME kilde som GetRecommendedSessionType (SmartCoach-
            // aggregert ukentlig HealthScore) — ingen ny/parallell helsekilde. Faller
            // helsen under progresjonsterskelen er brukeren i vedlikeholds-/recovery-
            // territorium, og soner skal IKKE utvides (Safety/Health > Progression).
            if (GetWeeklyHealthScore(userId) < MinHealthForProgression)
                return false;

            // Check stability requirement - need stable sessions
            var recentSessions = GetRecentSessionScores(userId, MinStableSessionsForProgression);
            if (recentSessions.Count < MinStableSessionsForProgression)
                return false;

            // Check if recent sessions are stable
            double avgStability = recentSessions.Average(s => s.PitchScore);
            return avgStability > 60;
        }

        /// <summary>
        /// Den ukentlige helsescoren (0-100) fra SmartCoach-aggregeringen — den ENE
        /// helsekilden som både <see cref="CanProgress"/> og
        /// <see cref="GetRecommendedSessionType"/> gater på. SmartCoachEngine aggregerer
        /// helsemonitoreringen; vi leser den via ukentlig progresjon for å unngå å
        /// duplisere DB-tilgang her.
        /// KANONISK ukestart (søndag) — ikke rullerende vindu: Today.AddDays(-6)
        /// ga en NY WeekStart-verdi per dag, og siden CalculateWeeklyProgress
        /// persisterer raden (UPDATE-på-WeekStart matchet aldri) fikk SmartCoach-
        /// historikken én duplikatrad per treningsdag — «samme uke 4 ganger».
        /// </summary>
        private double GetWeeklyHealthScore(int userId)
        {
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            return _smartCoach.CalculateWeeklyProgress(weekStart, userId).HealthScore;
        }

        /// <summary>
        /// Get recommended session type based on user state
        /// </summary>
        public SessionType GetRecommendedSessionType(int userId)
        {
            var baseline = _smartCoach.GetOrCalculateBaseline(userId);

            if (baseline == null)
                return SessionType.Progressive;

            // Samme ukentlige helsekilde som CanProgress (ingen duplisert DB-tilgang).
            var healthScore = GetWeeklyHealthScore(userId);
            if (healthScore < 60)
                return SessionType.Recovery;
            if (healthScore < 80)
                return SessionType.Maintenance;

            // Check if ready for progression
            if (CanProgress(userId))
                return SessionType.Progressive;

            return SessionType.Maintenance;
        }

        /// <summary>
        /// Generate SmartCoach explanation based on current state
        /// </summary>
        public string GenerateExplanation(CoachExplanationContext context)
        {
            // Analyze what affects score most
            double resonanceWeight = GetScoreComponentWeight(context.ResonanceScore);
            double pitchWeight = GetScoreComponentWeight(context.PitchScore);
            double healthWeight = GetScoreComponentWeight(context.VoiceHealthScore);
            double intonationWeight = GetScoreComponentWeight(context.IntonationScore);

            // Priority ordering
            var priorities = new List<(string component, double weight)>
            {
                ("resonance", resonanceWeight),
                ("pitch", pitchWeight),
                ("health", healthWeight),
                ("intonation", intonationWeight)
            };
            priorities.Sort((a, b) => b.weight.CompareTo(a.weight));

            // Generate explanation based on lowest-scoring component
            string lowestComponent = priorities[0].component;

            return lowestComponent switch
            {
                "resonance" when context.CurrentResonance < context.CurrentPitch / 2 =>
                    LocalizationService.Instance["AdaptiveComfort_ResonanceBelowPitch"],
                "resonance" =>
                    LocalizationService.Instance["AdaptiveComfort_ResonanceImprove"],
                "pitch" when context.Stability == StabilityState.Stable || context.Stability == StabilityState.VeryStable =>
                    LocalizationService.Instance["AdaptiveComfort_PitchStableIntonation"],
                "pitch" =>
                    LocalizationService.Instance["AdaptiveComfort_PitchVariationHigh"],
                "health" when context.Health == HealthState.Warning || context.Health == HealthState.Danger =>
                    LocalizationService.Instance["AdaptiveComfort_HealthStrain"],
                "health" =>
                    LocalizationService.Instance["AdaptiveComfort_HealthMonitor"],
                "intonation" =>
                    LocalizationService.Instance["AdaptiveComfort_IntonationVariation"],
                _ => LocalizationService.Instance["AdaptiveComfort_Default"]
            };
        }

        /// <summary>
        /// Calculate weight for score component (inverse - lower score = higher weight)
        /// </summary>
        private double GetScoreComponentWeight(double score)
        {
            return 100 - score;
        }

        /// <summary>
        /// Get recent session scores (simplified - would come from database)
        /// </summary>
        private List<FemVoiceScoreResult> GetRecentSessionScores(int userId, int count)
        {
            // Would normally fetch from database
            return new List<FemVoiceScoreResult>();
        }

        /// <summary>
        /// Get recent health issues (simplified - would come from database)
        /// </summary>
        // NOTE: Recent health issues are analyzed via SmartCoachEngine aggregation
        // methods (CalculateWeeklyProgress) to avoid duplicating DB access here.
    }
}
