using Avalonia;
using Avalonia.Styling;
using FemVoice.Avalonia.Preferences;

namespace FemVoice.Avalonia.Theming;

/// <summary>
/// Stage 2A — AVALONIA-OWNED runtime theme activation. Maps the saved <see cref="ThemePreference"/> to an Avalonia
/// <see cref="ThemeVariant"/> and applies it to the running app via <c>Application.RequestedThemeVariant</c> (the
/// FluentTheme honours it live, no restart). This is the ONLY place that activates a theme at runtime.
///
/// Strictly theme-only and Avalonia-only: it does NOT use any WPF theme manager, does NOT change culture/language, does
/// NOT touch reduce-motion behaviour, the DB, Core, or any clinical/audio code. It is null-safe (no-op when there
/// is no running <c>Application</c>, e.g. headless contexts). At startup it applies ONLY a valid user-saved
/// preference; with no/invalid preference it leaves the existing default (dark) baseline from App.axaml untouched.
/// </summary>
public static class ThemeActivation
{
    /// <summary>Map the Stage-1 preference to an Avalonia theme variant. System → Default (follow OS).</summary>
    public static ThemeVariant ToVariant(ThemePreference theme) => theme switch
    {
        ThemePreference.Light => ThemeVariant.Light,
        ThemePreference.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    /// <summary>Apply a chosen theme to the running app. Null-safe: no-op when no Application is present.</summary>
    public static void Apply(ThemePreference theme)
    {
        var app = Application.Current;
        if (app is not null) app.RequestedThemeVariant = ToVariant(theme);
    }

    /// <summary>
    /// Startup activation: apply the theme ONLY if a valid saved preferences file exists; otherwise leave the
    /// existing default (dark) baseline in place. Theme only — language and reduce-motion are intentionally not
    /// activated here. Returns <c>true</c> if a saved theme was applied.
    /// </summary>
    public static bool ApplyFromStore(UiPreferencesStore? store = null)
    {
        store ??= new UiPreferencesStore();
        if (store.TryLoad(out var prefs))
        {
            Apply(prefs.Theme);
            return true;
        }
        return false; // no/invalid saved preference → preserve the dark baseline (App.axaml RequestedThemeVariant="Dark")
    }
}
