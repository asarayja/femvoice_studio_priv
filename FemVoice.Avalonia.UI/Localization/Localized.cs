using System;
using System.Globalization;
using System.Resources;
using FemVoiceStudio.Services;   // LocalizationService (type used only for the shared resource assembly + default culture)

namespace FemVoice.Avalonia.Localization;

/// <summary>
/// Safe, READ-ONLY localization resolver for the Avalonia head. It resolves strings from the shared
/// FemVoiceStudio string resources (the single source of truth) via an AVALONIA-OWNED <see cref="ResourceManager"/>
/// instance, for an AVALONIA-LOCAL <see cref="CurrentCulture"/>. It never changes a resource key, never calls
/// SetLanguage, never changes the global thread culture, and never mutates the shared Core
/// <see cref="LocalizationService"/> state — so WPF/Core behaviour and the portable tests are untouched. A missing
/// key (or empty value / value equal to the key) falls back to the provided readable fallback. This is NOT a port
/// of the WPF localization markup extensions.
///
/// Stage 2B: <see cref="CurrentCulture"/> is the Avalonia-local UI culture. It defaults to the Core service's
/// culture (identical resolution at startup) and is updated only by <see cref="LanguageActivation"/> within
/// Avalonia scope. Changing it re-points resolution at a different culture in the shared resources; cultures
/// without a translated value fall back through their parent chain to the Norwegian neutral resource, and
/// Avalonia-only scaffold keys (absent from the Core resources) fall back to the provided fallback — so no native
/// parity is claimed for the backlog.
/// </summary>
public static class Localized
{
    // Avalonia-owned resource resolver over the SAME shared resources used by Core (read-only; a separate instance
    // from the Core/WPF LocalizationService — we never call SetLanguage and never touch thread/Core culture state).
    private static readonly ResourceManager Resources =
        new ResourceManager("FemVoiceStudio.Resources.Strings", typeof(LocalizationService).Assembly);

    /// <summary>Raised (on the calling thread) whenever <see cref="CurrentCulture"/> changes, so the Avalonia UI
    /// can re-resolve its localized text live. Avalonia-local only.</summary>
    public static event System.Action? LanguageChanged;

    private static CultureInfo _currentCulture = LocalizationService.Instance.CurrentCulture;

    /// <summary>Avalonia-LOCAL current UI culture (Stage 2B). Defaults to the Core service culture; set only via
    /// <see cref="LanguageActivation"/>. This is NOT the global thread culture and NOT the Core service culture.
    /// Changing it raises <see cref="LanguageChanged"/> for a live UI refresh.</summary>
    public static CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (value is null || Equals(_currentCulture, value)) return;
            _currentCulture = value;
            LanguageChanged?.Invoke();
        }
    }

    /// <summary>
    /// Resolve <paramref name="key"/> for the Avalonia-local <see cref="CurrentCulture"/>; return
    /// <paramref name="fallback"/> when the key is missing (resolved value is null/empty or equals the key).
    /// </summary>
    private static readonly CultureInfo English = new("en");
    private static readonly CultureInfo Norwegian = new("nb-NO");
    private static bool IsNorwegian(CultureInfo c) => c.TwoLetterISOLanguageName is "nb" or "no" or "nn";
    private static bool Valid(string? v, string key) => !string.IsNullOrWhiteSpace(v) && v != key;
    private static string? Core(string key, CultureInfo c) { try { return Resources.GetString(key, c); } catch (Exception) { return null; } }

    /// <summary>
    /// Resolve <paramref name="key"/> for the Avalonia-local <see cref="CurrentCulture"/>. Resolution order:
    /// (1) the per-language Avalonia overlay; (2) a genuine culture-specific value from the shared resources;
    /// (3) ENGLISH fallback (overlay then shared resources) for non-Norwegian cultures; (4) the Norwegian source
    /// string passed as <paramref name="fallback"/>. For Norwegian, the Norwegian source is used directly. This is
    /// Avalonia-local only — no SetLanguage, no thread-culture change, no shared-Core mutation.
    /// </summary>
    public static string Get(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        var culture = CurrentCulture ?? CultureInfo.CurrentUICulture;

        // 1) Avalonia overlay for the selected language (product-invariant + machine translations).
        if (ScaffoldStrings.TryGet(culture.Name, key, out var overlay) && !string.IsNullOrWhiteSpace(overlay))
            return overlay;

        // Norwegian is the source language: use the shared (neutral) resource or the Norwegian fallback directly.
        if (IsNorwegian(culture))
        {
            var nb = Core(key, culture);
            return Valid(nb, key) ? nb! : fallback;
        }

        // 2) Genuine culture-specific value from the shared resources (i.e. different from the Norwegian neutral).
        var neutral = Core(key, Norwegian);
        var cultureValue = Core(key, culture);
        if (Valid(cultureValue, key) && cultureValue != neutral)
            return cultureValue!;

        // 3) ENGLISH fallback (global) — Avalonia overlay first, then the shared English resource.
        if (ScaffoldStrings.TryGet("en", key, out var en) && !string.IsNullOrWhiteSpace(en))
            return en;
        var enCore = Core(key, English);
        if (Valid(enCore, key) && enCore != neutral)
            return enCore!;

        // 4) Norwegian source string (last resort).
        return fallback;
    }
}
