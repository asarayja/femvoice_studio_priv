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

            // Deficiency-/under-standard framing of the trainee's result: language that
            // measures the voice against a norm/expectation and finds it lacking
            // ("under forventning", "ikke god nok", "under det normale/normalen").
            // Technical/diagnostic metric copy ("for lav suksessrate") is a different
            // grammatical shape and is not touched by these rules.
            new(@"\bunder\s+forventning\w*\b", Options),
            new(@"\bikke\s+god\w*\s+nok\b", Options),
            new(@"\bunder\s+(?:det\s+)?(?:normale\w*|normalen|forventede)\b", Options),

            // -----------------------------------------------------------------------
            // Spec-mandated gender-affirming additions (gap analysis). These target the
            // most clinically harmful copy: telling the user their voice is "wrong",
            // labelling it as masculine/male, and pressuring them to push pitch higher.
            // -----------------------------------------------------------------------

            // "wrong voice" (EN) — a value judgement on the user's voice. The bare word
            // "wrong" is legitimate in education ("wrong intonation can reveal the voice",
            // "then it will be wrong"), so we require "wrong" immediately bound to "voice"
            // (no intervening words — "wrong intonation ... the voice" must NOT match).
            new(@"\bwrong\s+voice\b", Options),

            // "feil stemme" (NO) — the Norwegian "wrong voice". Distinct from the legitimate
            // technical "Feil"/"Feil ved lasting"/"Kalibrering feilet": those never place
            // "feil" directly before "stemme(n/r)". Require direct adjacency.
            new(@"\bfeil\s+stemme\w*\b", Options),

            // Masculine/male voice labelling — the single most gender-affirming-critical
            // class: the app must NEVER tell the user their voice sounds masculine/male.
            // Covers all four adjectives (EN masculine/male, NO maskulin/mannlig) bound to
            // voice/stemme, and (via the optional trailing group) the "...voice/stemme
            // detected/oppdaget" report shape. Adjacency to voice/stemme is required so the
            // Norwegian verb "å male" (to paint) and unrelated prose ("male bilder") are
            // never flagged.
            new(@"\b(?:masculine|male|maskulin\w*|mannlig\w*)\s+(?:voice|stemme\w*)" +
                @"(?:\s+(?:detected|oppdaget))?\b", Options),

            // Imperative MÅL-PRESS: telling the user to push pitch "higher"/"høyere".
            // We flag ONLY the directive shape — a pressure verb (go/aim/push/press,
            // gå/sikt/press/push) bound directly (optionally via one short particle such
            // as "it"/"den"/"litt") to higher/høyere, plus the "må (gå) høyere" /
            // "must (go) higher" forms. This deliberately spares the many legitimate
            // anatomy/resonance/position/education uses ("løft resonansen ... høyere i
            // munnen", "høyere overtoner/formant", "fra lavere til høyere pitch", "ligger
            // høyere/lysere enn sonen") and gentle coaching ("Prøv litt høyere") — because
            // "løft"/"prøv"/"reinforces"/"reach"/"lavere til" are not pressure verbs and
            // descriptive "-ing" forms ("pressing to reach higher notes") are not matched.
            new(@"\b(?:go|aim|push|press)\s+(?:it\s+)?higher\b", Options),
            new(@"\bmust\s+(?:go\s+)?higher\b", Options),
            new(@"\b(?:gå|sikt|press|push)\s+(?:den\s+|litt\s+)?høyere\b", Options),
            new(@"\bmå\s+(?:gå\s+)?høyere\b", Options),

            // Failure framing aimed at the user/voice/attempt (EN). The bare technical
            // operation errors "Loading failed"/"Calibration failed" (mirrors the NO
            // "Feil ved lasting"/"Kalibrering feilet") are legitimate, so we exempt
            // "failed" when it reports on a technical subject and otherwise flag the
            // person-/voice-/attempt-directed "failure"/"failed".
            new(@"\bfailure\b", Options),
            new(@"(?<!\b(?:loading|calibration|save|saving|connection|upload|download|sync|export|import|initialization|operation|request|test)\s)\bfailed\b", Options),

            // "bad voice" (EN) — shame label on the voice. "bad" alone is too broad
            // (legitimate "bad signal" hardware notes), so bind it to "voice".
            new(@"\bbad\s+voice\b", Options),

            // "try harder" (EN) — pressure imperative. Mirrors the NO "hardere"/"push harder"
            // rules already present.
            new(@"\btry\s+harder\b", Options),

            // -----------------------------------------------------------------------
            // SAFETY-CERT-03: Medical-diagnosis forbidden patterns (defense-in-depth).
            // The app must NEVER tell the user they have a medical condition or give
            // them a clinical diagnosis. These patterns target the VERB/PREDICATE forms
            // ("du har en sykdom", "you have a disease", "diagnostisert med") that
            // constitute an active diagnostic claim — NOT the legitimate DISCLAIMER
            // noun form "ikke medisinsk diagnose eller behandling" (which passes because
            // "medisinsk diagnose" is not preceded by "du har" / "diagnostisert med")
            // and NOT the anatomy description "stemmebåndene" (unbound from pathology).
            // Patterns are word-boundary anchored for precision.
            // -----------------------------------------------------------------------

            // Norwegian: "du har (en) sykdom/lidelse/stemmeskade/stemmesykdom/patologi"
            // — the app is telling the user they have a disease/condition.
            // Negative look-behind exempts "ikke" ("du har ikke en sykdom") which
            // would be a legitimate reassurance, not a diagnosis.
            new(@"\bdu\s+har\s+(?:en\s+)?(?:sykdom|lidelse|stemmeskade|stemmesykdom|stemmebåndsskade|patologi)\b",
                Options),

            // Norwegian: "diagnostisert med" — the app telling the user they have been
            // diagnosed (with something). Never appears in legitimate UI guidance.
            new(@"\bdiagnostisert\s+med\b", Options),

            // Norwegian: "diagnosen er / diagnosen lyder" — stating a diagnosis result.
            // Distinct from the disclaimer "ikke medisinsk diagnose eller behandling"
            // (which has neither "er" nor "lyder" directly after "diagnosen").
            new(@"\bdiagnosen\s+(?:er|lyder)\b", Options),

            // English: "you have a disease / disorder / laryngitis / dysphonia"
            // — English equivalent of the Norwegian "du har sykdom/lidelse" forms.
            new(@"\byou\s+have\s+(?:a\s+)?(?:disease|disorder|laryngitis|dysphonia|vocal\s+(?:cord\s+)?(?:damage|injury|nodule|polyp))\b",
                Options),

            // English: "diagnosed with" — states a clinical diagnosis.
            new(@"\bdiagnosed\s+with\b", Options),
        };

        /// <summary>
        /// The curated rule set as raw pattern strings, exposed for transparency and tests.
        /// </summary>
        public static IReadOnlyList<string> ForbiddenPatterns { get; } =
            CompiledForbidden.Select(r => r.ToString()).ToArray();

        // ---------------------------------------------------------------------------
        // The English-only subset of the forbidden rules. The full <see cref="Scan"/>
        // set is tuned for the canonical Norwegian resource (Strings.resx); several of
        // its Norwegian-shaped rules (e.g. the imperative "press"/"forc(e)" command
        // forms) legitimately fire against ordinary English UI copy such as "Press
        // start" or "without extra force", which is exactly why the live-resource CI
        // guard scans only the neutral Norwegian file. To let CI also guard the English
        // resources we expose just the language-agnostic / English-targeted rules — the
        // gender-affirming-critical ones that must never reach an English-speaking user.
        // ---------------------------------------------------------------------------
        private static readonly Regex[] CompiledEnglishForbidden =
        {
            new(@"\bwrong\s+voice\b", Options),
            new(@"\b(?:masculine|male)\s+voice(?:\s+detected)?\b", Options),
            new(@"\b(?:go|aim|push|press)\s+(?:it\s+)?higher\b", Options),
            new(@"\bmust\s+(?:go\s+)?higher\b", Options),
            new(@"\bfailure\b", Options),
            new(@"(?<!\b(?:loading|calibration|save|saving|connection|upload|download|sync|export|import|initialization|operation|request|test)\s)\bfailed\b", Options),
            new(@"\bbad\s+voice\b", Options),
            new(@"\btry\s+harder\b", Options),
        };

        /// <summary>
        /// The English-only forbidden rules as raw pattern strings — the subset of
        /// <see cref="ForbiddenPatterns"/> that is safe to run against the English
        /// resource files (Strings.en.resx / Strings_en.resx) without the Norwegian
        /// rules producing false positives on legitimate English UI copy.
        /// </summary>
        public static IReadOnlyList<string> EnglishForbiddenPatterns { get; } =
            CompiledEnglishForbidden.Select(r => r.ToString()).ToArray();

        /// <summary>
        /// Scans resource entries using only the English-targeted forbidden rules
        /// (<see cref="EnglishForbiddenPatterns"/>). Intended for guarding the English
        /// resource files, where the full Norwegian-tuned <see cref="Scan"/> would flag
        /// legitimate English copy ("Press start", "without extra force").
        /// </summary>
        public static IReadOnlyList<ClinicalLanguageViolation> ScanEnglish(
            IEnumerable<KeyValuePair<string, string>> texts) =>
            ScanWith(texts, CompiledEnglishForbidden);

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
                ["under forventning"] = "i utvikling",
                ["ikke god nok"] = "på vei",
                ["under normalen"] = "i ditt eget tempo",

                // Spec-mandated gender-affirming alternatives for the new forbidden copy.
                ["wrong voice"] = "Utforsk",
                ["feil stemme"] = "Utforsk",
                ["masculine voice"] = "Fremre resonans",
                ["male voice"] = "Fremre resonans",
                ["maskulin stemme"] = "Fremre resonans",
                ["mannlig stemme"] = "Fremre resonans",
                ["go higher"] = "Komfortabel",
                ["push higher"] = "Komfortabel",
                ["gå høyere"] = "Komfortabel",
                ["må høyere"] = "Komfortabel",
                ["higher"] = "Lysere",
                ["høyere"] = "Lettere",
                ["failure"] = "God innsats",
                ["failed"] = "God innsats",
                ["bad voice"] = "Rolig fonasjon",
                ["try harder"] = "Prøv forsiktig",

                // SAFETY-CERT-03: medical-diagnosis alternatives.
                ["du har sykdom"] = "Ta kontakt med en stemmefagperson",
                ["du har lidelse"] = "Ta kontakt med en stemmefagperson",
                ["diagnostisert med"] = "Snakk med en kvalifisert stemmefagperson",
                ["diagnosen er"] = "Ta en pause og kontakt helsepersonell",
                ["you have a disease"] = "Please consult a qualified voice professional",
                ["you have a disorder"] = "Please consult a qualified voice professional",
                ["diagnosed with"] = "Please consult a qualified voice professional",
            };

        /// <summary>
        /// Scans a set of (key, value) resource entries and returns one
        /// <see cref="ClinicalLanguageViolation"/> per forbidden match found.
        /// </summary>
        public static IReadOnlyList<ClinicalLanguageViolation> Scan(
            IEnumerable<KeyValuePair<string, string>> texts) =>
            ScanWith(texts, CompiledForbidden);

        private static IReadOnlyList<ClinicalLanguageViolation> ScanWith(
            IEnumerable<KeyValuePair<string, string>> texts, Regex[] rules)
        {
            ArgumentNullException.ThrowIfNull(texts);

            var violations = new List<ClinicalLanguageViolation>();

            foreach (var entry in texts)
            {
                var value = entry.Value;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                foreach (var rule in rules)
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
