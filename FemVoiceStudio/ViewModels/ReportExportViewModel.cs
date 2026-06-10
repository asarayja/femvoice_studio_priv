using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// Report Export view-model (Sprint E, Agent 3+10).
    ///
    /// Lets the user choose a report type (Clinical/Coach/Outcome/Timeline) and export
    /// format (Pdf/Csv/Json), then Generate builds an <see cref="OutcomeProfile"/> via
    /// <see cref="OutcomeProfileBuilder"/> and routes through <see cref="ReportAssembler"/>
    /// into <see cref="ExportWriter"/>.
    ///
    /// PARAMETERLESS CTOR: resolves all dependencies via App.Services; null-safe so the
    /// class loads without crashing at design-time or in test contexts where DI is absent.
    ///
    /// TEST CTOR: accepts all dependencies explicitly — no App.Services, no WPF, no disk I/O
    /// needed for unit tests.
    ///
    /// Dialog injection: in production the Generate command opens a
    /// Microsoft.Win32.SaveFileDialog. In tests, <see cref="FileSavePathOverride"/> bypasses
    /// the dialog so the MemoryStream/file path is injectable.
    /// </summary>
    public partial class ReportExportViewModel : ObservableObject
    {
        // ── Dependencies ──────────────────────────────────────────────────────────
        private readonly OutcomeProfileBuilder? _outcomeProfileBuilder;
        private readonly ReportAssembler? _reportAssembler;
        private readonly ExportWriter? _exportWriter;
        private readonly IDatabaseService? _database;
        private readonly IVoiceGoalProfileProvider? _goalProfileProvider;
        private readonly RecoveryIntelligenceService? _recoveryService;
        private readonly SessionAnalyticsStore? _analyticsStore;

        // ── Test seam: bypass SaveFileDialog by injecting the output path ─────────
        /// <summary>
        /// When non-null, Generate writes to this path instead of opening a dialog.
        /// Used by unit tests to avoid WPF/Win32.
        /// </summary>
        public string? FileSavePathOverride { get; set; }

        // ── Observable properties ─────────────────────────────────────────────────

        /// <summary>Selected report type (0=Clinical, 1=Coach, 2=Outcome, 3=Timeline).</summary>
        [ObservableProperty]
        private int _selectedReportTypeIndex;

        /// <summary>Selected export format (0=Pdf, 1=Csv, 2=Json).</summary>
        [ObservableProperty]
        private int _selectedFormatIndex;

        /// <summary>Feedback message shown after Generate completes (success or error).</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        /// <summary>True while Generate is running (blocks re-entry).</summary>
        [ObservableProperty]
        private bool _isGenerating;

        // ── Constructors ──────────────────────────────────────────────────────────

        /// <summary>
        /// Parameterless constructor — resolves from App.Services; null-safe.
        /// Used by WPF host at runtime.
        /// </summary>
        public ReportExportViewModel()
        {
            _outcomeProfileBuilder  = App.Services?.GetService(typeof(OutcomeProfileBuilder))  as OutcomeProfileBuilder;
            _reportAssembler        = App.Services?.GetService(typeof(ReportAssembler))         as ReportAssembler;
            _exportWriter           = App.Services?.GetService(typeof(ExportWriter))            as ExportWriter;
            _database               = App.Services?.GetService(typeof(IDatabaseService))        as IDatabaseService;
            _goalProfileProvider    = App.Services?.GetService(typeof(IVoiceGoalProfileProvider)) as IVoiceGoalProfileProvider;
            _recoveryService        = App.Services?.GetService(typeof(RecoveryIntelligenceService)) as RecoveryIntelligenceService;
            _analyticsStore         = App.Services?.GetService(typeof(SessionAnalyticsStore))   as SessionAnalyticsStore;
        }

        /// <summary>
        /// Test constructor — injects all dependencies directly; no WPF/DI required.
        /// </summary>
        public ReportExportViewModel(
            OutcomeProfileBuilder outcomeProfileBuilder,
            ReportAssembler reportAssembler,
            ExportWriter exportWriter,
            IDatabaseService database,
            IVoiceGoalProfileProvider? goalProfileProvider = null,
            RecoveryIntelligenceService? recoveryService = null,
            SessionAnalyticsStore? analyticsStore = null)
        {
            _outcomeProfileBuilder = outcomeProfileBuilder ?? throw new ArgumentNullException(nameof(outcomeProfileBuilder));
            _reportAssembler       = reportAssembler       ?? throw new ArgumentNullException(nameof(reportAssembler));
            _exportWriter          = exportWriter          ?? throw new ArgumentNullException(nameof(exportWriter));
            _database              = database              ?? throw new ArgumentNullException(nameof(database));
            _goalProfileProvider   = goalProfileProvider;
            _recoveryService       = recoveryService;
            _analyticsStore        = analyticsStore;
        }

        // ── Commands ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the selected report type, opens a SaveFileDialog (or uses
        /// <see cref="FileSavePathOverride"/> in tests), and writes via ExportWriter.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanGenerate))]
        private async Task GenerateAsync()
        {
            if (_outcomeProfileBuilder is null || _reportAssembler is null || _exportWriter is null || _database is null)
            {
                StatusMessage = LocalizationService.Instance.GetString("Report_StatusServicesUnavailable");
                return;
            }

            IsGenerating = true;
            StatusMessage = string.Empty;

            try
            {
                var now = DateTime.UtcNow;
                var periodStart = now.AddDays(-30);

                // 1) Build the outcome profile (best-effort; each source degrades gracefully).
                var recoveryService = _recoveryService ?? new RecoveryIntelligenceService(new RecoveryScorer());
                var analyticsStore = _analyticsStore
                    ?? new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());

                var outcome = await _outcomeProfileBuilder.AssembleFromStoreAsync(
                    _database,
                    _goalProfileProvider,
                    recoveryService,
                    analyticsStore,
                    now,
                    userId: 1).ConfigureAwait(false);

                // 2) Assemble the chosen report DTO.
                object report = SelectedReportTypeIndex switch
                {
                    0 => _reportAssembler.BuildClinicalReport(
                        outcome,
                        notes: System.Array.Empty<FemVoiceStudio.Models.ClinicalNote>(),
                        auditEvents: System.Array.Empty<FemVoiceStudio.Models.AuditEvent>(),
                        periodStart: periodStart,
                        periodEnd: now,
                        now: now),
                    1 => _reportAssembler.BuildCoachReport(outcome, periodStart, now, now),
                    2 => _reportAssembler.BuildOutcomeReport(outcome, periodStart, now, now),
                    3 => _reportAssembler.BuildTimelineReport(outcome, periodStart, now, now),
                    _ => _reportAssembler.BuildOutcomeReport(outcome, periodStart, now, now)
                };

                // 3) Resolve format.
                var format = SelectedFormatIndex switch
                {
                    0 => ExportFormat.Pdf,
                    1 => ExportFormat.Csv,
                    2 => ExportFormat.Json,
                    _ => ExportFormat.Json
                };

                // 4) Resolve output path (dialog or injected override).
                var filePath = ResolveFilePath(format);
                if (filePath is null)
                {
                    // User cancelled the dialog — no error.
                    StatusMessage = string.Empty;
                    return;
                }

                // 5) Write.
                await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                _exportWriter.Write(report, format, stream);

                Rc0RuntimeLog.Write("ReportGeneration",
                    $"ReportGenerated; Type={ReportTypeName(SelectedReportTypeIndex)}; Format={format}; Path=\"{filePath}\"");
                StatusMessage = LocalizationService.Instance.GetFormattedString("Report_StatusExportedToFormat", filePath);
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("ReportGeneration",
                    $"ReportGeneration FAILED; Type={ReportTypeName(SelectedReportTypeIndex)}; {ex.GetType().Name}: {ex.Message}");
                StatusMessage = LocalizationService.Instance.GetFormattedString("Report_StatusExportErrorFormat", ex.Message);
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private bool CanGenerate() => !IsGenerating;

        private static string ReportTypeName(int index) => index switch
        {
            0 => "Clinical",
            1 => "Coach",
            2 => "Outcome",
            3 => "Timeline",
            _ => "Outcome"
        };

        // ── Private helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the save path: uses <see cref="FileSavePathOverride"/> when set
        /// (test mode); otherwise opens a Win32 SaveFileDialog. Returns null when the
        /// user cancels.
        /// </summary>
        private string? ResolveFilePath(ExportFormat format)
        {
            if (FileSavePathOverride is not null)
                return FileSavePathOverride;

            // WPF/Win32 dialog — only reachable at runtime, never in tests.
            var (filter, ext) = format switch
            {
                ExportFormat.Pdf  => (LocalizationService.Instance.GetString("Report_FileFilterPdf"),  ".pdf"),
                ExportFormat.Csv  => (LocalizationService.Instance.GetString("Report_FileFilterCsv"),  ".csv"),
                ExportFormat.Json => (LocalizationService.Instance.GetString("Report_FileFilterJson"), ".json"),
                _                 => (LocalizationService.Instance.GetString("Report_FileFilterAll"),       "")
            };

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = LocalizationService.Instance.GetString("Report_DialogTitle"),
                Filter     = filter,
                DefaultExt = ext,
                FileName   = $"FemVoice_Report_{DateTime.Now:yyyyMMdd_HHmm}{ext}"
            };

            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }
}
