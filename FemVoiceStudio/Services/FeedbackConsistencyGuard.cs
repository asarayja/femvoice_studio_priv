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
        string ConflictKey = "",
        // Logisk UI-overflate kandidaten lander på. Den tidsbaserte «multiple
        // simultaneous hints»-anti-flommen scopes per kanal, slik at distinkte
        // paneler ikke sulter hverandre ut. Tom kanal ("") = den delte coach-
        // panel-overflaten (EDVM/SmartCoach/Progresjon/Helse) — uendret oppførsel.
        string Channel = "",
        DateTime CreatedAt = default);

    public sealed record FeedbackGuardContext(
        bool IsSafetyFreezeActive = false,
        bool IsHealthRiskActive = false,
        bool IsActiveStrainAlert = false,
        bool IsPauseRecommended = false,
        bool IsFatigueActive = false,
        bool IsHoldStable = true,
        int SessionElapsedSeconds = 0,
        DateTime? SessionStartedAt = null,
        double VoiceLoad = 0);

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
        private readonly Dictionary<string, DateTime> _lastApprovedByConflict = new();
        private readonly Dictionary<string, DateTime> _lastApprovedByMessage = new();
        private readonly Dictionary<string, DateTime> _lastApprovedAtByChannel = new();
        private readonly Dictionary<string, int> _shownThisSessionByBudgetKey = new();
        private readonly Dictionary<string, int> _suppressionCounts = new();
        private DateTime _lastApprovedAt = DateTime.MinValue;
        private DateTime _sessionStartedAt = DateTime.MinValue;
        private FeedbackCandidate? _activeWarning;

        public static readonly TimeSpan StaleFeedbackMaxAge = TimeSpan.FromMinutes(60);
        public static readonly TimeSpan LowPriorityRepeatCooldown = TimeSpan.FromSeconds(300);
        public const int FreshSessionGenericSuppressSeconds = 120;
        public const int LowPriorityPerSessionLimit = 1;
        public const int HydrationPerSessionLimit = 2;
        public const int GenericEncouragementPerSessionLimit = 2;

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

        public void BeginSession(DateTime? startedAt = null)
        {
            lock (_lock)
            {
                _sessionStartedAt = startedAt ?? _clock();
                _lastApprovedByReason.Clear();
                _lastApprovedByConflict.Clear();
                _lastApprovedByMessage.Clear();
                _lastApprovedAtByChannel.Clear();
                _shownThisSessionByBudgetKey.Clear();
                _suppressionCounts.Clear();
                _lastApprovedAt = DateTime.MinValue;
                _activeWarning = null;
            }
        }

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
                var eligibilityBlock = GetEligibilitySuppressionReason(candidate, context, now);
                if (eligibilityBlock != null)
                    return SuppressLocked(candidate, eligibilityBlock);

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

                // Anti-flom per UI-overflate: distinkte paneler (f.eks. forsidens fire
                // MainScreen-bindinger) skal ikke sulte hverandre ut. Den delte coach-
                // panel-overflaten (Channel == "") beholder coaching-anti-flommen.
                //
                // F1 (safety): HELE helse-båndet (HydrationSuggestion=40 / Pause=50 /
                // ActiveStrainAlert=60 og oppover) MÅ hoppe over den per-kanal tidsgaten.
                // Før kunne en tidligere lavere-prioritert coaching-melding på den delte
                // tomme kanalen tidsundertrykke en påfølgende helse-melding. Terskelen er
                // derfor senket fra HealthWarning(70) til HydrationSuggestion(40): bare
                // ren coaching (< 40: Progression/Praise/Technique) anti-flommes fortsatt
                // — det bevarer to-sub-40-tilfellet uendret.
                var channel = candidate.Channel ?? string.Empty;
                if (_lastApprovedAtByChannel.TryGetValue(channel, out var lastForChannel)
                    && now - lastForChannel < _minimumInterval
                    && candidate.Priority < FeedbackPriority.HydrationSuggestion)
                {
                    return SuppressLocked(candidate, "Rate limit prevents multiple simultaneous hints.");
                }

                _lastApprovedAt = now;
                _lastApprovedAtByChannel[channel] = now;
                _lastApprovedByReason[candidate.ReasonCode] = now;
                if (!string.IsNullOrWhiteSpace(candidate.ConflictKey))
                    _lastApprovedByConflict[candidate.ConflictKey] = now;
                _lastApprovedByMessage[candidate.Message] = now;
                IncrementSessionBudget(candidate);
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

            if (context.IsPauseRecommended && candidate.Priority <= FeedbackPriority.PerformancePraise)
                return "Pause recommendation suppresses praise and progression.";

            if (context.IsFatigueActive && candidate.Priority <= FeedbackPriority.PerformancePraise)
                return "Fatigue suppresses praise and progression.";

            if (!context.IsHoldStable && candidate.Priority == FeedbackPriority.PerformancePraise)
                return "Unstable hold suppresses praise.";

            if (context.IsSafetyFreezeActive && candidate.Priority == FeedbackPriority.HydrationSuggestion)
                return "Active freeze suppresses hydration hints.";

            return null;
        }

        private string? GetEligibilitySuppressionReason(
            FeedbackCandidate candidate,
            FeedbackGuardContext context,
            DateTime now)
        {
            if (IsHighPriorityBypass(candidate, context))
                return null;

            var createdAt = candidate.CreatedAt == default ? now : candidate.CreatedAt;
            if (IsLowPriority(candidate) && now - createdAt > StaleFeedbackMaxAge)
                return "Stale low-priority feedback candidate was removed.";

            var elapsed = context.SessionElapsedSeconds;
            if (elapsed <= 0 && context.SessionStartedAt.HasValue)
                elapsed = Math.Max(0, (int)(now - context.SessionStartedAt.Value).TotalSeconds);
            if (elapsed <= 0 && _sessionStartedAt != DateTime.MinValue)
                elapsed = Math.Max(0, (int)(now - _sessionStartedAt).TotalSeconds);

            if (elapsed > 0
                && elapsed < FreshSessionGenericSuppressSeconds
                && IsFreshSessionSuppressed(candidate))
            {
                return "Fresh session suppresses generic or low-priority feedback until enough context exists.";
            }

            if (!string.IsNullOrWhiteSpace(candidate.ConflictKey)
                && _lastApprovedByConflict.TryGetValue(candidate.ConflictKey, out var lastConflict)
                && IsLowPriority(candidate)
                && now - lastConflict < LowPriorityRepeatCooldown)
            {
                return "Duplicate conflict key is still cooling down.";
            }

            if (_lastApprovedByMessage.TryGetValue(candidate.Message, out var lastMessage)
                && IsLowPriority(candidate)
                && now - lastMessage < LowPriorityRepeatCooldown)
            {
                return "Duplicate message text/resource key is still cooling down.";
            }

            if (_lastApprovedByReason.TryGetValue(candidate.ReasonCode, out var lastReason)
                && IsLowPriority(candidate)
                && now - lastReason < LowPriorityRepeatCooldown)
            {
                return "Low-priority reason code is still cooling down.";
            }

            var budgetKey = GetSessionBudgetKey(candidate);
            if (_shownThisSessionByBudgetKey.TryGetValue(budgetKey, out var shown)
                && shown >= GetSessionLimit(candidate))
            {
                return "Per-session display limit reached for this feedback category.";
            }

            return null;
        }

        private static bool IsHighPriorityBypass(FeedbackCandidate candidate, FeedbackGuardContext context)
            => candidate.Priority >= FeedbackPriority.PauseRecommendation
               || candidate.Severity == MessageSeverity.Warning
               || context.IsSafetyFreezeActive
               || context.IsHealthRiskActive
               || context.IsActiveStrainAlert
               || context.IsPauseRecommended
               || context.IsFatigueActive;

        private static bool IsLowPriority(FeedbackCandidate candidate)
            => candidate.Priority <= FeedbackPriority.HydrationSuggestion;

        private static bool IsFreshSessionSuppressed(FeedbackCandidate candidate)
            => candidate.Priority is FeedbackPriority.ProgressionUpdate
                    or FeedbackPriority.PerformancePraise
                    or FeedbackPriority.HydrationSuggestion
               || IsGeneric(candidate);

        private static bool IsGeneric(FeedbackCandidate candidate)
        {
            var reason = candidate.ReasonCode ?? string.Empty;
            var conflict = candidate.ConflictKey ?? string.Empty;
            var source = candidate.Source ?? string.Empty;
            var message = candidate.Message ?? string.Empty;

            return reason.Contains("GENERIC", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("MOTIVATION", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("ACHIEVEMENT", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("HYDRATION", StringComparison.OrdinalIgnoreCase)
                || conflict.Contains("HYDRATION", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Generic", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Hydration", StringComparison.OrdinalIgnoreCase)
                || source.Equals("HydrationAdvisor", StringComparison.OrdinalIgnoreCase)
                || source.Equals("SmartCoachEngine", StringComparison.OrdinalIgnoreCase)
                    && candidate.Priority < FeedbackPriority.HealthWarning;
        }

        private static string GetSessionBudgetKey(FeedbackCandidate candidate)
        {
            if (IsHydration(candidate))
                return "HYDRATION";

            if (IsGenericEncouragement(candidate))
                return "GENERIC_ENCOURAGEMENT";

            if (IsGeneric(candidate))
                return $"GENERIC:{candidate.ConflictKey}:{candidate.ReasonCode}";

            return $"LOW:{candidate.ConflictKey}:{candidate.ReasonCode}";
        }

        private static int GetSessionLimit(FeedbackCandidate candidate)
        {
            if (IsHydration(candidate))
                return HydrationPerSessionLimit;

            if (IsGenericEncouragement(candidate))
                return GenericEncouragementPerSessionLimit;

            if (IsGeneric(candidate) || candidate.Priority <= FeedbackPriority.PerformancePraise)
                return LowPriorityPerSessionLimit;

            return int.MaxValue;
        }

        private static bool IsHydration(FeedbackCandidate candidate)
            => (candidate.ReasonCode ?? string.Empty).Contains("HYDRATION", StringComparison.OrdinalIgnoreCase)
               || (candidate.ConflictKey ?? string.Empty).Contains("HYDRATION", StringComparison.OrdinalIgnoreCase)
               || string.Equals(candidate.Source, "HydrationAdvisor", StringComparison.OrdinalIgnoreCase);

        private static bool IsGenericEncouragement(FeedbackCandidate candidate)
            => candidate.Priority == FeedbackPriority.PerformancePraise
               || (candidate.ReasonCode ?? string.Empty).Contains("MOTIVATION", StringComparison.OrdinalIgnoreCase)
               || (candidate.ReasonCode ?? string.Empty).Contains("ACHIEVEMENT", StringComparison.OrdinalIgnoreCase)
               || (candidate.Source ?? string.Empty).Equals("SmartCoachEngine", StringComparison.OrdinalIgnoreCase)
                    && candidate.Priority < FeedbackPriority.HealthWarning;

        private void IncrementSessionBudget(FeedbackCandidate candidate)
        {
            if (!IsLowPriority(candidate))
                return;

            var budgetKey = GetSessionBudgetKey(candidate);
            _shownThisSessionByBudgetKey.TryGetValue(budgetKey, out var shown);
            _shownThisSessionByBudgetKey[budgetKey] = shown + 1;
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
