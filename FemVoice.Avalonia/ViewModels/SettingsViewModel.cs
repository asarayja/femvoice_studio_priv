using System.Collections.Generic;
using System.Linq;
using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One display-only settings row: a label + a status/value. Always inert in this scaffold.</summary>
public sealed class SettingsRow
{
    public SettingsRow(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public string Value { get; }
    /// <summary>Always <c>false</c> — every settings action is deferred/inert in this scaffold.</summary>
    public bool IsEnabled => false;
}

/// <summary>A display-only settings card: title + description + inert rows.</summary>
public sealed class SettingsSection
{
    public SettingsSection(string title, string description, IReadOnlyList<SettingsRow> rows)
    {
        Title = title;
        Description = description;
        Rows = rows;
    }

    public string Title { get; }
    public string Description { get; }
    public IReadOnlyList<SettingsRow> Rows { get; }
}

/// <summary>
/// DISPLAY-ONLY Settings / Preferences scaffold. A purely static page: it holds NO services, NO commands,
/// is NOT IDisposable, starts no timers/subscriptions/capture, and performs NO side effects. Every control
/// is deferred/inert — nothing persists, no SetLanguage/culture change, no theme-switch side effect, no
/// voice-goal/profile write, no database/backup/restore, no clinical behaviour. Labels resolve through the
/// safe read-only localization adapter (real WPF Settings_*/Privacy_* keys where available; fallback otherwise).
/// </summary>
public sealed class SettingsViewModel
{
    public SettingsViewModel()
    {
        string deferred = Localized.Get("Settings_DeferredStatus", "Utsatt — kommer senere");

        Sections = new List<SettingsSection>
        {
            new(Localized.Get("Settings_General", "Generelt"),
                Localized.Get("Settings_GeneralDesc", "Generelle appinnstillinger (kun visning)."),
                new List<SettingsRow> { new(Localized.Get("Settings_FirstRun", "Førstegangsoppsett"), deferred) }),

            new(Localized.Get("Settings_Theme", "Utseende / tema"),
                Localized.Get("Settings_ThemeDescription", "Lyst/mørkt tema følger systemet (bytte er utsatt)."),
                new List<SettingsRow> { new(Localized.Get("Settings_ThemePreference", "Tema (System / Lyst / Mørkt)"), deferred) }),

            new(Localized.Get("Settings_Language", "Språk"),
                Localized.Get("Settings_SelectLanguage", "Velg språk (bytte er utsatt)."),
                new List<SettingsRow> { new(Localized.Get("Settings_LanguageRow", "Språk"), deferred) }),

            new(Localized.Get("Settings_AudioSettings", "Lydinngang"),
                Localized.Get("Settings_HearOwnVoiceDesc", "Mikrofon og «hør egen stemme» (utsatt)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Settings_Microphone", "Mikrofon"), deferred),
                    new(Localized.Get("Settings_HearOwnVoice", "Hør egen stemme"), deferred),
                }),

            new(Localized.Get("Settings_VoiceGoalTitle", "Øvelsespreferanser"),
                Localized.Get("Settings_VoiceGoalDesc", "Stemmemål og fokus (utsatt — ingen profilskriving)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Settings_VoiceGoalFocus", "Fokus"), deferred),
                    new(Localized.Get("Settings_VoiceGoalStyle", "Stil"), deferred),
                }),

            new(Localized.Get("Settings_Database", "Data / sikkerhetskopi"),
                Localized.Get("Settings_DatabaseDesc", "Sikkerhetskopi, gjenoppretting og databasehandlinger (utsatt)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Settings_Sikkerhetskopi", "Sikkerhetskopi"), deferred),
                    new(Localized.Get("Settings_Gjenopprett", "Gjenopprett"), deferred),
                    new(Localized.Get("Settings_TomDatabase", "Tøm database"), deferred),
                }),

            new(Localized.Get("Privacy_Title", "Personvern / diagnostikk"),
                Localized.Get("Privacy_LocalStorage", "Data lagres lokalt. Diagnostikk og forskning (utsatt)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Privacy_DiagnosticsConsent", "Diagnostikk-samtykke"), deferred),
                    new(Localized.Get("Privacy_ResearchWarning", "Forskningsdeling"), deferred),
                }),

            new(Localized.Get("Settings_About", "Om"),
                Localized.Get("Settings_AboutDesc", "Om FemVoice Studio."),
                new List<SettingsRow>
                {
                    new("FemVoice Studio", Localized.Get("Settings_AboutMode", "Avalonia · kun visning")),
                    new(Localized.Get("Settings_Version", "Versjon"), "dev"),
                }),
        };

        Title = Localized.Get("Settings_Title", "Innstillinger");
        DeferredBanner = Localized.Get("Settings_ScaffoldNotice",
            "Visning-bare innstillinger: alle valg er utsatt. Ingenting lagres, ingen språk-/tema-bytte, " +
            "ingen profilendring, ingen sikkerhetskopi — dette kommer i en senere fase.");
    }

    public string Title { get; }
    public string DeferredBanner { get; }
    public IReadOnlyList<SettingsSection> Sections { get; }

    /// <summary>Always <c>true</c>: every row/control in the scaffold is deferred/inert.</summary>
    public bool AllControlsDeferred => Sections.All(s => s.Rows.All(r => !r.IsEnabled));
}
