using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Data;            // SettingsDataService (real backup)
using FemVoice.Avalonia.Localization;    // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One real system-status row (label + value), read-only.</summary>
public sealed class DiagnosticsStatusRow
{
    public DiagnosticsStatusRow(string label, string value) { Label = label; Value = value; }
    public string Label { get; }
    public string Value { get; }
}

/// <summary>
/// FUNCTIONAL diagnostics page. Shows a real, read-only runtime + database status snapshot, exports a real
/// diagnostics text report (native save dialog, via the view code-behind), makes a real database backup (via
/// <see cref="SettingsDataService"/>), and shows the real on-disk data-folder path. No deferred placeholder cards.
/// </summary>
public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly SettingsDataService _data;

    public DiagnosticsViewModel() : this(null) { }

    public DiagnosticsViewModel(IDatabaseService? database)
    {
        _data = new SettingsDataService(database);
        BuildStatus(database);

        Title = Localized.Get("Diag_Title2", "Diagnostikk");
        Intro = Localized.Get("Diag_Intro2",
            "Ekte system- og databasestatus (kun lesing). Du kan eksportere en diagnostikkrapport og lage en " +
            "sikkerhetskopi av databasen.");
        DataFolderPath = FemVoiceStudio.Data.DatabaseService.ResolveAppDataDir();
    }

    public string Title { get; }
    public string Intro { get; }

    // ── Real system status (read-only) ────────────────────────────────────────────────────────────────────────
    public bool HasStatus { get; private set; }
    public IReadOnlyList<DiagnosticsStatusRow> Status { get; private set; } = Array.Empty<DiagnosticsStatusRow>();
    public string StatusHeading => Localized.Get("Diag_SystemStatus", "Systemstatus");
    public string DataFolderLabel => Localized.Get("Diag_DataFolder", "Datamappe");
    public string DataFolderPath { get; }

    // Build a real, read-only system-status snapshot (runtime + database facts). Never throws.
    private void BuildStatus(IDatabaseService? database)
    {
        try
        {
            var rows = new List<DiagnosticsStatusRow>
            {
                // The running app's version comes from the HEAD, not this shared library (which has no <Version> and
                // would report the SDK default 1.0.0 — what users actually saw here).
                new(Localized.Get("Diag_AppVersion", "Appversjon"), FemVoice.Avalonia.AppVersion.Current),
                new(Localized.Get("Diag_OS", "Operativsystem"), System.Runtime.InteropServices.RuntimeInformation.OSDescription),
                new(Localized.Get("Diag_Runtime", ".NET-kjøretid"), System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription),
                new(Localized.Get("Diag_Arch", "Arkitektur"), System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString()),
            };
            if (database is not null)
            {
                int sessionCount = database.GetRecentSessions(1000).Count;
                rows.Add(new(Localized.Get("Diag_Database", "Database"), Localized.Get("Diag_DbConnected", "Tilkoblet (ekte SQLite)")));
                rows.Add(new(Localized.Get("Diag_SavedSessions", "Lagrede økter"), sessionCount.ToString()));
                string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FemVoiceStudio", "femvoice.db");
                if (File.Exists(dbPath))
                    rows.Add(new(Localized.Get("Diag_DbSize", "Database-størrelse"), $"{new FileInfo(dbPath).Length / 1024} kB"));
            }
            else
            {
                rows.Add(new(Localized.Get("Diag_Database", "Database"), Localized.Get("Diag_DbNotConnected", "Ikke tilkoblet i denne visningen")));
            }
            Status = rows;
            HasStatus = true;
        }
        catch { HasStatus = false; }
    }

    // ── Real diagnostics export (text) ────────────────────────────────────────────────────────────────────────
    public string ExportLabel => Localized.Get("Diag_ExportReport", "Eksporter diagnostikk");
    public string SuggestedName => "femvoice-diagnostikk.txt";

    /// <summary>The plain-text diagnostics report (the same status shown on screen). Pure — the view writes it to a
    /// user-picked file. Timestamp is supplied by the caller (view) so this stays deterministic/testable.</summary>
    public string BuildDiagnosticsText(DateTime nowLocal)
    {
        var sb = new StringBuilder();
        sb.Append("FemVoice Studio — diagnostikk\r\n");
        sb.Append(nowLocal.ToString("yyyy-MM-dd HH:mm")).Append("\r\n\r\n");
        foreach (var r in Status) sb.Append(r.Label).Append(": ").Append(r.Value).Append("\r\n");
        sb.Append("\r\nDatamappe: ").Append(DataFolderPath).Append("\r\n");
        return sb.ToString();
    }

    // ── Real database backup ──────────────────────────────────────────────────────────────────────────────────
    public string BackupLabel => Localized.Get("Settings_CreateBackup", "Lag sikkerhetskopi");
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionStatus))]
    private string _actionStatus = string.Empty;
    public bool HasActionStatus => !string.IsNullOrEmpty(ActionStatus);

    [RelayCommand]
    private void Backup() => ActionStatus = _data.Backup(DateTime.Now).Message;
}
