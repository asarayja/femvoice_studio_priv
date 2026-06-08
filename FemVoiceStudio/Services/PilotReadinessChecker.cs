using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// The dependencies a <see cref="PilotReadinessChecker"/> validates (Sprint E, Agent 12 —
    /// Pilot Study Readiness). This is a lightweight, fully injectable context so the checker
    /// stays a READ-ONLY harness that depends on no App.Services singletons: tests build it
    /// from in-memory fakes, production builds it from the real SQLite-backed stores.
    ///
    /// <para>All five persistence facades are required. The four pure/utility engines
    /// (<see cref="ResearchAnonymizer"/>, <see cref="ReportAssembler"/>, <see cref="ExportWriter"/>,
    /// <see cref="ManualOverrideEngine"/>) default to fresh instances when not supplied, since
    /// they carry no state and never touch a store.</para>
    /// </summary>
    public sealed class PilotReadinessContext
    {
        public PilotReadinessContext(
            OutcomeProfileStore outcomeProfiles,
            AuditTrailStore auditTrail,
            ManualOverridesStore manualOverrides,
            ClinicalNotesStore clinicalNotes,
            CaseReviewsStore caseReviews,
            ResearchAnonymizer? anonymizer = null,
            ReportAssembler? reportAssembler = null,
            ExportWriter? exportWriter = null,
            ManualOverrideEngine? overrideEngine = null)
        {
            OutcomeProfiles = outcomeProfiles ?? throw new ArgumentNullException(nameof(outcomeProfiles));
            AuditTrail = auditTrail ?? throw new ArgumentNullException(nameof(auditTrail));
            ManualOverrides = manualOverrides ?? throw new ArgumentNullException(nameof(manualOverrides));
            ClinicalNotes = clinicalNotes ?? throw new ArgumentNullException(nameof(clinicalNotes));
            CaseReviews = caseReviews ?? throw new ArgumentNullException(nameof(caseReviews));

            Anonymizer = anonymizer ?? new ResearchAnonymizer();
            ReportAssembler = reportAssembler ?? new ReportAssembler();
            ExportWriter = exportWriter ?? new ExportWriter();
            OverrideEngine = overrideEngine ?? new ManualOverrideEngine();
        }

        public OutcomeProfileStore OutcomeProfiles { get; }
        public AuditTrailStore AuditTrail { get; }
        public ManualOverridesStore ManualOverrides { get; }
        public ClinicalNotesStore ClinicalNotes { get; }
        public CaseReviewsStore CaseReviews { get; }
        public ResearchAnonymizer Anonymizer { get; }
        public ReportAssembler ReportAssembler { get; }
        public ExportWriter ExportWriter { get; }
        public ManualOverrideEngine OverrideEngine { get; }
    }

    /// <summary>
    /// The outcome of a single pilot-readiness check.
    /// </summary>
    /// <param name="Name">Stable identifier of the check (e.g. "StoresConstructible").</param>
    /// <param name="Passed">True when the check passed.</param>
    /// <param name="Detail">Human-readable detail; a blocker explanation when failed.</param>
    public sealed record PilotReadinessCheck(string Name, bool Passed, string Detail);

    /// <summary>
    /// The aggregate pilot-readiness report (Sprint E, Agent 12). Carries one entry per check,
    /// a single roll-up <see cref="IsPilotReady"/> flag, and the list of human-readable blockers
    /// for any failed check.
    ///
    /// <para>This report is DESCRIPTIVE only — it asserts that the professional/research surface
    /// is operational and that the safety-override invariant holds. It never itself gates,
    /// blocks, or weakens any Safety/Health/Recovery authority.</para>
    /// </summary>
    public sealed record PilotReadinessReport
    {
        /// <summary>One result per executed check, in execution order.</summary>
        public IReadOnlyList<PilotReadinessCheck> Checks { get; init; } =
            Array.Empty<PilotReadinessCheck>();

        /// <summary>True only when EVERY check passed.</summary>
        public bool IsPilotReady => Checks.Count > 0 && Checks.All(c => c.Passed);

        /// <summary>Human-readable blockers for every failed check. Empty when ready.</summary>
        public IReadOnlyList<string> Blockers =>
            Checks.Where(c => !c.Passed).Select(c => $"{c.Name}: {c.Detail}").ToList();

        // ── Individual check accessors (convenience for tests / dashboards) ──────────

        /// <summary>Whether all five stores are constructible and their schema is ready.</summary>
        public bool StoresReady => Passed("StoresConstructible");

        /// <summary>Whether the audit trail is append-only (a duplicate append yields two rows).</summary>
        public bool AuditAppendOnly => Passed("AuditAppendOnly");

        /// <summary>Whether the research anonymizer produced PII-free output.</summary>
        public bool AnonymizerClean => Passed("AnonymizerClean");

        /// <summary>Whether export produced non-empty content for every format.</summary>
        public bool ExportNonEmpty => Passed("ExportNonEmpty");

        /// <summary>Whether outcome tracking round-tripped through the store.</summary>
        public bool OutcomeTrackingWorks => Passed("OutcomeTracking");

        /// <summary>Whether the safety-override invariant held (override never raises a target).</summary>
        public bool OverrideInvariantHolds => Passed("OverrideInvariant");

        private bool Passed(string name) =>
            Checks.FirstOrDefault(c => c.Name == name)?.Passed ?? false;
    }

    /// <summary>
    /// READ-ONLY pilot-study readiness harness (Sprint E, Agent 12). Exercises the
    /// professional / research surface end-to-end against the supplied
    /// <see cref="PilotReadinessContext"/> and reports whether each capability is operational.
    ///
    /// <para>The six checks are:</para>
    /// <list type="number">
    ///   <item><description>all five stores are constructible and their schema is ready
    ///   (proved by a tiny write/read round-trip through each facade);</description></item>
    ///   <item><description>the audit trail is APPEND-ONLY (a duplicate append yields two rows);</description></item>
    ///   <item><description>the research anonymizer emits PII-free output (no integer UserId,
    ///   no device name, no free text, no time-of-day);</description></item>
    ///   <item><description>export produces non-empty content for PDF, CSV, and JSON;</description></item>
    ///   <item><description>outcome tracking round-trips through the OutcomeProfileStore;</description></item>
    ///   <item><description>the safety-override invariant holds — an aggressive professional
    ///   intent under a blocked gate is never persisted less conservatively than baseline.</description></item>
    /// </list>
    ///
    /// <para>The harness only ever WRITES probe rows it created itself and READS them back; it
    /// never mutates real clinical data and never overrides any Safety/Health/Recovery gate.
    /// Every check is wrapped so a single failure produces a blocker rather than throwing,
    /// letting the report surface ALL problems in one pass.</para>
    /// </summary>
    public sealed class PilotReadinessChecker
    {
        // A fixed, deterministic probe user id well outside any realistic local user range, so
        // the harness's own probe rows can never collide with real clinical data.
        private const int ProbeUserId = -987_654;

        /// <summary>
        /// Runs every readiness check against <paramref name="context"/> and returns the
        /// aggregate <see cref="PilotReadinessReport"/>. <paramref name="now"/> is supplied
        /// explicitly so the harness is fully deterministic (no DateTime.UtcNow).
        /// </summary>
        public async Task<PilotReadinessReport> CheckAsync(
            PilotReadinessContext context,
            DateTime now,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var checks = new List<PilotReadinessCheck>
            {
                await CheckStoresConstructibleAsync(context, now, cancellationToken).ConfigureAwait(false),
                await CheckAuditAppendOnlyAsync(context, now, cancellationToken).ConfigureAwait(false),
                CheckAnonymizerClean(context, now),
                CheckExportNonEmpty(context, now),
                await CheckOutcomeTrackingAsync(context, now, cancellationToken).ConfigureAwait(false),
                CheckOverrideInvariant(context, now),
            };

            return new PilotReadinessReport { Checks = checks };
        }

        // ── (1) Stores constructible + schema ready ─────────────────────────────────

        private static async Task<PilotReadinessCheck> CheckStoresConstructibleAsync(
            PilotReadinessContext c, DateTime now, CancellationToken ct)
        {
            return await GuardedAsync("StoresConstructible", async () =>
            {
                // A write/read round-trip through every facade proves the store is
                // constructed AND its schema is queryable (EnsureSchema already ran in each
                // SQLite ctor; the in-memory fakes prove the same surface without I/O).

                // OutcomeProfileStore
                var outcome = MakeProbeOutcome(now);
                var outcomeId = await c.OutcomeProfiles.SaveSnapshotAsync(outcome, ct).ConfigureAwait(false);
                var fetchedOutcome = await c.OutcomeProfiles.GetSnapshotAsync(outcomeId, ct).ConfigureAwait(false);
                if (fetchedOutcome is null)
                    return (false, "OutcomeProfileStore did not round-trip a snapshot.");

                // AuditTrailStore
                await c.AuditTrail.AppendAsync(MakeProbeAudit(now), ct).ConfigureAwait(false);
                var auditRows = await c.AuditTrail.QueryAsync(ProbeUserId, cancellationToken: ct).ConfigureAwait(false);
                if (auditRows.Count == 0)
                    return (false, "AuditTrailStore did not round-trip an event.");

                // ManualOverridesStore
                await c.ManualOverrides.AppendAsync(MakeProbeOverrideLog(now), ct).ConfigureAwait(false);
                var overrideRows = await c.ManualOverrides
                    .GetOverridesAsync(ProbeUserId, now.AddDays(-1), now.AddDays(1), ct).ConfigureAwait(false);
                if (overrideRows.Count == 0)
                    return (false, "ManualOverridesStore did not round-trip a log row.");

                // ClinicalNotesStore
                await c.ClinicalNotes.SaveNoteAsync(MakeProbeNote(now), ct).ConfigureAwait(false);
                var notes = await c.ClinicalNotes
                    .GetNotesAsync(ProbeUserId, ClinicalNoteType.Coach, now.AddDays(-1), now.AddDays(1), ct)
                    .ConfigureAwait(false);
                if (notes.Count == 0)
                    return (false, "ClinicalNotesStore did not round-trip a note.");

                // CaseReviewsStore
                var review = MakeProbeReview(outcome, now);
                await c.CaseReviews.SaveAsync(review, ct).ConfigureAwait(false);
                var fetchedReview = await c.CaseReviews.GetByIdAsync(review.ReviewId, ct).ConfigureAwait(false);
                if (fetchedReview is null)
                    return (false, "CaseReviewsStore did not round-trip a review.");

                return (true, "All five stores constructible and schema-ready.");
            }).ConfigureAwait(false);
        }

        // ── (2) Audit append-only ───────────────────────────────────────────────────

        private static async Task<PilotReadinessCheck> CheckAuditAppendOnlyAsync(
            PilotReadinessContext c, DateTime now, CancellationToken ct)
        {
            return await GuardedAsync("AuditAppendOnly", async () =>
            {
                var sharedId = Guid.NewGuid();

                var first = MakeProbeAudit(now) with { AuditId = sharedId, ReasonCode = "PROBE_FIRST" };
                var second = MakeProbeAudit(now.AddSeconds(1)) with { AuditId = sharedId, ReasonCode = "PROBE_SECOND" };

                await c.AuditTrail.AppendAsync(first, ct).ConfigureAwait(false);
                await c.AuditTrail.AppendAsync(second, ct).ConfigureAwait(false);

                var rows = await c.AuditTrail
                    .QueryAsync(ProbeUserId, AuditEntityType.Override, cancellationToken: ct)
                    .ConfigureAwait(false);

                var dupes = rows.Count(r => r.AuditId == sharedId);
                if (dupes != 2)
                    return (false,
                        $"Append-only violated: a duplicate AuditId produced {dupes} row(s), expected 2.");

                return (true, "Audit trail is append-only (duplicate AuditId produced two rows).");
            }).ConfigureAwait(false);
        }

        // ── (3) Anonymizer clean ────────────────────────────────────────────────────

        private static PilotReadinessCheck CheckAnonymizerClean(PilotReadinessContext c, DateTime now)
        {
            return Guarded("AnonymizerClean", () =>
            {
                const string token = "pilot-probe-token";
                const string deviceName = "Probe USB Mic";
                const string subjective = "probe subjective free text";
                const string clinical = "probe clinical free text";

                var raw = new RawResearchRow
                {
                    UserId = ProbeUserId,
                    Timestamp = now.AddHours(13).AddMinutes(37),
                    CompositeVoiceScore = 70.0,
                    RecoveryScore0to100 = 60.0,
                    ExerciseId = 3,
                    ExerciseEffectiveness = 55.0,
                    PlateauActive = true,
                    Calibration = new Audio.MicrophoneCalibrationProfile
                    {
                        DeviceName = deviceName,
                        SignalToNoiseDb = 24.5
                    },
                    SubjectiveNote = subjective,
                    ClinicalNoteBody = clinical
                };

                var rows = c.Anonymizer.Anonymize(new[] { raw }, token);
                if (rows.Count != 1)
                    return (false, $"Anonymizer produced {rows.Count} rows, expected 1.");

                var json = JsonSerializer.Serialize(rows[0]);

                // No PII vector may survive in the serialized output graph.
                if (json.Contains(deviceName, StringComparison.Ordinal))
                    return (false, "Device name leaked into anonymized output.");
                if (json.Contains(subjective, StringComparison.Ordinal) ||
                    json.Contains(clinical, StringComparison.Ordinal))
                    return (false, "Free-text note leaked into anonymized output.");
                if (json.Contains("\"UserId\"", StringComparison.Ordinal) ||
                    json.Contains(ProbeUserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.Ordinal))
                    return (false, "Integer UserId leaked into anonymized output.");

                // Token survives; time-of-day is bucketed to midnight UTC.
                if (rows[0].ParticipantToken != token)
                    return (false, "Participant token was not applied to the anonymized row.");
                if (rows[0].DayBucket.TimeOfDay != TimeSpan.Zero)
                    return (false, "Time-of-day survived anonymization (not bucketed to midnight).");

                return (true, "Anonymizer output is PII-free (token applied, day-bucketed).");
            });
        }

        // ── (4) Export non-empty for each format ────────────────────────────────────

        private static PilotReadinessCheck CheckExportNonEmpty(PilotReadinessContext c, DateTime now)
        {
            return Guarded("ExportNonEmpty", () =>
            {
                var outcome = MakeProbeOutcome(now);
                var report = c.ReportAssembler.BuildOutcomeReport(
                    outcome, now.AddDays(-30), now, now);

                foreach (var format in new[] { ExportFormat.Json, ExportFormat.Csv, ExportFormat.Pdf })
                {
                    using var stream = new MemoryStream();
                    c.ExportWriter.Write(report, format, stream);
                    if (stream.Length == 0)
                        return (false, $"Export produced empty content for {format}.");
                }

                return (true, "Export produced non-empty content for JSON, CSV, and PDF.");
            });
        }

        // ── (5) Outcome tracking ────────────────────────────────────────────────────

        private static async Task<PilotReadinessCheck> CheckOutcomeTrackingAsync(
            PilotReadinessContext c, DateTime now, CancellationToken ct)
        {
            return await GuardedAsync("OutcomeTracking", async () =>
            {
                // Stamp this probe strictly AFTER any store-check probe so GetLatestForUserAsync
                // unambiguously returns THIS snapshot (the store check writes a probe at `now`
                // for the same probe user; a later GeneratedAt avoids any latest-tie ambiguity).
                var outcome = MakeProbeOutcome(now.AddMinutes(1)) with { HasEnoughData = true };
                var outcomeId = await c.OutcomeProfiles.SaveSnapshotAsync(outcome, ct).ConfigureAwait(false);

                var byId = await c.OutcomeProfiles.GetSnapshotAsync(outcomeId, ct).ConfigureAwait(false);
                if (byId is null)
                    return (false, "Outcome snapshot could not be read back by id.");
                if (byId.UserId != ProbeUserId)
                    return (false, "Outcome snapshot round-trip lost the UserId.");
                if (!byId.HasEnoughData)
                    return (false, "Outcome snapshot round-trip lost the HasEnoughData flag.");

                var latest = await c.OutcomeProfiles.GetLatestForUserAsync(ProbeUserId, ct).ConfigureAwait(false);
                if (latest is null)
                    return (false, "Latest-for-user lookup returned no outcome snapshot.");

                return (true, "Outcome tracking round-trips by id and latest-for-user.");
            }).ConfigureAwait(false);
        }

        // ── (6) Safety-override invariant ───────────────────────────────────────────

        private static PilotReadinessCheck CheckOverrideInvariant(PilotReadinessContext c, DateTime now)
        {
            return Guarded("OverrideInvariant", () =>
            {
                // A representative factory baseline (the clinical reference floor).
                var baseline = new ExerciseTargetProfile
                {
                    UsesResonance = true,
                    UsesStability = true,
                    TargetResonanceMin = 0.55,
                    TargetResonanceMax = 0.90,
                    StabilityThreshold = 0.50,
                    RequiredHoldSeconds = 3.0
                };

                // A professional tries to RAISE every requirement above baseline, under a
                // BLOCKED gate at Urgent severity — the worst-case for the invariant.
                var intended = new ExerciseTargetProfile
                {
                    UsesResonance = true,
                    UsesStability = true,
                    TargetResonanceMin = 0.85,
                    TargetResonanceMax = 0.99,
                    StabilityThreshold = 0.95,
                    RequiredHoldSeconds = 12.0
                };

                var request = new ManualOverrideRequest
                {
                    OverrideKind = ManualOverrideKind.ExerciseReco,
                    UserId = ProbeUserId,
                    ExerciseId = 3,
                    IntendedProfile = intended,
                    ReasonCode = "PILOT_PROBE",
                    ActorRole = "Clinician",
                    RequestedAt = now
                };

                var result = c.OverrideEngine.Evaluate(
                    request, baseline, gateBlocked: true,
                    recoverySeverity: RecoverySeverity.Urgent, style: VoiceStyleGoal.Feminine);

                if (!result.WasApplied || result.AppliedProfile is null)
                    return (false, "Override engine did not apply a profile for an exercise override.");

                var applied = result.AppliedProfile;

                // INVARIANT: under a blocked gate the override may only HOLD or LOWER a target,
                // never raise one above baseline.
                if (applied.TargetResonanceMin > baseline.TargetResonanceMin + 1e-9 ||
                    applied.StabilityThreshold > baseline.StabilityThreshold + 1e-9 ||
                    applied.RequiredHoldSeconds > baseline.RequiredHoldSeconds + 1e-9 ||
                    applied.TargetResonanceMax > baseline.TargetResonanceMax + 1e-9)
                {
                    return (false,
                        "Override invariant VIOLATED: a requirement was raised above baseline under a blocked gate.");
                }

                // The persisted profile must also be internally consistent.
                applied.Validate();

                return (true, "Override invariant holds (no requirement raised above baseline under a blocked gate).");
            });
        }

        // ── Probe-data builders ─────────────────────────────────────────────────────

        private static OutcomeProfile MakeProbeOutcome(DateTime now) => new()
        {
            UserId = ProbeUserId,
            GeneratedAt = now,
            HasEnoughData = false
        };

        private static AuditEvent MakeProbeAudit(DateTime now) => new()
        {
            AuditId = Guid.NewGuid(),
            UserId = ProbeUserId,
            OccurredAt = now,
            EntityType = AuditEntityType.Override,
            EntityId = "pilot-probe",
            ActorRole = "System",
            ReasonCode = "PILOT_PROBE",
            BeforeJson = null,
            AfterJson = null
        };

        private static ManualOverrideLogEntry MakeProbeOverrideLog(DateTime now) => new()
        {
            ManualOverrideId = Guid.NewGuid(),
            AuditId = Guid.NewGuid(),
            UserId = ProbeUserId,
            OverrideKind = ManualOverrideKind.ExerciseReco,
            ExerciseId = 3,
            RequestedAt = now,
            ActorRole = "Clinician",
            ReasonCode = "PILOT_PROBE",
            WasApplied = true,
            WasClamped = false,
            BlockedReasonCode = null
        };

        private static ClinicalNote MakeProbeNote(DateTime now) => new()
        {
            NoteId = Guid.NewGuid(),
            UserId = ProbeUserId,
            NoteType = ClinicalNoteType.Coach,
            AuthorRole = "Coach",
            CreatedAt = now,
            BodyText = "pilot probe note"
        };

        private static CaseReview MakeProbeReview(OutcomeProfile outcome, DateTime now) => new()
        {
            ReviewId = Guid.NewGuid(),
            UserId = ProbeUserId,
            ReviewType = ReviewType.Progress,
            PeriodStart = now.AddDays(-30),
            PeriodEnd = now,
            OutcomeSnapshotJson = JsonSerializer.Serialize(outcome),
            Status = ReviewStatus.Draft,
            CreatedAt = now,
            CompletedAt = null
        };

        // ── Guard helpers — turn any thrown exception into a failed check ───────────

        private static PilotReadinessCheck Guarded(string name, Func<(bool Passed, string Detail)> body)
        {
            try
            {
                var (passed, detail) = body();
                return new PilotReadinessCheck(name, passed, detail);
            }
            catch (Exception ex)
            {
                return new PilotReadinessCheck(name, false, $"Threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static async Task<PilotReadinessCheck> GuardedAsync(
            string name, Func<Task<(bool Passed, string Detail)>> body)
        {
            try
            {
                var (passed, detail) = await body().ConfigureAwait(false);
                return new PilotReadinessCheck(name, passed, detail);
            }
            catch (Exception ex)
            {
                return new PilotReadinessCheck(name, false, $"Threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
