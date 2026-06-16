using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services.Progression;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Smart Coach Engine - Kjernelogikk for individualisert veiledning
    /// Håndterer baseline-beregning, progresjonsvurdering, anbefalingsgenerering og helseovervåkning
    /// </summary>
    public class SmartCoachEngine
    {
        private readonly IDatabaseService _database;
        private readonly ILocalizationService _localization;
        private readonly FeedbackPipeline? _feedbackPipeline;
        private readonly SmartCoachFeedbackMapper? _feedbackMapper;
        private readonly IVoiceGoalProfileProvider? _voiceGoalProfiles;

        // VALGFRI kilde til VoiceMetrics-flerdimensjonsscorer (Sprint B / Bølge 1+2).
        // null ⇒ full bakoverkompat: motoren oppfører seg nøyaktig som før (pitch-/
        // baseline-drevet). Når satt, leser GenerateDailyRecommendation siste
        // VoiceIntelligence-trendpunkt og velger coaching-akse på den SVAKESTE
        // dimensjonen — alltid ETTER health-gaten (Safety > Coaching).
        private readonly SessionAnalyticsStore? _voiceIntelligence;

        // ── GOAL-/STIL-/CONFIDENCE-/VARIGHET-evolusjon (Sprint C, Bølge 2) ─────────────
        // Alle VALGFRIE. Hver enkelt er null ⇒ NØYAKTIG dagens oppførsel for den biten
        // (additivt + bakoverkompatibelt). Sammen lar de coachingen forstå brukerens
        // STAGE + langtids-fokus (LearningPathProfile), prediktiv restitusjon
        // (RecoveryForecast), STIL (DarkFeminine ≠ Feminine for samme fokus), og MESTRING
        // (positiv, feirende ramme) — uten å røre health-gaten eller aksevalget.
        private readonly RecoveryIntelligenceService? _recoveryIntelligence;
        private readonly LearningPathProfileBuilder? _learningPathBuilder;
        private readonly ComplexityEngine? _complexityEngine;

        // ── EFFEKTIVITETS-INTELLIGENS (Sprint C.2, Agent 7 — Recommendation Data Provider) ─
        // VALGFRI. null ⇒ NØYAKTIG dagens oppførsel. Når satt, henter
        // TryReadExerciseEffectiveness EvaluateAllAsync (async-over-sync, samme mønster som
        // TryReadLatestVoiceIntelligence / TryBuildLearningPathProfile) og mater den
        // observerte effektiviteten inn i LearningPath-anbefalingene (mest effektive først).
        // KLINISK: effektivitet er KUN en berikelse av HVILKEN øvelse — den kan ALDRI
        // overstyre health-/recovery-gaten, aksevalget eller stil-coachingen.
        private readonly ExerciseEffectivenessEngine? _effectivenessEngine;

        // Bro fra MasteryEvaluator → feirende coaching. Et rent delegat (ikke en mock):
        // produksjon wirer det fra MasteryEvaluator over en representativ øvelse; tester
        // gir en enkel lambda. null ⇒ ingen mestrings-melding (dagens oppførsel).
        private readonly Func<int, MasteryLevel?>? _masteryLevelProvider;

        // ── LONGITUDINALE INNSIKTER + MINNE (Sprint C.3/C.4, Bølge 2) ──────────────────
        // Alle seks er VALGFRIE. null ⇒ NØYAKTIG dagens oppførsel (additivt +
        // bakoverkompatibelt). Ingen av dem kan filtrere eller overstyre health/safety/
        // recovery-gates — all ny intelligens er BESKRIVENDE og legges PÅ ETTER gaten.
        private readonly LongitudinalInsightEngine? _insightEngine;
        private readonly RecommendationExplanationEngine? _explanationEngine;
        private readonly SmartCoachMemoryStore? _memory;
        private readonly VoiceKnowledgeGraphBuilder? _knowledgeGraphBuilder;
        private readonly TrendEngineService? _trendEngine;
        private readonly VoicePatternDetector? _patternDetector;

        private readonly Random _random = new();

        // Konstanter for terskelverdier
        private const double ResonancePriorityThreshold = 70.0; // Prioriter resonans under 70%
        private const double PitchPressThreshold = 180.0; // Hz - over dette regnes som press
        private const double FatigueScoreDrop = 20.0; // Prosent
        private const int BaselineMinDays = 7; // Minimum dager for baseline
        private const int BaselineIdealDays = 14; // Ideelle dager for baseline

        // ── VoiceMetrics-drevet aksevalg (Bølge 2) ────────────────────────────────
        // En dimensjon regnes som «svak» (og dermed kandidat for coaching-fokus) når
        // 0–100-scoren er under denne terskelen. Nøytral 50 (manglende signal) ligger
        // UNDER terskelen, men selve aksevalget krever i tillegg at minst én ekte
        // (ikke-nøytral) dimensjon trekker ned — se SelectVoiceIntelligenceFocus.
        private const double DimensionWeakThreshold = 65.0;

        // Hvor langt tilbake vi leter etter siste øktscore. Vid nok til å fange en
        // pause i treningen, smal nok til å være «nylig».
        private const int VoiceIntelligenceLookbackDays = 30;

        // Fallback for ukentlig økt-mål når brukeren ikke har en kalibrert
        // UserVoiceProfile ennå. Speiler standardverdien i UserVoiceProfile
        // (TrainingFrequencyPerWeek = 3) og WeeklyGoals-tabellen (TargetSessions
        // DEFAULT 3), så ingen ny parallell konstant innføres.
        private const int DefaultWeeklySessionTarget = 3;

        // ── Adaptiv øktvarighet (Sprint C, Agent 6-varighet) ──────────────────────────
        // RecommendedDurationMinutes var faste konstanter (kun strain ⇒ 5 min). Vi skalerer
        // nå MILDT fra recovery/konsistens, ALLTID innenfor trygge, snevre grenser.
        // KLINISK: skalering kan KUN STRAMME (gjøre kortere) ved restitusjonsbehov; en liten
        // forlengelse er bare tillatt når BÅDE recovery er god OG konsistensen er høy, og
        // aldri forbi taket. Health/Recovery > Goals: en kort økt presses aldri lengre.
        private const int MinAdaptiveDurationMinutes = 3;   // gulv — aldri kortere enn dette
        private const int MaxAdaptiveDurationMinutes = 12;  // tak — aldri lengre enn dette
        private const double DurationRecoveryShortenThreshold = 50.0;  // < dette ⇒ kort ned
        private const double DurationRecoveryLengthenThreshold = 80.0; // ≥ dette (+ konsist.) ⇒ litt lengre
        private const double DurationConsistencyLengthenThreshold = 75.0;
        private const int DurationShortenStepMinutes = 2;   // hvor mye vi korter
        private const int DurationLengthenStepMinutes = 1;  // hvor mye vi (forsiktig) forlenger

        /// <summary>
        /// Constructor with dependency injection (recommended)
        /// </summary>
        public SmartCoachEngine(
            IDatabaseService database,
            ILocalizationService? localization = null,
            FeedbackPipeline? feedbackPipeline = null,
            SmartCoachFeedbackMapper? feedbackMapper = null,
            IVoiceGoalProfileProvider? voiceGoalProfiles = null,
            SessionAnalyticsStore? voiceIntelligence = null,
            RecoveryIntelligenceService? recoveryIntelligence = null,
            LearningPathProfileBuilder? learningPathBuilder = null,
            ComplexityEngine? complexityEngine = null,
            Func<int, MasteryLevel?>? masteryLevelProvider = null,
            ExerciseEffectivenessEngine? effectivenessEngine = null,
            LongitudinalInsightEngine? insightEngine = null,
            RecommendationExplanationEngine? explanationEngine = null,
            SmartCoachMemoryStore? memory = null,
            VoiceKnowledgeGraphBuilder? knowledgeGraphBuilder = null,
            TrendEngineService? trendEngine = null,
            VoicePatternDetector? patternDetector = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _localization = localization ?? LocalizationService.Instance;
            _feedbackPipeline = feedbackPipeline;
            _feedbackMapper = feedbackMapper;
            _voiceGoalProfiles = voiceGoalProfiles;
            _voiceIntelligence = voiceIntelligence;
            _recoveryIntelligence = recoveryIntelligence;
            _learningPathBuilder = learningPathBuilder;
            _complexityEngine = complexityEngine;
            _masteryLevelProvider = masteryLevelProvider;
            _effectivenessEngine = effectivenessEngine;
            _insightEngine = insightEngine;
            _explanationEngine = explanationEngine;
            _memory = memory;
            _knowledgeGraphBuilder = knowledgeGraphBuilder;
            _trendEngine = trendEngine;
            _patternDetector = patternDetector;
        }

        
        
        /// <summary>
        /// Constructor for backward compatibility (uses concrete DatabaseService)
        /// </summary>
        [Obsolete("Use constructor with IDatabaseService interface instead")]
        public SmartCoachEngine(DatabaseService database)
            : this(database, null)
        {
        }
        
        #region Baseline Calculation
        
        /// <summary>
        /// Beregner brukerens baseline basert på treningsdata
        /// </summary>
        public SmartCoachBaseline CalculateBaseline(int userId = 1)
        {
            // Hent treningsdata fra siste 30 dager
            var from = DateTime.Now.AddDays(-30);
            var to = DateTime.Now;
            
            var sessions = _database.GetTrainingSessions(from, to);
            
            if (sessions.Count < 3)
            {
                // Ikke nok data for baseline
                return new SmartCoachBaseline
                {
                    UserId = userId,
                    CalculatedAt = DateTime.Now,
                    ConfidenceLevel = "low"
                };
            }
            
            var trainingDays = _database.GetTrainingDaysCount(from, to, userId);
            
            // Beregn gjennomsnittlige verdier - med null-sjekk
            var avgPitch = sessions.Any() ? sessions.Average(s => s.AveragePitch) : 0;
            var avgF1 = sessions.Where(s => s.AverageF1 > 0).Any() ? sessions.Where(s => s.AverageF1 > 0).Average(s => s.AverageF1) : 0;
            var avgF2 = sessions.Where(s => s.AverageF2 > 0).Any() ? sessions.Where(s => s.AverageF2 > 0).Average(s => s.AverageF2) : 0;
            var avgIntonation = sessions.Any() ? sessions.Average(s => s.IntonationScore) : 0;
            var avgResonance = sessions.Where(s => s.ResonanceScore > 0).Any() ? sessions.Where(s => s.ResonanceScore > 0).Average(s => s.ResonanceScore) : 0;
            
            // Bestem confidence level basert på antall treningsdager
            string confidenceLevel;
            if (trainingDays >= BaselineIdealDays)
                confidenceLevel = "high";
            else if (trainingDays >= BaselineMinDays)
                confidenceLevel = "medium";
            else
                confidenceLevel = "low";
            
            var baseline = new SmartCoachBaseline
            {
                UserId = userId,
                BaselinePitch = avgPitch,
                BaselineF1 = avgF1 > 0 ? avgF1 : 500, // Default F1 verdi
                BaselineF2 = avgF2 > 0 ? avgF2 : 1500, // Default F2 verdi
                BaselineIntonation = avgIntonation,
                BaselineResonanceScore = avgResonance,
                CalculatedAt = DateTime.Now,
                TrainingDaysCount = trainingDays,
                ConfidenceLevel = confidenceLevel
            };
            
            // Lagre til database
            _database.SaveSmartCoachBaseline(baseline);
            
            return baseline;
        }
        
        /// <summary>
        /// Henter eller beregner baseline
        /// </summary>
        public SmartCoachBaseline? GetOrCalculateBaseline(int userId = 1)
        {
            var existingBaseline = _database.GetSmartCoachBaseline(userId);
            
            if (existingBaseline == null)
            {
                // Beregn ny baseline
                return CalculateBaseline(userId);
            }
            
            // Sjekk om baseline bør oppdateres (etter 7 nye dager)
            if (existingBaseline.CalculatedAt.HasValue)
            {
                var daysSinceCalculation = (DateTime.Now - existingBaseline.CalculatedAt.Value).TotalDays;
                if (daysSinceCalculation > 7)
                {
                    return CalculateBaseline(userId);
                }
            }
            
            return existingBaseline;
        }
        
        #endregion
        
        #region Progression Assessment
        
        /// <summary>
        /// Beregner ukentlig progresjon og sammenligner med forrige uke
        /// </summary>
        public SmartCoachWeeklyProgress CalculateWeeklyProgress(DateTime weekStart, int userId = 1)
        {
            var weekEnd = weekStart.AddDays(6);
            
            // Hent denne ukens data
            var thisWeekStats = _database.GetTrainingStats(weekStart, weekEnd, userId);
            var thisWeekSessions = _database.GetTrainingSessions(weekStart, weekEnd)
                .Where(s => s.StartTime >= weekStart && s.StartTime <= weekEnd).ToList();
            
            // Hent forrige ukes data for sammenligning
            var lastWeekStart = weekStart.AddDays(-7);
            var lastWeekStats = _database.GetTrainingStats(lastWeekStart, weekStart.AddDays(-1), userId);
            
            // Beregn endringer
            var pitchChange = thisWeekStats.AvgPitch - lastWeekStats.AvgPitch;
            var resonanceChange = thisWeekStats.AvgResonance - lastWeekStats.AvgResonance;
            var intonationChange = 0.0; // Kan beregnes hvis vi har data
            
            // Beregn helsescore basert på ukens helseproblemer
            var healthIssues = _database.GetRecentHealthIssues(userId: userId, days: 7);
            var healthScore = 100.0;
            if (healthIssues.Any())
            {
                healthScore = 100.0 - healthIssues.Average(h => h.StrainLevel);
            }
            
            var progress = new SmartCoachWeeklyProgress
            {
                UserId = userId,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                SessionsCount = thisWeekStats.Sessions,
                TotalMinutes = thisWeekStats.Minutes,
                AverageScore = thisWeekStats.AvgScore,
                AveragePitch = thisWeekStats.AvgPitch,
                AverageResonance = thisWeekStats.AvgResonance,
                AverageIntonation = thisWeekStats.AvgIntonation,
                PitchChange = pitchChange,
                ResonanceChange = resonanceChange,
                IntonationChange = intonationChange,
                HealthScore = healthScore
            };
            
            // Lagre til database
            _database.SaveWeeklyProgress(progress);

            return progress;
        }

        /// <summary>
        /// Brukerens eget ukentlige økt-mål. Kilden er UserVoiceProfile.TrainingFrequencyPerWeek
        /// (brukeren setter dette selv) — IKKE en hardkodet konstant. Mangler profil, eller er
        /// verdien ikke-positiv, faller vi tilbake til <see cref="DefaultWeeklySessionTarget"/>.
        ///
        /// KLINISK: Dette er brukerens EGET mål. Det skal aldri brukes til å skamme eller
        /// presse («du ligger bak») — kun til å feire når brukeren når sin egen kadens og
        /// til å gi støttende, lavterskel-formuleringer ellers.
        /// </summary>
        public int GetWeeklySessionTarget(int userId = 1)
        {
            try
            {
                var profile = _database.GetUserVoiceProfile(userId);
                if (profile != null && profile.TrainingFrequencyPerWeek > 0)
                {
                    return profile.TrainingFrequencyPerWeek;
                }
            }
            catch
            {
                // Null-safe degradering: ved enhver lesefeil bruker vi fallback-målet
                // i stedet for å la coachingen kollapse.
            }

            return DefaultWeeklySessionTarget;
        }

        /// <summary>
        /// Genererer individuelle mål basert på baseline og progresjon
        /// </summary>
        public List<SmartCoachGoal> GenerateGoals(int userId = 1)
        {
            var goals = new List<SmartCoachGoal>();
            var baseline = GetOrCalculateBaseline(userId);

            if (baseline == null) return goals;

            // ==== HEALTH GATE: anstrengelse undertrykker pitch-ØKNINGsmålet ====
            // Samme tidlige helse-gate som GenerateDailyRecommendation/
            // GenerateMotivationalMessages. Et ubetinget «pitch +20 Hz»-mål under aktiv
            // strain ville presse brukeren oppover nettopp når stemmen trenger hvile —
            // brudd på Safety > Progression. Vi hopper derfor over pitch-ØKNINGsmålet
            // (Mål 1) når strain er aktiv; restitusjons-/resonansvennlige mål under
            // beholdes uendret.
            var goalHealthIssues = _database.GetRecentHealthIssues(userId: userId, days: 3);
            var strainActive = goalHealthIssues.Any(h => h.StrainDetected);

            // ==== STIL-GATE: det stil-blinde +20 Hz-pitchmålet passer ikke alle stiler ====
            // Pitch-ØKNINGsmålet (Mål 1) skyver brukeren mot LYSERE/HØYERE pitch (kappet
            // ved 220 Hz). For DarkFeminine og Androgynous er målet en VARMERE/LAVERE,
            // fyldigere stemme — ikke høyere pitch. Et stil-blindt +20 Hz-mål ville da
            // jobbe MOT brukerens eget mål. Vi demper det derfor for disse stilene.
            // Feminine (og Situational/Custom/ukjent stil) beholder målet uendret —
            // konservativt: vi fjerner KUN målet der det beviselig motvirker stilen.
            // Stilen leses via ResolveVoiceStyle (samme mønster som andre steder i
            // klassen). Klinisk: dette SVEKKER ingen helse-/restitusjons-gate (den eneste
            // effekten er å droppe et pitch-ØKENDE mål); gate-rekkefølgen i
            // GenerateDailyRecommendation er urørt.
            var voiceGoalProfile = _voiceGoalProfiles?.GetProfile(userId);
            var style = ResolveVoiceStyle(userId, voiceGoalProfile);
            var styleWantsLowerPitch =
                style == VoiceStyleGoal.DarkFeminine || style == VoiceStyleGoal.Androgynous;

            // Mål 1: Pitchøkning (realistisk: 10-20 Hz økning over 8 uker)
            // Kun når ingen aktiv strain — ellers ville vi be brukeren presse pitch opp
            // mens helse-gaten ber om restitusjon. OG kun når stilen ikke peker mot en
            // lavere/varmere stemme (DarkFeminine/Androgynous).
            if (!strainActive && !styleWantsLowerPitch)
            {
                goals.Add(new SmartCoachGoal
                {
                    UserId = userId,
                    GoalType = "pitch",
                    TargetValue = Math.Min(baseline.BaselinePitch + 20, 220), // Maks 220 Hz
                    CurrentValue = baseline.BaselinePitch,
                    StartDate = DateTime.Now,
                    TargetDate = DateTime.Now.AddDays(56), // 8 uker
                    Priority = 2
                });
            }
            
            // Mål 2: Resonansforbedring (prioriteres klinisk)
            if (baseline.BaselineResonanceScore < ResonancePriorityThreshold)
            {
                goals.Add(new SmartCoachGoal
                {
                    UserId = userId,
                    GoalType = "resonance",
                    TargetValue = Math.Min(baseline.BaselineResonanceScore + 15, 85),
                    CurrentValue = baseline.BaselineResonanceScore,
                    StartDate = DateTime.Now,
                    TargetDate = DateTime.Now.AddDays(42), // 6 uker
                    Priority = 3 // Høy prioritet
                });
            }
            
            // Mål 3: Intonasjonsvariasjon
            if (baseline.BaselineIntonation < 60)
            {
                goals.Add(new SmartCoachGoal
                {
                    UserId = userId,
                    GoalType = "intonation",
                    TargetValue = baseline.BaselineIntonation + 20,
                    CurrentValue = baseline.BaselineIntonation,
                    StartDate = DateTime.Now,
                    TargetDate = DateTime.Now.AddDays(56),
                    Priority = 1
                });
            }
            
            // Lagre mål til database
            foreach (var goal in goals)
            {
                _database.SaveSmartCoachGoal(goal);
            }
            
            return goals;
        }
        
        #endregion
        
        #region Recommendation Generation
        
        /// <summary>
        /// Genererer daglig anbefaling basert på treningsmønster og progresjon
        /// </summary>
        public SmartCoachDailyRecommendation GenerateDailyRecommendation(int userId = 1)
        {
            // Sjekk om anbefaling allerede finnes for i dag
            var existingRecommendation = _database.GetDailyRecommendation(DateTime.Today, userId);
            if (existingRecommendation != null)
            {
                return existingRecommendation;
            }
            
            var recommendation = new SmartCoachDailyRecommendation
            {
                UserId = userId,
                Date = DateTime.Today
            };
            
            // Hent nylige data for analyse
            var recentSessions = _database.GetRecentSessions(10, userId);
            var healthIssues = _database.GetRecentHealthIssues(userId: userId, days: 3);
            var baseline = GetOrCalculateBaseline(userId);
            var voiceGoalProfile = _voiceGoalProfiles?.GetProfile(userId);

            // STIL leses ÉN gang og bæres gjennom hele anbefalingen, slik at f.eks.
            // resonans-coaching kan differensieres (DarkFeminine/Androgynous skal IKKE
            // pushes mot lysere/fremre klang). null ⇒ ingen stil-info ⇒ dagens tekst.
            var style = ResolveVoiceStyle(userId, voiceGoalProfile);

            // ==== HEALTH CHECK: Sjekk for anstrengelse ====
            if (healthIssues.Any(h => h.StrainDetected))
            {
                var lastIssue = healthIssues.First();
                recommendation.HealthWarning = true;
                recommendation.HealthWarningText = lastIssue.Recommendation;

                // Endre fokus til restitusjon
                recommendation.FocusArea = "recovery";
                recommendation.RecommendationText = GetRecoveryRecommendation(lastIssue.StrainType);
                // Adaptiv varighet kan KUN stramme her (recovery-behov ⇒ kort). Vi sender
                // en lav recovery-score (strain ⇒ overbelastet) inn i skaleringen og
                // garanterer dermed en kort økt — aldri lengre enn de gamle 5 min.
                recommendation.RecommendedDurationMinutes =
                    ScaleDuration(5, recoveryScore: 0.0, consistencyScore: null, recoveryOriented: true);

                _database.SaveDailyRecommendation(recommendation);
                return recommendation;
            }

            // ==== GOAL-/STAGE-/RECOVERY-FORECAST-LAG (Sprint C, Bølge 2) ════════════════
            // Bygg (når tjenestene er injisert) det prediktive bildet ÉN gang: en
            // RecoveryForecast og en LearningPathProfile. Begge er VALGFRIE og rene —
            // null-tjenester ⇒ null bilde ⇒ dagens oppførsel. Disse driver to ting:
            //   1) Recovery > Goals: en prediktiv overload (Severity >= Recommend) eller et
            //      RestRecommended-flagg fra LearningPath gir restitusjons-coaching FØR
            //      ethvert mål — men det er IKKE en strain-advarsel (ingen HealthWarning).
            //   2) En adaptiv recovery-/consistency-score som skalerer varigheten.
            var forecast = TryGetRecoveryForecast(userId);
            var learningPath = TryBuildLearningPathProfile(userId, forecast);

            // ==== ANALYSE: Bestem fokusområde ====
            // Klinisk prioritering: resonans > pitch > intonasjon > pust
            string focusArea;
            string recommendationText;
            int exerciseId;
            int duration;
            bool recoveryOriented = false;

            // RECOVERY-FØR-MÅL-gate (Health/Recovery > Goals). Kjører ETTER strain-gaten
            // (Safety) men FØR aksevalg/goal-profile. En mild, omsorgsfull omdirigering —
            // ingen advarsel heises.
            var recoveryForecastFocus = TryGetRecoveryForecastRecommendation(forecast, learningPath, style);

            var voiceMetricsFocus = TryGetVoiceIntelligenceRecommendation(userId, baseline, recentSessions);
            var preference = TryGetGoalProfileRecommendation(voiceGoalProfile, baseline, recentSessions);
            if (recoveryForecastFocus.HasValue)
            {
                (focusArea, recommendationText, exerciseId, duration) = recoveryForecastFocus.Value;
                recoveryOriented = true;
            }
            else if (voiceMetricsFocus.HasValue)
            {
                (focusArea, recommendationText, exerciseId, duration) = voiceMetricsFocus.Value;
                // Stil-bevisst overstyring av resonans-/recovery-tekst (samme akse,
                // ulik stemme per stil). Pitch/consistency/intonation berøres ikke.
                recommendationText = StyleAwareText(focusArea, recommendationText, style);
                recoveryOriented = focusArea == "recovery";
            }
            else if (preference.HasValue)
            {
                (focusArea, recommendationText, exerciseId, duration) = preference.Value;
                recommendationText = StyleAwareText(focusArea, recommendationText, style);
            }
            else if (baseline != null && baseline.BaselineResonanceScore < ResonancePriorityThreshold)
            {
                // Prioriter resonans
                focusArea = "resonance";
                (recommendationText, exerciseId, duration) = GetResonanceRecommendation(baseline, recentSessions);
                recommendationText = StyleAwareText(focusArea, recommendationText, style);
            }
            else if (recentSessions.Count > 0)
            {
                // Sjekk pitch-kontroll
                var avgPitchVariation = recentSessions.Average(s => s.PitchVariation);
                if (avgPitchVariation > 30)
                {
                    focusArea = "pitch";
                    (recommendationText, exerciseId, duration) = GetPitchControlRecommendation(baseline);
                }
                else
                {
                    // Sjekk intonasjon
                    var avgIntonation = recentSessions.Average(s => s.IntonationScore);
                    if (avgIntonation < 50)
                    {
                        focusArea = "intonation";
                        (recommendationText, exerciseId, duration) = GetIntonationRecommendation();
                    }
                    else
                    {
                        // Alt ser bra ut - balansert trening
                        focusArea = "balanced";
                        (recommendationText, exerciseId, duration) = GetBalancedRecommendation(recentSessions);
                    }
                }
            }
            else
            {
                // Første gang - start med grunnleggende
                focusArea = "pitch";
                (recommendationText, exerciseId, duration) = GetStarterRecommendation();
            }

            // ==== ADAPTIV VARIGHET (Sprint C, Agent 6-varighet) ════════════════════════
            // Skaler den valgte basis-varigheten fra recovery/konsistens. Kun strammere
            // ved restitusjonsbehov; en liten forlengelse bare ved god recovery + høy
            // konsistens. recoveryOriented ⇒ alltid kort. Manglende signal ⇒ uendret.
            var (recoveryScore01, consistencyScore01) = ResolveDurationSignals(forecast, learningPath);
            duration = ScaleDuration(duration, recoveryScore01, consistencyScore01, recoveryOriented);

            recommendation.FocusArea = focusArea;
            recommendation.RecommendationText = recommendationText;
            recommendation.RecommendedExerciseId = exerciseId;
            recommendation.RecommendedDurationMinutes = duration;

            _database.SaveDailyRecommendation(recommendation);

            // ── MINNE-SKRIVING (Sprint C.3/C.4, Bølge 2 — spec-agent 8) ═══════════════
            // Logg den utstedte øvelsesanbefalingen best-effort til _memory. Kjører ETTER
            // den urørte gaten og ETTER persist — påvirker ALDRI den returnerte anbefalingen.
            // Kun øvelsesanbefalinger (exerciseId > 0 og focusArea ikke "recovery" fra
            // health-gaten) — health/strain-suppresjon gir tidlig return ovenfor, så vi
            // er alltid i øvelsesgreinen her. Dedupliser per dag+fokus for å unngå spam.
            TryLogAdviceToMemory(userId, focusArea, exerciseId, now: DateTime.Now);

            return recommendation;
        }

        /// <summary>
        /// Best-effort, null-safe logg av den utstedte anbefalingen til <see cref="SmartCoachMemoryStore"/>.
        /// ALDRI kast; ALDRI påvirk den returnerte anbefalingen. _memory==null ⇒ ingen skriving.
        /// Dedupliserer per dag+fokus: hopper over hvis identisk fokus+øvelse allerede er
        /// logget i dag (halvåpen [today, tomorrow)). Async-over-sync via Task.Run for å unngå
        /// WPF SynchronizationContext-deadlock — samme mønster som de øvrige Try*-metodene.
        /// </summary>
        private void TryLogAdviceToMemory(int userId, string focusArea, int exerciseId, DateTime now)
        {
            if (_memory == null)
                return;

            try
            {
                var today    = now.Date;
                var tomorrow = today.AddDays(1);

                // Dedupliser: hent dagens historikk og hopp over hvis identisk fokus/øvelse.
                var todayHistory = Task.Run(() =>
                    _memory.GetAdviceHistoryAsync(userId, today, tomorrow))
                    .GetAwaiter().GetResult();

                if (todayHistory != null && todayHistory.Any(e =>
                    string.Equals(e.FocusArea, focusArea, StringComparison.OrdinalIgnoreCase)
                    && e.RecommendedExerciseId == (exerciseId > 0 ? (int?)exerciseId : null)))
                {
                    return; // allerede logget i dag — ingen duplikat
                }

                var entry = new SmartCoachAdviceEntry
                {
                    AdviceId              = Guid.NewGuid(),
                    UserId                = userId,
                    RecommendedAt         = now,
                    FocusArea             = focusArea ?? string.Empty,
                    RecommendedExerciseId = exerciseId > 0 ? (int?)exerciseId : null,
                };

                Task.Run(() => _memory.SaveAdviceAsync(entry)).GetAwaiter().GetResult();
            }
            catch
            {
                // Best-effort: enhver feil undertrykkes — minne-skriving må aldri krasje coachingen.
            }
        }

        #region Stil-bevisst coaching (Sprint C, Agent 4)

        /// <summary>
        /// Leser brukerens ønskede stemme-STIL. Prioritet: en kalibrert
        /// <see cref="UserVoiceProfile.PreferredVoiceStyle"/> (brukerens eget valg) går
        /// foran <see cref="VoiceGoalProfile.GoalStyleKey"/>. Mangler begge ⇒ null
        /// (stil-nøytral — dagens coaching-tekst beholdes uendret).
        ///
        /// KLINISK: stil endrer KUN hvordan vi formulerer fokuset, aldri prioritetene.
        /// DarkFeminine/Androgynous er like gyldige mål som Feminine; en DarkFeminine-
        /// bruker skal aldri presses mot en lysere/fremre klang hun ikke ønsker.
        /// </summary>
        private VoiceStyleGoal? ResolveVoiceStyle(int userId, VoiceGoalProfile? voiceGoalProfile)
        {
            try
            {
                var profile = _database.GetUserVoiceProfile(userId);
                if (profile != null)
                    return profile.PreferredVoiceStyle;
            }
            catch
            {
                // Null-safe degradering: lesefeil ⇒ fall tilbake på goal-profilens nøkkel.
            }

            if (voiceGoalProfile != null && !string.IsNullOrWhiteSpace(voiceGoalProfile.GoalStyleKey))
                return UserVoiceProfile.FromGoalStyleKey(voiceGoalProfile.GoalStyleKey);

            return null;
        }

        /// <summary>
        /// Velger stil-bevisst coaching-tekst for et gitt fokus. Returnerer ALLTID en
        /// klinisk trygg streng. Per i dag differensieres RESONANS og RECOVERY:
        ///   • Resonans: Feminine ⇒ lysere/fremre klang; DarkFeminine/Androgynous ⇒
        ///     SENKE/fyldigere, varm resonans (aldri «lysere/fremre»); Situational ⇒
        ///     fleksibel; Custom/null ⇒ den opprinnelige (stil-nøytrale) teksten.
        ///   • Recovery: en mild, stil-fargelagt restitusjons-formulering.
        /// Andre fokus (pitch/consistency/intonation/comfort/breathing/balanced) er
        /// allerede stil-nøytrale og returneres uendret.
        /// </summary>
        private string StyleAwareText(string focusArea, string fallbackText, VoiceStyleGoal? style)
        {
            if (style == null)
                return fallbackText;

            switch (focusArea)
            {
                case "resonance":
                    return style switch
                    {
                        VoiceStyleGoal.Feminine     => _localization.GetString("SmartCoach_Style_Resonance_Feminine"),
                        VoiceStyleGoal.Androgynous  => _localization.GetString("SmartCoach_Style_Resonance_Androgynous"),
                        VoiceStyleGoal.DarkFeminine => _localization.GetString("SmartCoach_Style_Resonance_DarkFeminine"),
                        VoiceStyleGoal.Situational  => _localization.GetString("SmartCoach_Style_Resonance_Situational"),
                        _                           => fallbackText
                    };

                case "recovery":
                    return style switch
                    {
                        VoiceStyleGoal.Feminine     => _localization.GetString("SmartCoach_Style_Recovery_Feminine"),
                        VoiceStyleGoal.Androgynous  => _localization.GetString("SmartCoach_Style_Recovery_Androgynous"),
                        VoiceStyleGoal.DarkFeminine => _localization.GetString("SmartCoach_Style_Recovery_DarkFeminine"),
                        VoiceStyleGoal.Situational  => _localization.GetString("SmartCoach_Style_Recovery_Situational"),
                        _                           => fallbackText
                    };

                default:
                    return fallbackText;
            }
        }

        #endregion

        #region Recovery-forecast + LearningPath (Sprint C, Agent 8)

        /// <summary>
        /// Henter en prediktiv <see cref="RecoveryForecast"/> fra persistert historikk,
        /// eller null når ingen <see cref="RecoveryIntelligenceService"/> /
        /// <see cref="SessionAnalyticsStore"/> er injisert eller lesingen feiler. Kjører
        /// trygt synkront på thread-pool (in-memory-kilden fullfører uansett synkront).
        /// </summary>
        private RecoveryForecast? TryGetRecoveryForecast(int userId)
        {
            if (_recoveryIntelligence == null || _voiceIntelligence == null)
                return null;

            try
            {
                return Task.Run(() =>
                    _recoveryIntelligence.ForecastFromHistoryAsync(_voiceIntelligence, DateTime.Now, userId))
                    .GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Bygger en <see cref="LearningPathProfile"/> fra trend + recovery + complexity,
        /// eller null når byggeren ikke er injisert eller noe feiler. Trenden hentes fra
        /// VoiceIntelligence-kilden; recovery fra forecastet (eller en nøytral
        /// WellRecovered-default); complexity fra <see cref="ComplexityEngine"/> (eller en
        /// Foundation-default når motoren ikke er tilgjengelig — f.eks. i tester).
        /// </summary>
        private LearningPathProfile? TryBuildLearningPathProfile(int userId, RecoveryForecast? forecast)
        {
            if (_learningPathBuilder == null)
                return null;

            try
            {
                IReadOnlyList<VoiceIntelligenceTrendPoint> trend = Array.Empty<VoiceIntelligenceTrendPoint>();
                if (_voiceIntelligence != null)
                {
                    var to = DateTime.Now;
                    var from = to.AddDays(-VoiceIntelligenceLookbackDays);
                    trend = Task.Run(() =>
                        _voiceIntelligence.GetVoiceIntelligenceTrendAsync(from, to, userId))
                        .GetAwaiter().GetResult();
                }

                var recovery = forecast?.Current
                    ?? new RecoveryResult { Score = 100.0, Status = RecoveryStatus.WellRecovered, Explanation = string.Empty };

                ComplexityEvaluation complexity;
                if (_complexityEngine != null)
                {
                    complexity = _complexityEngine.EvaluateCurrentLevel(userId);
                }
                else
                {
                    complexity = new ComplexityEvaluation
                    {
                        CurrentLevel = SpeechComplexityLevel.IsolatedSounds
                    };
                }

                // Effektivitets-berikelse (Sprint C.2, Agent 7): når EffectivenessEngine er
                // injisert, mat den observerte per-øvelse-effektiviteten inn slik at
                // LearningPath-anbefalingene ledes av det som faktisk har fungert. Valgfri
                // (null ⇒ dagens bånd-logikk). Berik KUN HVILKEN øvelse — aldri prioritet.
                var effectiveness = TryReadExerciseEffectiveness(userId);

                return _learningPathBuilder.Build(trend, recovery, complexity, mastery: null, effectiveness);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Henter per-øvelse-effektivitet for ALLE reelle katalog-øvelser (1–15) via
        /// <see cref="ExerciseEffectivenessEngine.EvaluateAllAsync"/>, eller null når motoren
        /// ikke er injisert eller lesingen feiler. Async i kilden; her kjøres den trygt
        /// synkront på thread-pool (Task.Run().GetAwaiter().GetResult()) for å unngå enhver
        /// WPF-SynchronizationContext-deadlock — samme mønster som
        /// <see cref="TryReadLatestVoiceIntelligence"/> / <see cref="TryBuildLearningPathProfile"/>.
        /// Rent beskrivende data: påvirker KUN hvilken øvelse som anbefales, aldri en
        /// health-/recovery-gate eller aksevalget.
        /// </summary>
        private IReadOnlyList<ExerciseEffectivenessProfile>? TryReadExerciseEffectiveness(int userId)
        {
            if (_effectivenessEngine == null)
                return null;

            try
            {
                return Task.Run(() =>
                    _effectivenessEngine.EvaluateAllAsync(DateTime.Now, userId))
                    .GetAwaiter().GetResult();
            }
            catch
            {
                // Null-safe degradering: enhver lesefeil ⇒ dagens bånd-baserte anbefalinger.
                return null;
            }
        }

        #endregion

        #region Longitudinale innsikter + Forklaring + Minne + KnowledgeGraph (Sprint C.3/C.4, Bølge 2)

        /// <summary>
        /// Bygger en komplett <see cref="VoiceDevelopmentProfile"/> via TrendEngineService
        /// og VoicePatternDetector, eller null når en av tjenestene ikke er injisert eller
        /// noe feiler. Async-over-sync (Task.Run) for WPF-deadlock-sikkerhet.
        /// BESKRIVENDE INTELLIGENS: overstyre aldri en health/safety/recovery-gate.
        /// </summary>
        public VoiceDevelopmentProfile? TryBuildDevelopmentProfile(int userId, DateTime now)
        {
            if (_trendEngine == null)
                return null;

            try
            {
                var windows = Task.Run(() =>
                    _trendEngine.AnalyzeAsync(now, userId))
                    .GetAwaiter().GetResult();

                PlateauState? plateau = null;
                BreakthroughState? breakthrough = null;
                RegressionState? regression = null;
                if (_patternDetector != null)
                {
                    var (p, b, r) = _patternDetector.Compute(windows);
                    plateau      = p;
                    breakthrough = b;
                    regression   = r;
                }

                // Hent råpunktene fra 180-dagers vinduet for compositescore.
                var from = now.AddDays(-180);
                IReadOnlyList<VoiceIntelligenceTrendPoint> allPoints;
                if (_voiceIntelligence != null)
                {
                    allPoints = Task.Run(() =>
                        _voiceIntelligence.GetVoiceIntelligenceTrendAsync(from, now, userId))
                        .GetAwaiter().GetResult();
                }
                else
                {
                    allPoints = Array.Empty<VoiceIntelligenceTrendPoint>();
                }

                var profile = _trendEngine.BuildProfile(windows, userId, now, allPoints);

                // Patch pattern-states inn — BuildProfile setter disse til null.
                return profile with
                {
                    Plateau      = plateau,
                    Breakthrough = breakthrough,
                    Regression   = regression,
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Bygger en liste med <see cref="LongitudinalInsight"/>-objekter via
        /// <see cref="LongitudinalInsightEngine.Compute"/>, eller null når tjenestene ikke er
        /// injisert eller noe feiler. Bygger først VoiceDevelopmentProfile internt.
        /// BESKRIVENDE INTELLIGENS — overstyre aldri health/safety/recovery-gater.
        /// </summary>
        public IReadOnlyList<LongitudinalInsight>? TryBuildLongitudinalInsights(int userId, DateTime now)
        {
            if (_insightEngine == null)
                return null;

            try
            {
                var profile = TryBuildDevelopmentProfile(userId, now);
                if (profile == null)
                    return null;

                var allWindows = profile.WeeklyTrend.Concat(profile.MonthlyTrend).ToList();
                return _insightEngine.Compute(
                    allWindows,
                    profile.Plateau,
                    profile.Breakthrough,
                    profile.Regression,
                    profile);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Beregner en <see cref="InsightExplanation"/> for en gitt anbefaling via
        /// <see cref="RecommendationExplanationEngine.Compute"/>, eller null når tjenesten
        /// ikke er injisert eller noe feiler. Utleder fokus-dimensjon fra FocusArea-strengen;
        /// gjenbruker recovery-data og effektivitetsprofil fra eksisterende Try*-metoder.
        /// BESKRIVENDE INTELLIGENS — overstyre aldri health/safety/recovery-gater.
        /// </summary>
        public InsightExplanation? TryExplainRecommendation(
            SmartCoachDailyRecommendation reco, int userId, DateTime now)
        {
            if (_explanationEngine == null)
                return null;

            try
            {
                var focus = FocusAreaToDimension(reco.FocusArea);

                // Hent effektivitetsprofil for den anbefalte øvelsen (gjenbruk eksisterende bro).
                ExerciseEffectivenessProfile? effectivenessProfile = null;
                if (reco.RecommendedExerciseId > 0)
                {
                    var allEffectiveness = TryReadExerciseEffectiveness(userId);
                    effectivenessProfile = allEffectiveness
                        ?.FirstOrDefault(e => e.ExerciseId == reco.RecommendedExerciseId);
                }

                // Recovery-snapshot — bruk forecast hvis tilgjengelig, ellers standard nøytral.
                var forecast = TryGetRecoveryForecast(userId);
                var recovery = forecast?.Current
                    ?? new RecoveryResult { Score = 100.0, Status = RecoveryStatus.WellRecovered, Explanation = string.Empty };

                // Nyeste TrendWindow (7-dagers foretrukket, ellers 30-dagers).
                TrendWindow? recentWindow = null;
                if (_trendEngine != null)
                {
                    var windows = Task.Run(() => _trendEngine.AnalyzeAsync(now, userId))
                        .GetAwaiter().GetResult();
                    recentWindow = windows.FirstOrDefault(w => w.WindowDays == 7 && w.HasEnoughData)
                                ?? windows.FirstOrDefault(w => w.WindowDays == 30 && w.HasEnoughData);
                }

                // Stil for tone-framing.
                var voiceGoalProfile = _voiceGoalProfiles?.GetProfile(userId);
                var style = ResolveVoiceStyle(userId, voiceGoalProfile) ?? VoiceStyleGoal.Feminine;

                return _explanationEngine.Compute(focus, effectivenessProfile, recovery, recentWindow, style);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Bygger en <see cref="VoiceKnowledgeGraph"/> ved å forhåndshente nødvendige data og
        /// kalle <see cref="VoiceKnowledgeGraphBuilder.Build"/>, eller null når builder ikke er
        /// injisert eller noe feiler. Henter trend-punkter, goal-profil og effektivitetsprofiler.
        /// BESKRIVENDE INTELLIGENS — overstyre aldri health/safety/recovery-gater.
        /// </summary>
        public VoiceKnowledgeGraph? TryBuildKnowledgeGraph(int userId, DateTime now)
        {
            if (_knowledgeGraphBuilder == null)
                return null;

            try
            {
                IReadOnlyList<VoiceIntelligenceTrendPoint> trendPoints = Array.Empty<VoiceIntelligenceTrendPoint>();
                if (_voiceIntelligence != null)
                {
                    var from = now.AddDays(-180);
                    trendPoints = Task.Run(() =>
                        _voiceIntelligence.GetVoiceIntelligenceTrendAsync(from, now, userId))
                        .GetAwaiter().GetResult();
                }

                var goalProfile     = _voiceGoalProfiles?.GetProfile(userId);
                var exerciseProfiles = TryReadExerciseEffectiveness(userId)
                    ?? Array.Empty<ExerciseEffectivenessProfile>();

                var input = new VoiceKnowledgeGraphInput
                {
                    UserId           = userId,
                    GoalProfile      = goalProfile,
                    TrendPoints      = trendPoints,
                    Insights         = Array.Empty<SessionInsight>(),
                    ExerciseProfiles = exerciseProfiles,
                };

                return _knowledgeGraphBuilder.Build(input);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Henter råd-historikk for brukeren i det angitte tidsintervallet, eller null når
        /// <see cref="SmartCoachMemoryStore"/> ikke er injisert eller lesingen feiler.
        /// Async-over-sync (Task.Run) for WPF-deadlock-sikkerhet.
        /// </summary>
        public IReadOnlyList<SmartCoachAdviceEntry>? TryGetAdviceHistory(
            int userId, DateTime from, DateTime to)
        {
            if (_memory == null)
                return null;

            try
            {
                return Task.Run(() => _memory.GetAdviceHistoryAsync(userId, from, to))
                    .GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Beregner (adherenceRate, successRate) fra råd-historikken, eller (0, 0) når
        /// <see cref="SmartCoachMemoryStore"/> ikke er injisert, lesingen feiler, eller
        /// historikken er tom. Async-over-sync (Task.Run) for WPF-deadlock-sikkerhet.
        /// </summary>
        public (double adherence, double successRate) TryGetCoachMemoryStats(int userId, DateTime now)
        {
            if (_memory == null)
                return (0.0, 0.0);

            try
            {
                var from    = now.AddDays(-30);
                var history = Task.Run(() => _memory.GetAdviceHistoryAsync(userId, from, now))
                    .GetAwaiter().GetResult();

                if (history == null || history.Count == 0)
                    return (0.0, 0.0);

                var adherence   = SmartCoachMemoryStore.ComputeAdherenceRate(history);
                var successRate = SmartCoachMemoryStore.ComputeRecommendationSuccessRate(history);
                return (adherence, successRate);
            }
            catch
            {
                return (0.0, 0.0);
            }
        }

        /// <summary>
        /// Oversetter en FocusArea-streng til nærmeste <see cref="VoiceDimension"/>.
        /// Ukjente verdier mappes til <see cref="VoiceDimension.Resonance"/> (trygg fallback).
        /// </summary>
        private static VoiceDimension FocusAreaToDimension(string? focusArea)
        {
            return (focusArea ?? string.Empty).ToLowerInvariant() switch
            {
                "recovery"    => VoiceDimension.Recovery,
                "comfort"     => VoiceDimension.Comfort,
                "resonance"   => VoiceDimension.Resonance,
                "consistency" => VoiceDimension.Consistency,
                "intonation"  => VoiceDimension.Intonation,
                "vocalweight" => VoiceDimension.VocalWeight,
                "pitch"       => VoiceDimension.Pitch,
                "balanced"    => VoiceDimension.Resonance,
                "breathing"   => VoiceDimension.Comfort,
                _             => VoiceDimension.Resonance,
            };
        }

        #endregion

        /// <summary>
        /// Recovery-FØR-mål-gate. Når et prediktivt forecast melder Severity &gt;= Recommend
        /// ELLER LearningPath-profilen sier RestRecommended, gir vi restitusjons-coaching
        /// (stil-fargelagt) FØR ethvert mål — Health/Recovery &gt; Goals. Dette er IKKE en
        /// safety-blokk: ingen helse-advarsel heises (den eies av strain-gaten over).
        /// Returnerer null når ingen av tjenestene er injisert eller terskelen ikke nås.
        /// </summary>
        private (string focusArea, string text, int exerciseId, int duration)? TryGetRecoveryForecastRecommendation(
            RecoveryForecast? forecast,
            LearningPathProfile? learningPath,
            VoiceStyleGoal? style)
        {
            var forecastWantsRest = forecast.HasValue && forecast.Value.Severity >= RecoverySeverity.Recommend;
            var learningPathWantsRest = learningPath?.RecoveryRequirements.RestRecommended == true;

            if (!forecastWantsRest && !learningPathWantsRest)
                return null;

            var text = StyleAwareText("recovery", _localization.GetString("SmartCoach_Focus_Recovery"), style);
            return ("recovery", text, 7, 5);
        }

        #region Adaptiv varighet (Sprint C, Agent 6-varighet)

        /// <summary>
        /// Henter (recovery, consistency) som 0–1-signaler for varighetsskalering, eller
        /// (null, null) når intet bilde finnes. Recovery tas fra forecastets reaktive score;
        /// consistency fra LearningPath-svakhetsvurderingen (siste trendpunkt). Begge er
        /// rent beskrivende — de strammer/forlenger varighet, aldri prioritet.
        /// </summary>
        private static (double? recovery01, double? consistency01) ResolveDurationSignals(
            RecoveryForecast? forecast, LearningPathProfile? learningPath)
        {
            double? recovery01 = forecast.HasValue
                ? Math.Clamp(forecast.Value.Current.Score, 0.0, 100.0)
                : (double?)null;

            double? consistency01 = null;
            if (learningPath != null)
            {
                // Finn consistency-dimensjonens siste score blant strengths/weaknesses.
                var all = learningPath.Strengths.Concat(learningPath.Weaknesses);
                var consistency = all.FirstOrDefault(a => a.Dimension == VoiceDimension.Consistency);
                if (consistency != null)
                    consistency01 = Math.Clamp(consistency.Score, 0.0, 100.0);
            }

            return (recovery01, consistency01);
        }

        /// <summary>
        /// Skalerer en basis-varighet (minutter) fra recovery/konsistens innenfor de
        /// trygge grensene [<see cref="MinAdaptiveDurationMinutes"/>,
        /// <see cref="MaxAdaptiveDurationMinutes"/>].
        ///
        /// REGLER (Health/Recovery &gt; Goals):
        ///   • recoveryOriented ⇒ alltid kort ned (en restitusjonsdag skal være lett).
        ///   • Lav recovery (&lt; <see cref="DurationRecoveryShortenThreshold"/>) ⇒ kort ned.
        ///   • God recovery (&gt;= <see cref="DurationRecoveryLengthenThreshold"/>) OG høy
        ///     konsistens (&gt;= <see cref="DurationConsistencyLengthenThreshold"/>) ⇒ litt
        ///     lengre — men ALDRI forbi taket, og bare med ett lite steg.
        ///   • Ellers / manglende signal ⇒ uendret basis (clampet til grensene).
        /// Forlengelse er bevisst konservativ: vi forlenger aldri en allerede strammet økt.
        /// </summary>
        private static int ScaleDuration(
            int baseMinutes, double? recoveryScore, double? consistencyScore, bool recoveryOriented)
        {
            var minutes = baseMinutes;

            if (recoveryOriented)
            {
                // En restitusjonsorientert økt skal være lett: kort ned, aldri forleng.
                minutes -= DurationShortenStepMinutes;
                return Clamp(minutes);
            }

            var shortened = false;
            if (recoveryScore.HasValue && recoveryScore.Value < DurationRecoveryShortenThreshold)
            {
                minutes -= DurationShortenStepMinutes;
                shortened = true;
            }

            // Forleng KUN ved god recovery + høy konsistens, og aldri en allerede strammet økt.
            if (!shortened
                && recoveryScore.HasValue && recoveryScore.Value >= DurationRecoveryLengthenThreshold
                && consistencyScore.HasValue && consistencyScore.Value >= DurationConsistencyLengthenThreshold)
            {
                minutes += DurationLengthenStepMinutes;
            }

            return Clamp(minutes);

            static int Clamp(int m) =>
                Math.Clamp(m, MinAdaptiveDurationMinutes, MaxAdaptiveDurationMinutes);
        }

        #endregion

        private (string focusArea, string text, int exerciseId, int duration)? TryGetGoalProfileRecommendation(
            VoiceGoalProfile? profile,
            SmartCoachBaseline? baseline,
            List<TrainingSession> recentSessions)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.PrimaryFocus))
                return null;

            var focus = profile.PrimaryFocus.Trim().ToLowerInvariant();

            if (focus == "resonance")
            {
                var recommendation = GetResonanceRecommendation(baseline, recentSessions);
                return ("resonance", recommendation.text, recommendation.exerciseId, recommendation.duration);
            }

            if (focus == "intonation")
            {
                var recommendation = GetIntonationRecommendation();
                return ("intonation", recommendation.text, recommendation.exerciseId, recommendation.duration);
            }

            if (focus == "breathing")
                return ("breathing", _localization.GetString("SmartCoach_Daily_Breathing"), 7, 5);

            if (focus == "pitch" && baseline?.BaselineResonanceScore >= ResonancePriorityThreshold)
            {
                var recommendation = GetPitchControlRecommendation(baseline);
                return ("pitch", recommendation.text, recommendation.exerciseId, recommendation.duration);
            }

            return null;
        }

        #region VoiceMetrics-drevet aksevalg (Bølge 2)

        /// <summary>
        /// VoiceMetrics-drevet fokusvalg: leser siste VoiceIntelligence-trendpunkt og
        /// velger coaching-akse på den SVAKESTE dimensjonen.
        ///
        /// KLINISK HIERARKI (tie-break): Comfort &gt; Resonance &gt; Consistency &gt;
        /// Intonation &gt; Pitch. Recovery er en helse-dimensjon og veies høyest av alle
        /// (Health &gt; alt annet) når den er den svakeste. Pitch velges ALDRI som fokus
        /// så lenge en høyere-prioritert dimensjon er like svak eller svakere.
        ///
        /// Dette er IKKE en safety-gate: den kjører bevisst etter health-gaten i
        /// <see cref="GenerateDailyRecommendation"/>. Returnerer null når
        ///   • ingen VoiceIntelligence-kilde er injisert (null-provider ⇒ dagens
        ///     oppførsel, full bakoverkompat),
        ///   • det ikke finnes noe ekte (ikke-nøytralt) siste øktpunkt, eller
        ///   • ingen dimensjon er under <see cref="DimensionWeakThreshold"/>.
        /// I alle disse tilfellene faller daglig-anbefalingen tilbake til den
        /// eksisterende baseline-/goal-profile-logikken.
        /// </summary>
        private (string focusArea, string text, int exerciseId, int duration)? TryGetVoiceIntelligenceRecommendation(
            int userId,
            SmartCoachBaseline? baseline,
            List<TrainingSession> recentSessions)
        {
            var latest = TryReadLatestVoiceIntelligence(userId);
            if (latest == null)
                return null;

            var axis = SelectVoiceIntelligenceFocus(latest);
            if (axis == null)
                return null;

            switch (axis)
            {
                case "recovery":
                    // Lav restitusjon: behandle som en støttende, lavterskel
                    // restitusjonsdag. Speiler recovery-grenen i health-gaten, men
                    // uten å være en blokk — en mild omdirigering, ikke en advarsel.
                    return ("recovery", _localization.GetString("SmartCoach_Focus_Recovery"), 7, 5);

                case "comfort":
                    return ("comfort", _localization.GetString("SmartCoach_Focus_Comfort"), 7, 5);

                case "resonance":
                {
                    var recommendation = GetResonanceRecommendation(baseline, recentSessions);
                    return ("resonance", recommendation.text, recommendation.exerciseId, recommendation.duration);
                }

                case "consistency":
                    return ("consistency", _localization.GetString("SmartCoach_Focus_Consistency"), 5, 6);

                case "intonation":
                {
                    var recommendation = GetIntonationRecommendation();
                    return ("intonation", recommendation.text, recommendation.exerciseId, recommendation.duration);
                }

                case "pitch":
                {
                    var recommendation = GetPitchControlRecommendation(baseline);
                    return ("pitch", recommendation.text, recommendation.exerciseId, recommendation.duration);
                }

                default:
                    return null;
            }
        }

        /// <summary>
        /// Velger den svakeste VoiceMetrics-dimensjonen som coaching-akse, eller null
        /// når ingen dimensjon er svak nok (eller når kun den nøytrale 50-fallbacken
        /// finnes — da har vi ikke et reelt signal å handle på).
        ///
        /// Hierarkiet (Health-først) avgjør ved likhet: en lavere prioritetsverdi
        /// vinner. Dermed kan Pitch — som har den HØYESTE prioritetsverdien — aldri
        /// kapre fokus fra en like svak Comfort/Resonance/Consistency/Intonation, i
        /// tråd med «Pitch aldri eneste/viktigste».
        /// </summary>
        private static string? SelectVoiceIntelligenceFocus(VoiceIntelligenceTrendPoint p)
        {
            // (akse, score, hierarki-prioritet) — lavere prioritet = klinisk viktigere.
            // Recovery er Health og veies øverst; Pitch nederst.
            var candidates = new (string Axis, double Score, int Priority)[]
            {
                ("recovery", p.RecoveryScore100, 0),
                ("comfort", p.ComfortScore100, 1),
                ("resonance", p.ResonanceScore100, 2),
                ("consistency", p.ConsistencyScore100, 3),
                ("intonation", p.IntonationScore100, 4),
                ("pitch", p.PitchScore100, 5),
            };

            string? bestAxis = null;
            var bestScore = double.MaxValue;
            var bestPriority = int.MaxValue;

            // Manglende-signal-dimensjoner (Intonation/VocalWeight/Pitch ved ingen voiced
            // tick / ingen formanter) faller tilbake til NØYAKTIG 50.0. En slik umålt
            // dimensjon skal ALDRI velges som coaching-fokus (ellers kunne en umålt Pitch
            // kapre fokus fra ekte målt data — validering-funn). Vi hopper derfor over
            // kandidater som ligger på den nøytrale fallbacken. En ekte måling som ved
            // tilfeldighet treffer 50 mister kun målrettet coaching den dagen (faller
            // trygt til baseline-logikken), så dette er konservativt.
            const double NeutralFallback = 50.0;
            const double NeutralEpsilon = 1e-6;

            foreach (var c in candidates)
            {
                if (c.Score >= DimensionWeakThreshold)
                    continue;

                if (Math.Abs(c.Score - NeutralFallback) < NeutralEpsilon)
                    continue;

                // Strengt lavere score vinner; ved likhet vinner høyere-prioritert
                // (lavere Priority-tall) dimensjon.
                if (c.Score < bestScore ||
                    (c.Score == bestScore && c.Priority < bestPriority))
                {
                    bestAxis = c.Axis;
                    bestScore = c.Score;
                    bestPriority = c.Priority;
                }
            }

            return bestAxis;
        }

        /// <summary>
        /// Leser det nyeste VoiceIntelligence-trendpunktet for brukeren, eller null
        /// når ingen kilde er injisert, lesingen feiler, eller punktet er rent nøytralt
        /// (alle de fire råaggregat-baserte dimensjonene = 50 ⇒ intet reelt signal).
        ///
        /// Lesingen er async i kilden; her kjører vi den trygt synkront på en
        /// thread-pool-tråd (Task.Run) for å unngå enhver SynchronizationContext-
        /// deadlock i WPF. In-memory-kilden fullfører uansett synkront.
        /// </summary>
        private VoiceIntelligenceTrendPoint? TryReadLatestVoiceIntelligence(int userId)
        {
            if (_voiceIntelligence == null)
                return null;

            try
            {
                var to = DateTime.Now;
                var from = to.AddDays(-VoiceIntelligenceLookbackDays);

                var trend = Task.Run(() =>
                    _voiceIntelligence.GetVoiceIntelligenceTrendAsync(from, to, userId))
                    .GetAwaiter().GetResult();

                if (trend == null || trend.Count == 0)
                    return null;

                // GetVoiceIntelligenceTrendAsync returnerer kronologisk; siste = nyeste.
                var latest = trend[trend.Count - 1];

                // Et helt nøytralt punkt (KJENT GAP fra Bølge 1: intonasjon/vekt/pitch-
                // aggregater mangler ⇒ nøytral 50) gir ikke noe reelt signal å handle
                // på. Krev minst én ekte dimensjon som avviker fra 50 før vi overstyrer
                // baseline-logikken.
                if (IsNeutralPoint(latest))
                    return null;

                return latest;
            }
            catch
            {
                // Null-safe degradering: enhver lesefeil ⇒ dagens baseline-oppførsel.
                return null;
            }
        }

        /// <summary>
        /// Et trendpunkt er «nøytralt» når samtlige sju dimensjoner ligger på den
        /// nøytrale 50-fallbacken (intet reelt VoiceMetrics-signal er tilgjengelig).
        /// </summary>
        private static bool IsNeutralPoint(VoiceIntelligenceTrendPoint p)
        {
            const double neutral = 50.0;
            const double epsilon = 0.0001;

            return Math.Abs(p.ResonanceScore100 - neutral) < epsilon
                && Math.Abs(p.ComfortScore100 - neutral) < epsilon
                && Math.Abs(p.ConsistencyScore100 - neutral) < epsilon
                && Math.Abs(p.IntonationScore100 - neutral) < epsilon
                && Math.Abs(p.VocalWeightScore100 - neutral) < epsilon
                && Math.Abs(p.RecoveryScore100 - neutral) < epsilon
                && Math.Abs(p.PitchScore100 - neutral) < epsilon;
        }

        #endregion

        private (string text, int exerciseId, int duration) GetResonanceRecommendation(SmartCoachBaseline? baseline, List<TrainingSession> recentSessions)
        {
            var resonanceScore = baseline?.BaselineResonanceScore ?? 0;
            
            if (resonanceScore < 40)
            {
                return (
                    _localization.GetString("SmartCoach_Daily_ResonanceFoundation"),
                    1,
                    8
                );
            }
            else if (resonanceScore < 60)
            {
                return (
                    _localization.GetString("SmartCoach_Daily_ResonanceDevelopment"),
                    2,
                    6
                );
            }
            else
            {
                return (
                    _localization.GetString("SmartCoach_Daily_ResonanceMaintenance"),
                    3,
                    5
                );
            }
        }
        
        private (string text, int exerciseId, int duration) GetPitchControlRecommendation(SmartCoachBaseline? baseline)
        {
            var currentPitch = baseline?.BaselinePitch ?? 165;
            
            if (currentPitch < 160)
            {
                return (
                    _localization.GetString("SmartCoach_Daily_PitchControl"),
                    4,
                    7
                );
            }
            else
            {
                return (
                    _localization.GetString("SmartCoach_Daily_PitchStabilize"),
                    5,
                    5
                );
            }
        }
        
        private (string text, int exerciseId, int duration) GetIntonationRecommendation()
        {
            return (
                _localization.GetString("SmartCoach_Daily_Intonation"),
                6,
                6
            );
        }
        
        private (string text, int exerciseId, int duration) GetBalancedRecommendation(List<TrainingSession> recentSessions)
        {
            var hasBreathingIssues = recentSessions.Any(s => s.AveragePitch < 100);
            
            if (hasBreathingIssues)
            {
                return (
                    _localization.GetString("SmartCoach_Daily_Breathing"),
                    7,
                    5
                );
            }
            
            return (
                _localization.GetString("SmartCoach_Daily_Balanced"),
                8,
                6
            );
        }
        
        private (string text, int exerciseId, int duration) GetStarterRecommendation()
        {
            return (
                _localization.GetString("SmartCoach_Daily_Starter"),
                1,
                5
            );
        }
        
        private string GetRecoveryRecommendation(string? strainType)
        {
            return strainType switch
            {
                "pitch_press" => _localization.GetString("SmartCoach_Health_PitchPress"),
                "noise" => _localization.GetString("SmartCoach_Health_Noise"),
                "fatigue" => _localization.GetString("SmartCoach_Health_Fatigue"),
                _ => _localization.GetString("SmartCoach_Health_Default")
            };
        }
        
        #endregion
        
        #region Health Monitoring
        
        /// <summary>
        /// Analyserer en treningsøkt for tegn på anstrengelse
        /// </summary>
        public SmartCoachHealthMonitoring AnalyzeSessionForStrain(TrainingSession session, int userId = 1)
        {
            var health = new SmartCoachHealthMonitoring
            {
                UserId = userId,
                Date = session.StartTime.Date,
                SessionId = session.Id,
                CreatedAt = DateTime.Now
            };
            
            bool strainDetected = false;
            string? strainType = null;
            double strainLevel = 0;
            string? recommendation = null;
            
            // Sjekk for pitch press (høy pitch + høy intensitet over tid)
            if (session.AveragePitch > PitchPressThreshold)
            {
                strainDetected = true;
                strainType = "pitch_press";
                strainLevel = Math.Min(100, (session.AveragePitch - PitchPressThreshold) * 2 + 30);
                recommendation = _localization.GetString("SmartCoach_Strain_PitchPress");
            }
            
            // Sjekk for plutselig score-drop (fatigue)
            var recentSessions = _database.GetRecentSessions(5, userId);
            if (recentSessions.Count >= 1)
            {
                // Compare current session against most recent session in database (index 0)
                var previousSession = recentSessions[0];
                var scoreDrop = previousSession.OverallScore - session.OverallScore;
                
                if (scoreDrop > FatigueScoreDrop)
                {
                    strainDetected = true;
                    strainType = "fatigue";
                    strainLevel = scoreDrop;
                    recommendation = _localization.GetString("SmartCoach_Strain_Fatigue");
                }
            }
            
            // Sett helse-objektet
            health.StrainDetected = strainDetected;
            health.StrainType = strainType;
            health.StrainLevel = strainLevel;
            health.Recommendation = recommendation;
            
            // Lagre til database hvis det ble funnet anstrengelse
            if (strainDetected)
            {
                _database.SaveHealthMonitoring(health);
                
                // Generer også en helse-advarsel melding
                var message = new SmartCoachMessage
                {
                    UserId = userId,
                    Date = DateTime.Today,
                    MessageType = "health_warning",
                    Title = _localization.GetString("SmartCoach_HealthWarning"),
                    Message = recommendation ?? _localization.GetString("SmartCoach_Health_Default"),
                    CreatedAt = DateTime.Now
                };
                SaveCoachMessageThroughPipeline(message);
            }
            
            return health;
        }
        
        /// <summary>
        /// Genererer coach-meldinger basert på progresjon
        /// </summary>
        public void GenerateMotivationalMessages(int userId = 1)
        {
            var progress = _database.GetRecentWeeklyProgress(1, userId).FirstOrDefault();
            if (progress == null) return;

            // ==== HEALTH GATE: live helse/fatigue undertrykker ros & motivasjon ====
            // Samme tidlige helse-gate som GenerateDailyRecommendation (~324). Tidligere
            // bygde denne metoden guard-konteksten utelukkende fra meldingens egen type og
            // sjekket ALDRI persistert helse — en achievement/motivation-melding manglet
            // strain-flagging og kunne slippe gjennom under aktiv anstrengelse. Nå leser vi
            // GetRecentHealthIssues FØRST: ved aktiv strain produserer vi ingen ros/
            // motivasjon (Safety > Coaching). Selve health_warning-meldingen rutes allerede
            // av AnalyzeSessionForStrain ved øktslutt; her er det riktig å være stille.
            var healthIssues = _database.GetRecentHealthIssues(userId: userId, days: 3);
            if (healthIssues.Any(h => h.StrainDetected))
                return;

            // Sjekk om det er nylig melding
            var unreadCount = _database.GetUnreadMessageCount(userId);
            if (unreadCount > 0) return;
            
            string messageType;
            string title;
            string message;

            // ==== MESTRING/CONFIDENCE (Sprint C, Agent 7) ══════════════════════════════
            // POSITIV ramme: bygg en bro fra MasteryEvaluator (Stable/Mastered) til en
            // FEIRENDE, BEKREFTENDE melding. Tidligere fantes kun NEGATIVE rammeverk (forby
            // skam / demp). Når brukeren har konsolidert en øvelse (mestring), feirer vi det
            // som mestring og oppmuntrer TRYGG utforskning — aldri press. Kjører ETTER
            // health-gaten (Safety > Coaching) og foran de score-baserte rosene, fordi en
            // verifisert mestring er det sterkeste positive signalet vi har. null-provider
            // (eller Beginner/Developing) ⇒ vi faller trygt gjennom til dagens oppførsel.
            var masteryLevel = _masteryLevelProvider?.Invoke(userId);
            if (masteryLevel is MasteryLevel.Stable or MasteryLevel.Mastered)
            {
                messageType = "achievement";
                title = _localization.GetString("SmartCoach_Mastery_Title");
                message = masteryLevel == MasteryLevel.Mastered
                    ? _localization.GetString("SmartCoach_Mastery_Mastered")
                    : _localization.GetString("SmartCoach_Mastery_Stable");

                var masteryMessage = new SmartCoachMessage
                {
                    UserId = userId,
                    Date = DateTime.Today,
                    MessageType = messageType,
                    Title = title,
                    Message = message,
                    CreatedAt = DateTime.Now
                };
                SaveCoachMessageThroughPipeline(masteryMessage);
                return;
            }

            // Basert på progresjon
            if (progress.AverageScore > 80)
            {
                messageType = "achievement";
                title = _localization.GetString("SmartCoach_Message_AchievementTitle");
                message = _localization.GetFormattedString("Coach_WeeklyAverage", progress.AverageScore);
            }
            else if (progress.ResonanceChange > 5)
            {
                messageType = "tip";
                title = _localization.GetString("SmartCoach_Message_ResonanceTitle");
                message = _localization.GetString("SmartCoach_Message_ResonanceImproved");
            }
            else if (progress.PitchChange > 3)
            {
                messageType = "motivation";
                title = _localization.GetString("SmartCoach_Message_PitchTitle");
                message = _localization.GetString("SmartCoach_Message_PitchProgress");
            }
            else if (progress.SessionsCount >= GetWeeklySessionTarget(userId))
            {
                // Brukeren har nådd sitt EGET ukentlige økt-mål (UserVoiceProfile
                // .TrainingFrequencyPerWeek). Tidligere lå terskelen hardkodet på 5;
                // nå feirer vi brukerens egen valgte kadens. Støttende ramme — aldri
                // «du ligger bak».
                messageType = "motivation";
                title = _localization.GetString("SmartCoach_Message_ConsistentTitle");
                message = _localization.GetFormattedString("Coach_SessionsThisWeek", progress.SessionsCount);
            }
            else
            {
                messageType = "tip";
                title = _localization.GetString("SmartCoach_Message_WeeklyTipTitle");
                message = _localization.GetString("Coach_QualityOverQuantity");
            }
            
            var coachMessage = new SmartCoachMessage
            {
                UserId = userId,
                Date = DateTime.Today,
                MessageType = messageType,
                Title = title,
                Message = message,
                CreatedAt = DateTime.Now
            };
            
            SaveCoachMessageThroughPipeline(coachMessage);
        }

        private void SaveCoachMessageThroughPipeline(SmartCoachMessage message)
        {
            if (_feedbackPipeline == null || _feedbackMapper == null)
            {
                _database.SaveCoachMessage(message);
                return;
            }

            var candidate = _feedbackMapper.Map(message);
            if (candidate == null)
                return;

            var decision = _feedbackPipeline.Submit(candidate, _feedbackMapper.BuildContext(message));
            if (decision.Kind == FeedbackDecisionKind.Approved)
                _database.SaveCoachMessage(message);
        }
        
        /// <summary>
        /// Beregner estimert total treningsmengde for uken
        /// </summary>
        public int EstimateWeeklyTrainingMinutes(int userId = 1)
        {
            var thisWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var progress = _database.GetWeeklyProgress(thisWeekStart, userId);
            
            if (progress != null)
            {
                return progress.TotalMinutes;
            }
            
            // Estimér basert på gjennomsnitt
            var recentProgress = _database.GetRecentWeeklyProgress(2, userId);
            if (recentProgress.Any())
            {
                return (int)recentProgress.Average(p => p.TotalMinutes);
            }
            
            return 0;
        }
        
        /// <summary>
        /// Genererer en kort status-analyse
        /// </summary>
        public string GetStatusSummary(int userId = 1)
        {
            var baseline = GetOrCalculateBaseline(userId);
            var recentSessions = _database.GetRecentSessions(5, userId);
            var healthIssues = _database.GetRecentHealthIssues(userId: userId, days: 3);
            
            if (baseline == null || baseline.ConfidenceLevel == "low")
            {
                return _localization.GetString("SmartCoach_Status_NewUser");
            }
            
            if (healthIssues.Any(h => h.StrainDetected))
            {
                return _localization.GetString("SmartCoach_Status_VoiceHealthCareful");
            }
            
            var resonanceStatus = baseline.BaselineResonanceScore >= ResonancePriorityThreshold
                ? _localization.GetString("SmartCoach_Status_Good")
                : _localization.GetString("SmartCoach_Status_NeedsImprovement");
            var pitchStatus = baseline.BaselinePitch >= 170
                ? _localization.GetString("SmartCoach_Status_GoodShort")
                : _localization.GetString("SmartCoach_Status_Developing");
            
            return _localization.GetFormattedString("SmartCoach_Status_Summary", resonanceStatus, pitchStatus);
        }
        
        #endregion
    }
}
