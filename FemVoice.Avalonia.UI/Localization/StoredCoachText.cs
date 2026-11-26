using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FemVoice.Avalonia.Localization;

/// <summary>Earlier versions persisted rendered coach copy. Match only exact built-in resource values
/// when presenting a saved recommendation; never regenerate a clinical decision or rewrite its history.</summary>
internal static class StoredCoachText
{
    private static readonly Dictionary<string, Lazy<Dictionary<string, string?>>> Catalogs =
        ScaffoldStrings.Cultures.ToDictionary(name => name,
            name => new Lazy<Dictionary<string, string?>>(() => ReadCatalog(CultureInfo.GetCultureInfo(name))),
            StringComparer.OrdinalIgnoreCase);

    public static string Display(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // Try the active and original source languages before loading other installed resource sets.
        var cultures = new[] { Localized.CurrentCulture.Name, "nb-NO", "en-US" }.Concat(ScaffoldStrings.Cultures);
        foreach (var culture in cultures.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Catalogs.TryGetValue(culture, out var catalog) && catalog.Value.TryGetValue(text, out var key))
                return key is null ? text : Localized.Get(key, text);
        }
        return text;
    }

    private static Dictionary<string, string?> ReadCatalog(CultureInfo culture)
    {
        var catalog = new Dictionary<string, string?>(StringComparer.Ordinal);
        var resources = Localized.Resources.GetResourceSet(culture, true, true);
        if (resources is null) return catalog;
        foreach (DictionaryEntry entry in resources)
        {
            if (entry.Key is not string key || entry.Value is not string value) continue;
            if (!key.StartsWith("SmartCoach_", StringComparison.Ordinal) && !key.StartsWith("Coach_", StringComparison.Ordinal))
                continue;
            if (!catalog.TryAdd(value, key)) catalog[value] = null;
        }
        return catalog;
    }
}
