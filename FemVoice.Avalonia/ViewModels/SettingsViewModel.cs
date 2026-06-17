using System.Collections.Generic;
using System.Linq;
using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>The kind of (disabled) control a settings row visually represents — mirrors the WPF control type
/// (combo box / checkbox-toggle / button) so the scaffold looks like the real Settings page. Always inert.</summary>
public enum SettingsControlKind { Info, Toggle, Combo, Button }

/// <summary>One display-only settings row: a label + a deferred status, rendered as a DISABLED control of
/// <see cref="Kind"/>. Always inert in this scaffold (no command, no binding target, no side effect).</summary>
public sealed class SettingsRow
{
    public SettingsRow(string label, string status, SettingsControlKind kind = SettingsControlKind.Info, string controlText = "")
    {
        Label = label;
        Value = status;
        Kind = kind;
        ControlText = controlText;
    }

    public string Label { get; }
    /// <summary>Deferred status text (e.g. "Utsatt — kommer senere") or, for Info rows, the read-only value.</summary>
    public string Value { get; }
    public SettingsControlKind Kind { get; }
    /// <summary>Text shown inside the disabled control (combo placeholder / button caption / toggle caption).</summary>
    public string ControlText { get; }

    /// <summary>Always <c>false</c> — every settings action is deferred/inert in this scaffold.</summary>
    public bool IsEnabled => false;

    // Converter-free visibility switches for the view (render the matching disabled control kind).
    public bool IsInfo => Kind == SettingsControlKind.Info;
    public bool IsToggle => Kind == SettingsControlKind.Toggle;
    public bool IsCombo => Kind == SettingsControlKind.Combo;
    public bool IsButton => Kind == SettingsControlKind.Button;
    /// <summary>Compact "deferred" chip shown on actionable (non-Info) rows.</summary>
    public bool ShowDeferredChip => Kind != SettingsControlKind.Info;
    public string DeferredChip => Localized.Get("Scaffold_Pending", "Utsatt");
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
/// is a DISABLED representation (combo/toggle/button) of the WPF Settings control — nothing persists, no
/// SetLanguage/culture change, no theme-switch, no audio-device selection, no voice-goal/profile write, no
/// database/backup/restore/clear, no privacy export/delete, no diagnostics export, no clinical behaviour.
/// Labels resolve through the safe read-only localization adapter (real WPF Settings_*/Privacy_* keys where
/// available; fallback otherwise). Sections/controls mirror WPF `SettingsWindow.xaml` for visual parity.
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
                new List<SettingsRow>
                {
                    new(Localized.Get("Settings_FirstRun", "Førstegangsoppsett"), deferred, SettingsControlKind.Button, Localized.Get("Scaffold_ComingSoon", "Kommer senere")),
                }),

            // Theme — WPF: RadioButtons (System/Light/Dark). Shown as a disabled combo with the current value.
            new(Localized.Get("Settings_Theme", "Utseende / tema"),
                Localized.Get("Settings_ThemeDescription", "Lyst/mørkt tema følger systemet (bytte er utsatt)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Settings_ThemePreference", "Tema"), deferred, SettingsControlKind.Combo, Localized.Get("Settings_ThemeSystem", "System")),
                }),

            // Language — WPF: ComboBox (20 languages). Disabled combo showing the current language.
            new(Localized.Get("Settings_Language", "Språk"),
                Localized.Get("Settings_SelectLanguage", "Velg språk (bytte er utsatt)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Settings_LanguageRow", "Språk"), deferred, SettingsControlKind.Combo, "Norsk"),
                }),

            // Audio — WPF: CheckBox "hear own voice" + Button "open mic calibration".
            new(Localized.Get("Settings_AudioSettings", "Lydinngang"),
                Localized.Get("Settings_HearOwnVoiceDesc", "Mikrofon og «hør egen stemme» (utsatt)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Settings_Microphone", "Mikrofon"), deferred, SettingsControlKind.Combo, Localized.Get("Shell_MicStatus", "Syntetisk (kun visning)")),
                    new(Localized.Get("Settings_HearOwnVoice", "Hør egen stemme"), deferred, SettingsControlKind.Toggle, Localized.Get("Settings_HearOwnVoice", "Hør egen stemme")),
                    new(Localized.Get("MicCalibration_Open", "Mikrofonkalibrering"), deferred, SettingsControlKind.Button, Localized.Get("Scaffold_ComingSoon", "Kommer senere")),
                }),

            // Voice goal — WPF: two ComboBoxes (focus / style).
            new(Localized.Get("Settings_VoiceGoalTitle", "Øvelsespreferanser"),
                Localized.Get("Settings_VoiceGoalDesc", "Stemmemål og fokus (utsatt — ingen profilskriving)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Settings_VoiceGoalFocus", "Fokus"), deferred, SettingsControlKind.Combo, Localized.Get("VoiceGoalFocus_Balanced", "Balansert")),
                    new(Localized.Get("Settings_VoiceGoalStyle", "Stil"), deferred, SettingsControlKind.Combo, Localized.Get("VoiceGoalStyle_SoftFeminine", "Myk feminin")),
                }),

            // Accessibility — WPF: CheckBox(es), e.g. stress-sensitive mode.
            new(Localized.Get("Settings_Accessibility_Title", "Tilgjengelighet"),
                Localized.Get("Settings_Accessibility_Desc", "Tilgjengelighetsvalg (utsatt)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Settings_Accessibility_StressSensitive", "Stressømfintlig modus"), deferred, SettingsControlKind.Toggle, Localized.Get("Settings_Accessibility_StressSensitive", "Stressømfintlig modus")),
                }),

            // Data / backup — WPF: Buttons (clear / backup / restore).
            new(Localized.Get("Settings_Database", "Data / sikkerhetskopi"),
                Localized.Get("Settings_DatabaseDesc", "Sikkerhetskopi, gjenoppretting og databasehandlinger (utsatt)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Settings_Sikkerhetskopi", "Sikkerhetskopi"), deferred, SettingsControlKind.Button, Localized.Get("Settings_CreateBackup", "Lag sikkerhetskopi")),
                    new(Localized.Get("Settings_Gjenopprett", "Gjenopprett"), deferred, SettingsControlKind.Button, Localized.Get("Settings_RestoreBackup", "Gjenopprett")),
                    new(Localized.Get("Settings_TomDatabase", "Tøm database"), deferred, SettingsControlKind.Button, Localized.Get("UI_ClearDatabase", "Tøm database")),
                }),

            // Privacy / diagnostics — WPF: consent toggles.
            new(Localized.Get("Privacy_Title", "Personvern / diagnostikk"),
                Localized.Get("Privacy_LocalStorage", "Data lagres lokalt. Diagnostikk og forskning (utsatt)."),
                new List<SettingsRow>
                {
                    new(Localized.Get("Privacy_DiagnosticsConsent", "Diagnostikk-samtykke"), deferred, SettingsControlKind.Toggle, Localized.Get("Privacy_DiagnosticsConsent", "Diagnostikk-samtykke")),
                    new(Localized.Get("Privacy_ResearchWarning", "Forskningsdeling"), deferred, SettingsControlKind.Toggle, Localized.Get("Privacy_ResearchWarning", "Forskningsdeling")),
                }),

            // About — read-only info.
            new(Localized.Get("Settings_About", "Om"),
                Localized.Get("Settings_AboutDesc", "Om FemVoice Studio."),
                new List<SettingsRow>
                {
                    new("FemVoice Studio", Localized.Get("Settings_AboutMode", "Avalonia · kun visning")),
                    new(Localized.Get("Settings_Version", "Versjon"), "dev"),
                }),
        };

        Title = Localized.Get("Settings_Title", "Innstillinger");
        DeferredBadge = Localized.Get("Scaffold_DeferredBadge", "Utsatt · kun visning");
        DeferredBanner = Localized.Get("Settings_ScaffoldNotice",
            "Visning-bare innstillinger: alle valg er utsatt. Ingenting lagres, ingen språk-/tema-bytte, " +
            "ingen profilendring, ingen sikkerhetskopi — dette kommer i en senere fase.");
        SafetyNote = Localized.Get("Settings_ScaffoldSafety",
            "Kun visning · ingen lagring · ingen klinisk endring.");
    }

    public string Title { get; }
    public string DeferredBadge { get; }
    public string DeferredBanner { get; }
    public string SafetyNote { get; }
    public IReadOnlyList<SettingsSection> Sections { get; }

    /// <summary>Always <c>true</c>: every row/control in the scaffold is deferred/inert.</summary>
    public bool AllControlsDeferred => Sections.All(s => s.Rows.All(r => !r.IsEnabled));
}
