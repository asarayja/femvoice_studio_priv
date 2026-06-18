using System.Collections.Generic;
using System.Linq;

namespace FemVoice.Avalonia.Localization;

/// <summary>Per-culture translation review/readiness metadata for one supported language.</summary>
public sealed record CultureTranslationStatus(
    string Code,                 // full culture code (matches ScaffoldStrings.Cultures), e.g. "nb-NO"
    string DisplayName,          // endonym, for contributor docs/UI
    bool IsSource,               // the source/reference language (authored)
    bool IsFallback,             // the global fallback language
    bool IsMachineGenerated,     // overlay values are model-generated (NOT native-reviewed)
    bool IsNativeReviewed,       // a native speaker has reviewed/approved this language
    string Notes);

/// <summary>
/// AVALONIA-OWNED translation review/readiness REGISTRY for the 20 supported languages. This is governance metadata
/// only — it carries NO translated UI strings (those live in <see cref="ScaffoldTranslations"/>) and changes NO
/// Core/WPF behaviour. It lets the project distinguish: source language (nb), global fallback (en), machine/model-
/// generated languages (the other 18, NOT native-reviewed), and — in future — native-reviewed languages (flip
/// <see cref="CultureTranslationStatus.IsNativeReviewed"/> after a native speaker signs off).
///
/// IMPORTANT: the 18 machine-generated languages MUST NOT be marked native-reviewed until an actual native-speaker
/// review happens. No production/clinical native-parity may be claimed before that. To mark a language reviewed,
/// set its <see cref="CultureTranslationStatus.IsNativeReviewed"/> to true (and IsMachineGenerated to false) only
/// after a real native-speaker review.
/// </summary>
public static class TranslationStatus
{
    /// <summary>Prominent, non-removable caveat for the machine-generated languages.</summary>
    public const string MachineTranslationCaveat =
        "Machine/model-generated translation — NOT native-speaker reviewed. Do not claim production/clinical or " +
        "native-language parity until a native speaker has reviewed and approved this language.";

    private const string MachineNotes = "Machine/model-generated; awaiting native-speaker review.";

    /// <summary>Review/readiness metadata for every supported culture (keyed by full culture code).</summary>
    public static readonly IReadOnlyList<CultureTranslationStatus> All = new[]
    {
        new CultureTranslationStatus("nb-NO", "Norsk (bokmål)", IsSource: true,  IsFallback: false, IsMachineGenerated: false, IsNativeReviewed: true,  "Source/reference language (authored)."),
        new CultureTranslationStatus("en-US", "English",        IsSource: false, IsFallback: true,  IsMachineGenerated: false, IsNativeReviewed: false, "Global fallback (authored English); native review still recommended."),
        new CultureTranslationStatus("sv-SE", "Svenska",        IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("da-DK", "Dansk",          IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("fi-FI", "Suomi",          IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("de-DE", "Deutsch",        IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("fr-FR", "Français",       IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("es-ES", "Español",        IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("pt-BR", "Português (Brasil)", IsSource: false, IsFallback: false, IsMachineGenerated: true, IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("it-IT", "Italiano",       IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("hr-HR", "Hrvatski",       IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("nl-NL", "Nederlands",     IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("pl-PL", "Polski",         IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("tr-TR", "Türkçe",         IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("uk-UA", "Українська",     IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("ro-RO", "Română",         IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("cs-CZ", "Čeština",        IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("hu-HU", "Magyar",         IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("el-GR", "Ελληνικά",       IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
        new CultureTranslationStatus("ar",    "العربية",        IsSource: false, IsFallback: false, IsMachineGenerated: true,  IsNativeReviewed: false, MachineNotes),
    };

    /// <summary>The 2-letter language code for a culture (e.g. "nb-NO" → "nb"), matching the overlay keys.</summary>
    public static string TwoLetter(string code) => code.Length >= 2 ? code.Substring(0, 2).ToLowerInvariant() : code.ToLowerInvariant();

    /// <summary>Required visible scaffold keys = the English overlay key set (the reference coverage set).</summary>
    public static IReadOnlyCollection<string> RequiredVisibleKeys =>
        ScaffoldTranslations.ByLanguage.TryGetValue("en", out var en) ? en.Keys.ToArray() : System.Array.Empty<string>();

    public static CultureTranslationStatus? Get(string code) =>
        All.FirstOrDefault(c => string.Equals(c.Code, code, System.StringComparison.OrdinalIgnoreCase));
}
