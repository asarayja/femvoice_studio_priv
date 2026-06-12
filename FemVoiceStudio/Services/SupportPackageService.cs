using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace FemVoiceStudio.Services
{
    public sealed class SupportPackageService
    {
        private const int CurrentPackageFormatVersion = 1;
        private readonly SupportPackagePaths _paths;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public SupportPackageService(SupportPackagePaths paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        public static SupportPackageService CreateDefault() => new(new SupportPackagePaths(
            DiagnosticsRoot: DiagnosticsNaming.PrimaryRoot,
            RuntimeDiagnosticsRoot: DiagnosticsNaming.RuntimeDirectory,
            SettingsPath: ThemeManager.SettingsPath,
            ExportRoot: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio",
                "SupportPackages")));

        public SupportPackageResult CreatePackage(SupportPackageOptions? options = null, DateTime? timestamp = null)
        {
            options ??= SupportPackageOptions.Default;

            try
            {
                Directory.CreateDirectory(_paths.ExportRoot);
                var createdAt = timestamp ?? DateTime.Now;
                var packagePath = Path.Combine(
                    _paths.ExportRoot,
                    $"FemVoiceStudio_SupportPackage_{createdAt:yyyyMMdd_HHmmss}.zip");

                var included = new List<string>();
                var excludedSensitive = new List<string>
                {
                    "professional free-text notes",
                    "clinical note bodies",
                    "personal notes",
                    "research exports with identifiable data",
                    "secrets",
                    "raw private user text"
                };

                using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    AddLatestIfExists(archive, _paths.DiagnosticsRoot, DiagnosticsNaming.EvidenceJson, "diagnostics/EVIDENCE.json", included);
                    AddLatestIfExists(archive, _paths.DiagnosticsRoot, DiagnosticsNaming.VerificationReport, "diagnostics/VERIFICATION_REPORT.md", included);
                    AddLatestIfExists(archive, _paths.DiagnosticsRoot, DiagnosticsNaming.AudioPipelineDiagnosticReport, "diagnostics/AUDIO_PIPELINE_DIAGNOSTIC_REPORT.md", included);
                    AddLatestIfExists(archive, _paths.DiagnosticsRoot, DiagnosticsNaming.ErrorsOnly, "diagnostics/ERRORS_ONLY.txt", included);
                    AddLatestIfExists(archive, _paths.DiagnosticsRoot, DiagnosticsNaming.ScreenshotChecklist, "diagnostics/SCREENSHOT_CHECKLIST.md", included);
                    AddLatestIfExists(archive, _paths.DiagnosticsRoot, DiagnosticsNaming.SessionSummary, "diagnostics/SESSION_SUMMARY.md", included);
                    AddLatestRuntimeLogIfExists(archive, included);

                    AddJson(archive, "app-version.json", new
                    {
                        AppVersion = typeof(SupportPackageService).Assembly.GetName().Version?.ToString() ?? "",
                        CreatedAtUtc = DateTime.UtcNow
                    }, included);

                    AddJson(archive, "system-summary.json", new
                    {
                        OS = Environment.OSVersion.ToString(),
                        Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                        ProcessorCount = Environment.ProcessorCount
                    }, included);

                    AddJson(archive, "privacy-summary.json", PrivacyConsentPolicy.Snapshot(), included);
                    AddJson(archive, "settings-summary.json", BuildSettingsSummaryNoThrow(), included);

                    if (options.IncludeProfessionalFreeText)
                    {
                        AddJson(archive, "professional-notes-included-warning.json", new
                        {
                            IncludedOnlyBecauseExplicitlySelected = true,
                            Warning = PrivacyConsentPolicy.Snapshot().ProfessionalNotesPrivacyWarning
                        }, included);
                    }

                    AddJson(archive, "manifest.json", new SupportPackageManifest(
                        AppId: "FemVoiceStudio",
                        PackageFormatVersion: CurrentPackageFormatVersion,
                        CreatedAtUtc: DateTime.UtcNow,
                        IncludedFiles: included.ToArray(),
                        ExcludedSensitiveFiles: excludedSensitive.ToArray(),
                        ProfessionalFreeTextIncluded: options.IncludeProfessionalFreeText), included: null);
                }

                return SupportPackageResult.Ok(packagePath, included.ToArray(), excludedSensitive.ToArray());
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("SupportPackageService.CreatePackage", _paths.ExportRoot, ex);
                return SupportPackageResult.Fail(
                    SafeFailureMessages.For(SafeFailureKind.DiagnosticsExport),
                    SafeFailureMessages.TechnicalReason(ex));
            }
        }

        private Dictionary<string, object> BuildSettingsSummaryNoThrow()
        {
            try
            {
                var result = SettingsMigrationService.LoadOrRecover(_paths.SettingsPath, "SupportPackageService.SettingsSummary");
                var summary = PrivacyConsentPolicy.BuildSettingsSummary(result.Settings)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                foreach (var key in summary.Keys.ToArray())
                {
                    if (PrivacyConsentPolicy.IsSensitiveSettingsKey(key))
                        summary.Remove(key);
                }

                return summary;
            }
            catch
            {
                return new Dictionary<string, object>
                {
                    ["SettingsSummaryStatus"] = "NOT_AVAILABLE"
                };
            }
        }

        private void AddLatestRuntimeLogIfExists(ZipArchive archive, List<string> included)
        {
            if (!Directory.Exists(_paths.RuntimeDiagnosticsRoot))
                return;

            var latest = Directory
                .EnumerateFiles(_paths.RuntimeDiagnosticsRoot, "RUNTIME_*.txt", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (latest is null)
                return;

            AddFile(archive, latest, "runtime/RUNTIME_LOG.txt", included);
        }

        private static void AddLatestIfExists(
            ZipArchive archive,
            string root,
            string fileName,
            string entryName,
            List<string> included)
        {
            if (!Directory.Exists(root))
                return;

            var direct = Path.Combine(root, fileName);
            if (File.Exists(direct))
            {
                AddFile(archive, direct, entryName, included);
                return;
            }

            var latest = Directory
                .EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (latest is not null)
                AddFile(archive, latest, entryName, included);
        }

        private static void AddFile(ZipArchive archive, string sourcePath, string entryName, List<string> included)
        {
            if (!File.Exists(sourcePath))
                return;

            archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Optimal);
            included.Add(entryName);
        }

        private static void AddJson(ZipArchive archive, string entryName, object value, List<string>? included)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            JsonSerializer.Serialize(stream, value, JsonOptions);
            included?.Add(entryName);
        }
    }

    public sealed record SupportPackagePaths(
        string DiagnosticsRoot,
        string RuntimeDiagnosticsRoot,
        string SettingsPath,
        string ExportRoot);

    public sealed record SupportPackageOptions(bool IncludeProfessionalFreeText)
    {
        public static SupportPackageOptions Default { get; } = new(false);
    }

    public sealed record SupportPackageManifest(
        string AppId,
        int PackageFormatVersion,
        DateTime CreatedAtUtc,
        string[] IncludedFiles,
        string[] ExcludedSensitiveFiles,
        bool ProfessionalFreeTextIncluded);

    public sealed record SupportPackageResult(
        bool Success,
        string? ExportPath,
        string ExportStatus,
        string SafeUserMessage,
        string[] IncludedFiles,
        string[] ExcludedSensitiveFiles,
        string[] Errors)
    {
        public static SupportPackageResult Ok(
            string exportPath,
            string[] includedFiles,
            string[] excludedSensitiveFiles) =>
            new(true, exportPath, "PASS", string.Empty, includedFiles, excludedSensitiveFiles, Array.Empty<string>());

        public static SupportPackageResult Fail(string safeUserMessage, string error) =>
            new(false, null, "FAIL", safeUserMessage, Array.Empty<string>(), Array.Empty<string>(), new[] { error });
    }
}
