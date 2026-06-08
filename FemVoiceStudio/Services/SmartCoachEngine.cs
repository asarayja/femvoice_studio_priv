using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

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
        private readonly Random _random = new();
        
        // Konstanter for terskelverdier
        private const double ResonancePriorityThreshold = 70.0; // Prioriter resonans under 70%
        private const double PitchPressThreshold = 180.0; // Hz - over dette regnes som press
        private const double FatigueScoreDrop = 20.0; // Prosent
        private const int BaselineMinDays = 7; // Minimum dager for baseline
        private const int BaselineIdealDays = 14; // Ideelle dager for baseline

        // Fallback for ukentlig økt-mål når brukeren ikke har en kalibrert
        // UserVoiceProfile ennå. Speiler standardverdien i UserVoiceProfile
        // (TrainingFrequencyPerWeek = 3) og WeeklyGoals-tabellen (TargetSessions
        // DEFAULT 3), så ingen ny parallell konstant innføres.
        private const int DefaultWeeklySessionTarget = 3;
        
        /// <summary>
        /// Constructor with dependency injection (recommended)
        /// </summary>
        public SmartCoachEngine(
            IDatabaseService database,
            ILocalizationService? localization = null,
            FeedbackPipeline? feedbackPipeline = null,
            SmartCoachFeedbackMapper? feedbackMapper = null,
            IVoiceGoalProfileProvider? voiceGoalProfiles = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _localization = localization ?? LocalizationService.Instance;
            _feedbackPipeline = feedbackPipeline;
            _feedbackMapper = feedbackMapper;
            _voiceGoalProfiles = voiceGoalProfiles;
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
            // beholdes uendret. (Stil-bevissthet for DarkFeminine er utelatt her — kan
            // legges til som egen demping senere; dokumentert i contractNotes.)
            var goalHealthIssues = _database.GetRecentHealthIssues(userId: userId, days: 3);
            var strainActive = goalHealthIssues.Any(h => h.StrainDetected);

            // Mål 1: Pitchøkning (realistisk: 10-20 Hz økning over 8 uker)
            // Kun når ingen aktiv strain — ellers ville vi be brukeren presse pitch opp
            // mens helse-gaten ber om restitusjon.
            if (!strainActive)
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
            
            // ==== HEALTH CHECK: Sjekk for anstrengelse ====
            if (healthIssues.Any(h => h.StrainDetected))
            {
                var lastIssue = healthIssues.First();
                recommendation.HealthWarning = true;
                recommendation.HealthWarningText = lastIssue.Recommendation;
                
                // Endre fokus til restitusjon
                recommendation.FocusArea = "recovery";
                recommendation.RecommendationText = GetRecoveryRecommendation(lastIssue.StrainType);
                recommendation.RecommendedDurationMinutes = 5;
                
                _database.SaveDailyRecommendation(recommendation);
                return recommendation;
            }
            
            // ==== ANALYSE: Bestem fokusområde ====
            // Klinisk prioritering: resonans > pitch > intonasjon > pust
            string focusArea;
            string recommendationText;
            int exerciseId;
            int duration;

            var preference = TryGetGoalProfileRecommendation(voiceGoalProfile, baseline, recentSessions);
            if (preference.HasValue)
            {
                (focusArea, recommendationText, exerciseId, duration) = preference.Value;
            }
            else if (baseline != null && baseline.BaselineResonanceScore < ResonancePriorityThreshold)
            {
                // Prioriter resonans
                focusArea = "resonance";
                (recommendationText, exerciseId, duration) = GetResonanceRecommendation(baseline, recentSessions);
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
            
            recommendation.FocusArea = focusArea;
            recommendation.RecommendationText = recommendationText;
            recommendation.RecommendedExerciseId = exerciseId;
            recommendation.RecommendedDurationMinutes = duration;
            
            _database.SaveDailyRecommendation(recommendation);
            
            return recommendation;
        }

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
