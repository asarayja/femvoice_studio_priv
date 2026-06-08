using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Agent 5 (Research Mode) — verifies the PII-stripping <see cref="ResearchAnonymizer"/>
    /// and the <see cref="ParticipantTokenProvider"/>.
    ///
    /// House style: no mocking frameworks — real classes + injected temp directory / fixed
    /// token factory so every assertion is deterministic and never wall-clock dependent.
    ///
    /// The PRIMARY contract under test is the privacy invariant: no integer UserId, no device
    /// name, no free text, and no time-of-day may survive anonymization.
    /// </summary>
    public class ResearchAnonymizerTests
    {
        private const string Token = "11111111-2222-3333-4444-555555555555";

        // ── 1. No PII leaks: device name, free text, UserId, time-of-day all gone ─────
        [Fact]
        public void Anonymize_StripsAllPii_NoDeviceNameFreeTextUserIdOrClockTime()
        {
            var anonymizer = new ResearchAnonymizer();
            var deviceName = "Jane's Blue Yeti USB";
            var subjective = "Felt great today, met my therapist Dr. Smith.";
            var clinical = "Patient reports improvement; phone 555-0100.";

            var raw = new RawResearchRow
            {
                UserId = 42,
                Timestamp = new DateTime(2026, 6, 7, 14, 37, 19, DateTimeKind.Utc),
                CompositeVoiceScore = 71.0,
                RecoveryScore0to100 = 60.0,
                ExerciseId = 3,
                ExerciseEffectiveness = 55.0,
                PlateauActive = true,
                Calibration = new MicrophoneCalibrationProfile
                {
                    DeviceName = deviceName,
                    SignalToNoiseDb = 24.5
                },
                SubjectiveNote = subjective,
                ClinicalNoteBody = clinical
            };

            var rows = anonymizer.Anonymize(new[] { raw }, Token);
            var row = Assert.Single(rows);

            // Token replaced the integer id.
            Assert.Equal(Token, row.ParticipantToken);

            // Serialize the whole output and assert NOTHING identifying appears anywhere.
            var json = JsonSerializer.Serialize(row);
            Assert.DoesNotContain(deviceName, json);
            Assert.DoesNotContain("Blue Yeti", json);
            Assert.DoesNotContain(subjective, json);
            Assert.DoesNotContain("Dr. Smith", json);
            Assert.DoesNotContain(clinical, json);
            Assert.DoesNotContain("555-0100", json);
            Assert.DoesNotContain("Jane", json);
            // The integer UserId 42 must not appear as a value anywhere.
            Assert.DoesNotContain("\"UserId\"", json);
            Assert.DoesNotContain(":42", json);

            // Time-of-day (14:37:19) is gone — bucket is midnight UTC.
            Assert.Equal(new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Utc), row.DayBucket);
            Assert.Equal(0, row.DayBucket.Hour);
            Assert.Equal(0, row.DayBucket.Minute);
            Assert.Equal(0, row.DayBucket.Second);

            // Non-identifying acoustic number survives; device name does not.
            Assert.True(row.HasCalibration);
            Assert.Equal(24.5, row.CalibrationSignalToNoiseDb, 6);
        }

        // ── 2. Output type has no PII-capable field at all (by construction) ──────────
        [Fact]
        public void ResearchParticipantRow_HasNoPiiCapableProperty()
        {
            var props = typeof(ResearchParticipantRow).GetProperties().Select(p => p.Name).ToList();

            Assert.DoesNotContain("UserId", props);
            Assert.DoesNotContain("DeviceName", props);
            Assert.DoesNotContain("SubjectiveNote", props);
            Assert.DoesNotContain("ClinicalNoteBody", props);
            Assert.DoesNotContain("Timestamp", props);
            // The only identifier is the opaque token.
            Assert.Contains("ParticipantToken", props);
            Assert.Contains("DayBucket", props);
        }

        // ── 3. Timestamp bucketing: same day, different times ⇒ same bucket ───────────
        [Fact]
        public void Anonymize_BucketsTimestampsToDayGranularity()
        {
            var anonymizer = new ResearchAnonymizer();
            var morning = Raw(new DateTime(2026, 3, 15, 6, 12, 0, DateTimeKind.Utc));
            var evening = Raw(new DateTime(2026, 3, 15, 23, 58, 0, DateTimeKind.Utc));
            var nextDay = Raw(new DateTime(2026, 3, 16, 0, 1, 0, DateTimeKind.Utc));

            var rows = anonymizer.Anonymize(new[] { morning, evening, nextDay }, Token);

            Assert.Equal(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), rows[0].DayBucket);
            Assert.Equal(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), rows[1].DayBucket);
            // Same calendar day ⇒ identical bucket.
            Assert.Equal(rows[0].DayBucket, rows[1].DayBucket);
            // One minute later, next day ⇒ different bucket.
            Assert.Equal(new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), rows[2].DayBucket);
            Assert.NotEqual(rows[1].DayBucket, rows[2].DayBucket);
        }

        // ── 4. ToDayBucketUtc normalises kind and yields UTC midnight ────────────────
        [Fact]
        public void ToDayBucketUtc_AlwaysReturnsUtcMidnight()
        {
            var utc = ResearchAnonymizer.ToDayBucketUtc(
                new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            Assert.Equal(DateTimeKind.Utc, utc.Kind);
            Assert.Equal(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), utc);

            // Unspecified is assumed UTC (no surprise shift).
            var unspecified = ResearchAnonymizer.ToDayBucketUtc(
                new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified));
            Assert.Equal(DateTimeKind.Utc, unspecified.Kind);
            Assert.Equal(2, unspecified.Day);
        }

        // ── 5. No calibration ⇒ HasCalibration false, SNR 0, still no leak ───────────
        [Fact]
        public void Anonymize_NoCalibration_HasCalibrationFalse()
        {
            var anonymizer = new ResearchAnonymizer();
            var raw = Raw(new DateTime(2026, 6, 7, 9, 0, 0, DateTimeKind.Utc));
            // Raw() leaves Calibration null and notes null.

            var row = Assert.Single(anonymizer.Anonymize(new[] { raw }, Token));
            Assert.False(row.HasCalibration);
            Assert.Equal(0.0, row.CalibrationSignalToNoiseDb, 6);
        }

        // ── 6. Null / empty input handled, blank token rejected ──────────────────────
        [Fact]
        public void Anonymize_NullRows_ReturnsEmpty_BlankToken_Throws()
        {
            var anonymizer = new ResearchAnonymizer();
            Assert.Empty(anonymizer.Anonymize(null, Token));
            Assert.Empty(anonymizer.Anonymize(Array.Empty<RawResearchRow>(), Token));
            Assert.Throws<ArgumentException>(() =>
                anonymizer.Anonymize(new[] { Raw(DateTime.UtcNow) }, "  "));
        }

        // ── 7. Numeric measurements pass through unchanged ───────────────────────────
        [Fact]
        public void Anonymize_PreservesNonIdentifyingMeasurements()
        {
            var anonymizer = new ResearchAnonymizer();
            var raw = new RawResearchRow
            {
                UserId = 9,
                Timestamp = new DateTime(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc),
                CompositeVoiceScore = 73.25,
                RecoveryScore0to100 = 81.5,
                ExerciseId = 11,
                ExerciseEffectiveness = 64.0,
                PlateauActive = false
            };

            var row = Assert.Single(anonymizer.Anonymize(new[] { raw }, Token));
            Assert.Equal(73.25, row.CompositeVoiceScore, 6);
            Assert.Equal(81.5, row.RecoveryScore0to100, 6);
            Assert.Equal(11, row.ExerciseId);
            Assert.Equal(64.0, row.ExerciseEffectiveness!.Value, 6);
            Assert.False(row.PlateauActive);
        }

        // ── ParticipantTokenProvider ─────────────────────────────────────────────────

        // ── 8. First call mints a token; second call reuses it (idempotent) ─────────
        [Fact]
        public void TokenProvider_GetOrCreate_IsStableAcrossInstances()
        {
            using var dir = new TempDir();
            var mintCount = 0;
            Func<string> factory = () => { mintCount++; return "fixed-token-abc"; };

            var first = new ParticipantTokenProvider(dir.Path, factory).GetOrCreateToken();
            // Fresh instance over the same directory must read the persisted token, not re-mint.
            var second = new ParticipantTokenProvider(dir.Path, factory).GetOrCreateToken();

            Assert.Equal("fixed-token-abc", first);
            Assert.Equal(first, second);
            Assert.Equal(1, mintCount); // minted exactly once
        }

        // ── 9. Token is persisted to a local JSON file (never the DB) ────────────────
        [Fact]
        public void TokenProvider_PersistsTokenToLocalJsonFile()
        {
            using var dir = new TempDir();
            var token = new ParticipantTokenProvider(dir.Path, () => "json-token-xyz").GetOrCreateToken();

            var file = Path.Combine(dir.Path, "participant-token.json");
            Assert.True(File.Exists(file));
            var contents = File.ReadAllText(file);
            Assert.Contains("json-token-xyz", contents);
            Assert.Equal("json-token-xyz", token);
        }

        // ── 10. Corrupt token file ⇒ re-mint, never throw ───────────────────────────
        [Fact]
        public void TokenProvider_CorruptFile_ReMintsWithoutThrowing()
        {
            using var dir = new TempDir();
            Directory.CreateDirectory(dir.Path);
            File.WriteAllText(Path.Combine(dir.Path, "participant-token.json"), "{not valid json");

            var token = new ParticipantTokenProvider(dir.Path, () => "recovered-token").GetOrCreateToken();
            Assert.Equal("recovered-token", token);
        }

        // ── 11. Default factory mints a parseable UUID (no fixed value injected) ─────
        [Fact]
        public void TokenProvider_DefaultFactory_MintsParseableGuid()
        {
            using var dir = new TempDir();
            var token = new ParticipantTokenProvider(dir.Path).GetOrCreateToken();
            Assert.True(Guid.TryParse(token, out _));
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static RawResearchRow Raw(DateTime timestamp) => new()
        {
            UserId = 1,
            Timestamp = timestamp,
            CompositeVoiceScore = 50.0,
            RecoveryScore0to100 = 50.0
        };

        /// <summary>Disposable temp directory (no mocks, real disk I/O).</summary>
        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"femvoice-research-{Guid.NewGuid():N}");
            }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
        }
    }
}
