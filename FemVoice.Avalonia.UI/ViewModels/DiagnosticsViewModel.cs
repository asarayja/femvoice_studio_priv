using System.Collections.Generic;
using System.Linq;
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
public sealed class DiagnosticsViewModel
{
    public DiagnosticsViewModel()
    {
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
            "Visning-bare diagnostikk/eksport: alt er statiske plassholdere. Ingen støttepakke lages, ingen " +
            "filer eksporteres, ingen sikkerhetskopi/gjenoppretting, ingen historikk/database leses eller " +
            "skrives, ingen endring av RC-0-bevis eller forskning/anonymisering — ekte verktøy kommer senere.");
    }

    public IReadOnlyList<DiagnosticsCard> Cards { get; }
    public string Title { get; }
    public string ScaffoldNotice { get; }

    /// <summary>Always <c>true</c>: every card/action in the scaffold is deferred/inert.</summary>
    public bool AllActionsDeferred => Cards.All(c => !c.IsEnabled);
}
