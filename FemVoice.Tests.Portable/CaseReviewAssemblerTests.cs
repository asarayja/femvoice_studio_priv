using System;
using System.Text.Json;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// W0-A8 (CaseReview Assembler) — verifies <see cref="CaseReviewAssembler"/>.
    ///
    /// House style: no mocking frameworks — real classes only. All dates are fixed constants.
    /// The assembler is pure (no I/O), so every test runs without infrastructure.
    /// </summary>
    public class CaseReviewAssemblerTests
    {
        private static readonly DateTime FixedNow =
            new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime PeriodStart =
            new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime PeriodEnd =
            new(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);

        private readonly CaseReviewAssembler _assembler = new();

        // ── 1. Period frame is preserved exactly ─────────────────────────────────
        [Fact]
        public void Build_CorrectPeriodFrame_PeriodStartAndEndRoundTrip()
        {
            var outcome = MakeOutcome(userId: 1);

            var review = _assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Monthly, FixedNow);

            Assert.Equal(PeriodStart, review.PeriodStart);
            Assert.Equal(PeriodEnd,   review.PeriodEnd);
        }

        // ── 2. UserId is copied from outcome ─────────────────────────────────────
        [Fact]
        public void Build_UserId_CopiedFromOutcome()
        {
            var outcome = MakeOutcome(userId: 42);

            var review = _assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Goal, FixedNow);

            Assert.Equal(42, review.UserId);
        }

        // ── 3. ReviewType is preserved ───────────────────────────────────────────
        [Theory]
        [InlineData(ReviewType.Monthly)]
        [InlineData(ReviewType.Goal)]
        [InlineData(ReviewType.Progress)]
        [InlineData(ReviewType.Recovery)]
        public void Build_ReviewType_Preserved(ReviewType type)
        {
            var outcome = MakeOutcome(userId: 1);

            var review = _assembler.Build(outcome, PeriodStart, PeriodEnd, type, FixedNow);

            Assert.Equal(type, review.ReviewType);
        }

        // ── 4. Status is Draft on creation ───────────────────────────────────────
        [Fact]
        public void Build_Status_IsDraftOnCreation()
        {
            var outcome = MakeOutcome(userId: 1);

            var review = _assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Monthly, FixedNow);

            Assert.Equal(ReviewStatus.Draft, review.Status);
        }

        // ── 5. CompletedAt is null on creation ────────────────────────────────────
        [Fact]
        public void Build_CompletedAt_IsNullOnCreation()
        {
            var outcome = MakeOutcome(userId: 1);

            var review = _assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Monthly, FixedNow);

            Assert.Null(review.CompletedAt);
        }

        // ── 6. CreatedAt is stamped from now parameter ────────────────────────────
        [Fact]
        public void Build_CreatedAt_IsNowParameter()
        {
            var outcome = MakeOutcome(userId: 1);

            var review = _assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Monthly, FixedNow);

            Assert.Equal(FixedNow, review.CreatedAt);
        }

        // ── 7. ReviewId is a non-empty Guid ──────────────────────────────────────
        [Fact]
        public void Build_ReviewId_IsNewNonEmptyGuid()
        {
            var outcome = MakeOutcome(userId: 1);

            var review = _assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Monthly, FixedNow);

            Assert.NotEqual(Guid.Empty, review.ReviewId);
        }

        // ── 8. Two calls produce different ReviewIds ──────────────────────────────
        [Fact]
        public void Build_CalledTwice_ProducesDifferentReviewIds()
        {
            var outcome = MakeOutcome(userId: 1);

            var r1 = _assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Monthly, FixedNow);
            var r2 = _assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Monthly, FixedNow);

            Assert.NotEqual(r1.ReviewId, r2.ReviewId);
        }

        // ── 9. OutcomeSnapshotJson round-trips UserId and HasEnoughData ───────────
        [Fact]
        public void Build_OutcomeSnapshotJson_RoundTripsTopLevelFields()
        {
            var outcome = MakeOutcome(userId: 7, hasEnoughData: true);

            var review = _assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Monthly, FixedNow);

            Assert.False(string.IsNullOrWhiteSpace(review.OutcomeSnapshotJson));
            var restored = JsonSerializer.Deserialize<OutcomeProfile>(review.OutcomeSnapshotJson);
            Assert.NotNull(restored);
            Assert.Equal(7, restored!.UserId);
            Assert.True(restored.HasEnoughData);
        }

        // ── 10. OutcomeSnapshotJson round-trips GeneratedAt ───────────────────────
        [Fact]
        public void Build_OutcomeSnapshotJson_RoundTripsGeneratedAt()
        {
            var generatedAt = new DateTime(2026, 5, 31, 18, 0, 0, DateTimeKind.Utc);
            var outcome = MakeOutcome(userId: 1, generatedAt: generatedAt);

            var review = _assembler.Build(outcome, PeriodStart, PeriodEnd, ReviewType.Monthly, FixedNow);

            var restored = JsonSerializer.Deserialize<OutcomeProfile>(review.OutcomeSnapshotJson);
            Assert.NotNull(restored);
            Assert.Equal(generatedAt, restored!.GeneratedAt);
        }

        // ── 11. Null outcome throws ArgumentNullException ─────────────────────────
        [Fact]
        public void Build_NullOutcome_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _assembler.Build(null!, PeriodStart, PeriodEnd, ReviewType.Monthly, FixedNow));
        }

        // ── 12. PeriodEnd before PeriodStart throws ArgumentException ─────────────
        [Fact]
        public void Build_PeriodEndBeforePeriodStart_ThrowsArgumentException()
        {
            var outcome = MakeOutcome(userId: 1);
            var before = PeriodStart.AddDays(-1);

            Assert.Throws<ArgumentException>(() =>
                _assembler.Build(outcome, PeriodStart, before, ReviewType.Monthly, FixedNow));
        }

        // ── 13. PeriodEnd equal to PeriodStart is valid (point-in-time review) ────
        [Fact]
        public void Build_PeriodEndEqualsPeriodStart_IsValid()
        {
            var outcome = MakeOutcome(userId: 1);

            var review = _assembler.Build(outcome, PeriodStart, PeriodStart, ReviewType.Progress, FixedNow);

            Assert.Equal(PeriodStart, review.PeriodStart);
            Assert.Equal(PeriodStart, review.PeriodEnd);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static OutcomeProfile MakeOutcome(
            int userId = 1,
            bool hasEnoughData = false,
            DateTime? generatedAt = null)
        {
            return new OutcomeProfile
            {
                UserId        = userId,
                GeneratedAt   = generatedAt ?? FixedNow,
                HasEnoughData = hasEnoughData,
            };
        }
    }
}
