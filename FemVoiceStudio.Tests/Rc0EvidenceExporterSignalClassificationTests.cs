using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public sealed class Rc0EvidenceExporterSignalClassificationTests
    {
        [Fact]
        public void ValidVoiceEvidence_DoesNotEndAsSignalLevelCollapse()
        {
            var evidence = ValidSessionEvidence();
            var audio = ValidAudioSnapshot() with
            {
                FailureClassification = AudioFailureClassification.SIGNAL_LEVEL_COLLAPSES,
                LevelCollapsed = true,
                RmsLevel = 0.002,
                RmsP90 = 0.018,
                CurrentSilenceThreshold = 0.006
            };

            var classification = Rc0EvidenceExporter.ResolveSessionFailureClassification(evidence, audio);
            var result = Rc0EvidenceExporter.ResolveResultForDiagnostics(evidence, audio);

            Assert.Equal(AudioFailureClassification.UNKNOWN, classification);
            Assert.Equal("PASS", result);
        }

        [Fact]
        public void StartupOrTailSilence_AfterValidVoiceEvidence_DoesNotDominateSessionResult()
        {
            var evidence = ValidSessionEvidence() with
            {
                PitchSamplesCount = 83,
                PitchDetectorCalledCount = 150,
                ResonanceRealSampleCount = 165,
                ResonanceAcceptedCount = 550
            };
            var audio = ValidAudioSnapshot() with
            {
                DataAvailableCount = 742,
                FailureClassification = AudioFailureClassification.SIGNAL_LEVEL_COLLAPSES,
                VoicedFramePercent = 12,
                RmsLevel = 0.001,
                RmsP90 = 0.002,
                CurrentSilenceThreshold = 0.006
            };

            Assert.Equal(AudioFailureClassification.UNKNOWN,
                Rc0EvidenceExporter.ResolveSessionFailureClassification(evidence, audio));
            Assert.Equal("PASS", Rc0EvidenceExporter.ResolveResultForDiagnostics(evidence, audio));
        }

        [Fact]
        public void SilenceGateTail_AfterValidVoiceEvidence_DoesNotDowngradeSession()
        {
            var evidence = ValidSessionEvidence();
            var audio = ValidAudioSnapshot() with
            {
                FailureClassification = AudioFailureClassification.SILENCE_GATE_REJECTS_SIGNAL,
                SilenceDetected = true,
                IsSignalRejected = true,
                SignalRejectedReason = "BELOW_RMS_THRESHOLD"
            };

            Assert.Equal(AudioFailureClassification.UNKNOWN,
                Rc0EvidenceExporter.ResolveSessionFailureClassification(evidence, audio));
            Assert.Equal("PASS", Rc0EvidenceExporter.ResolveResultForDiagnostics(evidence, audio));
        }

        [Fact]
        public void TrulyCollapsedInput_WithoutPitchOrResonance_RemainsSignalLevelCollapse()
        {
            var evidence = ValidSessionEvidence() with
            {
                PitchSamplesCount = 0,
                ResonanceRealSampleCount = 0,
                ResonanceAcceptedCount = 0,
                GraphUpdateCount = 0
            };
            var audio = ValidAudioSnapshot() with
            {
                FailureClassification = AudioFailureClassification.SIGNAL_LEVEL_COLLAPSES,
                VoicedFramePercent = 0,
                RmsP90 = 0.001,
                CurrentSilenceThreshold = 0.006
            };

            Assert.Equal(AudioFailureClassification.SIGNAL_LEVEL_COLLAPSES,
                Rc0EvidenceExporter.ResolveSessionFailureClassification(evidence, audio));
        }

        [Fact]
        public void Export_IncludesReportVerificationMetadata()
        {
            var timestamp = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
            var evidence = ValidSessionEvidence() with
            {
                ClinicalReportStatus = "PASS",
                CoachReportStatus = "NOT_GENERATED",
                OutcomeReportStatus = "FAIL",
                TimelineReportStatus = "PASS",
                ReportVerificationErrors = new[] { "Outcome: PDF output did not contain a valid PDF header." },
                GeneratedReportPaths = new[] { "C:\\Reports\\clinical.pdf", "C:\\Reports\\timeline.json" },
                VerifiedReportCount = 2,
                ReportVerificationTimestamp = timestamp
            };

            var folder = Path.Combine(Path.GetTempPath(), "FemVoiceStudio.Tests", "Rc0Evidence", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                typeof(Rc0EvidenceExporter)
                    .GetMethod("WriteJson", BindingFlags.NonPublic | BindingFlags.Static)!
                    .Invoke(null, new object[] { folder, evidence, ValidAudioSnapshot(), "PASS" });

                var jsonPath = Path.Combine(folder, DiagnosticsNaming.EvidenceJson);
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                var verification = doc.RootElement.GetProperty("ReportVerification");

                Assert.True(doc.RootElement.GetProperty("ReportsGenerated").GetProperty("Clinical").GetBoolean());
                Assert.False(doc.RootElement.GetProperty("ReportsGenerated").GetProperty("Coach").GetBoolean());
                Assert.Equal("PASS", verification.GetProperty("ClinicalReportStatus").GetString());
                Assert.Equal("NOT_GENERATED", verification.GetProperty("CoachReportStatus").GetString());
                Assert.Equal("FAIL", verification.GetProperty("OutcomeReportStatus").GetString());
                Assert.Equal(2, verification.GetProperty("VerifiedReportCount").GetInt32());
                Assert.Equal(timestamp, verification.GetProperty("ReportVerificationTimestamp").GetDateTime());
                Assert.Equal(2, verification.GetProperty("GeneratedReportPaths").GetArrayLength());
                Assert.NotEmpty(verification.GetProperty("ReportVerificationErrors").EnumerateArray());
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        }

        private static Rc0EvidenceExporter.SessionEvidence ValidSessionEvidence() => new()
        {
            SessionId = 87,
            ExerciseId = 101,
            ExerciseName = "Grunnleggende humming",
            Language = "nb",
            StartTime = DateTime.UtcNow.AddMinutes(-1),
            EndTime = DateTime.UtcNow,
            Duration = TimeSpan.FromMinutes(1),
            CompletionStatus = "COMPLETED",
            Score = 82,
            ScoreSource = "SESSION_ANALYTICS",
            PitchDetectorCalledCount = 121,
            PitchSamplesCount = 110,
            ResonanceSamplesCount = 216,
            ResonanceEngineCalledCount = 238,
            ResonanceAcceptedCount = 216,
            ResonanceRejectedCount = 22,
            ResonanceRealSampleCount = 216,
            GraphUpdateCount = 110,
            GuidanceItemCount = 4,
            SmartCoachGenerated = true,
            VoiceHealthEvaluated = true,
            AnalyticsWritten = true,
            PersistenceSaved = true,
            PersistenceReadBack = true,
            PersistenceReadBackStatus = "PASS"
        };

        private static AudioCaptureDiagnosticsSnapshot ValidAudioSnapshot() => new()
        {
            IsRecording = true,
            DataAvailableCount = 601,
            BytesReceived = 120200,
            SamplesReceived = 60100,
            FailureClassification = AudioFailureClassification.UNKNOWN,
            VoicedFramePercent = 42,
            RmsMean = 0.010,
            RmsMedian = 0.008,
            RmsP90 = 0.022,
            CurrentSilenceThreshold = 0.006
        };
    }
}
