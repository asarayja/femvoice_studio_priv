using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// Case Review view-model (Sprint E, Agent 8).
    ///
    /// Lets the clinician/coach choose a <see cref="ReviewType"/> and period, then
    /// Create builds an <see cref="OutcomeProfile"/> via <see cref="OutcomeProfileBuilder"/>,
    /// assembles a <see cref="CaseReview"/> via <see cref="CaseReviewAssembler"/>, and
    /// persists it via <see cref="CaseReviewsStore"/>. Existing reviews are listed in
    /// <see cref="SavedReviews"/>. Complete transitions a Draft review to Completed.
    ///
    /// PARAMETERLESS CTOR: resolves all dependencies via App.Services; null-safe.
    /// TEST CTOR: accepts all dependencies explicitly — no App.Services, no disk I/O.
    /// </summary>
    public partial class CaseReviewViewModel : ObservableObject
    {
        // ── Dependencies ──────────────────────────────────────────────────────────
        private readonly OutcomeProfileBuilder? _outcomeProfileBuilder;
        private readonly CaseReviewAssembler? _assembler;
        private readonly CaseReviewsStore? _store;
        private readonly IDatabaseService? _database;
        private readonly IVoiceGoalProfileProvider? _goalProfileProvider;
        private readonly RecoveryIntelligenceService? _recoveryService;
        private readonly SessionAnalyticsStore? _analyticsStore;

        // ── Observable properties ─────────────────────────────────────────────────

        /// <summary>Selected review type (0=Monthly, 1=Goal, 2=Progress, 3=Recovery).</summary>
        [ObservableProperty]
        private int _selectedReviewTypeIndex;

        /// <summary>Start of the review period (defaults to first day of current month).</summary>
        [ObservableProperty]
        private DateTime _periodStart = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>End of the review period (defaults to today UTC).</summary>
        [ObservableProperty]
        private DateTime _periodEnd = DateTime.UtcNow;

        /// <summary>Feedback message after Create or Complete.</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        /// <summary>True while Create is running.</summary>
        [ObservableProperty]
        private bool _isCreating;

        /// <summary>The review selected in the list (used for Complete).</summary>
        [ObservableProperty]
        private CaseReview? _selectedReview;

        /// <summary>All persisted reviews for the current user, ordered by PeriodStart.</summary>
        public ObservableCollection<CaseReview> SavedReviews { get; } = new();

        // ── Constructors ──────────────────────────────────────────────────────────

        /// <summary>
        /// Parameterless constructor — resolves from App.Services; null-safe.
        /// </summary>
        public CaseReviewViewModel()
        {
            _outcomeProfileBuilder = App.Services?.GetService(typeof(OutcomeProfileBuilder)) as OutcomeProfileBuilder;
            _assembler             = App.Services?.GetService(typeof(CaseReviewAssembler))   as CaseReviewAssembler;
            _store                 = App.Services?.GetService(typeof(CaseReviewsStore))       as CaseReviewsStore;
            _database              = App.Services?.GetService(typeof(IDatabaseService))       as IDatabaseService;
            _goalProfileProvider   = App.Services?.GetService(typeof(IVoiceGoalProfileProvider)) as IVoiceGoalProfileProvider;
            _recoveryService       = App.Services?.GetService(typeof(RecoveryIntelligenceService)) as RecoveryIntelligenceService;
            _analyticsStore        = App.Services?.GetService(typeof(SessionAnalyticsStore))  as SessionAnalyticsStore;
        }

        /// <summary>
        /// Test constructor — injects all dependencies directly.
        /// </summary>
        public CaseReviewViewModel(
            OutcomeProfileBuilder outcomeProfileBuilder,
            CaseReviewAssembler assembler,
            CaseReviewsStore store,
            IDatabaseService database,
            IVoiceGoalProfileProvider? goalProfileProvider = null,
            RecoveryIntelligenceService? recoveryService = null,
            SessionAnalyticsStore? analyticsStore = null)
        {
            _outcomeProfileBuilder = outcomeProfileBuilder ?? throw new ArgumentNullException(nameof(outcomeProfileBuilder));
            _assembler             = assembler             ?? throw new ArgumentNullException(nameof(assembler));
            _store                 = store                 ?? throw new ArgumentNullException(nameof(store));
            _database              = database              ?? throw new ArgumentNullException(nameof(database));
            _goalProfileProvider   = goalProfileProvider;
            _recoveryService       = recoveryService;
            _analyticsStore        = analyticsStore;
        }

        // ── Commands ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds an OutcomeProfile, assembles a Draft CaseReview, persists it, and
        /// adds it to <see cref="SavedReviews"/>.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCreate))]
        private async Task CreateAsync()
        {
            if (_outcomeProfileBuilder is null || _assembler is null || _store is null || _database is null)
            {
                StatusMessage = LocalizationService.Instance.GetString("CaseReview_StatusServicesUnavailable");
                return;
            }

            IsCreating = true;
            StatusMessage = string.Empty;

            try
            {
                var now = DateTime.UtcNow;

                // 1) Build outcome profile (each source degrades gracefully to empty).
                var recoveryService = _recoveryService ?? new RecoveryIntelligenceService(new RecoveryScorer());
                var analyticsStore  = _analyticsStore
                    ?? new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());

                var outcome = await _outcomeProfileBuilder.AssembleFromStoreAsync(
                    _database,
                    _goalProfileProvider,
                    recoveryService,
                    analyticsStore,
                    now,
                    userId: 1).ConfigureAwait(false);

                // 2) Map index → ReviewType.
                var reviewType = SelectedReviewTypeIndex switch
                {
                    0 => ReviewType.Monthly,
                    1 => ReviewType.Goal,
                    2 => ReviewType.Progress,
                    3 => ReviewType.Recovery,
                    _ => ReviewType.Monthly
                };

                // 3) Assemble a Draft review.
                var review = _assembler.Build(outcome, PeriodStart, PeriodEnd, reviewType, now);

                // 4) Persist.
                await _store.SaveAsync(review).ConfigureAwait(false);

                // 5) Add to observable list (on UI thread — this is a fire-and-forget path in tests too).
                SavedReviews.Insert(0, review);

                StatusMessage = LocalizationService.Instance.GetFormattedString("CaseReview_StatusCreatedFormat", review.ReviewId);
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("CaseReview", $"Create FAILED; {ex.GetType().Name}: {ex.Message}");
                StatusMessage = SafeFailureMessages.For(SafeFailureKind.PersistenceReadback);
            }
            finally
            {
                IsCreating = false;
            }
        }

        private bool CanCreate() => !IsCreating;

        /// <summary>
        /// Transitions the <see cref="SelectedReview"/> from Draft to Completed.
        /// No-op when no review is selected or review is already completed.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanComplete))]
        private async Task CompleteAsync()
        {
            if (_store is null || SelectedReview is null)
                return;

            try
            {
                var completed = await _store.CompleteAsync(SelectedReview.ReviewId, DateTime.UtcNow)
                    .ConfigureAwait(false);

                if (completed is null)
                {
                    StatusMessage = LocalizationService.Instance.GetString("CaseReview_StatusNotFound");
                    return;
                }

                // Replace the Draft record in the list with the Completed record.
                var idx = FindReviewIndex(SelectedReview.ReviewId);
                if (idx >= 0)
                    SavedReviews[idx] = completed;

                SelectedReview = completed;
                StatusMessage = LocalizationService.Instance.GetString("CaseReview_StatusCompleted");
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("CaseReview", $"Complete FAILED; {ex.GetType().Name}: {ex.Message}");
                StatusMessage = SafeFailureMessages.For(SafeFailureKind.PersistenceReadback);
            }
        }

        private bool CanComplete() => SelectedReview?.Status == ReviewStatus.Draft;

        /// <summary>
        /// Loads all existing reviews for the current user into <see cref="SavedReviews"/>.
        /// </summary>
        [RelayCommand]
        private async Task LoadReviewsAsync()
        {
            if (_store is null)
                return;

            try
            {
                var reviews = await _store.GetByUserAsync(userId: 1).ConfigureAwait(false);
                SavedReviews.Clear();
                foreach (var r in reviews)
                    SavedReviews.Add(r);
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("CaseReview", $"Load FAILED; {ex.GetType().Name}: {ex.Message}");
                StatusMessage = SafeFailureMessages.For(SafeFailureKind.EmptyAnalytics);
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private int FindReviewIndex(Guid reviewId)
        {
            for (int i = 0; i < SavedReviews.Count; i++)
            {
                if (SavedReviews[i].ReviewId == reviewId)
                    return i;
            }
            return -1;
        }
    }
}
