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
        _selectedStyle = StyleOptions.FirstOrDefault(s => s.Token == p.VoiceGoalStyle) ?? StyleOptions[0];
        _selectedFrequency = FrequencyOptions.FirstOrDefault(f => f.Value == p.TrainingFrequency) ?? FrequencyOptions[1];
    }

    /// <summary>One voice-goal-style choice (token + localized label). ToString → label for the combo.</summary>
    public sealed record StyleOption(string Token, string Label) { public override string ToString() => Label; }
    /// <summary>One training-frequency choice (days + localized label).</summary>
    public sealed record FrequencyOption(int Value, string Label) { public override string ToString() => Label; }

    public IReadOnlyList<ThemePreference> ThemeOptions { get; } =
        new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark };
    public IReadOnlyList<string> LanguageOptions { get; } = ScaffoldStrings.Cultures;
    // WPF FirstTimeSetup captures voice-goal-style + training-frequency too (real shared-RESX labels).
    public IReadOnlyList<StyleOption> StyleOptions { get; } = new[]
    {
        new StyleOption("soft_feminine", Localized.Get("VoiceGoalStyle_SoftFeminine", "Myk feminin")),
        new StyleOption("bright_neutral", Localized.Get("VoiceGoalStyle_BrightNeutral", "Lys nøytral")),
        new StyleOption("androgynous", Localized.Get("VoiceGoalStyle_Androgynous", "Androgyn")),
        new StyleOption("custom", Localized.Get("VoiceGoalStyle_Custom", "Egendefinert")),
    };
    public IReadOnlyList<FrequencyOption> FrequencyOptions { get; } = new[]
    {
        new FrequencyOption(2, Localized.Get("Settings_Accessibility_Frequency2", "2 dager")),
        new FrequencyOption(3, Localized.Get("Settings_Accessibility_Frequency3", "3 dager (anbefalt)")),
        new FrequencyOption(4, Localized.Get("Settings_Accessibility_Frequency4", "4 dager")),
        new FrequencyOption(5, Localized.Get("Settings_Accessibility_Frequency5", "5 eller flere dager")),
    };

    [ObservableProperty] private string _language;
    [ObservableProperty] private ThemePreference _theme;
    [ObservableProperty] private StyleOption _selectedStyle;
    [ObservableProperty] private FrequencyOption _selectedFrequency;

    public string StyleLabel => Localized.Get("FirstSetup_StyleGoalLabel", "Velg målstil");
    public string StyleDesc => Localized.Get("FirstSetup_StyleGoalDesc", "Hvilken klang ønsker du å utforske?");
    public string FrequencyLabel => Localized.Get("FirstSetup_FrequencyLabel", "Treningsdager per uke");
    public string FrequencyDesc => Localized.Get("FirstSetup_FrequencyDesc", "Hvor ofte ønsker du å trene?");

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
            prefs.VoiceGoalStyle = SelectedStyle.Token;
            prefs.TrainingFrequency = SelectedFrequency.Value;
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
