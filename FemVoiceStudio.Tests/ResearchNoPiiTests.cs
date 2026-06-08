using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Agent 12 — SCENARIO "Research Export".
    ///
    /// Proves that the <see cref="ResearchAnonymizer"/> + <see cref="ResearchAggregator"/>
    /// output graph contains NO personally identifiable data, by walking the whole record
    /// graph recursively and asserting:
    /// <list type="bullet">
    ///   <item><description>no integer <c>UserId</c> property exists anywhere;</description></item>
    ///   <item><description>no device name (e.g. a <see cref="MicrophoneCalibrationProfile.DeviceName"/>)
    ///   value survives;</description></item>
    ///   <item><description>no free text (subjective note / clinical-note body) survives;</description></item>
    ///   <item><description>every timestamp is day-bucketed to midnight UTC;</description></item>
    ///   <item><description>the opaque <c>ParticipantToken</c> is used as the only identifier.</description></item>
    /// </list>
    /// And that at N=1 <see cref="ResearchDataset.HasSufficientCohort"/> is false while the
    /// single-participant longitudinal series is STILL computed and emitted.
    ///
    /// House style: no mocking frameworks — real anonymizer/aggregator, fixed inputs.
    /// </summary>
    public class ResearchNoPiiTests
    {
        private const string Token = "research-participant-token-001";
        private const int RawUserId = 4242;
        private const string DeviceName = "Jane's Blue Yeti USB";
        private const string SubjectiveText = "Felt great today, met Dr. Smith at 3pm.";
        private const string ClinicalText = "Patient reports improvement; phone 555-0100.";

        private static readonly DateTime Day0 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── 1. N=1 dataset graph carries NO PII (recursive scan) ────────────────────
        [Fact]
        public void ResearchExport_SingleParticipant_GraphHasNoPii()
        {
            var dataset = BuildSingleParticipantDataset();

            var findings = new List<string>();
            PiiScanner.Scan(dataset, "ResearchDataset", findings);

            Assert.True(findings.Count == 0,
                "PII vectors found in research export graph:\n" + string.Join("\n", findings));
        }

        // ── 2. No forbidden string VALUE appears anywhere in the graph ──────────────
        [Fact]
        public void ResearchExport_NoForbiddenStringValueSurvives()
        {
            var dataset = BuildSingleParticipantDataset();

            var strings = new List<string>();
            PiiScanner.CollectStrings(dataset, strings);

            foreach (var forbidden in new[] { DeviceName, "Blue Yeti", "Jane", SubjectiveText,
                                              "Dr. Smith", ClinicalText, "555-0100" })
            {
                Assert.DoesNotContain(strings,
                    s => s.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
            }

            // The integer UserId must not appear as a value anywhere either.
            Assert.DoesNotContain(strings,
                s => s.Contains(RawUserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal));
        }

        // ── 3. No property anywhere in the graph is named / typed like PII ──────────
        [Fact]
        public void ResearchExport_NoPiiCapablePropertyInGraph()
        {
            // Walk every reachable record TYPE and assert no PII-named property exists.
            var visited = new HashSet<Type>();
            var offenders = new List<string>();
            PiiScanner.ScanTypesForPiiProperties(typeof(ResearchDataset), visited, offenders);

            Assert.True(offenders.Count == 0,
                "PII-capable properties reachable from ResearchDataset:\n" + string.Join("\n", offenders));
        }

        // ── 4. Every timestamp in the graph is bucketed to midnight UTC ─────────────
        [Fact]
        public void ResearchExport_AllTimestampsAreDayBucketed()
        {
            var dataset = BuildSingleParticipantDataset();

            var timestamps = new List<DateTime>();
            PiiScanner.CollectDateTimes(dataset, timestamps);

            // At least the per-row DayBuckets must be present, and all must be midnight.
            Assert.NotEmpty(timestamps);
            Assert.All(timestamps, t => Assert.Equal(TimeSpan.Zero, t.TimeOfDay));
        }

        // ── 5. The opaque token is the only identifier on every row ─────────────────
        [Fact]
        public void ResearchExport_ParticipantTokenIsTheOnlyIdentifier()
        {
            var dataset = BuildSingleParticipantDataset();

            Assert.NotEmpty(dataset.DayBucketedRows);
            Assert.All(dataset.DayBucketedRows, r => Assert.Equal(Token, r.ParticipantToken));
        }

        // ── 6. N=1: HasSufficientCohort false, but the series is STILL computed ──────
        [Fact]
        public void ResearchExport_AtN1_InsufficientCohortButSeriesEmitted()
        {
            var dataset = BuildSingleParticipantDataset();

            // Cohort generalisation withheld at N=1.
            Assert.Equal(1, dataset.ParticipantCount);
            Assert.False(dataset.HasSufficientCohort);
            Assert.False(string.IsNullOrWhiteSpace(dataset.VolumeCaveatNote));
            Assert.Contains("N=1", dataset.VolumeCaveatNote);

            // ...yet the single-participant longitudinal series is fully present.
            Assert.Equal(2, dataset.DayBucketedRows.Count);

            // ...and group aggregates are still computed (just flagged non-generalisable).
            var ex = Assert.Single(dataset.AggregateMetrics.ExerciseEffectiveness);
            Assert.Equal(3, ex.ExerciseId);
            Assert.Equal(52.5, ex.MeanEffectiveness, 6); // (50 + 55) / 2
            Assert.Equal(1, ex.ContributingParticipantCount);
        }

        // ── 7. Anonymizer drops device name but keeps non-identifying SNR ───────────
        [Fact]
        public void ResearchExport_DropsDeviceName_KeepsNonIdentifyingAcoustics()
        {
            var anonymizer = new ResearchAnonymizer();
            var raw = MakeRaw(Day0, exerciseId: 3, eff: 50);
            var rows = anonymizer.Anonymize(new[] { raw }, Token);

            var row = Assert.Single(rows);
            Assert.True(row.HasCalibration);
            Assert.Equal(24.5, row.CalibrationSignalToNoiseDb, 6);
            // The device name is not carried anywhere on the row.
            var strings = new List<string>();
            PiiScanner.CollectStrings(row, strings);
            Assert.DoesNotContain(strings, s => s.Contains("Yeti", StringComparison.OrdinalIgnoreCase));
        }

        // ── Dataset builder ─────────────────────────────────────────────────────────

        private static ResearchDataset BuildSingleParticipantDataset()
        {
            var anonymizer = new ResearchAnonymizer();
            var aggregator = new ResearchAggregator();

            // One participant, two PII-bearing raw rows on consecutive days.
            var raw = new[]
            {
                MakeRaw(Day0,            exerciseId: 3, eff: 50, plateau: false),
                MakeRaw(Day0.AddDays(1), exerciseId: 3, eff: 55, plateau: true),
            };

            var series = anonymizer.Anonymize(raw, Token);
            return aggregator.BuildDataset(
                new[] { (IReadOnlyList<ResearchParticipantRow>)series.ToList() }, Token);
        }

        private static RawResearchRow MakeRaw(
            DateTime timestamp, int exerciseId, double eff, bool plateau = false) => new()
        {
            UserId = RawUserId,
            // Deliberately give a non-midnight time-of-day to prove it is bucketed away.
            Timestamp = timestamp.AddHours(14).AddMinutes(37),
            CompositeVoiceScore = 70.0,
            RecoveryScore0to100 = 60.0,
            ExerciseId = exerciseId,
            ExerciseEffectiveness = eff,
            PlateauActive = plateau,
            Calibration = new MicrophoneCalibrationProfile
            {
                DeviceName = DeviceName,
                SignalToNoiseDb = 24.5
            },
            SubjectiveNote = SubjectiveText,
            ClinicalNoteBody = ClinicalText
        };

        // ── Recursive PII scanner ───────────────────────────────────────────────────

        /// <summary>
        /// Reflection-based recursive scanner used to PROVE the research output graph carries
        /// no PII. It walks every reachable property of the dataset record graph, collects all
        /// string and DateTime values, and flags any property NAMED like a PII vector.
        /// </summary>
        private static class PiiScanner
        {
            // Property names that would represent a re-identification vector if they appeared.
            private static readonly HashSet<string> ForbiddenPropertyNames = new(StringComparer.OrdinalIgnoreCase)
            {
                "UserId", "DeviceName", "SubjectiveNote", "ClinicalNoteBody", "BodyText",
                "Timestamp", "DeviceId", "MachineName", "UserName", "Email",
            };

            /// <summary>Walks the live object graph and records any forbidden property carrying a value.</summary>
            public static void Scan(object? node, string path, List<string> findings)
            {
                if (node is null) return;
                var type = node.GetType();

                if (IsLeaf(type))
                    return;

                if (node is IEnumerable enumerable && type != typeof(string))
                {
                    var index = 0;
                    foreach (var item in enumerable)
                        Scan(item, $"{path}[{index++}]", findings);
                    return;
                }

                foreach (var prop in ReadableProperties(type))
                {
                    if (ForbiddenPropertyNames.Contains(prop.Name))
                        findings.Add($"{path}.{prop.Name} (forbidden PII property reachable in output graph)");

                    var value = SafeGet(prop, node);
                    Scan(value, $"{path}.{prop.Name}", findings);
                }
            }

            /// <summary>Collects every string value reachable in the graph.</summary>
            public static void CollectStrings(object? node, List<string> sink)
            {
                if (node is null) return;
                var type = node.GetType();

                if (node is string s) { sink.Add(s); return; }
                if (IsLeaf(type)) return;

                if (node is IEnumerable enumerable)
                {
                    foreach (var item in enumerable) CollectStrings(item, sink);
                    return;
                }

                foreach (var prop in ReadableProperties(type))
                    CollectStrings(SafeGet(prop, node), sink);
            }

            /// <summary>Collects every DateTime value reachable in the graph.</summary>
            public static void CollectDateTimes(object? node, List<DateTime> sink)
            {
                if (node is null) return;
                var type = node.GetType();

                if (node is DateTime dt) { sink.Add(dt); return; }
                if (IsLeaf(type)) return;

                if (node is IEnumerable enumerable && type != typeof(string))
                {
                    foreach (var item in enumerable) CollectDateTimes(item, sink);
                    return;
                }

                foreach (var prop in ReadableProperties(type))
                    CollectDateTimes(SafeGet(prop, node), sink);
            }

            /// <summary>
            /// Statically walks the reachable record TYPES (not values) and records any
            /// property whose name is a known PII vector — proving the SHAPE is PII-free.
            /// </summary>
            public static void ScanTypesForPiiProperties(Type type, HashSet<Type> visited, List<string> offenders)
            {
                if (!visited.Add(type)) return;
                if (IsLeaf(type)) return;

                if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
                {
                    foreach (var arg in type.GetGenericArguments())
                        ScanTypesForPiiProperties(arg, visited, offenders);
                    if (type.IsArray)
                        ScanTypesForPiiProperties(type.GetElementType()!, visited, offenders);
                    return;
                }

                foreach (var prop in ReadableProperties(type))
                {
                    if (ForbiddenPropertyNames.Contains(prop.Name))
                        offenders.Add($"{type.Name}.{prop.Name}");
                    ScanTypesForPiiProperties(prop.PropertyType, visited, offenders);
                }
            }

            // ── Reflection helpers ──────────────────────────────────────────────────

            private static IEnumerable<PropertyInfo> ReadableProperties(Type type) =>
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

            private static object? SafeGet(PropertyInfo prop, object node)
            {
                try { return prop.GetValue(node); }
                catch { return null; }
            }

            // A "leaf" is a type we never need to recurse into.
            private static bool IsLeaf(Type type) =>
                type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(Guid)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan)
                || (Nullable.GetUnderlyingType(type) is { } u && IsLeaf(u));
        }
    }
}
