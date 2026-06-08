using System;
using System.Text.Json;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Pure factory that assembles a <see cref="CaseReview"/> from an already-built
    /// <see cref="OutcomeProfile"/>.
    ///
    /// This class is PURE: it performs no I/O, calls no stores, and has no mutable state.
    /// All output is deterministic given the same inputs, which makes it directly
    /// unit-testable without any infrastructure.
    ///
    /// CLINICAL FRAMING: the assembled review is DESCRIPTIVE / EXPLANATORY only. It never
    /// overrides Safety &gt; Health &gt; Recovery gates. The <c>OutcomeSnapshotJson</c>
    /// serialises the profile opaquely — the assembler inspects only the top-level fields
    /// (<see cref="OutcomeProfile.UserId"/>, <see cref="OutcomeProfile.GeneratedAt"/>,
    /// <see cref="OutcomeProfile.HasEnoughData"/>) for framing; the rest is carried through
    /// unchanged.
    ///
    /// Every new review is created with <see cref="ReviewStatus.Draft"/>; the caller is
    /// responsible for persisting it via <see cref="CaseReviewsStore"/> and later
    /// transitioning it to <see cref="ReviewStatus.Completed"/>.
    /// </summary>
    public sealed class CaseReviewAssembler
    {
        /// <summary>
        /// Assembles a new <see cref="CaseReview"/> in <see cref="ReviewStatus.Draft"/>
        /// from an already-built <see cref="OutcomeProfile"/>.
        /// </summary>
        /// <param name="outcome">
        /// The fully assembled outcome profile for the user and period.
        /// The entire profile is serialised into <see cref="CaseReview.OutcomeSnapshotJson"/>.
        /// Only top-level fields are inspected here; sub-records are carried through opaquely.
        /// </param>
        /// <param name="periodStart">
        /// Inclusive start of the review period (UTC). Must be &lt;= <paramref name="periodEnd"/>.
        /// </param>
        /// <param name="periodEnd">
        /// Inclusive end of the review period (UTC). Must be &gt;= <paramref name="periodStart"/>.
        /// </param>
        /// <param name="type">Clinical context of the review.</param>
        /// <param name="now">
        /// Current UTC timestamp — used as <see cref="CaseReview.CreatedAt"/>. Pass a fixed
        /// value in tests for determinism.
        /// </param>
        /// <returns>
        /// A new <see cref="CaseReview"/> with a fresh <see cref="CaseReview.ReviewId"/>,
        /// <see cref="ReviewStatus.Draft"/> status, and a JSON snapshot of
        /// <paramref name="outcome"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="outcome"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="periodEnd"/> is before <paramref name="periodStart"/>.
        /// </exception>
        public CaseReview Build(
            OutcomeProfile outcome,
            DateTime periodStart,
            DateTime periodEnd,
            ReviewType type,
            DateTime now)
        {
            if (outcome is null) throw new ArgumentNullException(nameof(outcome));
            if (periodEnd < periodStart)
                throw new ArgumentException(
                    $"periodEnd ({periodEnd:o}) must be >= periodStart ({periodStart:o}).",
                    nameof(periodEnd));

            var snapshotJson = JsonSerializer.Serialize(outcome);

            return new CaseReview
            {
                ReviewId            = Guid.NewGuid(),
                UserId              = outcome.UserId,
                ReviewType          = type,
                PeriodStart         = periodStart,
                PeriodEnd           = periodEnd,
                OutcomeSnapshotJson = snapshotJson,
                Status              = ReviewStatus.Draft,
                CreatedAt           = now,
                CompletedAt         = null,
            };
        }
    }
}
