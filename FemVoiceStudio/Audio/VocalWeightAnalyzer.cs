using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Vokalvekt-kategori — opplevd «tyngde» av stemmen.
    /// Lettere stemme = sterkere feminiseringssignal; tyngre stemme = svakere.
    /// </summary>
    public enum WeightCategory
    {
        /// <summary>Lett, lys stemme — høy spektral spredning oppover. Score ~75-100.</summary>
        Light = 0,
        /// <summary>Middels vekt — overgangssone. Score ~45-75.</summary>
        Medium = 1,
        /// <summary>Tung, mørk stemme — energi konsentrert lavt. Score ~0-45.</summary>
        Heavy = 2
    }

    /// <summary>
    /// Resultat fra vokalvekt-analyse. Forklarbart (bærer en kort tekst-forklaring)
    /// og sporbart (bærer rå-inndataene scoren ble avledet fra).
    /// </summary>
    public sealed class VocalWeightResult
    {
        /// <summary>Vokalvekt-score, 0-100. Høyere = lettere/lysere stemme = sterkere feminisering.</summary>
        public double Score { get; }

        /// <summary>Diskret kategorisering av <see cref="Score"/>.</summary>
        public WeightCategory Category { get; }

        /// <summary>Kort, klinikervennlig forklaring av hvorfor scoren ble som den ble.</summary>
        public string Explanation { get; }

        /// <summary>
        /// Rå-inndataene scoren ble avledet fra (sporbarhet). For session-aggregering
        /// er dette de robuste snittene som ble brukt.
        /// </summary>
        public IReadOnlyDictionary<string, double> RawInputs { get; }

        public VocalWeightResult(
            double score,
            WeightCategory category,
            string explanation,
            IReadOnlyDictionary<string, double> rawInputs)
        {
            Score = score;
            Category = category;
            Explanation = explanation;
            RawInputs = rawInputs;
        }
    }

    /// <summary>
    /// Beregner en 0-100 vokalvekt-score fra spektrale råsignaler (IKKE pitch).
    ///
    /// KLINISK GRUNNLAG
    /// ────────────────
    /// Opplevd vokalvekt («tyngde») korrelerer med spektral tilt og energifordeling,
    /// ikke med fundamentalfrekvens. En tyngre/mørkere stemme konsentrerer energi i
    /// lavfrekvensområdet (lav spektral centroid), har lavere første formant F1
    /// (lengre/større vokaltrakt) og leses som «tung». En lettere/lysere stemme har
    /// energi spredt høyere opp (høy centroid) og høyere F1.
    ///
    /// Derfor: HØYERE centroid og HØYERE F1 ⇒ LETTERE stemme ⇒ HØYERE score.
    /// Mappingen er monoton og dokumentert med eksplisitte ankerpunkter under.
    ///
    /// VEKTING (summerer til 1.0)
    /// ──────────────────────────
    ///   Spektral centroid   0.55  — primær akustisk korrelat til spektral tilt/vekt.
    ///   F1                  0.30  — vokaltrakt-størrelse; sekundær vekt-indikator.
    ///   HNR                 0.10  — klarhet/kvalitet; ren stemme leses litt lettere.
    ///   Intensitet          0.05  — presset/høyt trykk leses litt tyngre (svak modulasjon).
    ///
    /// Pitch er BEVISST utelatt som parameter — den er ikke en proxy for vokalvekt.
    /// </summary>
    public sealed class VocalWeightAnalyzer
    {
        // ─────────────────────────────────────────────────────────────────────
        // ANKERPUNKTER (lineær, klemmet mapping rå → 0..100 delscore)
        // Dokumentert slik at en kliniker kan etterprøve magnitudene.
        //
        // Spektral centroid (Hz): tung stemme ~1000 Hz, lett stemme ~2600 Hz.
        //   Verdier under/over klemmes til 0/100.
        // ─────────────────────────────────────────────────────────────────────
        private const double CentroidHeavyHz = 1000.0; // ⇒ 0
        private const double CentroidLightHz = 2600.0;  // ⇒ 100

        // F1 (Hz): tung/stor vokaltrakt ~350 Hz, lett ~750 Hz.
        private const double F1HeavyHz = 350.0; // ⇒ 0
        private const double F1LightHz = 750.0; // ⇒ 100

        // HNR (dB): typisk talespenn ~5 dB (uklar) → ~25 dB (klar).
        // Klarere stemme leses marginalt lettere; svak vekt.
        private const double HnrHeavyDb = 5.0;  // ⇒ 0
        private const double HnrLightDb = 25.0; // ⇒ 100

        // Intensitet (RMS 0..1): høyt trykk leses litt TYNGRE, så denne aksen er
        // INVERTERT — lav intensitet ⇒ høy delscore. Ankere innenfor optimalspenn.
        private const double IntensityLight = 0.10; // lavt trykk ⇒ 100
        private const double IntensityHeavy = 0.80; // høyt trykk ⇒ 0

        private const double WeightCentroid = 0.55;
        private const double WeightF1 = 0.30;
        private const double WeightHnr = 0.10;
        private const double WeightIntensity = 0.05;

        // Kategori-grenser på samlet score.
        private const double LightThreshold = 75.0; // >= 75 ⇒ Light
        private const double HeavyThreshold = 45.0; // <  45 ⇒ Heavy ; mellom ⇒ Medium

        // Nøytral midt-score når signalet er utilstrekkelig.
        private const double NeutralScore = 50.0;

        /// <summary>
        /// Skår én frame/observasjon fra dens spektrale råsignaler.
        /// </summary>
        /// <param name="f1Hz">Første formant F1 i Hz.</param>
        /// <param name="spectralCentroidHz">Spektral centroid i Hz (primærsignal).</param>
        /// <param name="hnrDb">Harmonics-to-Noise Ratio i dB.</param>
        /// <param name="intensity">RMS-intensitet 0..1.</param>
        public VocalWeightResult Score(
            double f1Hz, double spectralCentroidHz, double hnrDb, double intensity)
        {
            // ── Robusthet: degenerert inndata ⇒ nøytral midt-score + forklaring ──
            // Vi krever et meningsfullt centroid- ELLER F1-signal; uten dem kan vekt
            // ikke avledes. NaN/Inf/0 på begge primærakser ⇒ utilstrekkelig signal.
            bool centroidUsable = IsUsable(spectralCentroidHz) && spectralCentroidHz > 0;
            bool f1Usable = IsUsable(f1Hz) && f1Hz > 0;

            if (!centroidUsable && !f1Usable)
            {
                var raw = BuildRaw(f1Hz, spectralCentroidHz, hnrDb, intensity);
                return new VocalWeightResult(
                    NeutralScore,
                    WeightCategory.Medium,
                    "Utilstrekkelig signal: verken spektral centroid eller F1 er målbar — nøytral midt-score gitt.",
                    raw);
            }

            // Delscorer (0..100). Hvis et signal mangler, faller vekten dens bort og
            // de gjenværende vektene renormaliseres slik at total fortsatt er 0..100.
            double wSum = 0, acc = 0;

            if (centroidUsable)
            {
                acc += WeightCentroid * MapLinear(spectralCentroidHz, CentroidHeavyHz, CentroidLightHz);
                wSum += WeightCentroid;
            }
            if (f1Usable)
            {
                acc += WeightF1 * MapLinear(f1Hz, F1HeavyHz, F1LightHz);
                wSum += WeightF1;
            }
            if (IsUsable(hnrDb))
            {
                acc += WeightHnr * MapLinear(hnrDb, HnrHeavyDb, HnrLightDb);
                wSum += WeightHnr;
            }
            if (IsUsable(intensity) && intensity > 0)
            {
                // Invertert akse: lav intensitet ⇒ lett.
                acc += WeightIntensity * MapLinear(intensity, IntensityLight, IntensityHeavy);
                wSum += WeightIntensity;
            }

            double score = Clamp01To100(acc / wSum);
            var category = Categorize(score);
            var rawInputs = BuildRaw(f1Hz, spectralCentroidHz, hnrDb, intensity);

            return new VocalWeightResult(score, category, Explain(score, category, spectralCentroidHz, f1Hz), rawInputs);
        }

        /// <summary>
        /// Skår en hel økt ved å snitte råsignalene robust (median-trimmet snitt),
        /// og kjøre samme mapping. Returnerer samme resultattype.
        /// Tom/utelukkende-degenerert sekvens ⇒ nøytral midt-score + forklaring.
        /// </summary>
        public VocalWeightResult ScoreSession(
            IEnumerable<(double f1Hz, double centroidHz, double hnrDb, double intensity)> frames)
        {
            if (frames is null)
            {
                return new VocalWeightResult(
                    NeutralScore, WeightCategory.Medium,
                    "Utilstrekkelig signal: ingen frames levert — nøytral midt-score gitt.",
                    BuildRaw(double.NaN, double.NaN, double.NaN, double.NaN));
            }

            var f1List = new List<double>();
            var centroidList = new List<double>();
            var hnrList = new List<double>();
            var intensityList = new List<double>();

            foreach (var (f1Hz, centroidHz, hnrDb, intensity) in frames)
            {
                if (IsUsable(f1Hz) && f1Hz > 0) f1List.Add(f1Hz);
                if (IsUsable(centroidHz) && centroidHz > 0) centroidList.Add(centroidHz);
                if (IsUsable(hnrDb)) hnrList.Add(hnrDb);
                if (IsUsable(intensity) && intensity > 0) intensityList.Add(intensity);
            }

            // Hvis ingen brukbare primærsignaler i hele økta ⇒ degenerert.
            if (f1List.Count == 0 && centroidList.Count == 0)
            {
                return new VocalWeightResult(
                    NeutralScore, WeightCategory.Medium,
                    "Utilstrekkelig signal: ingen målbar centroid eller F1 i økten — nøytral midt-score gitt.",
                    BuildRaw(double.NaN, double.NaN, double.NaN, double.NaN));
            }

            // Robust aggregering: bruk NaN for tomme akser slik at Score() dropper dem.
            double f1 = f1List.Count > 0 ? RobustMean(f1List) : double.NaN;
            double centroid = centroidList.Count > 0 ? RobustMean(centroidList) : double.NaN;
            double hnr = hnrList.Count > 0 ? RobustMean(hnrList) : double.NaN;
            double intensity2 = intensityList.Count > 0 ? RobustMean(intensityList) : double.NaN;

            return Score(f1, centroid, hnr, intensity2);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Hjelpere
        // ─────────────────────────────────────────────────────────────────────

        private static bool IsUsable(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        /// <summary>
        /// Monoton lineær mapping av <paramref name="value"/> fra [lowAnchor..highAnchor]
        /// til [0..100], klemmet i begge ender. lowAnchor ⇒ 0, highAnchor ⇒ 100.
        /// </summary>
        private static double MapLinear(double value, double lowAnchor, double highAnchor)
        {
            if (highAnchor == lowAnchor) return 50.0;
            double t = (value - lowAnchor) / (highAnchor - lowAnchor);
            return Clamp01To100(t * 100.0);
        }

        private static double Clamp01To100(double v)
        {
            if (double.IsNaN(v)) return NeutralScore;
            if (v < 0) return 0;
            if (v > 100) return 100;
            return v;
        }

        private static WeightCategory Categorize(double score)
        {
            if (score >= LightThreshold) return WeightCategory.Light;
            if (score < HeavyThreshold) return WeightCategory.Heavy;
            return WeightCategory.Medium;
        }

        /// <summary>
        /// Robust snitt: trimmet snitt rundt medianen for å dempe utliggere uten
        /// å innføre eksterne avhengigheter. For ≤2 verdier brukes aritmetisk snitt.
        /// </summary>
        private static double RobustMean(List<double> values)
        {
            if (values.Count <= 2)
            {
                double s = 0;
                foreach (var v in values) s += v;
                return s / values.Count;
            }

            var sorted = new List<double>(values);
            sorted.Sort();

            // Klipp 20 % i hver ende (minst behold midten).
            int trim = (int)(sorted.Count * 0.20);
            int start = trim;
            int end = sorted.Count - trim; // eksklusiv
            if (end <= start) { start = 0; end = sorted.Count; }

            double sum = 0;
            int n = 0;
            for (int i = start; i < end; i++) { sum += sorted[i]; n++; }
            return sum / n;
        }

        private static IReadOnlyDictionary<string, double> BuildRaw(
            double f1Hz, double centroidHz, double hnrDb, double intensity) =>
            new Dictionary<string, double>
            {
                ["F1Hz"] = f1Hz,
                ["SpectralCentroidHz"] = centroidHz,
                ["HnrDb"] = hnrDb,
                ["Intensity"] = intensity
            };

        private static string Explain(
            double score, WeightCategory category, double centroidHz, double f1Hz)
        {
            string tilt = category switch
            {
                WeightCategory.Light =>
                    "Energien er spredt høyt i spekteret (høy centroid) og vokaltrakten leses lys — lett, feminiserende vokalvekt.",
                WeightCategory.Heavy =>
                    "Energien er konsentrert lavt (lav spektral centroid / lav F1) — tung, mørk vokalvekt som trekker ned feminiseringen.",
                _ =>
                    "Spektralfordelingen ligger i overgangssonen — middels vokalvekt.",
            };

            string centroidPart = IsUsable(centroidHz) && centroidHz > 0
                ? $"centroid {centroidHz:F0} Hz"
                : "centroid utilgjengelig";
            string f1Part = IsUsable(f1Hz) && f1Hz > 0
                ? $"F1 {f1Hz:F0} Hz"
                : "F1 utilgjengelig";

            return $"Vokalvekt {score:F0}/100 ({category}): {tilt} (avledet av {centroidPart}, {f1Part}).";
        }
    }
}
