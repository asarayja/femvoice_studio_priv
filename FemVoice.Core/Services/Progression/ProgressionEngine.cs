using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services.Progression
{
    /// <summary>
    /// ProgressionEngine: Main progression decision engine.
    /// Determines progression mode based on session results and clinical gates.
    /// Integrates with ComplexityEngine for speech complexity progression.
    /// </summary>
    public class ProgressionEngine
    {
        private readonly DatabaseService _db;
        private readonly FemVoiceScore _score;
        private readonly ComplexityEngine _complexityEngine;
        private ProgressionConfig _cfg;
        private UserProgressionProfile? _profile;
        private List<FemVoiceScoreResult> _scores = new();
        private int _sessionsSinceStrain;
        private DateTime? _lastStrain;
        
        /// <summary>
        /// Gets the ComplexityEngine instance for speech complexity management.
        /// </summary>
        public ComplexityEngine ComplexityEngine => _complexityEngine;
        
        public ProgressionEngine(DatabaseService db, FemVoiceScore score, ProgressionConfig? cfg = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _score = score ?? throw new ArgumentNullException(nameof(score));
            _cfg = cfg ?? ProgressionConfig.CreateDefault();
            _profile = UserProgressionProfile.CreateDefault();
            _complexityEngine = new ComplexityEngine(db);
        }
        
        /// <summary>
        /// Evaluates a training session and returns progression decision with complexity info.
        /// </summary>
        public ProgressionDecisionResult EvaluateSession(TrainingSession s, SubjectiveReport? r = null)
        {
            var si = new FemVoiceScoreInput
            {
                AveragePitch = s.AveragePitch,
                MinPitch = s.MinPitch,
                MaxPitch = s.MaxPitch,
                PitchVariation = s.PitchVariation,
                AverageF1 = s.AverageF1,
                AverageF2 = s.AverageF2,
                AverageF3 = s.AverageF3,
                SpectralCentroid = s.SpectralCentroid,
                IntonationRange = s.IntonationScore,
                IntonationRiseScore = s.IntonationScore,
                StrainLevel = s.StrainLevel,
                DifficultyLevel = s.DifficultyLevel,
                ResonanceScore = s.ResonanceScore,
                VoiceHealthScore = s.VoiceHealthScore,
                ConsecutiveStableSessions = _profile?.ConsecutiveStableSessions ?? 0
            };
            
            var result = _score.Calculate(si);
            _scores.Add(result);
            if (_scores.Count > 30)
                _scores.RemoveRange(0, 30);
            
            if (!string.IsNullOrEmpty(result.WarningFlags) && 
                (result.WarningFlags.Contains("STRAIN") || result.WarningFlags.Contains("CRITICAL")))
            {
                _sessionsSinceStrain = 0;
                _lastStrain = DateTime.Now;
            }
            else
            {
                _sessionsSinceStrain++;
            }
            
            var p = _profile ?? UserProgressionProfile.CreateDefault();
            
            // Check subjective health report
            if (r != null && r.IndicatesHealthConcern)
            {
                var complexityEval = _complexityEngine.EvaluateCurrentLevel(1);
                return new ProgressionDecisionResult
                {
                    Mode = ProgressionMode.HealthRecovery,
                    Reasoning = "Helsefokus på grunn av subjektiv rapportering.",
                    NextTargets = TargetAdjustment.None,
                    AllowProgression = false,
                    DecidedAt = DateTime.Now,
                    RecommendedComplexityLevel = complexityEval.CurrentLevel,
                    ComplexityCanAdvance = false,
                    ComplexityReason = "Helsefokus blokkerer progresjon.",
                    ComplexityEvaluation = complexityEval
                };
            }
            
            var gates = CheckGates(result);
            
            if (!gates.AllGatesOpen)
            {
                var complexityEval = _complexityEngine.EvaluateCurrentLevel(1);
                var mode = !gates.IsResonanceGateOpen 
                    ? ProgressionMode.ResonanceRefinement 
                    : (!gates.IsStabilityGateOpen 
                        ? ProgressionMode.Maintenance 
                        : ProgressionMode.HealthRecovery);
                
                return new ProgressionDecisionResult
                {
                    Mode = mode,
                    Reasoning = "Gater ikke åpne for progresjon.",
                    NextTargets = TargetAdjustment.None,
                    AllowProgression = false,
                    DecidedAt = DateTime.Now,
                    RecommendedComplexityLevel = complexityEval.CurrentLevel,
                    ComplexityCanAdvance = _complexityEngine.CanAdvanceToNextLevel(complexityEval),
                    ComplexityReason = GetComplexityBlockReason(complexityEval),
                    ComplexityEvaluation = complexityEval
                };
            }
            
            var mode2 = SelectMode(p, result, gates);
            var targets = CalcTargets(p, mode2, result);
            
            // Get complexity evaluation
            var complexityEvaluation = _complexityEngine.EvaluateCurrentLevel(1);
            var canAdvanceComplexity = _complexityEngine.CanAdvanceToNextLevel(complexityEvaluation);
            
            return new ProgressionDecisionResult
            {
                Mode = mode2,
                Reasoning = "Progresjon godkjent.",
                NextTargets = targets,
                AllowProgression = true,
                DecidedAt = DateTime.Now,
                RecommendedComplexityLevel = complexityEvaluation.CurrentLevel,
                ComplexityCanAdvance = canAdvanceComplexity,
                ComplexityReason = canAdvanceComplexity 
                    ? $"Klar for {ComplexityLevelStep.GetDisplayName(_complexityEngine.DetermineNextLevel(complexityEvaluation.CurrentLevel))}"
                    : GetComplexityBlockReason(complexityEvaluation),
                ComplexityEvaluation = complexityEvaluation
            };
        }
        
        /// <summary>
        /// Evaluates progression with safety checks including complexity.
        /// </summary>
        public ProgressionDecisionResult EvaluateProgressionWithSafety()
        {
            var complexityEval = _complexityEngine.EvaluateCurrentLevel(1);
            
            // Check if we have enough recent scores
            if (_scores.Count < 3)
            {
                return new ProgressionDecisionResult
                {
                    Mode = ProgressionMode.Maintenance,
                    Reasoning = "Ikke nok data for progresjonsvurdering.",
                    AllowProgression = true,
                    DecidedAt = DateTime.Now,
                    RecommendedComplexityLevel = complexityEval.CurrentLevel,
                    ComplexityCanAdvance = false,
                    ComplexityReason = "Venter på mer treningsdata.",
                    ComplexityEvaluation = complexityEval
                };
            }
            
            var recentAvg = _scores.TakeLast(5).Average(s => s.OverallScore);
            var gates = CheckGates(_scores.Last());
            
            if (!gates.AllGatesOpen)
            {
                return new ProgressionDecisionResult
                {
                    Mode = ProgressionMode.Maintenance,
                    Reasoning = "Sikkerhetsgater krever vedlikehold.",
                    AllowProgression = false,
                    DecidedAt = DateTime.Now,
                    RecommendedComplexityLevel = complexityEval.CurrentLevel,
                    ComplexityCanAdvance = _complexityEngine.CanAdvanceToNextLevel(complexityEval),
                    ComplexityReason = GetComplexityBlockReason(complexityEval),
                    ComplexityEvaluation = complexityEval
                };
            }
            
            return new ProgressionDecisionResult
            {
                Mode = ProgressionMode.PitchProgression,
                Reasoning = $"Gjennomsnittlig score: {recentAvg:F0}%",
                AllowProgression = true,
                DecidedAt = DateTime.Now,
                RecommendedComplexityLevel = complexityEval.CurrentLevel,
                ComplexityCanAdvance = _complexityEngine.CanAdvanceToNextLevel(complexityEval),
                ComplexityReason = _complexityEngine.CanAdvanceToNextLevel(complexityEval)
                    ? "Alle kriterier for kompleksitetsprogresjon er oppfylt."
                    : GetComplexityBlockReason(complexityEval),
                ComplexityEvaluation = complexityEval
            };
        }
        
        /// <summary>
        /// Gets the blocking reason for complexity progression.
        /// </summary>
        private string GetComplexityBlockReason(ComplexityEvaluation eval)
        {
            if (eval.BlockingReasons.Count > 0)
                return eval.BlockingReasons[0];
            
            if (!eval.HealthAllowsProgression)
                return "Helseproblemer blokkerer progresjon.";
            
            return "Ikke klar for progresjon enda.";
        }
        
        /// <summary>
        /// Checks all progression gates.
        /// </summary>
        public ProgressionGateStatus CheckGates(FemVoiceScoreResult r)
        {
            var g = new ProgressionGateStatus();
            g.IsResonanceGateOpen = r.ResonanceScore >= _cfg.MinimumResonanceScore;
            g.IsHealthGateOpen = r.VoiceHealthScore >= _cfg.MinimumVoiceHealthScore;
            g.IsStabilityGateOpen = _scores.Where(s => s.VoiceHealthScore >= _cfg.MinimumVoiceHealthScore).Count() >= _cfg.MinimumConsecutiveStableSessions;
            g.IsConsecutiveSessionsGateOpen = !_lastStrain.HasValue || (DateTime.Now - _lastStrain.Value).TotalDays >= 7;
            return g;
        }
        
        private ProgressionMode SelectMode(UserProgressionProfile p, FemVoiceScoreResult r, ProgressionGateStatus g)
        {
            if (r.ResonanceScore < _cfg.MinimumResonanceScore)
                return ProgressionMode.ResonanceRefinement;
            
            if (g.AllGatesOpen)
            {
                return p.CurrentMode switch
                {
                    ProgressionMode.Maintenance => ProgressionMode.PitchProgression,
                    ProgressionMode.ResonanceRefinement => ProgressionMode.PitchProgression,
                    ProgressionMode.PitchProgression => ProgressionMode.ProsodyExpansion,
                    _ => ProgressionMode.Maintenance
                };
            }
            
            if (r.VoiceHealthScore < _cfg.HighFatigueHealthThreshold)
                return ProgressionMode.HealthRecovery;
            
            return ProgressionMode.Maintenance;
        }
        
        private TargetAdjustment CalcTargets(UserProgressionProfile p, ProgressionMode m, FemVoiceScoreResult r)
        {
            return m switch
            {
                ProgressionMode.PitchProgression => TargetAdjustment.CreatePitchIncrease(
                    p.TargetPitchMin + _cfg.PitchIncreasePerCycle, 
                    p.TargetPitchMax + _cfg.PitchIncreasePerCycle),
                ProgressionMode.ResonanceRefinement => TargetAdjustment.CreateResonanceImprovement(
                    r.ResonanceScore + _cfg.ResonanceRefinementBonus),
                _ => TargetAdjustment.None
            };
        }
    }
    
    /// <summary>
    /// Status of progression gates.
    /// </summary>
    public class ProgressionGateStatus
    {
        public bool IsResonanceGateOpen { get; set; }
        public bool IsHealthGateOpen { get; set; }
        public bool IsStabilityGateOpen { get; set; }
        public bool IsConsecutiveSessionsGateOpen { get; set; }
        
        public bool AllGatesOpen => IsResonanceGateOpen && IsHealthGateOpen && 
                                    IsStabilityGateOpen && IsConsecutiveSessionsGateOpen;
    }
}
