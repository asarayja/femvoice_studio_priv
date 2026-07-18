using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One display-only diagnostics card: title + description + a deferred status. Always inert.</summary>
public sealed class DiagnosticsCard
{
    public DiagnosticsCard(string title, string description, string status)
    {
        Title = title;
        Description = description;
        Status = status;
    }

    public string Title { get; }
    public string Description { get; }
    public string Status { get; }
    /// <summary>Always <c>false</c> — every diagnostics/export/backup action is deferred/inert in this scaffold.</summary>
    public bool IsEnabled => false;
}

/// <summary>
/// DISPLAY-ONLY diagnostics / export / backup scaffold. A purely static page: it holds NO services, NO
/// commands, is NOT IDisposable, starts no timers/subscriptions/capture/background work, opens no file
/// dialogs/folders, and reads/writes NOTHING (no database, no session history, no support package, no
/// export, no sikkerhetskopi/gjenoppretting). It shows static placeholder cards mirroring the WPF
/// diagnostics/export/backup surfaces, every action deferred. It changes no RC-0 evidence behaviour and no
/// forskning/anonymisering behaviour. Labels resolve through the safe read-only localization adapter
/// (namespaced keys with Norwegian fallback).
/// </summary>
/// <summary>One real system-status row (label + value), read-only.</summary>
public sealed class DiagnosticsStatusRow
{
    public DiagnosticsStatusRow(string label, string value) { Label = label; Value = value; }
    public string Label { get; }
    public string Value { get; }
}

public sealed class DiagnosticsViewModel
{
    public DiagnosticsViewModel() : this(null) { }

    public DiagnosticsViewModel(IDatabaseService? database)
    {
        BuildStatus(database);
        string deferred = Localized.Get("Diag_DeferredStatus", "Utsatt — kommer senere");
        string sample = Localized.Get("Diag_SampleStatus", "Eksempel (ikke lagret)");

        Cards = new List<DiagnosticsCard>
        {
            new(Localized.Get("Diag_SystemStatus", "Systemstatus"),
                Localized.Get("Diag_SystemStatusDesc", "Systemstatus (eksempeltekst — ingen ekte måling)."), sample),

            new(Localized.Get("Diag_AppDiagnostikk", "App-diagnostikk"),
                Localized.Get("Diag_AppDiagnostikkDesc", "Applikasjonsdiagnostikk (plassholder, ingen ekte innsamling)."), sample),

            new(Localized.Get("Diag_Stottepakke", "Støttepakke"),
                Localized.Get("Diag_StottepakkeDesc", "Generering av støttepakke (utsatt — ingen pakke lages, ingen fildialog)."), deferred),

            new(Localized.Get("Diag_Sikkerhetskopi", "Sikkerhetskopi"),
                Localized.Get("Diag_SikkerhetskopiDesc", "Sikkerhetskopiering (utsatt — ingenting skrives)."), deferred),

            new(Localized.Get("Diag_Gjenoppretting", "Gjenoppretting"),
                Localized.Get("Diag_GjenopprettingDesc", "Gjenoppretting fra sikkerhetskopi (utsatt — ingenting leses/utføres)."), deferred),

            new(Localized.Get("Diag_DataEksport", "Dataeksport"),
                Localized.Get("Diag_DataEksportDesc", "Eksport av data (utsatt — ingen fildialog, ingen filer skrives)."), deferred),

            new(Localized.Get("Diag_Forskning", "Forskning / anonymisering"),
                Localized.Get("Diag_ForskningDesc", "Forsknings- og anonymiseringsvisning (utsatt — ingen endring av oppførsel)."), deferred),

            new(Localized.Get("Diag_Feilsoking", "Feilsøking"),
                Localized.Get("Diag_FeilsokingDesc", "Feilsøkingsverktøy (utsatt — kun visning)."), deferred),
        };

        Title = Localized.Get("Diag_ScaffoldTitle", "Diagnostikk og eksport");
        ScaffoldNotice = Localized.Get("Diag_ScaffoldNotice",
            "Systemstatusen over leses fra kjøretiden og databasen (kun lesing). Støttepakke, eksport, " +
            "sikkerhetskopi/gjenoppretting og RC-0/forsknings-anonymisering er fortsatt utsatt — ekte verktøy " +
            "kommer senere.");
    }

    public IReadOnlyList<DiagnosticsCard> Cards { get; }
    public string Title { get; }
    public string ScaffoldNotice { get; }

    // ── Real system status (read-only) ────────────────────────────────────────────────────────────────────────
    public bool HasStatus { get; private set; }
    public IReadOnlyList<DiagnosticsStatusRow> Status { get; private set; } = Array.Empty<DiagnosticsStatusRow>();

    // Build a real, read-only system-status snapshot (runtime + database facts). NO export, NO support package, NO
    // backup/restore (those need file dialogs + privacy filtering — a follow-up). Never throws.
    private void BuildStatus(IDatabaseService? database)
    {
        try
        {
            var rows = new List<DiagnosticsStatusRow>
            {
                new("Operativsystem", System.Runtime.InteropServices.RuntimeInformation.OSDescription),
                new(".NET-kjøretid", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription),
                new("Arkitektur", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString()),
            };
            if (database is not null)
            {
                int sessionCount = database.GetRecentSessions(1000).Count;
                rows.Add(new("Database", "Tilkoblet (ekte SQLite)"));
                rows.Add(new("Lagrede økter", sessionCount.ToString()));
                string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FemVoiceStudio", "femvoice.db");
                if (File.Exists(dbPath))
                    rows.Add(new("Database-størrelse", $"{new FileInfo(dbPath).Length / 1024} kB"));
            }
            else
            {
                rows.Add(new("Database", "Ikke tilkoblet i denne visningen"));
            }
            Status = rows;
            HasStatus = true;
        }
        catch { HasStatus = false; }
    }

    /// <summary>Always <c>true</c>: every card/action in the scaffold is deferred/inert.</summary>
    public bool AllActionsDeferred => Cards.All(c => !c.IsEnabled);
}
