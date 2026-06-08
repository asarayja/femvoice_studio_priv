using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Agent 12 — SCENARIO "Coach Review" + audit completeness.
    ///
    /// Two contracts are proved here:
    /// <list type="bullet">
    ///   <item><description>AUDIT COMPLETENESS: every audit-worthy action category
    ///   (<see cref="AuditEntityType.Override"/>, <see cref="AuditEntityType.GoalChange"/>,
    ///   <see cref="AuditEntityType.ReviewAction"/>, <see cref="AuditEntityType.RecoveryEvent"/>,
    ///   <see cref="AuditEntityType.Recommendation"/>) that is appended to the
    ///   <see cref="AuditTrailStore"/> is found again by <c>QueryAsync</c>.</description></item>
    ///   <item><description>COACH REVIEW: a coach can assemble a <see cref="CaseReview"/> from an
    ///   <see cref="OutcomeProfile"/> via <see cref="CaseReviewAssembler"/>, persist it through
    ///   <see cref="CaseReviewsStore"/>, and drive it Draft → Completed.</description></item>
    /// </list>
    ///
    /// House style: no mocking frameworks — real stores over in-memory repositories; every
    /// timestamp is a fixed constant for determinism.
    /// </summary>
    public class AuditCompletenessTests
    {
        private const int UserId = 4242;
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime PeriodStart = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime PeriodEnd = new(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);

        // All five audit-worthy action categories.
        private static readonly AuditEntityType[] AllEntityTypes =
        {
            AuditEntityType.Override,
            AuditEntityType.GoalChange,
            AuditEntityType.ReviewAction,
            AuditEntityType.RecoveryEvent,
            AuditEntityType.Recommendation,
        };

        // ── 1. Every audit-worthy category is appended AND found by query ───────────
        [Fact]
        public async Task AuditTrail_EveryActionCategory_IsAppendedAndQueryable()
        {
            var store = new AuditTrailStore(new InMemoryAuditTrailRepository());

            var i = 0;
            foreach (var type in AllEntityTypes)
            {
                await store.AppendAsync(new AuditEvent
                {
                    AuditId = Guid.NewGuid(),
                    UserId = UserId,
                    OccurredAt = Now.AddMinutes(i++),
                    EntityType = type,
                    EntityId = $"entity-{type}",
                    ActorRole = "Coach",
                    ReasonCode = $"{type}_RECORDED",
                });
            }

            var all = await store.QueryAsync(UserId);
            Assert.Equal(AllEntityTypes.Length, all.Count);

            // Each category is individually retrievable by its EntityType filter.
            foreach (var type in AllEntityTypes)
            {
                var ofType = await store.QueryAsync(UserId, entityType: type);
                var ev = Assert.Single(ofType);
                Assert.Equal(type, ev.EntityType);
                Assert.Equal($"entity-{type}", ev.EntityId);
                Assert.Equal($"{type}_RECORDED", ev.ReasonCode);
            }
        }

        // ── 2. No audit-worthy category is silently dropped (set equality) ──────────
        [Fact]
        public async Task AuditTrail_NoCategoryDropped_SetMatchesExactly()
        {
            var store = new AuditTrailStore(new InMemoryAuditTrailRepository());

            foreach (var type in AllEntityTypes)
                await store.AppendAsync(MakeEvent(type, Now));

            var found = (await store.QueryAsync(UserId)).Select(e => e.EntityType).ToHashSet();

            Assert.Equal(AllEntityTypes.ToHashSet(), found);
        }

        // ── 3. A coach REVIEW action is itself audited and traceable to the review ──
        [Fact]
        public async Task CoachReviewAction_IsAuditedAndLinksToReview()
        {
            var auditStore = new AuditTrailStore(new InMemoryAuditTrailRepository());
            var reviewsStore = new CaseReviewsStore(new InMemoryCaseReviewsRepository());
            var assembler = new CaseReviewAssembler();

            // Coach assembles + persists a review of the user's outcome.
            var outcome = MakeOutcome(hasEnoughData: true);
            var review = assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Monthly, Now);
            await reviewsStore.SaveAsync(review);

            // The act of reviewing is recorded in the audit trail, linked to the ReviewId.
            await auditStore.AppendAsync(new AuditEvent
            {
                AuditId = Guid.NewGuid(),
                UserId = UserId,
                OccurredAt = Now,
                EntityType = AuditEntityType.ReviewAction,
                EntityId = review.ReviewId.ToString("D"),
                ActorRole = "Coach",
                ReasonCode = "REVIEW_OPENED",
            });

            var reviewAudits = await auditStore.QueryAsync(UserId, entityType: AuditEntityType.ReviewAction);
            var audit = Assert.Single(reviewAudits);
            Assert.Equal(review.ReviewId.ToString("D"), audit.EntityId);

            // The audited review is itself retrievable from the reviews store.
            var persisted = await reviewsStore.GetByIdAsync(review.ReviewId);
            Assert.NotNull(persisted);
            Assert.Equal(UserId, persisted!.UserId);
        }

        // ── 4. COACH REVIEW lifecycle: Build (Draft) → persist → Complete ───────────
        [Fact]
        public async Task CoachReview_DraftToCompleted_FullLifecycle()
        {
            var reviewsStore = new CaseReviewsStore(new InMemoryCaseReviewsRepository());
            var assembler = new CaseReviewAssembler();
            var outcome = MakeOutcome(hasEnoughData: true);

            // Build → Draft.
            var draft = assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Goal, Now);
            Assert.Equal(ReviewStatus.Draft, draft.Status);
            Assert.Null(draft.CompletedAt);

            await reviewsStore.SaveAsync(draft);

            // Complete → Completed + CompletedAt stamped.
            var completedAt = Now.AddHours(2);
            var completed = await reviewsStore.CompleteAsync(draft.ReviewId, completedAt);

            Assert.NotNull(completed);
            Assert.Equal(ReviewStatus.Completed, completed!.Status);
            Assert.Equal(completedAt, completed.CompletedAt);

            // The persisted row reflects the completed state (upsert, same ReviewId).
            var reloaded = await reviewsStore.GetByIdAsync(draft.ReviewId);
            Assert.NotNull(reloaded);
            Assert.Equal(ReviewStatus.Completed, reloaded!.Status);
            Assert.Equal(completedAt, reloaded.CompletedAt);
            Assert.Equal(draft.ReviewId, reloaded.ReviewId);
        }

        // ── 5. The review snapshot round-trips the reviewed outcome (traceability) ──
        [Fact]
        public async Task CoachReview_SnapshotPreservesReviewedOutcome()
        {
            var reviewsStore = new CaseReviewsStore(new InMemoryCaseReviewsRepository());
            var assembler = new CaseReviewAssembler();
            var outcome = MakeOutcome(hasEnoughData: true);

            var review = assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Recovery, Now);
            await reviewsStore.SaveAsync(review);

            var reloaded = await reviewsStore.GetByIdAsync(review.ReviewId);
            Assert.NotNull(reloaded);
            var restored = System.Text.Json.JsonSerializer
                .Deserialize<OutcomeProfile>(reloaded!.OutcomeSnapshotJson);
            Assert.NotNull(restored);
            Assert.Equal(UserId, restored!.UserId);
            Assert.True(restored.HasEnoughData);
        }

        // ── 6. Completing an already-completed review is idempotent (no second stamp) ─
        [Fact]
        public async Task CoachReview_CompleteTwice_KeepsFirstCompletionTime()
        {
            var reviewsStore = new CaseReviewsStore(new InMemoryCaseReviewsRepository());
            var assembler = new CaseReviewAssembler();
            var review = assembler.Build(MakeOutcome(true), PeriodStart, PeriodEnd, ReviewType.Monthly, Now);
            await reviewsStore.SaveAsync(review);

            var firstCompletion = Now.AddHours(1);
            var firstCompleted = await reviewsStore.CompleteAsync(review.ReviewId, firstCompletion);
            // A second completion at a different time must not re-stamp an already-completed review.
            var secondCompleted = await reviewsStore.CompleteAsync(review.ReviewId, Now.AddHours(5));

            Assert.NotNull(firstCompleted);
            Assert.NotNull(secondCompleted);
            Assert.Equal(firstCompletion, secondCompleted!.CompletedAt);
        }

        // ── 7. Completing a non-existent review returns null, writes nothing ────────
        [Fact]
        public async Task CoachReview_CompleteUnknownId_ReturnsNull()
        {
            var reviewsStore = new CaseReviewsStore(new InMemoryCaseReviewsRepository());

            var result = await reviewsStore.CompleteAsync(Guid.NewGuid(), Now);

            Assert.Null(result);
        }

        // ── 8. Audit query date-window scopes a coach's review session ──────────────
        [Fact]
        public async Task AuditTrail_DateWindow_ScopesReviewSession()
        {
            var store = new AuditTrailStore(new InMemoryAuditTrailRepository());

            // One in-window review action, one out-of-window recommendation.
            await store.AppendAsync(MakeEvent(AuditEntityType.ReviewAction, Now));
            await store.AppendAsync(MakeEvent(AuditEntityType.Recommendation, Now.AddDays(10)));

            var window = await store.QueryAsync(
                UserId, from: Now.AddHours(-1), to: Now.AddHours(1));

            var ev = Assert.Single(window);
            Assert.Equal(AuditEntityType.ReviewAction, ev.EntityType);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        private static AuditEvent MakeEvent(AuditEntityType type, DateTime occurredAt) => new()
        {
            AuditId = Guid.NewGuid(),
            UserId = UserId,
            OccurredAt = occurredAt,
            EntityType = type,
            EntityId = $"entity-{type}",
            ActorRole = "Coach",
            ReasonCode = $"{type}_RECORDED",
        };

        private static OutcomeProfile MakeOutcome(bool hasEnoughData) => new()
        {
            UserId = UserId,
            GeneratedAt = Now,
            HasEnoughData = hasEnoughData,
        };
    }
}
