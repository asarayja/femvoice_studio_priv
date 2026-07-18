using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoice.Avalonia.Localization;   // ScaffoldStrings.Cultures (Avalonia-owned culture list; no WPF)
using FemVoice.Avalonia.Preferences;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Stage 1 — INTERACTIVE but display-only UI-preference editor. Binds the three harmless preferences (theme,
/// language, reduce-motion) and persists them to the Avalonia-local <see cref="UiPreferencesStore"/>. It is
/// PERSISTENCE-ONLY: Save/Reload round-trip the file; nothing is applied to the running app — no theme switch, no
/// culture change, no audio/database/clinical/WPF interaction (runtime activation is a future Stage 2). Holds no
/// WPF/Core service; not IDisposable. Loads current values on construction (only when the Settings page hosts it).
/// </summary>
public partial class UiPreferencesViewModel : ObservableObject
{
    private readonly UiPreferencesStore _store;

    public UiPreferencesViewModel(UiPreferencesStore? store = null)
    {
        _store = store ?? new UiPreferencesStore();
        var p = _store.Load();
        _theme = p.Theme;
        _language = p.Language;
        _reduceMotion = p.ReduceMotion;
        _setupCompleted = p.FirstTimeSetupCompleted;   // preserved verbatim on Save (not edited here)
    }

    // Onboarding-completed flag is owned by FirstTimeSetup; this panel only round-trips it so a later Save here
    // does not wipe it.
    private readonly bool _setupCompleted;

    // Display-only option lists (Avalonia-owned; no WPF LocalizationService).
    public IReadOnlyList<ThemePreference> ThemeOptions { get; } =
        new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark };
    public IReadOnlyList<string> LanguageOptions { get; } = ScaffoldStrings.Cultures;

    [ObservableProperty] private ThemePreference _theme;
    [ObservableProperty] private string _language;
    [ObservableProperty] private bool _reduceMotion;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    /// <summary>Converter-free visibility flag for the status line.</summary>
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    public string Heading => Localized.Get("Settings_LocalPrefs_Title", "Lokale UI-innstillinger");
    public string Note => Localized.Get("Settings_LocalPrefs_Note",
        "Lagres lokalt på denne maskinen. Tema og språk brukes med en gang " +
        "(kun oversatt tekst følger språket; resten vises på norsk inntil videre). Reduser bevegelse er aktiv og respekteres av appens bevegelseseffekter.");
    public string SaveLabel => Localized.Get("Settings_LocalPrefs_Save", "Lagre");
    public string ReloadLabel => Localized.Get("Settings_LocalPrefs_Reload", "Last på nytt");
    public string ThemeLabel => Localized.Get("Settings_ThemePreference", "Tema");
    public string LanguageLabel => Localized.Get("Settings_LanguageRow", "Språk");
    public string ReduceMotionLabel => Localized.Get("Settings_LocalPrefs_ReduceMotion", "Reduser bevegelse");

    /// <summary>The Avalonia-local file these preferences persist to (shown for transparency).</summary>
    public string FilePath => _store.FilePath;

    /// <summary>Current edited values as a model (no I/O).</summary>
    public UiPreferences Current() => new() { Theme = Theme, Language = Language, ReduceMotion = ReduceMotion, FirstTimeSetupCompleted = _setupCompleted };

    // Persist, then apply THEME (Stage 2A) and LANGUAGE (Stage 2B) LIVE — both take effect immediately. Language
    // activation raises Localized.LanguageChanged, which makes the shell re-render its localized text in the new
    // culture without a restart. Only TRANSLATED text changes; strings with no translation stay Norwegian (the
    // status says so). Reduce-motion remains persisted-only. Fail-safe: a failed write surfaces a status message.
    [RelayCommand]
    private void Save()
    {
        bool ok = _store.Save(Current());
        if (ok)
        {
            FemVoice.Avalonia.Theming.ThemeActivation.Apply(Theme);                  // theme — live
            FemVoice.Avalonia.Localization.LanguageActivation.Apply(Language);       // language — live (raises LanguageChanged)
            FemVoice.Avalonia.Accessibility.MotionActivation.Apply(ReduceMotion);    // reduce-motion — live (Avalonia motion state)
        }
        Status = ok
            ? Localized.Get("Settings_LocalPrefs_Saved",
                "Lagret. Tema, språk og bevegelsesvalg er oppdatert (kun oversatt tekst endres; resten vises på norsk inntil videre).")
            : Localized.Get("Settings_LocalPrefs_SaveFailed", "Kunne ikke lagre innstillingene lokalt.");
    }

    // Reload from disk (discards unsaved edits).
    [RelayCommand]
    private void Reload()
    {
        var p = _store.Load();
        Theme = p.Theme;
        Language = p.Language;
        ReduceMotion = p.ReduceMotion;
        Status = Localized.Get("Settings_LocalPrefs_Reloaded", "Lastet fra lagret fil.");
    }
}
