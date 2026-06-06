using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    public enum FeedbackPriority
    {
        ProgressionUpdate = 10,
        PerformancePraise = 20,
        TechniqueCorrection = 30,
        HydrationSuggestion = 40,
        PauseRecommendation = 50,
        ActiveStrainAlert = 60,
        HealthWarning = 70,
        SafetyFreeze = 80
    }

    public enum FeedbackDecisionKind
    {
        Approved,
        Suppressed,
        Escalated
    }

    public sealed record FeedbackCandidate(
        string Message,
        string ReasonCode,
        FeedbackPriority Priority,
        MessageSeverity Severity,
        string Source = "",
        string ConflictKey = "");

    public sealed record FeedbackGuardContext(
        bool IsSafetyFreezeActive = false,
        bool IsHealthRiskActive = false,
        bool IsActiveStrainAlert = false,
        bool IsPauseRecommended = false,
        bool IsFatigueActive = false,
        bool IsHoldStable = true);

    public sealed record FeedbackDecision(
        FeedbackDecisionKind Kind,
        FeedbackCandidate Candidate,
        string Reason);

    public sealed class FeedbackConsistencyGuard
    {
        private readonly object _lock = new();
        private readonly Func<DateTime> _clock;
        private readonly TimeSpan _minimumInterval;
        private readonly int _escalationThreshold;
        private readonly Dictionary<string, DateTime> _lastApprovedByReason = new();
        private readonly Dictionary<string, int> _suppressionCounts = new();
        private DateTime _lastApprovedAt = DateTime.MinValue;
        private FeedbackCandidate? _activeWarning;

        public FeedbackConsistencyGuard(
            Func<DateTime>? clock = null,
            TimeSpan? minimumInterval = null,
            int escalationThreshold = 3)
        {
            _clock = clock ?? (() => DateTime.UtcNow);
            _minimumInterval = minimumInterval ?? TimeSpan.FromSeconds(2);
            _escalationThreshold = Math.Max(1, escalationThreshold);
        }

        public event Action<FeedbackCandidate>? FeedbackApproved;
        public event Action<FeedbackCandidate, string>? FeedbackSuppressed;
        public event Action<FeedbackCandidate, string>? FeedbackEscalated;

        public IReadOnlyList<FeedbackDecision> SubmitMany(
            IEnumerable<FeedbackCandidate> candidates,
            FeedbackGuardContext? context = null)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            var ordered = candidates
                .Where(c => !string.IsNullOrWhiteSpace(c.Message))
                .OrderByDescending(c => c.Priority)
                .ToList();

            if (ordered.Count == 0)
                return Array.Empty<FeedbackDecision>();

            var decisions = new List<FeedbackDecision>(ordered.Count);
            var approved = false;

            foreach (var candidate in ordered)
            {
                if (approved)
                {
                    decisions.Add(Suppress(candidate, "A higher-priority feedback message was already approved."));
                    continue;
                }

                var decision = Submit(candidate, context);
                decisions.Add(decision);
                approved = decision.Kind == FeedbackDecisionKind.Approved;
            }

            return decisions;
        }

        public FeedbackDecision Submit(FeedbackCandidate candidate, FeedbackGuardContext? context = null)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (string.IsNullOrWhiteSpace(candidate.Message))
                return Suppress(candidate, "Empty feedback message.");

            context ??= new FeedbackGuardContext();

            lock (_lock)
            {
                var clinicalBlock = GetClinicalSuppressionReason(candidate, context);
                if (clinicalBlock != null)
                    return SuppressLocked(candidate, clinicalBlock);

                var now = _clock();
                if (_lastApprovedByReason.TryGetValue(candidate.ReasonCode, out var lastForReason)
                    && now - lastForReason < _minimumInterval)
                {
                    return SuppressLocked(candidate, "Rate limit for repeated feedback reason.");
                }

                if (_activeWarning != null
                    && candidate.Priority < _activeWarning.Priority
                    && now - _lastApprovedAt < _minimumInterval)
                {
                    return SuppressLocked(candidate, "Active warning has priority over lower-priority feedback.");
                }

                if (_lastApprovedAt != DateTime.MinValue
                    && now - _lastApprovedAt < _minimumInterval
                    && candidate.Priority < FeedbackPriority.HealthWarning)
                {
                    return SuppressLocked(candidate, "Rate limit prevents multiple simultaneous hints.");
                }

                _lastApprovedAt = now;
                _lastApprovedByReason[candidate.ReasonCode] = now;
                _suppressionCounts.Remove(GetSuppressionKey(candidate));

                if (candidate.Priority >= FeedbackPriority.HealthWarning)
                    _activeWarning = candidate;

                FeedbackApproved?.Invoke(candidate);
                return new FeedbackDecision(FeedbackDecisionKind.Approved, candidate, "Approved.");
            }
        }

        private string? GetClinicalSuppressionReason(FeedbackCandidate candidate, FeedbackGuardContext context)
        {
            if (context.IsSafetyFreezeActive && candidate.Priority < FeedbackPriority.SafetyFreeze)
                return "Safety freeze suppresses all lower-priority feedback.";

            if (context.IsHealthRiskActive && candidate.Priority <= FeedbackPriority.ProgressionUpdate)
                return "Health risk suppresses progression updates.";

            if (context.IsHealthRiskActive && candidate.Priority == FeedbackPriority.PerformancePraise)
                return "Health risk suppresses praise.";

            if (context.IsActiveStrainAlert && candidate.Priority <= FeedbackPriority.PerformancePraise)
                return "Active strain suppresses praise and progression.";

            if (context.IsPauseRecommended && candidate.Priority == FeedbackPriority.TechniqueCorrection)
                return "Pause recommendation suppresses technique correction.";

            if (context.IsFatigueActive && candidate.Priority == FeedbackPriority.ProgressionUpdate)
                return "Fatigue suppresses progression updates.";

            if (!context.IsHoldStable && candidate.Priority == FeedbackPriority.PerformancePraise)
                return "Unstable hold suppresses praise.";

            if (context.IsSafetyFreezeActive && candidate.Priority == FeedbackPriority.HydrationSuggestion)
                return "Active freeze suppresses hydration hints.";

            return null;
        }

        private FeedbackDecision Suppress(FeedbackCandidate candidate, string reason)
        {
            lock (_lock)
            {
                return SuppressLocked(candidate, reason);
            }
        }

        private FeedbackDecision SuppressLocked(FeedbackCandidate candidate, string reason)
        {
            var key = GetSuppressionKey(candidate);
            _suppressionCounts.TryGetValue(key, out var count);
            count++;
            _suppressionCounts[key] = count;

            if (count >= _escalationThreshold)
            {
                FeedbackEscalated?.Invoke(candidate, reason);
                return new FeedbackDecision(FeedbackDecisionKind.Escalated, candidate, reason);
            }

            FeedbackSuppressed?.Invoke(candidate, reason);
            return new FeedbackDecision(FeedbackDecisionKind.Suppressed, candidate, reason);
        }

        private static string GetSuppressionKey(FeedbackCandidate candidate)
            => string.IsNullOrWhiteSpace(candidate.ConflictKey)
                ? candidate.ReasonCode
                : candidate.ConflictKey;
    }
}
