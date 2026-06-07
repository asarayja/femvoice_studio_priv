using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// A single detected breach of the clinical-language policy.
    /// <see cref="Key"/> is the RESX resource key, <see cref="Pattern"/> the rule that
    /// matched, and <see cref="MatchedText"/> the exact offending fragment.
    /// </summary>
    public sealed record ClinicalLanguageViolation
    {
        public required string Key { get; init; }
        public required string Pattern { get; init; }
        public required string MatchedText { get; init; }

        public override string ToString() => $"{Key}: /{Pattern}/ matched \"{MatchedText}\"";
    }

    /// <summary>
    /// Stateless rule engine that enforces calm, exploratory, comfort-oriented language
    /// in line with modern gender-affirming voice training. The clinical safety hierarchy
    /// (Safety &gt; Health &gt; Recovery &gt; Progression &gt; Coaching &gt; UI) demands that
    /// user-facing copy never shames the trainee or pressures them to push their voice.
    ///
    /// The patterns are deliberately curated for precision over breadth: word-boundary
    /// anchored and context-aware, so that legitimate clinical text — anti-pressure
    /// instructions ("unngå press", "ikke press"), symptom education ("press i halsen"),
    /// physiological description ("tvinger frem semi-okkludert vokaltrakt"), diagnostic
    /// metrics ("for lav suksessrate") and technical UI error labels ("Feil ved lasting")
    /// — is NOT flagged. We only catch shame labels and imperatives that tell the user to
    /// strain or that judge their voice as deficient.
    /// </summary>
    public static class ClinicalLanguagePolicy
    {
        private const RegexOptions Options =
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;

        // ---------------------------------------------------------------------------
        // Forbidden patterns. Each entry is documented with the clinical rationale and,
        // where needed, a negative look-behind/look-ahead that exempts the legitimate
        // contexts surfaced when scanning the live resource file.
        // ---------------------------------------------------------------------------
        private static readonly Regex[] CompiledForbidden =
        {
            // Imperative "press" as a directive to strain the voice — the genuinely
            // harmful form. We target the COMMAND sense, not the noun. The noun "press"
            // (a detected symptom) appears in legitimate clinical copy — symptom
            // observation ("viser/kjente press", "tegn på press"), health awareness
            // ("oppmerksom på press"), avoidance instructions ("unngå/ikke/uten å presse"),
            // physiology ("press i halsen"), and detection ("press detektert") — all of
            // which must pass. So we only flag "press(e)" when it follows a directive
            // lead-in (sentence start, or "må/skal/kan") that is NOT itself negated, and
            // never when a preceding "uten/ikke/unngå" marks an avoidance instruction.
            new(@"(?<!\b(?:ikke|unngå|uten)\s)(?<!\b(?:ikke|unngå|uten)\så\s)" +
                @"(?:^|(?<=[.!?]\s)|(?<=\bmå )|(?<=\bskal )|(?<=\bkan ))" +
                @"press(?:e[rs]?)?\b" +
                @"(?!\s+(?:detektert|i\s+halsen))", Options),

            // Judgmental "too much pressure" labels aimed at the trainee's production.
            new(@"\bfor\s+(?:høyt|mye|stort)\s+press\b", Options),

            // English imperative to press/push the voice harder.
            new(@"\bpress(?:\s+(?:it|the\s+voice|harder|more))\b", Options),

            // English imperative to push harder — already guarded by ResourceTextPolicyTests
            // but mirrored here so the engine is self-contained.
            new(@"\bpush(?:\s+(?:it|the\s+voice|yourself))?\s+harder\b", Options),

            // "hardere" as a directive (do it harder). Allowed when softened/negated
            // ("ikke hardere", "mykere, ikke hardere").
            new(@"(?<!\b(?:ikke|aldri|unngå)\s)\bhardere\b", Options),

            // Coercion verbs aimed at the user. "tving"/"tvang" as pressure on the trainee.
            // Exempt the physiology sense "tvinger frem <struktur>" (SOVT/straw technique).
            new(@"\btving\b", Options),
            new(@"\btvang\b", Options),
            new(@"\btvinge(?:r|s)?\b(?!\s+frem\b)", Options),

            // "force" / "forcing" the voice (English shame/pressure copy).
            new(@"\bforc(?:e|es|ed|ing)\b", Options),

            // "aggressiv*" directed at voice production. Exempt the hardware noise-gate
            // troubleshooting sense ("aggressiv gate/noise gate/støygate").
            new(@"\baggressiv\w*\b(?!\s+(?:gate|noise|støy))", Options),

            // Shame labels: "dårlig" used to judge the user's voice/result.
            // Allowed in mic-troubleshooting "dårlig signal/forbindelse/mikrofon".
            new(@"\bdårlig\w*\b(?!\s+(?:signal|forbindelse|tilkobling|mikrofon|nett|internett))", Options),

            // Shame exclamations: "Feil!" / "Dårlig!" / "Mislykket!" as feedback outcry.
            // The bare technical label "Feil" (no exclamation) stays allowed.
            new(@"\b(?:feil|dårlig|mislyk\w*|svak\w*)\s*!", Options),

            // Failure framing aimed at the trainee.
            new(@"\bmislyk\w*\b", Options),

            // Direct shaming of the person.
            new(@"\bskam\w*\b", Options),
        };

        /// <summary>
        /// The curated rule set as raw pattern strings, exposed for transparency and tests.
        /// </summary>
        public static IReadOnlyList<string> ForbiddenPatterns { get; } =
            CompiledForbidden.Select(r => r.ToString()).ToArray();

        /// <summary>
        /// Recommendation dictionary mapping discouraged phrasing to the calm, exploratory,
        /// comfort-oriented alternative preferred in gender-affirming voice work. Keys are
        /// lower-cased fragments; this is advisory copy for authors, not an automatic
        /// rewriter.
        /// </summary>
        public static IReadOnlyDictionary<string, string> PreferredAlternatives { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["press"] = "utforsk",
                ["press harder"] = "utforsk i ditt eget tempo",
                ["push harder"] = "utforsk i ditt eget tempo",
                ["for høy"] = "prøv en lettere, lysere klang",
                ["for høyt press"] = "prøv en lettere, mykere klang",
                ["for lav"] = "utforsk en lysere resonans",
                ["feil"] = "la oss justere litt",
                ["feil!"] = "la oss justere litt",
                ["hardere"] = "mykere",
                ["force"] = "la lyden komme naturlig",
                ["tving"] = "la det skje i sitt eget tempo",
                ["aggressiv"] = "rolig",
                ["dårlig"] = "tidlig i utviklingen",
                ["svak"] = "lett",
                ["mislykket"] = "ikke helt der ennå - prøv igjen",
                ["skam"] = "",
            };

        /// <summary>
        /// Scans a set of (key, value) resource entries and returns one
        /// <see cref="ClinicalLanguageViolation"/> per forbidden match found.
        /// </summary>
        public static IReadOnlyList<ClinicalLanguageViolation> Scan(
            IEnumerable<KeyValuePair<string, string>> texts)
        {
            ArgumentNullException.ThrowIfNull(texts);

            var violations = new List<ClinicalLanguageViolation>();

            foreach (var entry in texts)
            {
                var value = entry.Value;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                foreach (var rule in CompiledForbidden)
                {
                    foreach (Match match in rule.Matches(value))
                    {
                        if (!match.Success)
                            continue;

                        violations.Add(new ClinicalLanguageViolation
                        {
                            Key = entry.Key,
                            Pattern = rule.ToString(),
                            MatchedText = match.Value
                        });
                    }
                }
            }

            return violations;
        }
    }
}
