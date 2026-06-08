using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Computes an <see cref="InsightExplanation"/> for a recommendation: explains why a
    /// particular <see cref="VoiceDimension"/> was chosen as focus and what the associated
    /// exercise is expected to improve, given effectiveness evidence and recent trend data.
    ///
    /// ── DESIGN (pure / deterministic, no IO) ──────────────────────────────────────────
    /// The single entry point is <see cref="Compute"/>. Given:
    ///   • <paramref name="focus"/>          — the dimension being recommended.
    ///   • <paramref name="effectiveness"/>  — per-exercise effectiveness profile (nullable).
    ///   • <paramref name="recovery"/>       — current recovery result.
    ///   • <paramref name="recentWindow"/>   — optional 7-day or 30-day trend window.
    ///   • <paramref name="style"/>          — user's preferred voice style (tone framing only).
    ///
    /// Output routing:
    ///   1. Low-data / no-effectiveness   ⇒ ReasonCode "INSUFFICIENT_EVIDENCE"
    ///      (HasEnoughData false OR effectiveness null). Never fabricates a claim.
    ///   2. Recovery / Comfort dimension  ⇒ ReasonCode "RECOVERY_FOCUS" / "COMFORT_FOCUS"
    ///      (Health-layer framing; exercise outcome is comfort/rest, not dimension gain).
    ///   3. Weakest dimension in window   ⇒ ReasonCode "WEAKEST_DIMENSION"
    ///      (dimension slope is negative or well below other dimensions in the window).
    ///   4. Most improvement potential    ⇒ ReasonCode "MOST_GAIN_POTENTIAL"
    ///      (positive effectiveness gain, upward slope in window, room to grow).
    ///
    /// Tone is STYLE-AWARE: the framing adapts to <see cref="VoiceStyleGoal"/> (e.g.
    /// "resonance" vs "timbre clarity") — the FOCUS dimension never changes.
    ///
    /// ConfidenceLabel threshold: ≥70 = High, ≥40 = Medium, else Low.
    ///
    /// Text is resolved via <see cref="ILocalizationService"/> using the frozen RESX keys:
    ///   Explanation_WhyFocus_Template, Explanation_WhatItImproves_Template,
    ///   Explanation_ExpectedOutcome_Template, Explanation_Confidence_High/Medium/Low.
    ///
    /// CLINICAL INVARIANT: this engine produces DESCRIPTIVE intelligence only. It must
    /// never be used to override a Safety, Health, or Recovery gate. The hierarchy
    /// (Safety &gt; Health &gt; Recovery &gt; Comfort &gt; Goals &gt; Progression &gt; Coaching)
    /// is enforced upstream; this engine is downstream of all gates.
    /// </summary>
    public sealed class RecommendationExplanationEngine
    {
        // ── Confidence thresholds ─────────────────────────────────────────────────
        private const double ConfidenceHighThreshold   = 70.0;
        private const double ConfidenceMediumThreshold = 40.0;

        // ── Weakness / gain-potential thresholds ─────────────────────────────────
        /// <summary>
        /// A dimension with a score below this in the trend window is considered genuinely
        /// weak (eligible for "WEAKEST_DIMENSION" framing). Matches LearningPathProfileBuilder.
        /// </summary>
        private const double WeaknessScoreThreshold = 60.0;

        /// <summary>
        /// Minimum <see cref="ExerciseEffectivenessProfile.SessionCount"/> to trust that
        /// the effectiveness profile carries real evidence. Below this we emit INSUFFICIENT.
        /// </summary>
        private const int MinSessionsForEvidence = 3;

        private readonly ILocalizationService _loc;

        /// <param name="localization">
        /// Localization service used to resolve all human-facing strings via frozen RESX keys.
        /// Pass a <see cref="TestLocalizationService"/> in tests.
        /// </param>
        public RecommendationExplanationEngine(ILocalizationService localization)
        {
            _loc = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        /// <summary>
        /// Computes an <see cref="InsightExplanation"/> for the given <paramref name="focus"/>
        /// dimension. Pure: no IO, fully deterministic given fixed inputs.
        /// </summary>
        /// <param name="focus">The dimension being recommended as today's focus.</param>
        /// <param name="effectiveness">
        /// Effectiveness profile for the recommended exercise, or <c>null</c> when no
        /// exercise has been evaluated yet (forces INSUFFICIENT_EVIDENCE path).
        /// </param>
        /// <param name="recovery">Current recovery result from <see cref="RecoveryScorer"/>.</param>
        /// <param name="recentWindow">
        /// The most recent trend window (7- or 30-day), or <c>null</c> when insufficient
        /// history exists. Drives slope-based routing.
        /// </param>
        /// <param name="style">User's preferred voice style — affects tone framing only.</param>
        public InsightExplanation Compute(
            VoiceDimension              focus,
            ExerciseEffectivenessProfile? effectiveness,
            RecoveryResult              recovery,
            TrendWindow?                recentWindow,
            VoiceStyleGoal              style)
        {
            // ── Determine routing branch ──────────────────────────────────────────
            var reasonCode = ClassifyReasonCode(focus, effectiveness, recentWindow);

            // ── Compute confidence ────────────────────────────────────────────────
            var confidence = ComputeConfidence(effectiveness, recentWindow);

            // ── Localised dimension label (style-aware) ───────────────────────────
            var dimLabel = StyleAwareDimensionLabel(focus, style);

            // ── Build localised strings ───────────────────────────────────────────
            var whyThisFocus   = BuildWhyThisFocus(reasonCode, dimLabel, focus, recentWindow);
            var whatItImproves = BuildWhatItImproves(reasonCode, dimLabel, effectiveness);
            var expectedOutcome = BuildExpectedOutcome(reasonCode, dimLabel, effectiveness, recentWindow, recovery);
            var confidenceLabel = BuildConfidenceLabel(confidence);

            return new InsightExplanation
            {
                ReasonCode      = reasonCode,
                Focus           = focus,
                WhyThisFocus    = whyThisFocus,
                WhatItImproves  = whatItImproves,
                ExpectedOutcome = expectedOutcome,
                ConfidenceLabel = confidenceLabel
            };
        }

        // ── Routing ───────────────────────────────────────────────────────────────

        private static string ClassifyReasonCode(
            VoiceDimension               focus,
            ExerciseEffectivenessProfile? effectiveness,
            TrendWindow?                 window)
        {
            // No data branch takes precedence.
            if (!HasEvidence(effectiveness))
                return "INSUFFICIENT_EVIDENCE";

            // Health-layer dimensions get their own codes regardless of slope.
            if (focus == VoiceDimension.Recovery)
                return "RECOVERY_FOCUS";
            if (focus == VoiceDimension.Comfort)
                return "COMFORT_FOCUS";

            // Slope-based routing from the trend window.
            if (window is not null && window.HasEnoughData)
            {
                var slope = GetDimensionSlope(window, focus);

                // Negative slope or mean score below weakness threshold ⇒ weakest dimension.
                var mean = window.CompositeMean; // proxy when per-dim mean not tracked
                if (slope <= 0.0 || mean < WeaknessScoreThreshold)
                    return "WEAKEST_DIMENSION";

                // Positive slope + positive effectiveness gain ⇒ gain potential.
                if (slope > 0.0 && effectiveness!.ResonanceGain > 0.0)
                    return "MOST_GAIN_POTENTIAL";
            }

            // Fallback: use the effectiveness gain direction.
            var primaryGain = GetPrimaryGainForDimension(focus, effectiveness!);
            return primaryGain > 0.0 ? "MOST_GAIN_POTENTIAL" : "WEAKEST_DIMENSION";
        }

        private static bool HasEvidence(ExerciseEffectivenessProfile? eff)
        {
            if (eff is null) return false;
            // Require HasEnoughData (as defined by the profile) AND a minimum raw session
            // count so we never misread a profile with HasEnoughData=false.
            return eff.HasEnoughData && eff.SessionCount >= MinSessionsForEvidence;
        }

        // ── Confidence ────────────────────────────────────────────────────────────

        private static double ComputeConfidence(
            ExerciseEffectivenessProfile? eff,
            TrendWindow?                 window)
        {
            // Base (mirrors BuildConfidence empty-history value = 35).
            const double baseScore = 35.0;

            if (!HasEvidence(eff))
                return baseScore * 0.5; // halved for no-evidence path

            // Volume component: min(sessions, 12) / 12 × 25  (matches spec: volume min(points,12)/12*25).
            const int   volumeCap    = 12;
            const double volumeWeight = 25.0;
            var sessions = eff!.SessionCount;
            var volumeTerm = Math.Min(sessions, volumeCap) / (double)volumeCap * volumeWeight;

            // Trend component from the window's composite slope (mirrors BuildConfidence).
            const double trendWeight = 20.0;
            double trendTerm;
            if (window is not null && window.HasEnoughData)
            {
                var normalised = Math.Clamp(window.CompositeSlope, -1.0, 1.0);
                var trend01    = (normalised + 1.0) / 2.0;
                trendTerm      = trend01 * trendWeight;
            }
            else
            {
                trendTerm = 0.5 * trendWeight; // neutral half-weight (too short to judge)
            }

            // Stability: use CompositeEffectiveness as a proxy for signal stability.
            const double stabilityWeight = 20.0;
            var stabProxy = Math.Clamp(eff.CompositeEffectiveness / 100.0, 0.0, 1.0);
            var stabilityTerm = stabProxy * stabilityWeight;

            var raw = baseScore * 0.5 + volumeTerm + trendTerm + stabilityTerm;
            return Math.Clamp(raw, 0.0, 100.0);
        }

        // ── Localised string builders ─────────────────────────────────────────────

        private string BuildWhyThisFocus(
            string         reasonCode,
            string         dimLabel,
            VoiceDimension focus,
            TrendWindow?   window)
        {
            var template = _loc["Explanation_WhyFocus_Template"];
            if (string.IsNullOrEmpty(template) || template == "Explanation_WhyFocus_Template")
            {
                // Fallback when key is missing from test stub.
                return BuildFallbackWhy(reasonCode, dimLabel, focus, window);
            }
            return string.Format(CultureInfo.InvariantCulture, template, dimLabel, reasonCode);
        }

        private static string BuildFallbackWhy(
            string         reasonCode,
            string         dimLabel,
            VoiceDimension focus,
            TrendWindow?   window)
        {
            return reasonCode switch
            {
                "INSUFFICIENT_EVIDENCE" =>
                    $"[{dimLabel}] Utilstrekkelig grunnlag for å forklare fokusvalget.",
                "RECOVERY_FOCUS" =>
                    $"[{dimLabel}] Stemmen trenger hvile — restitusjon prioriteres over trening.",
                "COMFORT_FOCUS" =>
                    $"[{dimLabel}] Komfort er lavest og krever oppmerksomhet nå.",
                "WEAKEST_DIMENSION" =>
                    $"[{dimLabel}] Denne dimensjonen er svakest og har mest å hente.",
                "MOST_GAIN_POTENTIAL" =>
                    $"[{dimLabel}] Effektivitetsdata viser størst fremgangspotensial her.",
                _ =>
                    $"[{dimLabel}] Fokusert trening på denne dimensjonen er anbefalt."
            };
        }

        private string BuildWhatItImproves(
            string                        reasonCode,
            string                        dimLabel,
            ExerciseEffectivenessProfile? eff)
        {
            var template = _loc["Explanation_WhatItImproves_Template"];
            if (string.IsNullOrEmpty(template) || template == "Explanation_WhatItImproves_Template")
            {
                return BuildFallbackWhatItImproves(reasonCode, dimLabel, eff);
            }
            // {0} = dimension label, {1} = gain note
            var gainNote = BuildGainNote(eff);
            return string.Format(CultureInfo.InvariantCulture, template, dimLabel, gainNote);
        }

        private static string BuildFallbackWhatItImproves(
            string                        reasonCode,
            string                        dimLabel,
            ExerciseEffectivenessProfile? eff)
        {
            if (reasonCode == "INSUFFICIENT_EVIDENCE")
                return $"[{dimLabel}] Økt-nivå: for lite data til å kvantifisere forventet forbedring.";

            var gainNote = BuildGainNote(eff);
            return $"[{dimLabel}] Økt-nivå: {gainNote}";
        }

        private string BuildExpectedOutcome(
            string                        reasonCode,
            string                        dimLabel,
            ExerciseEffectivenessProfile? eff,
            TrendWindow?                  window,
            RecoveryResult                recovery)
        {
            var template = _loc["Explanation_ExpectedOutcome_Template"];
            if (string.IsNullOrEmpty(template) || template == "Explanation_ExpectedOutcome_Template")
            {
                return BuildFallbackOutcome(reasonCode, dimLabel, eff, window, recovery);
            }
            // {0} = dimension, {1} = outcome summary
            var outcomeSummary = BuildOutcomeSummary(reasonCode, eff, window, recovery);
            return string.Format(CultureInfo.InvariantCulture, template, dimLabel, outcomeSummary);
        }

        private static string BuildFallbackOutcome(
            string                        reasonCode,
            string                        dimLabel,
            ExerciseEffectivenessProfile? eff,
            TrendWindow?                  window,
            RecoveryResult                recovery)
        {
            if (reasonCode == "INSUFFICIENT_EVIDENCE")
                return $"[{dimLabel}] INSUFFICIENT_EVIDENCE: for lite treningshistorikk til å forutsi utvikling.";

            var summary = BuildOutcomeSummary(reasonCode, eff, window, recovery);
            return $"[{dimLabel}] {summary}";
        }

        private static string BuildOutcomeSummary(
            string                        reasonCode,
            ExerciseEffectivenessProfile? eff,
            TrendWindow?                  window,
            RecoveryResult                recovery)
        {
            if (reasonCode == "INSUFFICIENT_EVIDENCE")
                return "Utilstrekkelig grunnlag.";

            if (reasonCode is "RECOVERY_FOCUS" or "COMFORT_FOCUS")
            {
                var statusLabel = recovery.Status switch
                {
                    RecoveryStatus.Overtrained   => "overtrent",
                    RecoveryStatus.Strained      => "belastet",
                    RecoveryStatus.Adequate      => "tilstrekkelig restituert",
                    RecoveryStatus.WellRecovered => "godt restituert",
                    _                            => "ukjent status"
                };
                return $"Fokus på restitusjon gir lavere belastning (nåværende status: {statusLabel}).";
            }

            var trendPart = window is not null && window.HasEnoughData
                ? (window.CompositeSlope > 0.5
                    ? "Stigende trend støtter forventet fremgang."
                    : window.CompositeSlope < -0.5
                        ? "Nedadgående trend — regelmessig øvelse forventes å snu kursen."
                        : "Stabil trend — jevn fremgang forventes.")
                : "Trend ikke tilgjengelig.";

            var effPart = eff is not null
                ? string.Create(CultureInfo.InvariantCulture,
                    $"Suksessrate {eff.UserSuccessRate:0}%, effektivitet {eff.CompositeEffectiveness:0}/100.")
                : string.Empty;

            return string.IsNullOrEmpty(effPart)
                ? trendPart
                : $"{trendPart} {effPart}";
        }

        private string BuildConfidenceLabel(double confidence)
        {
            if (confidence >= ConfidenceHighThreshold)
                return _loc["Explanation_Confidence_High"];
            if (confidence >= ConfidenceMediumThreshold)
                return _loc["Explanation_Confidence_Medium"];
            return _loc["Explanation_Confidence_Low"];
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the OLS slope for <paramref name="dim"/> from the window's
        /// <see cref="TrendWindow.DimensionSlopes"/> dictionary. Falls back to the
        /// composite slope when the dimension key is absent.
        /// </summary>
        private static double GetDimensionSlope(TrendWindow window, VoiceDimension dim)
        {
            if (window.DimensionSlopes.TryGetValue(dim, out var s))
                return s;
            return window.CompositeSlope;
        }

        /// <summary>
        /// Returns the primary gain metric for <paramref name="dim"/> from the effectiveness
        /// profile. Resonance/Consistency/Intonation/VocalWeight/Pitch use ResonanceGain as a
        /// proxy when no dedicated gain exists.
        /// </summary>
        private static double GetPrimaryGainForDimension(
            VoiceDimension               dim,
            ExerciseEffectivenessProfile eff)
            => dim switch
            {
                VoiceDimension.Resonance    => eff.ResonanceGain,
                VoiceDimension.Comfort      => eff.ComfortGain,
                VoiceDimension.Consistency  => eff.ConsistencyGain,
                // VocalWeight, Intonation, Pitch: no dedicated gain field — use resonance as proxy.
                _                           => eff.ResonanceGain
            };

        private static string BuildGainNote(ExerciseEffectivenessProfile? eff)
        {
            if (eff is null)
                return "ingen effektivitetsdata tilgjengelig";

            var parts = new List<string>(3);
            if (eff.ResonanceGain > 0)
                parts.Add(string.Create(CultureInfo.InvariantCulture,
                    $"resonansfremgang +{eff.ResonanceGain:0.1} poeng/økt"));
            if (eff.ComfortGain > 0 && eff.HasComfortData)
                parts.Add(string.Create(CultureInfo.InvariantCulture,
                    $"komfortfremgang +{eff.ComfortGain:0.1} poeng/økt"));
            if (eff.ConsistencyGain > 0)
                parts.Add(string.Create(CultureInfo.InvariantCulture,
                    $"konsistensfremgang +{eff.ConsistencyGain:0.1} poeng/økt"));

            return parts.Count > 0
                ? string.Join(", ", parts)
                : "ingen positiv effektivitet registrert";
        }

        /// <summary>
        /// Returns a style-aware label for the <paramref name="dimension"/>, adapting
        /// terminology to <paramref name="style"/> without changing the focus.
        /// Only the label/framing changes — the underlying dimension is unchanged.
        /// </summary>
        private static string StyleAwareDimensionLabel(VoiceDimension dimension, VoiceStyleGoal style)
        {
            // For most styles, use canonical Norwegian labels.
            // DarkFeminine gets slightly adjusted resonance/weight framing.
            return (dimension, style) switch
            {
                (VoiceDimension.Resonance,   VoiceStyleGoal.DarkFeminine)  => "klangfarge",
                (VoiceDimension.VocalWeight, VoiceStyleGoal.DarkFeminine)  => "stemmevekt",
                (VoiceDimension.Resonance,   VoiceStyleGoal.Androgynous)   => "klangnøytralitet",
                (VoiceDimension.Recovery,    _)                            => "restitusjon",
                (VoiceDimension.Comfort,     _)                            => "komfort",
                (VoiceDimension.Resonance,   _)                            => "resonans",
                (VoiceDimension.Consistency, _)                            => "konsistens",
                (VoiceDimension.Intonation,  _)                            => "intonasjon",
                (VoiceDimension.VocalWeight, _)                            => "stemmevekt",
                (VoiceDimension.Pitch,       _)                            => "tonehøyde",
                _                                                          => dimension.ToString()
            };
        }

        // ── OLS slope (verbatim copy from ExerciseEffectivenessEngine.LinearSlope) ──
        // One private copy per motor as per codebase convention.

        /// <summary>
        /// Ordinary-least-squares slope of y over x = 0,1,2,… Returns 0 when undetermined
        /// (mirrors LearningPathProfileBuilder.LinearSlope / RecoveryIntelligenceService).
        /// </summary>
        private static double LinearSlope(IReadOnlyList<double> y)
        {
            var clean = (y ?? Array.Empty<double>())
                .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
                .ToList();
            var n = clean.Count;
            if (n < 2) return 0.0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var i = 0; i < n; i++)
            {
                sumX += i;
                sumY += clean[i];
                sumXY += i * clean[i];
                sumX2 += (double)i * i;
            }

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 0.0001) return 0.0;
            return (n * sumXY - sumX * sumY) / denominator;
        }
    }
}
