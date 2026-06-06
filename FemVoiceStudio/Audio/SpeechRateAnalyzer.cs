using System;
using System.Collections.Generic;
using System.Linq;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Analyzer for speech rate/tempo - beregner ord per minutt (WPM) og stavelser per sekund (SPS)
    /// </summary>
    public class SpeechRateAnalyzer
    {
        // Estimert antall stavelser per ord (gjennomsnitt for norsk)
        private const double AverageSyllablesPerWord = 1.5;
        
        /// <summary>
        /// Beregn tale-hastighet basert på varighet og antall ord
        /// </summary>
        /// <param name="durationSeconds">Varighet i sekunder</param>
        /// <param name="estimatedWordCount">Estimert antall ord (kan bruke varighet som estimat)</param>
        /// <returns>SpeechRateMetrics med WPM, SPS, etc.</returns>
        public SpeechRateMetrics CalculateSpeechRate(TimeSpan duration, int estimatedWordCount)
        {
            var metrics = new SpeechRateMetrics();
            
            if (duration.TotalSeconds <= 0)
                return metrics;
            
            // Ord per minutt
            double minutes = duration.TotalMinutes;
            metrics.WordsPerMinute = estimatedWordCount / minutes;
            
            // Stavelser per sekund
            double estimatedSyllables = estimatedWordCount * AverageSyllablesPerWord;
            metrics.SyllablesPerSecond = estimatedSyllables / duration.TotalSeconds;
            
            // Estimer antall ord basert på varighet (gjennomsnittlig tale)
            // Typisk tale: 150-180 ord/min = 2.5-3 ord/sekund
            metrics.EstimatedWordsPerMinute = (duration.TotalSeconds * 2.5);
            
            // Sjekk om tempo er innenfor målområdet
            metrics.IsInTargetRange = metrics.WordsPerMinute >= 130 && metrics.WordsPerMinute <= 220;
            
            return metrics;
        }
        
        /// <summary>
        /// Beregn tale-hastighet basert kun på varighet (estimert)
        /// </summary>
        public SpeechRateMetrics EstimateFromDuration(TimeSpan duration)
        {
            // Gjennomsnittlig talehastighet: 150-180 WPM
            double avgWpm = 165;
            double words = (duration.TotalSeconds / 60) * avgWpm;
            
            return CalculateSpeechRate(duration, (int)words);
        }
        
        /// <summary>
        /// Analyser tempo-kategori
        /// </summary>
        public TempoCategory GetTempoCategory(double wpm)
        {
            if (wpm < 120) return TempoCategory.Slow;
            if (wpm < 150) return TempoCategory.SlightlySlow;
            if (wpm <= 180) return TempoCategory.Normal;
            if (wpm <= 200) return TempoCategory.FeminineTarget;
            if (wpm <= 220) return TempoCategory.Fast;
            return TempoCategory.TooFast;
        }
        
        /// <summary>
        /// Gi feedback basert på tempo
        /// </summary>
        public string GetTempoFeedback(double wpm)
        {
            return GetTempoCategory(wpm) switch
            {
                TempoCategory.Slow => "Du snakker litt sakte. Prøv å øke tempoet litt for en mer naturlig flyt.",
                TempoCategory.SlightlySlow => "Godt tempo! Du er litt under gjennomsnittet.",
                TempoCategory.Normal => "Perfekt tempo! Ditt naturlige tempo fungerer bra.",
                TempoCategory.FeminineTarget => "Utmerket! Dette er et typisk tempo for feminine stemmer.",
                TempoCategory.Fast => "Du snakker ganske raskt. Prøv å redusere litt for bedre tydelighet.",
                TempoCategory.TooFast => "Du snakker veldig raskt. Prøv å bremse litt og pust dypt.",
                _ => "Ukjent tempo."
            };
        }
    }
    
    public class SpeechRateMetrics
    {
        public double WordsPerMinute { get; set; }
        public double EstimatedWordsPerMinute { get; set; }
        public double SyllablesPerSecond { get; set; }
        public bool IsInTargetRange { get; set; }
    }
    
    public enum TempoCategory
    {
        Slow,           // < 120 WPM
        SlightlySlow,   // 120-150 WPM
        Normal,         // 150-180 WPM
        FeminineTarget, // 180-200 WPM
        Fast,           // 200-220 WPM
        TooFast         // > 220 WPM
    }
}
