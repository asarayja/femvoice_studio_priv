using System;
using System.Globalization;
using FemVoice.Avalonia.Preferences;

namespace FemVoice.Avalonia.Localization;

/// <summary>
/// Stage 2B — AVALONIA-OWNED runtime language activation. Applies the saved language preference to the Avalonia
/// head by setting the Avalonia-LOCAL <see cref="Localized.CurrentCulture"/> only. It does NOT call the Core/WPF
/// LocalizationService, does NOT call SetLanguage, does NOT change the global thread culture, and does NOT mutate
/// shared Core state — so WPF/Core behaviour and the portable tests are untouched. It is the single language-
/// activation point.
///
/// Boundary: this changes which culture the Avalonia <see cref="Localized"/> resolver reads from the shared
/// resources. Newly-constructed Avalonia views pick up the new language; already-rendered text refreshes on the
/// next navigation / app restart (no broad VM/INotify refactor — intentionally out of scope). Languages without a
/// translated value fall back through their parent chain to the Norwegian neutral resource; Avalonia-only scaffold
/// keys keep their Norwegian fallback (no native parity claimed for the backlog).
/// </summary>
public static class LanguageActivation
{
    /// <summary>Apply a language (culture code) to the Avalonia-local resolver. Unknown/invalid → nb-NO fallback.</summary>
    public static void Apply(string? languageCode)
    {
        Localized.CurrentCulture = Resolve(languageCode);
    }

    private static CultureInfo Resolve(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return new CultureInfo("nb-NO");
        try { return new CultureInfo(languageCode); }
        catch (CultureNotFoundException) { return new CultureInfo("nb-NO"); }
    }

    /// <summary>
    /// Startup activation: apply the saved language ONLY if a valid saved preferences file exists; otherwise leave
    /// the existing default culture in place. The model normalizes unknown/unsupported languages to nb-NO, so this
    /// never selects an unsupported culture. Returns <c>true</c> if a saved language was applied.
    /// </summary>
    public static bool ApplyFromStore(UiPreferencesStore? store = null)
    {
        store ??= new UiPreferencesStore();
        if (store.TryLoad(out var prefs))
        {
            Apply(prefs.Language);
            return true;
        }
        return false; // no/invalid saved preference → preserve the current default culture
    }
}
