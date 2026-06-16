using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Adaptiv vanskelighet basert på brukerens prestasjoner
    /// </summary>
    public class AdaptiveDifficultyService
    {
        private const int MinSessionsForPromotion = 3;
        private const int MinAvgScoreForPromotion = 70;
        private const int MaxAvgScoreForDemotion = 40;
        
        /// <summary>
        /// Evaluer om brukeren skal flyttes til annen vanskelighetsgrad
        /// </summary>
        public DifficultyRecommendation Evaluate(
            List<SessionPerformance> recentSessions,
            DifficultyLevel currentLevel)
        {
            if (recentSessions.Count < MinSessionsForPromotion)
            {
                return new DifficultyRecommendation
                {
                    RecommendedLevel = currentLevel,
                    Reason = LocalizationService.Instance["AdaptiveDifficulty_NotEnoughData"],
                    Confidence = 0
                };
            }
            
            double avgScore = recentSessions.Average(s => s.OverallScore);
            double avgPitchAccuracy = recentSessions.Average(s => s.PitchAccuracy);
            double consistency = CalculateConsistency(recentSessions);
            
            var recommendation = new DifficultyRecommendation
            {
                RecommendedLevel = currentLevel,
                AverageScore = avgScore,
                Consistency = consistency,
                Confidence = Math.Min(1.0, recentSessions.Count / 10.0)
            };
            
            if (avgScore >= MinAvgScoreForPromotion && 
                consistency >= 60 && 
                recentSessions.Count >= MinSessionsForPromotion)
            {
                recommendation.RecommendedLevel = GetNextLevel(currentLevel);
                recommendation.Reason = LocalizationService.Instance.GetFormattedString("AdaptiveDifficulty_PromotionFormat", avgScore);
                recommendation.ShouldCelebrate = true;
            }
            else if (avgScore <= MaxAvgScoreForDemotion && recentSessions.Count >= 5)
            {
                recommendation.RecommendedLevel = GetPreviousLevel(currentLevel);
                recommendation.Reason = LocalizationService.Instance.GetFormattedString("AdaptiveDifficulty_DemotionFormat", avgScore);
                recommendation.ShouldEncourage = true;
            }
            else if (avgScore >= MinAvgScoreForPromotion)
            {
                recommendation.Reason = LocalizationService.Instance.GetFormattedString("AdaptiveDifficulty_GoodScoreFormat", avgScore);
            }
            else
            {
                recommendation.Reason = LocalizationService.Instance.GetFormattedString("AdaptiveDifficulty_KeepPracticingFormat", avgScore);
            }
            
            return recommendation;
        }
        
        /// <summary>
        /// Anbefal spesifikk øvelse basert på svakheter
        /// </summary>
        public ExerciseRecommendation RecommendExercise(List<SessionPerformance> recentSessions)
        {
            if (recentSessions.Count < 3)
            {
                return new ExerciseRecommendation
                {
                    Category = "random",
                    Reason = LocalizationService.Instance["AdaptiveDifficulty_PracticeVariedTexts"]
                };
            }
            
            double avgPitchAccuracy = recentSessions.Average(s => s.PitchAccuracy);
            double avgVariation = recentSessions.Average(s => s.VariationScore);
            double avgIntonation = recentSessions.Average(s => s.IntonationScore);
            
            var scores = new Dictionary<string, double>
            {
                ["pitch"] = avgPitchAccuracy,
                ["variation"] = avgVariation,
                ["intonation"] = avgIntonation
            };
            
            var weakest = scores.OrderBy(s => s.Value).First();
            
            return weakest.Key switch
            {
                "pitch" => new ExerciseRecommendation
                {
                    Category = "pitch_focus",
                    TargetPitch = "tight",
                    Reason = LocalizationService.Instance["AdaptiveDifficulty_PitchFocus"]
                },
                "variation" => new ExerciseRecommendation
                {
                    Category = "variation_focus", 
                    TargetVariation = "high",
                    Reason = LocalizationService.Instance["AdaptiveDifficulty_VariationFocus"]
                },
                "intonation" => new ExerciseRecommendation
                {
                    Category = "question_focus",
                    TargetIntonation = "rising",
                    Reason = LocalizationService.Instance["AdaptiveDifficulty_IntonationFocus"]
                },
                _ => new ExerciseRecommendation { Category = "random" }
            };
        }
        
        // ============================================================================
        // ADAPTIV VOLUM (Sprint C — Agent VOL)
        // ============================================================================
        //
        // Øvelsesvolum (antall øvelser + sett per økt) ble aldri justert noe sted —
        // forsidens treningsløkke kjørte én øvelse av gangen i det uendelige. Denne
        // rene/testbare beregningen foreslår et volum ut fra Comfort/Recovery/
        // Consistency/Health, og STRAMMER ALLTID inn ved recovery-/helse-behov.
        //
        // Klinisk invariant (ufravikelig): Health > Progression. Et recovery-/helse-
        // behov kan ALDRI overstyres av god konsistens — lav recovery eller lav helse
        // gir alltid FÆRRE/lettere øvelser, uansett hvor jevn brukeren er. Først NÅR
        // recovery og helse er trygge får god konsistens lov å løfte volumet moderat.
        //
        // Metoden er total og ren: ingen DB, ingen IO, ingen DateTime.Now. Alle utfall
        // er klampet til [MinExerciseCount, MaxExerciseCount] / [MinSetCount, MaxSetCount]
        // og er monotone i hver akse (bedre recovery/komfort/konsistens ⇒ aldri lavere
        // volum; verre ⇒ aldri høyere).

        /// <summary>Lavest tillatte antall øvelser et volum-forslag kan foreslå.</summary>
        public const int MinExerciseCount = 2;

        /// <summary>Høyest tillatte antall øvelser et volum-forslag kan foreslå.</summary>
        public const int MaxExerciseCount = 8;

        /// <summary>Standard / moderat utgangspunkt for antall øvelser.</summary>
        public const int BaselineExerciseCount = 4;

        /// <summary>Lavest tillatte antall sett.</summary>
        public const int MinSetCount = 1;

        /// <summary>Høyest tillatte antall sett.</summary>
        public const int MaxSetCount = 3;

        /// <summary>
        /// Recovery-/helse-score på/under denne grensen tvinger volumet NED (recovery-
        /// gate). Health &gt; Progression: ingen konsistens kan oppveie dette.
        /// </summary>
        public const double LowRecoveryHealthThreshold = 50.0;

        /// <summary>
        /// Recovery-/helse-/komfort-score på/over denne grensen regnes som "trygg" — en
        /// forutsetning for at god konsistens får lov å løfte volumet.
        /// </summary>
        public const double SafeRecoveryHealthThreshold = 70.0;

        /// <summary>Konsistens på/over denne gir et moderat volumløft (kun når trygt).</summary>
        public const double StrongConsistencyThreshold = 70.0;

        /// <summary>
        /// Foreslår øktvolum (antall øvelser + sett) ut fra de fire styrende aksene.
        /// Alle parametre er 0–100-scorer. <paramref name="recovery"/> gir den autoritative
        /// recovery-statusen (Strained/Overtrained tvinger alltid inn-stramming, uavhengig
        /// av tallene). Ren, total, klampet og monoton — se region-kommentaren over.
        /// </summary>
        /// <param name="recovery">Autoritativ recovery-status (Health-laget).</param>
        /// <param name="comfortScore">Komfort-score 0–100 (Health-nær).</param>
        /// <param name="consistencyScore">Konsistens-score 0–100 (Progression-signal).</param>
        /// <param name="healthScore">Helse-score 0–100 (uke-/øktbasert).</param>
        public VolumeRecommendation RecommendVolume(
            RecoveryResult recovery,
            double comfortScore = 70,
            double consistencyScore = 70,
            double healthScore = 100)
        {
            comfortScore     = ClampScore(comfortScore);
            consistencyScore = ClampScore(consistencyScore);
            healthScore      = ClampScore(healthScore);
            var recoveryScore = ClampScore(recovery.Score);

            // ── 1. RECOVERY-/HELSE-GATE (Health > Progression — kan ALDRI overstyres) ──
            // Strained/Overtrained ELLER lav recovery/helse ⇒ alltid det laveste volumet.
            var recoveryGated =
                recovery.Status == RecoveryStatus.Overtrained ||
                recovery.Status == RecoveryStatus.Strained ||
                recoveryScore <= LowRecoveryHealthThreshold ||
                healthScore   <= LowRecoveryHealthThreshold;

            if (recoveryGated)
            {
                // Overtrained er strengere enn Strained / akkurat-under-grensen: minimum.
                var hard =
                    recovery.Status == RecoveryStatus.Overtrained ||
                    recoveryScore <= LowRecoveryHealthThreshold / 2.0;

                var gatedExercises = hard ? MinExerciseCount : MinExerciseCount + 1;
                return BuildVolume(
                    exerciseCount: gatedExercises,
                    setCount: MinSetCount,
                    tier: VolumeTier.Reduced,
                    isReducedForRecovery: true,
                    reason: LocalizationService.Instance["AdaptiveVolume_ReducedForRecovery"]);
            }

            // ── 2. Trygg sone: baseline, evt. moderat løft ved sterk konsistens ────────
            // Et "trygt" volumløft krever at BÅDE recovery, helse OG komfort er over den
            // trygge grensen. Konsistensen alene løfter aldri uten denne tryggheten.
            var safeToProgress =
                recoveryScore  >= SafeRecoveryHealthThreshold &&
                healthScore    >= SafeRecoveryHealthThreshold &&
                comfortScore   >= SafeRecoveryHealthThreshold;

            int exerciseCount = BaselineExerciseCount;
            int setCount = MinSetCount + 1; // moderat baseline = 2 sett
            VolumeTier tier = VolumeTier.Baseline;
            string reason = LocalizationService.Instance["AdaptiveVolume_Baseline"];

            if (safeToProgress && consistencyScore >= StrongConsistencyThreshold)
            {
                // Moderat løft — skalert med hvor sterk konsistensen er over grensen.
                // +1 øvelse ved akkurat grensen, +2 nær toppen. Sett løftes til maks.
                var headroom = (consistencyScore - StrongConsistencyThreshold)
                               / (100.0 - StrongConsistencyThreshold); // 0..1
                var bump = 1 + (int)Math.Floor(headroom * 2.0);          // 1..3, clamp under
                exerciseCount = BaselineExerciseCount + Math.Min(2, bump);
                setCount = MaxSetCount;
                tier = VolumeTier.Increased;
                reason = LocalizationService.Instance["AdaptiveVolume_IncreasedConsistency"];
            }

            return BuildVolume(exerciseCount, setCount, tier, isReducedForRecovery: false, reason);
        }

        private static VolumeRecommendation BuildVolume(
            int exerciseCount, int setCount, VolumeTier tier, bool isReducedForRecovery, string reason)
            => new VolumeRecommendation
            {
                ExerciseCount = Math.Clamp(exerciseCount, MinExerciseCount, MaxExerciseCount),
                SetCount = Math.Clamp(setCount, MinSetCount, MaxSetCount),
                Tier = tier,
                IsReducedForRecovery = isReducedForRecovery,
                Reason = reason
            };

        private static double ClampScore(double score)
            => double.IsNaN(score) ? 0.0 : Math.Clamp(score, 0.0, 100.0);

        private DifficultyLevel GetNextLevel(DifficultyLevel current)
        {
            return current switch
            {
                DifficultyLevel.Nybegynner => DifficultyLevel.Middels,
                DifficultyLevel.Middels => DifficultyLevel.Avansert,
                _ => DifficultyLevel.Avansert
            };
        }
        
        private DifficultyLevel GetPreviousLevel(DifficultyLevel current)
        {
            return current switch
            {
                DifficultyLevel.Avansert => DifficultyLevel.Middels,
                DifficultyLevel.Middels => DifficultyLevel.Nybegynner,
                _ => DifficultyLevel.Nybegynner
            };
        }
        
        private double CalculateConsistency(List<SessionPerformance> sessions)
        {
            if (sessions.Count < 2) return 100;
            double avg = sessions.Average(s => s.OverallScore);
            double sumSquares = sessions.Sum(s => Math.Pow(s.OverallScore - avg, 2));
            double stdDev = Math.Sqrt(sumSquares / sessions.Count);
            return Math.Max(0, 100 - stdDev);
        }
    }
    
    public class SessionPerformance
    {
        public DateTime Date { get; set; }
        public double OverallScore { get; set; }
        public double PitchAccuracy { get; set; }
        public double VariationScore { get; set; }
        public double IntonationScore { get; set; }
    }
    
    public class DifficultyRecommendation
    {
        public DifficultyLevel RecommendedLevel { get; set; }
        public string Reason { get; set; } = "";
        public double Confidence { get; set; }
        public double AverageScore { get; set; }
        public double Consistency { get; set; }
        public bool ShouldCelebrate { get; set; }
        public bool ShouldEncourage { get; set; }
    }
    
    public class ExerciseRecommendation
    {
        public string Category { get; set; } = "";
        public string? TargetPitch { get; set; }
        public string? TargetVariation { get; set; }
        public string? TargetIntonation { get; set; }
        public string Reason { get; set; } = "";
    }

    /// <summary>
    /// Hvor et volum-forslag ligger i forhold til baseline. Brukt for visning/farge.
    /// </summary>
    public enum VolumeTier
    {
        /// <summary>Strammet inn pga. recovery-/helse-behov (Health &gt; Progression).</summary>
        Reduced = 0,

        /// <summary>Standard / moderat utgangspunkt.</summary>
        Baseline = 1,

        /// <summary>Moderat løftet — trygt OG sterk konsistens.</summary>
        Increased = 2
    }

    /// <summary>
    /// Et rent, forklarbart volum-forslag for en økt: antall øvelser, antall sett, hvilken
    /// tier det ligger i, om det er strammet inn av recovery/helse, og en lokalisert
    /// begrunnelse. Produsert av <see cref="AdaptiveDifficultyService.RecommendVolume"/>.
    /// </summary>
    public sealed class VolumeRecommendation
    {
        /// <summary>Foreslått antall øvelser, klampet til [Min,Max]ExerciseCount.</summary>
        public int ExerciseCount { get; init; }

        /// <summary>Foreslått antall sett, klampet til [Min,Max]SetCount.</summary>
        public int SetCount { get; init; }

        /// <summary>Hvor forslaget ligger relativt til baseline.</summary>
        public VolumeTier Tier { get; init; }

        /// <summary>
        /// Sann når forslaget er strammet inn pga. lav recovery/helse (Health-laget).
        /// Når sann er volumet alltid på/nær minimum — uavhengig av konsistens.
        /// </summary>
        public bool IsReducedForRecovery { get; init; }

        /// <summary>Lokalisert, klinisk-trygg begrunnelse (ingen skam/press, ingen rå Hz).</summary>
        public string Reason { get; init; } = "";
    }
}
