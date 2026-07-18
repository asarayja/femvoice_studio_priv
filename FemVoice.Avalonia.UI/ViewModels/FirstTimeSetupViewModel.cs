using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoice.Avalonia.Localization;   // ScaffoldStrings.Cultures + Localized
using FemVoice.Avalonia.Preferences;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// REAL first-time onboarding, ported from the WPF <c>FirstTimeSetupWindow</c>. A welcome header plus the two
/// harmless choices the Avalonia app can actually honour today — language and theme — persisted to the
/// Avalonia-owned <see cref="UiPreferencesStore"/> and applied LIVE (same store/activation the Settings panel uses).
/// Completing (or skipping) records <see cref="UiPreferences.FirstTimeSetupCompleted"/> so onboarding is shown only
/// once. Holds no WPF/Core service, is not IDisposable, starts no capture/timers, touches no database/clinical code.
/// Voice-goal-style / training-frequency from WPF are intentionally NOT captured here (they write a clinical-adjacent
/// profile that has no Avalonia consumer yet) — they belong to a later profile slice.
/// </summary>
public partial class FirstTimeSetupViewModel : ObservableObject
{
    private readonly UiPreferencesStore _store;

    public FirstTimeSetupViewModel(UiPreferencesStore? store = null)
    {
        _store = store ?? new UiPreferencesStore();
        var p = _store.Load();
        _language = p.Language;
        _theme = p.Theme;
        _completed = p.FirstTimeSetupCompleted;
    }

    public IReadOnlyList<ThemePreference> ThemeOptions { get; } =
        new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark };
    public IReadOnlyList<string> LanguageOptions { get; } = ScaffoldStrings.Cultures;

    [ObservableProperty] private string _language;
    [ObservableProperty] private ThemePreference _theme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotCompleted))]
    private bool _completed;

    public bool HasStatus => !string.IsNullOrEmpty(Status);
    /// <summary>True while onboarding has not yet been completed/skipped (drives the "not done" hint).</summary>
    public bool NotCompleted => !Completed;

    // ── Localized captions ──────────────────────────────────────────────────────────────────────────────────────
    public string Title => Localized.Get("FirstSetup_Title", "Velkommen til FemVoice Studio");
    public string Welcome => Localized.Get("FirstSetup_Welcome",
        "La oss sette opp appen. Velg språk og tema — du kan endre alt senere under Innstillinger.");
    public string LanguageLabel => Localized.Get("FirstSetup_SelectLanguage", "Velg språk");
    public string LanguageDesc => Localized.Get("FirstSetup_SelectLanguageDescription",
        "Språket brukes for oversatt tekst i appen.");
    public string ThemeLabel => Localized.Get("FirstSetup_SelectTheme", "Velg tema");
    public string ThemeDesc => Localized.Get("FirstSetup_SelectThemeDescription",
        "Lyst, mørkt eller følg systemet.");
    public string ContinueLabel => Localized.Get("FirstSetup_Continue", "Fullfør oppsett");
    public string SkipLabel => Localized.Get("FirstSetup_Skip", "Hopp over");
    public string CompletedLabel => Localized.Get("FirstSetup_Completed", "Oppsett fullført ✓");
    public string NotCompletedLabel => Localized.Get("FirstSetup_NotCompleted",
        "Oppsettet er ikke fullført ennå.");

    // Persist language + theme + the completed flag, then apply theme/language LIVE (same activation the Settings
    // panel uses). Fail-safe: a failed write surfaces a status message and leaves Completed unchanged.
    [RelayCommand]
    private void Complete() => Finish(applyChoices: true);

    // Skip: record completion without changing the current language/theme.
    [RelayCommand]
    private void Skip() => Finish(applyChoices: false);

    private void Finish(bool applyChoices)
    {
        var prefs = _store.Load();                 // preserve reduce-motion etc. that this screen doesn't edit
        if (applyChoices)
        {
            prefs.Language = Language;
            prefs.Theme = Theme;
        }
        prefs.FirstTimeSetupCompleted = true;

        bool ok = _store.Save(prefs);
        if (ok)
        {
            Completed = true;
            if (applyChoices)
            {
                FemVoice.Avalonia.Theming.ThemeActivation.Apply(Theme);
                FemVoice.Avalonia.Localization.LanguageActivation.Apply(Language);
            }
            Status = applyChoices
                ? Localized.Get("FirstSetup_Saved", "Oppsett lagret. Språk og tema er oppdatert.")
                : Localized.Get("FirstSetup_Skipped", "Oppsett hoppet over. Du kan endre alt under Innstillinger.");
        }
        else
        {
            Status = Localized.Get("FirstSetup_SaveFailed", "Kunne ikke lagre oppsettet lokalt.");
        }
    }
}
