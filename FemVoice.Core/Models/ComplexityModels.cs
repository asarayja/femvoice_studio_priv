using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Speech complexity levels for voice feminization training.
    /// Represents clinical progression from isolated sounds to natural conversation.
    /// </summary>
    public enum SpeechComplexityLevel
    {
        /// <summary>
        /// Isolated sounds: humming, sustained vowels ("aaaa")
        /// </summary>
        IsolatedSounds = 0,
        
        /// <summary>
        /// Syllables: repeated patterns ("la-la-la", "mi-mi-mi")
        /// </summary>
        Syllables = 1,
        
        /// <summary>
        /// Single words: functional words ("hei", "takk", "god morgen")
        /// </summary>
        Words = 2,
        
        /// <summary>
        /// Short phrases: 2-5 word combinations
        /// </summary>
        Phrases = 3,
        
        /// <summary>
        /// Complete sentences: structured statements
        /// </summary>
        StructuredSentences = 4,
        
        /// <summary>
        /// Spontaneous speech: planned speech with pauses
        /// </summary>
        SpontaneousSpeech = 5,
        
        /// <summary>
        /// Conversational: natural dialogue with fluid exchange
        /// </summary>
        Conversational = 6
    }
    
    /// <summary>
    /// Result of complexity level evaluation.
    /// Contains all information about user's current complexity progression status.
    /// </summary>
    public class ComplexityEvaluation
    {
        /// <summary>
        /// Current complexity level
        /// </summary>
        public SpeechComplexityLevel CurrentLevel { get; set; }
        
        /// <summary>
        /// Success rate (0-100) based on last 5 sessions at this level
        /// </summary>
        public double SuccessRate { get; set; }
        
        /// <summary>
        /// Whether user meets all criteria for advancement to next level
        /// </summary>
        public bool IsReadyForNext { get; set; }
        
        /// <summary>
        /// Pedagogical feedback explaining current status
        /// </summary>
        public string Feedback { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of sessions completed at current level
        /// </summary>
        public int SessionsAtCurrentLevel { get; set; }
        
        /// <summary>
        /// When level was last changed
        /// </summary>
        public DateTime LastLevelChange { get; set; }
        
        /// <summary>
        /// Average resonance score (last 3 sessions)
        /// </summary>
        public double AverageResonance { get; set; }
        
        /// <summary>
        /// Pitch stability score (0-100)
        /// </summary>
        public double PitchStability { get; set; }
        
        /// <summary>
        /// Average intonation score (0-100)
        /// </summary>
        public double IntonationScore { get; set; }
        
        /// <summary>
        /// Voice health score (0-100)
        /// </summary>
        public double VoiceHealthScore { get; set; }
        
        /// <summary>
        /// Average strain level (0-100, lower is better)
        /// </summary>
        public double StrainLevel { get; set; }
        
        /// <summary>
        /// Sessions per week (last 2 weeks)
        /// </summary>
        public double SessionsPerWeek { get; set; }
        
        /// <summary>
        /// Whether health status allows progression
        /// </summary>
        public bool HealthAllowsProgression { get; set; }
        
        /// <summary>
        /// List of blocking reasons if not ready for advancement
        /// </summary>
        public List<string> BlockingReasons { get; set; } = new();
    }
    
    /// <summary>
    /// Complexity level step for UI visualization.
    /// Used in progression dashboard to show 7-step ladder.
    /// </summary>
    public class ComplexityLevelStep
    {
        /// <summary>
        /// The complexity level
        /// </summary>
        public SpeechComplexityLevel Level { get; set; }
        
        /// <summary>
        /// Whether this level has been completed
        /// </summary>
        public bool IsCompleted { get; set; }
        
        /// <summary>
        /// Whether this is the current active level
        /// </summary>
        public bool IsCurrent { get; set; }
        
        /// <summary>
        /// Display name in Norwegian
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Emoji icon for visual representation
        /// </summary>
        public string Icon { get; set; } = string.Empty;
        
        /// <summary>
        /// Progress percentage towards next level (0-100)
        /// </summary>
        public int ProgressToNext { get; set; }
        
        /// <summary>
        /// Gets display name for a complexity level in Norwegian
        /// </summary>
        public static string GetDisplayName(SpeechComplexityLevel level)
        {
            return level switch
            {
                SpeechComplexityLevel.IsolatedSounds => "Isolerte lyder",
                SpeechComplexityLevel.Syllables => "Stavelser",
                SpeechComplexityLevel.Words => "Enkeltord",
                SpeechComplexityLevel.Phrases => "Korte fraser",
                SpeechComplexityLevel.StructuredSentences => "Fullendte setninger",
                SpeechComplexityLevel.SpontaneousSpeech => "Spontan tale",
                SpeechComplexityLevel.Conversational => "Naturlig dialog",
                _ => "Ukjent"
            };
        }
        
        /// <summary>
        /// Gets emoji icon for a complexity level
        /// </summary>
        public static string GetIcon(SpeechComplexityLevel level)
        {
            return level switch
            {
                SpeechComplexityLevel.IsolatedSounds => "🎵",
                SpeechComplexityLevel.Syllables => "🔤",
                SpeechComplexityLevel.Words => "💬",
                SpeechComplexityLevel.Phrases => "📝",
                SpeechComplexityLevel.StructuredSentences => "📖",
                SpeechComplexityLevel.SpontaneousSpeech => "🗣️",
                SpeechComplexityLevel.Conversational => "👩",
                _ => "❓"
            };
        }
    }
    
    /// <summary>
    /// Complexity-based exercise recommendation
    /// </summary>
    public class ComplexityExerciseRecommendation
    {
        /// <summary>
        /// Exercise ID
        /// </summary>
        public int ExerciseId { get; set; }
        
        /// <summary>
        /// Exercise name
        /// </summary>
        public string ExerciseName { get; set; } = string.Empty;
        
        /// <summary>
        /// Target complexity level for this exercise
        /// </summary>
        public SpeechComplexityLevel TargetComplexity { get; set; }
        
        /// <summary>
        /// Whether this is a preview exercise for next level
        /// </summary>
        public bool IsPreview { get; set; }
        
        /// <summary>
        /// Session day recommendation (0=Monday, 6=Sunday)
        /// </summary>
        public int RecommendedDay { get; set; }
        
        /// <summary>
        /// Reasoning for this recommendation
        /// </summary>
        public string Reasoning { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Complexity progress database model
    /// </summary>
    public class ComplexityProgress
    {
        /// <summary>
        /// User ID (default 1)
        /// </summary>
        public int UserId { get; set; } = 1;
        
        /// <summary>
        /// Current complexity level (enum value)
        /// </summary>
        public int CurrentLevel { get; set; }
        
        /// <summary>
        /// Number of sessions at current level
        /// </summary>
        public int SessionsAtLevel { get; set; }
        
        /// <summary>
        /// Success rate (0-100)
        /// </summary>
        public double SuccessRate { get; set; }
        
        /// <summary>
        /// Last evaluation date
        /// </summary>
        public string LastEvaluationDate { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether ready for next level
        /// </summary>
        public bool IsReadyForNext { get; set; }
    }
}
