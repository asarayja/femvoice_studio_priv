using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public sealed class PrivacyConsentReadinessTests
    {
        [Fact]
        public void PrivacyPolicy_DisablesCloudUploadAndHiddenTelemetry_ByDefault()
        {
            var snapshot = PrivacyConsentPolicy.Snapshot();

            Assert.False(snapshot.CloudUploadEnabledByDefault);
            Assert.False(snapshot.HiddenTelemetryEnabled);
            Assert.True(snapshot.DiagnosticsExportRequiresUserAction);
            Assert.True(snapshot.ResearchExportAnonymizedByDefault);
            Assert.True(snapshot.ProfessionalNotesExcludedFromResearchByDefault);
            Assert.True(snapshot.ProfessionalNotesExcludedFromSupportPackageByDefault);
            Assert.Contains("FemVoiceStudio", snapshot.LocalDataFolder);
            Assert.Contains("Diagnostics", snapshot.DiagnosticsFolder);
        }

        [Fact]
        public void ResearchExport_DropsPii_FreeText_AndMicrophoneDeviceName()
        {
            var raw = new RawResearchRow
            {
                UserId = 42,
                Timestamp = new DateTime(2026, 6, 12, 17, 45, 31, DateTimeKind.Local),
                CompositeVoiceScore = 72,
                RecoveryScore0to100 = 81,
                ExerciseId = 3,
                ExerciseEffectiveness = 74,
                PlateauActive = true,
                Calibration = new MicrophoneCalibrationProfile
                {
                    DeviceName = "Asarayja USB Microphone",
                    SignalToNoiseDb = 21.5
                },
                SubjectiveNote = "private note with name Asarayja",
                ClinicalNoteBody = "professional free text"
            };

            var row = new ResearchAnonymizer().AnonymizeRow(raw, "participant-token");
            var json = JsonSerializer.Serialize(row);

            Assert.Contains("participant-token", json);
            Assert.DoesNotContain("42", json);
            Assert.DoesNotContain("Asarayja", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("USB Microphone", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private note", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("professional free text", json, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, row.DayBucket.Hour);
            Assert.Equal(0, row.DayBucket.Minute);
            Assert.True(row.HasCalibration);
            Assert.Equal(21.5, row.CalibrationSignalToNoiseDb);
        }

        [Fact]
        public void SupportPackage_Default_ExcludesProfessionalNotesAndSensitiveFreeText()
        {
            using var temp = new TempPrivacyFolder();
            temp.WriteDiagnostics();
            File.WriteAllText(temp.Paths.SettingsPath, """
            {
              "SettingsVersion": 2,
              "Language": "nb",
              "Theme": "Dark",
              "SecretToken": "do-not-export"
            }
            """);

            var result = new SupportPackageService(temp.Paths)
                .CreatePackage(SupportPackageOptions.Default, new DateTime(2026, 6, 12, 12, 0, 0));

            Assert.True(result.Success);
            Assert.NotNull(result.ExportPath);
            Assert.Contains("professional free-text notes", result.ExcludedSensitiveFiles);
            Assert.DoesNotContain(result.IncludedFiles, f => f.Contains("ClinicalNotes", StringComparison.OrdinalIgnoreCase));

            using var archive = ZipFile.OpenRead(result.ExportPath!);
            Assert.Null(archive.GetEntry("professional-notes-included-warning.json"));
            var names = archive.Entries.Select(e => e.FullName).ToArray();
            Assert.Contains("manifest.json", names);
            Assert.Contains("settings-summary.json", names);

            var allText = string.Join(
                "\n",
                archive.Entries.Select(ReadEntryText));
            Assert.DoesNotContain("do-not-export", allText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("professional note body", allText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SupportPackage_ProfessionalNotesMarkerOnlyWhenExplicitlySelected()
        {
            using var temp = new TempPrivacyFolder();
            temp.WriteDiagnostics();

            var result = new SupportPackageService(temp.Paths)
                .CreatePackage(new SupportPackageOptions(IncludeProfessionalFreeText: true));

            Assert.True(result.Success);
            using var archive = ZipFile.OpenRead(result.ExportPath!);
            Assert.NotNull(archive.GetEntry("professional-notes-included-warning.json"));
        }

        [Fact]
        public void SettingsSummary_ExcludesSecretsAndTokens()
        {
            var settings = new AppSettings
            {
                SettingsVersion = 2,
                Language = "en-US",
                Theme = AppTheme.Dark,
                HearOwnVoice = true,
                Debug = new DebugSettings
                {
                    EnableRc0Diagnostics = true
                }
            };

            var summary = PrivacyConsentPolicy.BuildSettingsSummary(settings, includeDebugFlags: true);

            Assert.Contains(nameof(AppSettings.Language), summary.Keys);
            Assert.DoesNotContain(summary.Keys, PrivacyConsentPolicy.IsSensitiveSettingsKey);
            Assert.DoesNotContain("ParticipantToken", summary.Keys);
            Assert.DoesNotContain("Secret", string.Join(",", summary.Keys), StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadEntryText(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private sealed class TempPrivacyFolder : IDisposable
        {
            public TempPrivacyFolder()
            {
                Root = Path.Combine(Path.GetTempPath(), "FemVoicePrivacyTests_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
                DiagnosticsRoot = Path.Combine(Root, "diagnostics");
                RuntimeRoot = Path.Combine(Root, "runtime");
                ExportRoot = Path.Combine(Root, "support");
                Directory.CreateDirectory(DiagnosticsRoot);
                Directory.CreateDirectory(RuntimeRoot);
                Directory.CreateDirectory(ExportRoot);
                Paths = new SupportPackagePaths(
                    DiagnosticsRoot,
                    RuntimeRoot,
                    Path.Combine(Root, "settings.json"),
                    ExportRoot);
            }

            public string Root { get; }
            public string DiagnosticsRoot { get; }
            public string RuntimeRoot { get; }
            public string ExportRoot { get; }
            public SupportPackagePaths Paths { get; }

            public void WriteDiagnostics()
            {
                File.WriteAllText(Path.Combine(DiagnosticsRoot, DiagnosticsNaming.EvidenceJson), "{\"Errors\":[]}");
                File.WriteAllText(Path.Combine(DiagnosticsRoot, DiagnosticsNaming.VerificationReport), "verification ok");
                File.WriteAllText(Path.Combine(DiagnosticsRoot, DiagnosticsNaming.AudioPipelineDiagnosticReport), "audio ok");
                File.WriteAllText(Path.Combine(DiagnosticsRoot, DiagnosticsNaming.ErrorsOnly), "No errors captured.");
                File.WriteAllText(Path.Combine(DiagnosticsRoot, DiagnosticsNaming.ScreenshotChecklist), "checklist");
                File.WriteAllText(Path.Combine(DiagnosticsRoot, DiagnosticsNaming.SessionSummary), "summary");
                File.WriteAllText(Path.Combine(RuntimeRoot, "RUNTIME_2026-06-12_120000.txt"), "runtime log");
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root))
                        Directory.Delete(Root, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }
    }
}
