using FemVoiceStudio.Services;   // LocalizationService

namespace FemVoice.Avalonia.Localization;

/// <summary>
/// Safe, READ-ONLY localization resolver for the Avalonia head. It reads strings from the shared
/// FemVoiceStudio <see cref="LocalizationService"/> (the single source of truth) without changing any
/// resource key, culture, or localization semantics, and never calls SetLanguage / writes anything.
/// The service's indexer returns the key itself when a string is missing, so a missing key (or an
/// empty value) falls back to a readable provided fallback. This is NOT a port of the WPF localization
/// markup extensions — it only reuses the existing Core API.
/// </summary>
public static class Localized
{
    /// <summary>
    /// Resolve <paramref name="key"/> via <see cref="LocalizationService"/>; return
    /// <paramref name="fallback"/> when the key is missing (resolved value is null/empty or equals the key).
    /// </summary>
    public static string Get(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        // 1) Avalonia-owned scaffold overlay (trusted, culture-invariant values only; Core resx untouched).
        if (ScaffoldStrings.TryGet(LocalizationService.Instance.CurrentCulture?.Name, key, out var overlay)
            && !string.IsNullOrWhiteSpace(overlay))
            return overlay;
        // 2) Shared Core resolver (per-culture); 3) Norwegian neutral fallback when missing.
        string value = LocalizationService.Instance[key];
        return string.IsNullOrWhiteSpace(value) || value == key ? fallback : value;
    }
}
