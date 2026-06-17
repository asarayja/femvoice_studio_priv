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

    /// <summary>Avalonia-LOCAL current UI culture (Stage 2B). Defaults to the Core service culture; set only via
    /// <see cref="LanguageActivation"/>. This is NOT the global thread culture and NOT the Core service culture.</summary>
    public static CultureInfo CurrentCulture { get; set; } = LocalizationService.Instance.CurrentCulture;

    /// <summary>
    /// Resolve <paramref name="key"/> for the Avalonia-local <see cref="CurrentCulture"/>; return
    /// <paramref name="fallback"/> when the key is missing (resolved value is null/empty or equals the key).
    /// </summary>
    public static string Get(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        var culture = CurrentCulture ?? CultureInfo.CurrentUICulture;
        // 1) Avalonia-owned scaffold overlay (trusted, culture-invariant values only).
        if (ScaffoldStrings.TryGet(culture.Name, key, out var overlay) && !string.IsNullOrWhiteSpace(overlay))
            return overlay;
        // 2) Avalonia-owned read-only resolution of the shared resources for the Avalonia-local culture;
        //    3) provided fallback when missing (covers Avalonia-only scaffold keys absent from the resources).
        string? value;
        try { value = Resources.GetString(key, culture); }
        catch (Exception) { value = null; }
        return string.IsNullOrWhiteSpace(value) || value == key ? fallback : value;
    }
}
