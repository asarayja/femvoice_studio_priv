using System;
using System.Collections.Generic;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// PURE (no I/O) transform that turns PII-BEARING <see cref="RawResearchRow"/> input into
    /// PII-FREE <see cref="ResearchParticipantRow"/> output for Research Mode (Sprint E,
    /// Agent 5).
    ///
    /// <para><b>PRIVACY INVARIANT — what is removed.</b> For every row the anonymizer:</para>
    /// <list type="bullet">
    ///   <item><description>replaces the integer <see cref="RawResearchRow.UserId"/> with the
    ///   opaque participant token (the UserId never reaches the output);</description></item>
    ///   <item><description>truncates <see cref="RawResearchRow.Timestamp"/> to its calendar
    ///   day at midnight UTC, discarding the time-of-day fingerprint;</description></item>
    ///   <item><description>drops the microphone
    ///   <see cref="Audio.MicrophoneCalibrationProfile.DeviceName"/> entirely, keeping only
    ///   non-identifying acoustic numbers (signal-to-noise);</description></item>
    ///   <item><description>drops ALL free text — subjective notes and clinical-note bodies —
    ///   because free text can never be guaranteed PII-free.</description></item>
    /// </list>
    ///
    /// <para>The output record graph has no field capable of carrying a name, a device, a raw
    /// note, or a clock time, so the transform is PII-free by construction, not merely by
    /// convention.</para>
    ///
    /// <para>This is a REPORTING-tier utility. It never influences any Safety/Health/Recovery
    /// decision.</para>
    /// </summary>
    public sealed class ResearchAnonymizer
    {
        /// <summary>
        /// Anonymizes a batch of raw rows into PII-free rows tagged with
        /// <paramref name="participantToken"/>.
        /// </summary>
        /// <param name="rows">Raw, PII-bearing input rows. Null is treated as empty.</param>
        /// <param name="participantToken">
        /// Opaque participant token (from <see cref="ParticipantTokenProvider"/>) that
        /// replaces the integer UserId on every output row.
        /// </param>
        /// <returns>One PII-free <see cref="ResearchParticipantRow"/> per input row.</returns>
        public IReadOnlyList<ResearchParticipantRow> Anonymize(
            IReadOnlyList<RawResearchRow>? rows, string participantToken)
        {
            if (string.IsNullOrWhiteSpace(participantToken))
                throw new ArgumentException("A participant token is required.", nameof(participantToken));

            var result = new List<ResearchParticipantRow>();
            if (rows == null) return result;

            foreach (var row in rows)
            {
                if (row == null) continue;
                result.Add(AnonymizeRow(row, participantToken));
            }
            return result;
        }

        /// <summary>
        /// Anonymizes a single raw row. The integer UserId, device name, all free text, and
        /// the time-of-day are dropped; only the participant token, the day bucket, and the
        /// non-identifying numeric measurements survive.
        /// </summary>
        public ResearchParticipantRow AnonymizeRow(RawResearchRow row, string participantToken)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrWhiteSpace(participantToken))
                throw new ArgumentException("A participant token is required.", nameof(participantToken));

            // Calibration: keep only non-identifying acoustic numbers; DeviceName is dropped.
            var hasCalibration = row.Calibration != null;
            var snr = row.Calibration?.SignalToNoiseDb ?? 0.0;

            return new ResearchParticipantRow
            {
                // UserId is intentionally NOT copied — replaced by the opaque token.
                ParticipantToken = participantToken,

                // Time-of-day discarded: truncate to the calendar day at midnight UTC.
                DayBucket = ToDayBucketUtc(row.Timestamp),

                CompositeVoiceScore = row.CompositeVoiceScore,
                RecoveryScore0to100 = row.RecoveryScore0to100,
                ExerciseId = row.ExerciseId,
                ExerciseEffectiveness = row.ExerciseEffectiveness,
                PlateauActive = row.PlateauActive,

                HasCalibration = hasCalibration,
                CalibrationSignalToNoiseDb = snr

                // row.SubjectiveNote, row.ClinicalNoteBody, and Calibration.DeviceName are
                // deliberately NOT mapped — free text and device names are PII and dropped.
            };
        }

        /// <summary>
        /// Truncates a timestamp to its calendar day at midnight, normalised to UTC. The
        /// time-of-day (a re-identification fingerprint) is discarded. The returned value has
        /// <see cref="DateTimeKind.Utc"/> so day buckets compare consistently across rows.
        /// </summary>
        public static DateTime ToDayBucketUtc(DateTime timestamp)
        {
            // Normalise to UTC first so a local 23:30 and a UTC 23:30 do not land on different
            // days inconsistently. Unspecified-kind values are assumed to already be UTC.
            var utc = timestamp.Kind switch
            {
                DateTimeKind.Local => timestamp.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
                _ => timestamp
            };
            return new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
