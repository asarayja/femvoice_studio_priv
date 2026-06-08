using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// SPEC AGENT 13 — Validation. Freezes the Professional/Research-Edition (Sprint E)
    /// RESX contract and the clinical-language safety bar for it.
    ///
    /// Two guarantees:
    ///   (1) EVERY frozen Sprint-E key (the Nav_/Clinician_/Coach_/Report_/Override_/
    ///       CaseReview_/Research_Export navigation + dashboard + report + override +
    ///       case-review + research-export surface) exists in the neutral Strings.resx, so
    ///       a future refactor cannot silently drop a key the new UI binds to;
    ///   (2) the user-facing VALUES of those keys pass
    ///       <see cref="ClinicalLanguagePolicy.Scan"/> — no shame, no pressure-to-strain,
    ///       no medical-diagnosis copy reaches a clinician/coach/participant screen.
    ///
    /// Patterned after <see cref="ResourceTextPolicyTests"/> (XDocument over the live
    /// Resources directory; no mocks). The frozen-key list is the contract: adding a new
    /// Sprint-E key means adding it here too — intentional friction on a clinical surface.
    /// </summary>
    public class ProfessionalResxPolicyTests
    {
        // ── The frozen Sprint-E RESX contract ───────────────────────────────────────
        // Every key the Professional/Research-Edition UI binds to. Grouped by surface so a
        // missing key reports against the right area.
        private static readonly string[] FrozenKeys =
        {
            // Navigation entries for the new professional surfaces.
            "Nav_Home", "Nav_Exercises", "Nav_Analyzer", "Nav_Statistics", "Nav_Settings",
            "Nav_ClinicianDashboard", "Nav_CoachDashboard", "Nav_Reports",
            "Nav_ManualOverride", "Nav_CaseReview",

            // Clinician dashboard.
            "Clinician_Title", "Clinician_VoiceMetrics", "Clinician_RecoveryStatus",
            "Clinician_ComfortTrend", "Clinician_ResonanceTrend", "Clinician_ConsistencyTrend",
            "Clinician_ExerciseEffectiveness", "Clinician_LearningPath",

            // Coach dashboard.
            "Coach_Title", "Coach_FocusAreas", "Coach_Recommendations", "Coach_Breakthroughs",
            "Coach_PlateauWarnings", "Coach_RecoveryNeeds",

            // Report export.
            "Report_Title", "Report_Format", "Report_Type", "Report_Generate", "Report_Export",
            "Report_Clinical", "Report_Coach", "Report_Outcome", "Report_Timeline",

            // Manual override.
            "Override_Title", "Override_Kind", "Override_Reason", "Override_Apply",
            "Override_Clamped", "Override_Blocked", "Override_Logged",

            // Case review.
            "CaseReview_Title", "CaseReview_Type", "CaseReview_Period",
            "CaseReview_Create", "CaseReview_Complete",

            // Research export.
            "Research_Export",
        };

        // ── 1. Every frozen Sprint-E key exists in the neutral Strings.resx ──────────
        [Fact]
        public void FrozenProfessionalKeys_AllExistInNeutralResources()
        {
            var neutral = LoadResourceValues(Path.Combine(FindResourceDirectory(), "Strings.resx"));

            var missing = FrozenKeys
                .Where(key => !neutral.ContainsKey(key))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            Assert.True(missing.Count == 0,
                "Frozen Sprint-E RESX keys missing from Strings.resx:" + Environment.NewLine +
                string.Join(Environment.NewLine, missing));
        }

        // ── 2. No frozen key carries an empty/whitespace user-facing value ───────────
        // A blank navigation entry or dashboard heading is itself a clinical defect (an
        // invisible control). The contract is that every frozen key resolves to real copy.
        [Fact]
        public void FrozenProfessionalKeys_AllHaveNonEmptyValues()
        {
            var neutral = LoadResourceValues(Path.Combine(FindResourceDirectory(), "Strings.resx"));

            var blank = FrozenKeys
                .Where(key => neutral.TryGetValue(key, out var v) && string.IsNullOrWhiteSpace(v))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            Assert.True(blank.Count == 0,
                "Frozen Sprint-E RESX keys present but blank in Strings.resx:" + Environment.NewLine +
                string.Join(Environment.NewLine, blank));
        }

        // ── 3. Frozen-key VALUES pass the clinical-language policy (no shame/press/dx) ─
        [Fact]
        public void FrozenProfessionalKeyValues_PassClinicalLanguagePolicy()
        {
            var neutral = LoadResourceValues(Path.Combine(FindResourceDirectory(), "Strings.resx"));

            // Only the frozen keys that are actually present — the existence test (1) owns the
            // "missing key" failure; this test owns the "unsafe copy" failure, so the two
            // never mask each other.
            var entries = FrozenKeys
                .Where(neutral.ContainsKey)
                .Select(key => new KeyValuePair<string, string>(key, neutral[key]));

            var violations = ClinicalLanguagePolicy.Scan(entries);

            Assert.True(violations.Count == 0,
                "Clinical-language policy violations in frozen Sprint-E resource values:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
        }

        // ── 4. EVERY localised variant of the frozen keys passes the policy too ──────
        // Strings.resx is the neutral file, but any Strings.<culture>.resx that *defines* a
        // frozen key must also be safe. We scan whatever localisations exist (forward-safe:
        // if a Norwegian/English translation is added later it is automatically guarded).
        [Fact]
        public void FrozenProfessionalKeyValues_PassPolicyInEveryLocalization()
        {
            var resourceDirectory = FindResourceDirectory();
            var failures = new List<string>();

            foreach (var file in Directory.EnumerateFiles(resourceDirectory, "Strings*.resx"))
            {
                var values = LoadResourceValues(file);
                var entries = FrozenKeys
                    .Where(values.ContainsKey)
                    .Select(key => new KeyValuePair<string, string>(key, values[key]))
                    .ToList();
                if (entries.Count == 0)
                    continue;

                // Norwegian-tuned Scan is correct for the canonical file; for non-neutral
                // files we additionally run it (the frozen copy is short, neutral labels — no
                // legitimate "Press start"/"without force" English idioms among these keys),
                // so the full Scan is safe here.
                foreach (var v in ClinicalLanguagePolicy.Scan(entries))
                    failures.Add($"{Path.GetFileName(file)}:{v}");
            }

            Assert.True(failures.Count == 0,
                "Clinical-language policy violations in a localised frozen Sprint-E value:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        // ── Helpers (mirrored from ResourceTextPolicyTests) ─────────────────────────

        private static string FindResourceDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "FemVoiceStudio", "Resources");
                if (Directory.Exists(candidate))
                    return candidate;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not find FemVoiceStudio/Resources from test output path.");
        }

        private static Dictionary<string, string> LoadResourceValues(string path)
        {
            var document = XDocument.Load(path);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var element in document.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
            {
                var name = (string?)element.Attribute("name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                // Last write wins is fine — frozen keys are unique in the contract.
                result[name] = element.Element("value")?.Value ?? string.Empty;
            }
            return result;
        }
    }
}
