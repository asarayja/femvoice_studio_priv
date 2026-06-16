using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Subsystems.Analysis;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Resonans-kategorier basert på F1/F2 ratio.
    /// </summary>
    public enum AudioResonanceCategory
    {
        ForwardResonant,
        NeutralResonant,
        BackResonant
    }

    /// <summary>
    /// Resonans-scoringsservice som evaluerer klangkvalitet basert på formantanalyse.
    /// Vurderer fremre vs bakre resonans og genererer normalisert resonans-score (0-100).
    /// </summary>
    /// <remarks>
    /// Algoritme-referanser:
    /// - Stevens (2000): Acoustic Phonetics
    /// - Fant (1975): Speech Sounds and Features
    /// 
    /// Feminine formantområder:
    /// - F1: 280-500 Hz (lavere = mer fremre)
    /// - F2: 1800-2600 Hz (høyere = mer fremre)
    /// - F1/F2 ratio: < 0.2 = fremre resonans (feminint)
    /// </remarks>
    public class ResonansScoringService
    {
        #region Configuration

        // Stil-bevisste resonansmål. Default = Feminine ⇒ NØYAKTIG de historiske
        // konstantene (F1=330, F2=2200, F1-bånd 280-450, F2-bånd 1800-2600,
        // centroid=2500). Ingen atferdsendring for det vanlige tilfellet. SetVoiceStyle
        // senker F2/centroid-optima for mørkere stiler, slik at en mørkere klang
        // scorer høyere — feedback peker mot brukerens faktiske mål.
        private VoiceStyleGoal _voiceStyle = VoiceStyleGoal.Feminine;
        private ResonanceStyleTarget _target = ResonanceStyleTarget.ForScoring(VoiceStyleGoal.Feminine);

        private double TargetF1Min => _target.F1Min;
        private double TargetF1Max => _target.F1Max;
        private double TargetF1Optimal => _target.F1Optimal;

        private double TargetF2Min => _target.F2Min;
        private double TargetF2Max => _target.F2Max;
        private double TargetF2Optimal => _target.F2Optimal;

        private const double ForwardRatioThreshold = 0.20;
        private const double NeutralRatioThreshold = 0.35;

        private double TargetSpectralCentroid => _target.CentroidOptimal;

        private const double F1Weight = 0.30;
        private const double F2Weight = 0.35;
        private const double RatioWeight = 0.20;
        private const double CentroidWeight = 0.15;

        #endregion

        #region Private Fields

        private readonly int _smoothingWindowSize;
        private readonly Queue<ResonansSnapshot> _recentSnapshots;
        private readonly bool _enableSmoothing;
        private readonly double _smoothingFactor;

        #endregion

        #region Public Properties

        public AudioResonanceCategory CurrentCategory { get; private set; }
        public double CurrentScore { get; private set; }
        public double SessionAverageScore { get; private set; }
        public double CurrentRatio { get; private set; }
        public double CurrentSpectralCentroid { get; private set; }
        public double SessionMinScore { get; private set; }
        public double SessionMaxScore { get; private set; }

        /// <summary>
        /// Aktivt stilmål for scoringen. Default = <see cref="VoiceStyleGoal.Feminine"/>
        /// (historisk lys/fremre feminin klang). Sett via <see cref="SetVoiceStyle"/>.
        /// </summary>
        public VoiceStyleGoal VoiceStyle => _voiceStyle;

        #endregion

        #region Constructor

        public ResonansScoringService(
            int smoothingWindowSize = 30,
            bool enableSmoothing = true,
            double smoothingFactor = 0.7)
        {
            _smoothingWindowSize = smoothingWindowSize;
            _enableSmoothing = enableSmoothing;
            _smoothingFactor = smoothingFactor;
            
            _recentSnapshots = new Queue<ResonansSnapshot>();
            
            Reset();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Beregner resonans-score fra formantdata.
        /// </summary>
        public ResonansScore EvaluateResonance(FormantAnalysisResult formantResult)
        {
            var score = new ResonansScore
            {
                Timestamp = formantResult.Timestamp,
                F1 = formantResult.SmoothedF1,
                F2 = formantResult.SmoothedF2,
                F3 = formantResult.SmoothedF3,
                Rms = formantResult.FrameRms,
                Confidence = formantResult.Confidence
            };

            if (!formantResult.IsValid || formantResult.Confidence < 0.3)
            {
                score.IsValid = false;
                score.TotalScore = 0;
                return score;
            }

            if (score.F2 > 0)
            {
                score.F1F2Ratio = score.F1 / score.F2;
                CurrentRatio = score.F1F2Ratio;
            }

            score.SpectralCentroid = CalculateSpectralCentroid(formantResult);
            CurrentSpectralCentroid = score.SpectralCentroid;

            score.Category = DetermineCategory(score.F1F2Ratio);
            CurrentCategory = score.Category;

            score.F1Score = CalculateF1Score(score.F1);
            score.F2Score = CalculateF2Score(score.F2);
            score.RatioScore = CalculateRatioScore(score.F1F2Ratio);
            score.CentroidScore = CalculateCentroidScore(score.SpectralCentroid);

            score.TotalScore = 
                (score.F1Score * F1Weight) +
                (score.F2Score * F2Weight) +
                (score.RatioScore * RatioWeight) +
                (score.CentroidScore * CentroidWeight);

            if (_enableSmoothing)
            {
                score.TotalScore = ApplySmoothing(score.TotalScore);
            }

            CurrentScore = score.TotalScore;
            UpdateSessionStats(score.TotalScore);

            return score;
        }

        /// <summary>
        /// Beregner økt-basert resonans-score.
        /// </summary>
        public ResonansSessionResult CalculateSessionResult(IEnumerable<ResonansScore> frameScores)
        {
            var validScores = frameScores.Where(s => s.IsValid).ToList();
            
            var result = new ResonansSessionResult();
            
            if (validScores.Count == 0)
            {
                result.IsValid = false;
                return result;
            }

            result.IsValid = true;
            
            result.AverageF1 = validScores.Average(s => s.F1);
            result.AverageF2 = validScores.Average(s => s.F2);
            result.AverageF3 = validScores.Average(s => s.F3);
            result.AverageScore = validScores.Average(s => s.TotalScore);
            result.AverageRatio = validScores.Average(s => s.F1F2Ratio);
            result.AverageSpectralCentroid = validScores.Average(s => s.SpectralCentroid);
            
            result.MinScore = validScores.Min(s => s.TotalScore);
            result.MaxScore = validScores.Max(s => s.TotalScore);
            result.MinF1 = validScores.Min(s => s.F1);
            result.MaxF1 = validScores.Max(s => s.F1);
            result.MinF2 = validScores.Min(s => s.F2);
            result.MaxF2 = validScores.Max(s => s.F2);
            
            result.Category = DetermineCategory(result.AverageRatio);
            
            result.Confidence = (double)validScores.Count / 
                (validScores.Count + validScores.Count(s => !s.IsValid));
            
            if (validScores.Count >= 10)
            {
                var firstHalf = validScores.Take(validScores.Count / 2).Average(s => s.TotalScore);
                var secondHalf = validScores.Skip(validScores.Count / 2).Average(s => s.TotalScore);
                result.Trend = secondHalf - firstHalf;
            }

            return result;
        }

        /// <summary>
        /// Genererer norsk tilbakemelding basert på resonans-score.
        /// </summary>
        public ResonanceFeedback GetFeedback(ResonansScore score)
        {
            var feedback = new ResonanceFeedback();

            if (!score.IsValid || score.TotalScore < 10)
            {
                feedback.Message = "Ikke nok data til resonansanalyse ennå.";
                feedback.Hint = "Snakk tydelig og hold stemmen jevn.";
                feedback.Improvement = 0;
                return feedback;
            }

            switch (score.Category)
            {
                case AudioResonanceCategory.ForwardResonant:
                    feedback.Message = $"Bra! Resonansen din ({score.F1:F0}/{score.F2:F0} Hz) er i det feminine området.";
                    feedback.Hint = "Fortsatt å bruke denne fremre klangen. Konsentrer deg om å holde F2 høy.";
                    feedback.Improvement = 0;
                    break;

                case AudioResonanceCategory.NeutralResonant:
                    if (score.TotalScore >= 60)
                    {
                        feedback.Message = "God resonans! Du er på vei mot et mer feminint klangområde.";
                        feedback.Hint = "Prøv å flytte lyden litt mer frem i munnen. Tenk på å 'smile' når du snakker.";
                        feedback.Improvement = 20;
                    }
                    else
                    {
                        feedback.Message = "Resonansen din er nøytral. Det er rom for forbedring.";
                        feedback.Hint = "Fokuser på å heve F2 ved å plassere lyden mer fremre i munnen.";
                        feedback.Improvement = 30;
                    }
                    break;

                case AudioResonanceCategory.BackResonant:
                    if (score.TotalScore >= 40)
                    {
                        feedback.Message = "Resonansen er litt bakre. Du kan oppnå en mer feminin klang.";
                        feedback.Hint = "Prøv å forestille deg at lyden kommer fra nesen eller baksiden av munnen. Flytt den fremover!";
                        feedback.Improvement = 40;
                    }
                    else
                    {
                        feedback.Message = "Resonansen er tydelig bakre. Dette gir en mørkere klang.";
                        feedback.Hint = "Viktig: Fokuser på å heve F2 (fremre resonans) ved å endre tunge- og leppeposisjon. Tenk på å 'henge' lyden i nesetippen.";
                        feedback.Improvement = 50;
                    }
                    break;
            }

            if (score.F1 < TargetF1Min - 50)
            {
                feedback.Hint += " Unngå å senke F1 for mye - det kan høres ut som 'knirking'.";
            }
            else if (score.F1 > TargetF1Max + 50)
            {
                feedback.Hint += " Heve F1 litt kan hjelpe med å få en lysere klang.";
            }

            return feedback;
        }

        /// <summary>
        /// Bytter stilmål-settet scoringen sikter mot. Mørkere stiler (DarkFeminine,
        /// Androgynous) senker F2/centroid-optima og F2-båndet slik at en mørkere klang
        /// scorer høyere — feedback peker mot brukerens faktiske mål, ikke en universell
        /// feminin klang. Feminine/Situational/Custom beholder de historiske målene.
        /// Påvirker kun fremtidige <see cref="EvaluateResonance"/>-kall; nullstiller ikke
        /// glatting/øktstatistikk.
        /// </summary>
        public void SetVoiceStyle(VoiceStyleGoal style)
        {
            _voiceStyle = style;
            _target = ResonanceStyleTarget.ForScoring(style);
        }

        /// <summary>
        /// Nullstiller service for ny økt.
        /// </summary>
        public void Reset()
        {
            _recentSnapshots.Clear();
            CurrentCategory = AudioResonanceCategory.NeutralResonant;
            CurrentScore = 0;
            SessionAverageScore = 0;
            SessionMinScore = 100;
            SessionMaxScore = 0;
            CurrentRatio = 0;
            CurrentSpectralCentroid = 0;
        }

        #endregion

        #region Private Methods

        private AudioResonanceCategory DetermineCategory(double ratio)
        {
            if (ratio <= 0 || ratio < ForwardRatioThreshold)
                return AudioResonanceCategory.ForwardResonant;
            else if (ratio < NeutralRatioThreshold)
                return AudioResonanceCategory.NeutralResonant;
            else
                return AudioResonanceCategory.BackResonant;
        }

        private double CalculateF1Score(double f1)
        {
            if (f1 <= 0) return 0;
            
            double optimalDist = Math.Abs(f1 - TargetF1Optimal);
            
            if (f1 >= TargetF1Min && f1 <= TargetF1Max)
                return Math.Max(0, 100 - optimalDist * 0.5);
            else
                return Math.Max(0, 60 - optimalDist * 0.3);
        }

        private double CalculateF2Score(double f2)
        {
            if (f2 <= 0) return 0;
            
            double optimalDist = Math.Abs(f2 - TargetF2Optimal);
            
            if (f2 >= TargetF2Min && f2 <= TargetF2Max)
                return Math.Max(0, 100 - optimalDist * 0.08);
            else
                return Math.Max(0, 50 - optimalDist * 0.05);
        }

        private double CalculateRatioScore(double ratio)
        {
            if (ratio <= 0) return 0;
            
            if (ratio <= ForwardRatioThreshold)
                return 100;
            else if (ratio <= NeutralRatioThreshold)
                return Math.Max(0, 100 - (ratio - ForwardRatioThreshold) * 800);
            else
                return Math.Max(0, 60 - (ratio - NeutralRatioThreshold) * 200);
        }

        private double CalculateCentroidScore(double centroid)
        {
            if (centroid <= 0) return 0;
            
            double optimalDist = Math.Abs(centroid - TargetSpectralCentroid);
            return Math.Max(0, 100 - optimalDist * 0.04);
        }

        private double CalculateSpectralCentroid(FormantAnalysisResult result)
        {
            if (result.F3 > 0)
                return (result.F1 * 0.2 + result.F2 * 0.4 + result.F3 * 0.4);
            else if (result.F2 > 0)
                return (result.F1 * 0.3 + result.F2 * 0.7);
            else if (result.F1 > 0)
                return result.F1;
            
            return 0;
        }

        private double ApplySmoothing(double newScore)
        {
            _recentSnapshots.Enqueue(new ResonansSnapshot { Score = newScore, Timestamp = DateTime.Now });
            
            if (_recentSnapshots.Count > _smoothingWindowSize)
                _recentSnapshots.Dequeue();
            
            double smoothed = newScore;
            foreach (var snapshot in _recentSnapshots)
            {
                smoothed = smoothed * _smoothingFactor + snapshot.Score * (1 - _smoothingFactor);
            }
            
            return smoothed;
        }

        private void UpdateSessionStats(double score)
        {
            if (_recentSnapshots.Count > 0)
            {
                SessionAverageScore = _recentSnapshots.Average(s => s.Score);
                SessionMinScore = Math.Min(SessionMinScore, score);
                SessionMaxScore = Math.Max(SessionMaxScore, score);
            }
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Enkelt resonans-scoringsresultat per frame.
    /// </summary>
    public class ResonansScore
    {
        public DateTime Timestamp { get; set; }
        public double F1 { get; set; }
        public double F2 { get; set; }
        public double F3 { get; set; }
        public double F1F2Ratio { get; set; }
        public double SpectralCentroid { get; set; }
        public double Rms { get; set; }
        public double Confidence { get; set; }
        public AudioResonanceCategory Category { get; set; }
        public double F1Score { get; set; }
        public double F2Score { get; set; }
        public double RatioScore { get; set; }
        public double CentroidScore { get; set; }
        public double TotalScore { get; set; }
        public bool IsValid { get; set; } = true;
    }

    /// <summary>
    /// Aggregert resonans-resultat for hel økt.
    /// </summary>
    public class ResonansSessionResult
    {
        public bool IsValid { get; set; }
        public double AverageF1 { get; set; }
        public double AverageF2 { get; set; }
        public double AverageF3 { get; set; }
        public double AverageScore { get; set; }
        public double AverageRatio { get; set; }
        public double AverageSpectralCentroid { get; set; }
        public double MinScore { get; set; }
        public double MaxScore { get; set; }
        public double MinF1 { get; set; }
        public double MaxF1 { get; set; }
        public double MinF2 { get; set; }
        public double MaxF2 { get; set; }
        public AudioResonanceCategory Category { get; set; }
        public double Confidence { get; set; }
        public double Trend { get; set; }
    }

    /// <summary>
    /// Norsk tilbakemelding om resonans.
    /// </summary>
    public class ResonanceFeedback
    {
        public string Message { get; set; } = "";
        public string Hint { get; set; } = "";
        public double Improvement { get; set; }
        public bool IsPositive { get; set; }
    }

    internal class ResonansSnapshot
    {
        public double Score { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion
}
