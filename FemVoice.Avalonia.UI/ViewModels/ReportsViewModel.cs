using System.Collections.Generic;
using System.Linq;
using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One display-only reports/professional card: title + description + a deferred status. Always inert.</summary>
public sealed class ReportsCard
{
    public ReportsCard(string title, string description, string status)
    {
        Title = title;
        Description = description;
        Status = status;
    }

    public string Title { get; }
    public string Description { get; }
    public string Status { get; }
    /// <summary>Always <c>false</c> — every report/professional action is deferred/inert in this scaffold.</summary>
    public bool IsEnabled => false;
}

/// <summary>
/// DISPLAY-ONLY Reports / Professional-workflow scaffold. A purely static page: it holds NO services, NO
/// commands, is NOT IDisposable, starts no timers/subscriptions/capture/background work, opens no file
/// dialogs, and reads/writes NOTHING (no database, no session history, no report generation, no export).
/// It shows static placeholder cards mirroring the WPF reports/professional surfaces, every action deferred.
/// No clinical scoring, SmartCoach/progression, Voice-Health/recovery, or diagnostics. Labels resolve through
/// the safe read-only localization adapter (namespaced keys with Norwegian fallback).
/// </summary>
public sealed class ReportsViewModel
{
    public ReportsViewModel()
    {
        string deferred = Localized.Get("Reports_DeferredStatus", "Utsatt — kommer senere");
        string sample = Localized.Get("Reports_SampleStatus", "Eksempel (ikke lagret)");

        Cards = new List<ReportsCard>
        {
            new(Localized.Get("Reports_Preview", "Rapport-forhåndsvisning"),
                Localized.Get("Reports_PreviewDesc", "Forhåndsvisning av rapport (eksempeltekst, ingen ekte generering)."), sample),

            new(Localized.Get("Reports_ProgressSummary", "Fremgangssammendrag"),
                Localized.Get("Reports_ProgressSummaryDesc", "Sammendrag av fremgang (plassholder, eksempeldata)."), sample),

            new(Localized.Get("Reports_SessionHistory", "Økthistorikk"),
                Localized.Get("Reports_SessionHistoryDesc", "Tidligere økter — krever lagring senere (ingen historikk lest)."), deferred),

            new(Localized.Get("Reports_Clinician", "Klinikerpanel"),
                Localized.Get("Reports_ClinicianDesc", "Profesjonelt klinikerpanel (utsatt, ingen funksjonalitet)."), deferred),

            new(Localized.Get("Reports_Coach", "Veilederpanel"),
                Localized.Get("Reports_CoachDesc", "Profesjonelt veilederpanel (utsatt, ingen funksjonalitet)."), deferred),

            new(Localized.Get("Reports_Saksgjennomgang", "Saksgjennomgang"),
                Localized.Get("Reports_SaksgjennomgangDesc", "Saksgjennomgang for fagperson (utsatt, ingen funksjonalitet)."), deferred),

            new(Localized.Get("Reports_Calendar", "Kalender / historikk"),
                Localized.Get("Reports_CalendarDesc", "Kalender- og historikkvisning (utsatt, ingen lagring)."), deferred),

            new(Localized.Get("Reports_Exports", "Eksport"),
                Localized.Get("Reports_ExportsDesc", "Eksport av rapporter (utsatt — ingen fildialog, ingen filer skrives)."), deferred),
        };

        Title = Localized.Get("Reports_ScaffoldTitle", "Rapporter og profesjonelle verktøy");
        ScaffoldNotice = Localized.Get("Reports_ScaffoldNotice",
            "Visning-bare rapporter/profesjonelle verktøy: alt er statiske plassholdere. Ingen rapport genereres, " +
            "ingen filer eksporteres, ingen historikk/database leses eller skrives, ingen klinisk beregning — " +
            "ekte rapporter og fagverktøy kommer i en senere fase.");
    }

    public IReadOnlyList<ReportsCard> Cards { get; }
    public string Title { get; }
    public string ScaffoldNotice { get; }

    /// <summary>Always <c>true</c>: every card/action in the scaffold is deferred/inert.</summary>
    public bool AllActionsDeferred => Cards.All(c => !c.IsEnabled);
}
