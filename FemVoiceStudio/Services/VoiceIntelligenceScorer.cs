using System;
using System.Collections.Generic;
using System.Globalization;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Pure (no DB / no IO) input snapshot for <see cref="VoiceIntelligenceScorer"/>.
    /// Every field is a session-level aggregate a caller has already computed; this
    /// scorer only reasons over the numbers. Robust against missing signals — leave
    /// an optional field at its sentinel and that dimension falls back to neutral.
    /// </summary>
    public readonly record struct VoiceIntelligenceInput
    {
        // ── Resonance ────────────────────────────────────────────────────────────
        /// <summary>Average resonance, normalised 0..1 (×100 ⇒ 0..100).
        /// Negative ⇒ treated as missing (neutral fallback).</summary>
        public double AverageResonance01 { get; init; }

        /// <summary>Optional precomputed resonance score already on a 0..100 scale
        /// (e.g. from ResonansScoringService). When &gt;= 0 it takes precedence over
        /// <see cref="AverageResonance01"/>. Default -1 ⇒ unset.</summary>
        public double ResonanceScore0100 { get; init; }

        // ── Comfort (Health) ─────────────────────────────────────────────────────
        /// <summary>Comfort compliance 0..1 (fraction of time inside the comfort zone).
        /// Negative ⇒ missing.</summary>
        public double ComfortCompliance01 { get; init; }

        /// <summary>Number of comfort-zone breach episodes in the session. Each one
        /// nudges the comfort score down.</summary>
        public int ComfortBreaches { get; init; }

        // ── Consistency ──────────────────────────────────────────────────────────
        /// <summary>Average stability 0..1 (×100 ⇒ 0..100). Negative ⇒ missing.</summary>
        public double AverageStability01 { get; init; }

        // ── Intonation ───────────────────────────────────────────────────────────
        /// <summary>Intonation range in Hz (semitone-equivalent spread). &lt;= 0 ⇒
        /// neutral fallback. Routed through <see cref="FemVoiceScore"/>.</summary>
        public double IntonationRangeHz { get; init; }

        /// <summary>Optional intonation rise score (>30 nudges up). Default 0.</summary>
        public double IntonationRiseScore { get; init; }

        // ── VocalWeight (spectral) ───────────────────────────────────────────────
        /// <summary>Session-average F1 in Hz. &lt;= 0 ⇒ treated as missing on this axis.</summary>
        public double AverageF1Hz { get; init; }

        /// <summary>Session-average spectral centroid in Hz (primary weight signal).
        /// &lt;= 0 ⇒ treated as missing on this axis.</summary>
        public double AverageSpectralCentroidHz { get; init; }

        /// <summary>Session-average HNR in dB. NaN ⇒ missing on this axis.</summary>
        public double AverageHnrDb { get; init; }

        /// <summary>Session-average RMS intensity 0..1. &lt;= 0 ⇒ missing on this axis.</summary>
        public double AverageIntensity { get; init; }

        // ── Recovery (Health) ────────────────────────────────────────────────────
        /// <summary>Recovery input snapshot (already aggregated from analytics history).
        /// When null, recovery falls back to neutral.</summary>
        public RecoveryScoreInput? Recovery { get; init; }

        // ── Pitch ────────────────────────────────────────────────────────────────
        /// <summary>Session-average pitch in Hz. &lt;= 0 ⇒ neutral fallback. Routed
        /// through <see cref="FemVoiceScore"/> (already resonance/zone-gated there).</summary>
        public double AveragePitchHz { get; init; }

        /// <summary>Pitch variation (Hz). Default 0.</summary>
        public double PitchVariation { get; init; }

        /// <summary>
        /// Convenience factory with the right "unset" sentinels for optional fields.
        /// Use this rather than the default struct so optional axes default to missing
        /// rather than to 0 (which would be a real, low value).
        /// </summary>
        public static VoiceIntelligenceInput Empty() => new VoiceIntelligenceInput
        {
            AverageResonance01 = -1,
            ResonanceScore0100 = -1,
            ComfortCompliance01 = -1,
            AverageStability01 = -1,
            IntonationRangeHz = -1,
            AverageF1Hz = -1,
            AverageSpectralCentroidHz = -1,
            AverageHnrDb = double.NaN,
            AverageIntensity = -1,
            AveragePitchHz = -1,
        };
    }

    /// <summary>
    /// Combines the seven voice dimensions into explainable, traceable 0–100 scores
    /// plus one hierarchy-weighted <see cref="VoiceIntelligenceScores.CompositeVoiceScore"/>.
    /// Pure: no DB, no IO. Reuses existing engines rather than re-deriving signal —
    /// <see cref="FemVoiceScore"/> for Intonation &amp; Pitch, <see cref="VocalWeightAnalyzer"/>
    /// for VocalWeight, <see cref="RecoveryScorer"/> for Recovery.
    ///
    /// ── COMPOSITE WEIGHTS (documented; sum = 1.0) ────────────────────────────────
    ///   Resonance    0.22  — highest single TRAINING weight (rule b).
    ///   Comfort      0.18  ┐ Health pair.
    ///   Recovery     0.15  ┘ Comfort + Recovery = 0.33 &gt; Resonance 0.22 (rule c).
    ///   Consistency  0.15
    ///   Intonation   0.12
    ///   VocalWeight  0.10
    ///   Pitch        0.08  — LOWEST weight; never dominant (rule a).
    /// All seven represented (rule d). The composite is a MEASUREMENT, never a gate.
    /// </summary>
    public sealed class VoiceIntelligenceScorer
    {
        // ── Composite weights (sum must equal 1.0 — covered by a test) ────────────
        public const double ResonanceWeight = 0.22;
        public const double ComfortWeight = 0.18;
        public const double ConsistencyWeight = 0.15;
        public const double RecoveryWeight = 0.15;
        public const double IntonationWeight = 0.12;
        public const double VocalWeightWeight = 0.10;
        public const double PitchWeight = 0.08;

        private const double NeutralScore = 50.0;
        private const double ComfortBreachPenalty = 6.0; // per breach, capped below
        private const double ComfortBreachPenaltyCap = 40.0;

        private readonly VocalWeightAnalyzer _vocalWeight;
        private readonly RecoveryScorer _recovery;
        private readonly FemVoiceScore _femVoiceScore;

        public VoiceIntelligenceScorer()
            : this(new VocalWeightAnalyzer(), new RecoveryScorer(), new FemVoiceScore())
        {
        }

        /// <summary>DI-friendly ctor (real classes, no mocks — matches house style).</summary>
        public VoiceIntelligenceScorer(
            VocalWeightAnalyzer vocalWeight,
            RecoveryScorer recovery,
            FemVoiceScore femVoiceScore)
        {
            _vocalWeight = vocalWeight ?? throw new ArgumentNullException(nameof(vocalWeight));
            _recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
            _femVoiceScore = femVoiceScore ?? throw new ArgumentNullException(nameof(femVoiceScore));
        }

        /// <summary>
        /// Computes all seven dimension scores plus the hierarchy-weighted composite.
        /// Total/robust: any missing signal yields a neutral dimension with an
        /// explanation rather than throwing.
        /// </summary>
        public VoiceIntelligenceScores Compute(VoiceIntelligenceInput input)
        {
            var resonance = ScoreResonance(input);
            var comfort = ScoreComfort(input);
            var consistency = ScoreConsistency(input);
            var intonation = ScoreIntonation(input);
            var vocalWeight = ScoreVocalWeight(input);
            var recovery = ScoreRecovery(input);
            var pitch = ScorePitch(input);

            double composite =
                resonance.Score * ResonanceWeight +
                comfort.Score * ComfortWeight +
                consistency.Score * ConsistencyWeight +
                recovery.Score * RecoveryWeight +
                intonation.Score * IntonationWeight +
                vocalWeight.Score * VocalWeightWeight +
                pitch.Score * PitchWeight;

            composite = Math.Clamp(composite, 0.0, 100.0);

            return new VoiceIntelligenceScores
            {
                Resonance = resonance,
                Comfort = comfort,
                Consistency = consistency,
                Intonation = intonation,
                VocalWeight = vocalWeight,
                Recovery = recovery,
                Pitch = pitch,
                CompositeVoiceScore = composite,
                ComputedAt = DateTime.Now,
                RawInputs = BuildRawInputs(input),
            };
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Dimension scorers
        // ──────────────────────────────────────────────────────────────────────────

        private static DimensionScore ScoreResonance(VoiceIntelligenceInput input)
        {
            // Prefer an explicit 0..100 resonance score when supplied.
            if (input.ResonanceScore0100 >= 0 && IsUsable(input.ResonanceScore0100))
            {
                double s = Math.Clamp(input.ResonanceScore0100, 0, 100);
                return new DimensionScore(s, string.Create(CultureInfo.InvariantCulture,
                    $"Resonance {s:0}/100 from a precomputed resonance score."));
            }

            if (input.AverageResonance01 < 0 || !IsUsable(input.AverageResonance01))
            {
                return DimensionScore.Neutral(
                    "Resonance: no resonance signal this session — neutral 50 assigned.");
            }

            double score = Math.Clamp(input.AverageResonance01, 0, 1) * 100.0;
            return new DimensionScore(score, string.Create(CultureInfo.InvariantCulture,
                $"Resonance {score:0}/100 from average resonance {input.AverageResonance01:0.00} (×100)."));
        }

        private static DimensionScore ScoreComfort(VoiceIntelligenceInput input)
        {
            if (input.ComfortCompliance01 < 0 || !IsUsable(input.ComfortCompliance01))
            {
                return DimensionScore.Neutral(
                    "Comfort: no comfort-compliance signal — neutral 50 assigned.");
            }

            double baseScore = Math.Clamp(input.ComfortCompliance01, 0, 1) * 100.0;
            int breaches = Math.Max(0, input.ComfortBreaches);
            double penalty = Math.Min(breaches * ComfortBreachPenalty, ComfortBreachPenaltyCap);
            double score = Math.Clamp(baseScore - penalty, 0, 100);

            string note = breaches == 0
                ? "no comfort-zone breaches"
                : string.Create(CultureInfo.InvariantCulture,
                    $"{breaches} comfort breach{(breaches == 1 ? "" : "es")} (−{penalty:0})");
            return new DimensionScore(score, string.Create(CultureInfo.InvariantCulture,
                $"Comfort {score:0}/100 from compliance {input.ComfortCompliance01:0.00} (×100), {note}."));
        }

        private static DimensionScore ScoreConsistency(VoiceIntelligenceInput input)
        {
            if (input.AverageStability01 < 0 || !IsUsable(input.AverageStability01))
            {
                return DimensionScore.Neutral(
                    "Consistency: no stability-steadiness signal — neutral 50 assigned.");
            }

            // AverageStability01 here is the in-session stability STEADINESS (reproducibility:
            // 1 − stdDev/scale), NOT the raw voicing-clarity mean — so this dimension reflects
            // how steady the voice was, not how voiced it was (CONS-1/2).
            double score = Math.Clamp(input.AverageStability01, 0, 1) * 100.0;
            return new DimensionScore(score, string.Create(CultureInfo.InvariantCulture,
                $"Consistency {score:0}/100 from in-session stability steadiness {input.AverageStability01:0.00} (×100; reproducibility, not voicing level)."));
        }

        private DimensionScore ScoreIntonation(VoiceIntelligenceInput input)
        {
            if (input.IntonationRangeHz <= 0 || !IsUsable(input.IntonationRangeHz))
            {
                return DimensionScore.Neutral(
                    "Intonation: no usable intonation range — neutral 50 assigned.");
            }

            // Reuse FemVoiceScore's intonation logic via the public Calculate() result
            // (CalculateIntonationScore is private). Build a minimal, intonation-only
            // input at standard difficulty so no tolerance bonus is applied.
            var fvs = _femVoiceScore.Calculate(new FemVoiceScoreInput
            {
                IntonationRange = input.IntonationRangeHz,
                IntonationRiseScore = input.IntonationRiseScore,
                DifficultyLevel = Models.DifficultyLevel.Middels,
            });
            double score = Math.Clamp(fvs.IntonationScore, 0, 100);
            return new DimensionScore(score, string.Create(CultureInfo.InvariantCulture,
                $"Intonation {score:0}/100 from intonation range {input.IntonationRangeHz:0} Hz (via FemVoiceScore)."));
        }

        private DimensionScore ScoreVocalWeight(VoiceIntelligenceInput input)
        {
            bool centroidUsable = input.AverageSpectralCentroidHz > 0 && IsUsable(input.AverageSpectralCentroidHz);
            bool f1Usable = input.AverageF1Hz > 0 && IsUsable(input.AverageF1Hz);

            if (!centroidUsable && !f1Usable)
            {
                return DimensionScore.Neutral(
                    "VocalWeight: no measurable spectral centroid or F1 — neutral 50 assigned.");
            }

            // Pass missing axes as the analyzer's own "missing" sentinels.
            double f1 = f1Usable ? input.AverageF1Hz : double.NaN;
            double centroid = centroidUsable ? input.AverageSpectralCentroidHz : double.NaN;
            double hnr = IsUsable(input.AverageHnrDb) ? input.AverageHnrDb : double.NaN;
            double intensity = (input.AverageIntensity > 0 && IsUsable(input.AverageIntensity))
                ? input.AverageIntensity : double.NaN;

            var r = _vocalWeight.Score(f1, centroid, hnr, intensity);
            return new DimensionScore(r.Score, r.Explanation);
        }

        private DimensionScore ScoreRecovery(VoiceIntelligenceInput input)
        {
            if (input.Recovery is null)
            {
                return DimensionScore.Neutral(
                    "Recovery: no recovery history supplied — neutral 50 assigned.");
            }

            var r = _recovery.Score(input.Recovery.Value);
            return new DimensionScore(r.Score, r.Explanation);
        }

        private DimensionScore ScorePitch(VoiceIntelligenceInput input)
        {
            if (input.AveragePitchHz <= 0 || !IsUsable(input.AveragePitchHz))
            {
                return DimensionScore.Neutral(
                    "Pitch: no usable pitch signal — neutral 50 assigned.");
            }

            // Reuse FemVoiceScore's pitch logic (resonance/zone-gated) via the public
            // Calculate() result; CalculatePitchScore is private. Feed the resonance
            // score we derived so the gate behaves as in production.
            double resonanceForGate = 0;
            if (input.ResonanceScore0100 >= 0 && IsUsable(input.ResonanceScore0100))
                resonanceForGate = Math.Clamp(input.ResonanceScore0100, 0, 100);
            else if (input.AverageResonance01 >= 0 && IsUsable(input.AverageResonance01))
                resonanceForGate = Math.Clamp(input.AverageResonance01, 0, 1) * 100.0;

            var fvs = _femVoiceScore.Calculate(new FemVoiceScoreInput
            {
                AveragePitch = input.AveragePitchHz,
                PitchVariation = Math.Max(0, input.PitchVariation),
                ResonanceScore = resonanceForGate,
                DifficultyLevel = Models.DifficultyLevel.Middels,
            });
            double score = Math.Clamp(fvs.PitchScore, 0, 100);
            return new DimensionScore(score, string.Create(CultureInfo.InvariantCulture,
                $"Pitch {score:0}/100 from average pitch {input.AveragePitchHz:0} Hz (resonance-gated via FemVoiceScore)."));
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Traceability + helpers
        // ──────────────────────────────────────────────────────────────────────────

        private static IReadOnlyDictionary<string, double> BuildRawInputs(VoiceIntelligenceInput input)
        {
            var d = new Dictionary<string, double>
            {
                ["averageResonance01"] = input.AverageResonance01,
                ["resonanceScore0100"] = input.ResonanceScore0100,
                ["comfortCompliance01"] = input.ComfortCompliance01,
                ["comfortBreaches"] = input.ComfortBreaches,
                ["averageStability01"] = input.AverageStability01,
                ["intonationRangeHz"] = input.IntonationRangeHz,
                ["averageF1Hz"] = input.AverageF1Hz,
                ["averageSpectralCentroidHz"] = input.AverageSpectralCentroidHz,
                ["averageHnrDb"] = input.AverageHnrDb,
                ["averageIntensity"] = input.AverageIntensity,
                ["averagePitchHz"] = input.AveragePitchHz,
                ["pitchVariation"] = input.PitchVariation,
            };
            if (input.Recovery is { } rec)
            {
                d["recovery.recentFatigueIndicators"] = rec.RecentFatigueIndicators;
                d["recovery.recentStrainEpisodes"] = rec.RecentStrainEpisodes;
                d["recovery.recentSafetyLocks"] = rec.RecentSafetyLocks;
                d["recovery.recentPauseRecommendations"] = rec.RecentPauseRecommendations;
                d["recovery.sessionsLast7Days"] = rec.SessionsLast7Days;
                d["recovery.hoursSinceLastSession"] = rec.HoursSinceLastSession;
            }
            return d;
        }

        private static bool IsUsable(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
