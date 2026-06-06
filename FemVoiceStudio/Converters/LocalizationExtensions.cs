using System.Windows.Markup;
using System.Windows.Data;
using System.Windows;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Converters;

/// <summary>
/// Markup extension for easy localization in XAML
/// Usage: Text="{loc:Loc KeyName}"
/// </summary>
public class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;
    
    public LocExtension() { }
    
    public LocExtension(string key)
    {
        Key = key;
    }
    
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return string.Empty;
        
        // Try to get localization service
        try
        {
            var binding = new Binding($"[{Key}]")
            {
                Source = LocalizationService.Instance,
                Mode = BindingMode.OneWay
            };
            
            return binding.ProvideValue(serviceProvider);
        }
        catch
        {
            return Key;
        }
    }
}

/// <summary>
/// Static helper class for code-behind localization
/// </summary>
public static class Loc
{
    private static LocalizationService? _service;
    
    private static LocalizationService Service =>
        _service ??= LocalizationService.Instance;
    
    public static string Get(string key) => Service[key];
    
    // Navigation
    public static string Nav_Home => Service["Nav_Home"];
    public static string Nav_Exercises => Service["Nav_Exercises"];
    public static string Nav_Analyzer => Service["Nav_Analyzer"];
    public static string Nav_Statistics => Service["Nav_Statistics"];
    public static string Nav_Settings => Service["Nav_Settings"];
    
    // Home
    public static string Home_Welcome => Service["Home_Welcome"];
    public static string Home_TodayProgress => Service["Home_TodayProgress"];
    public static string Home_Minutes => Service["Home_Minutes"];
    public static string Home_Sessions => Service["Home_Sessions"];
    public static string Home_StartTraining => Service["Home_StartTraining"];
    public static string Home_QuickExercises => Service["Home_QuickExercises"];
    public static string Home_RecentActivity => Service["Home_RecentActivity"];
    public static string Home_NoActivity => Service["Home_NoActivity"];
    public static string Home_CurrentStreak => Service["Home_CurrentStreak"];
    public static string Home_Days => Service["Home_Days"];
    
    // Exercises
    public static string Exercise_Title => Service["Exercise_Title"];
    public static string Exercise_Subtitle => Service["Exercise_Subtitle"];
    public static string Exercise_TodaysProgress => Service["Exercise_TodaysProgress"];
    public static string Exercise_All => Service["Exercise_All"];
    public static string Exercise_Pitch => Service["Exercise_Pitch"];
    public static string Exercise_Resonance => Service["Exercise_Resonance"];
    public static string Exercise_Intonation => Service["Exercise_Intonation"];
    public static string Exercise_Breathing => Service["Exercise_Breathing"];
    public static string Exercise_Practice => Service["Exercise_Practice"];
    public static string Exercise_Steps => Service["Exercise_Steps"];
    public static string Exercise_StartExercise => Service["Exercise_StartExercise"];
    public static string Exercise_Stop => Service["Exercise_Stop"];
    public static string Exercise_RecommendedTime => Service["Exercise_RecommendedTime"];
    public static string Exercise_Minutes => Service["Exercise_Minutes"];
    public static string Exercise_YourProgress => Service["Exercise_YourProgress"];
    public static string Exercise_Sessions => Service["Exercise_Sessions"];
    public static string Exercise_SessionsCount => Service["Exercise_SessionsCount"];
    public static string Exercise_Average => Service["Exercise_Average"];
    public static string Exercise_Back => Service["Exercise_Back"];
    public static string Exercise_ScientificRationale => Service["Exercise_ScientificRationale"];
    public static string Exercise_TargetPitch => Service["Exercise_TargetPitch"];
    public static string Exercise_Hz => Service["Exercise_Hz"];
    public static string Exercise_Min => Service["Exercise_Min"];
    public static string Exercise_Okter => Service["Exercise_Okter"];
    public static string Exercise_AverageScoreFormat => Service["Exercise_AverageScoreFormat"];
    public static string Exercise_SessionsFormat => Service["Exercise_SessionsFormat"];
    public static string Exercise_DurationFormat => Service["Exercise_DurationFormat"];
    public static string Exercise_ProgressFormat => Service["Exercise_ProgressFormat"];
    public static string Exercise_StepsProgress => Service["Exercise_StepsProgress"];
    public static string Exercise_MinutesFormat => Service["Exercise_MinutesFormat"];
    public static string Exercise_HzFormat => Service["Exercise_HzFormat"];
    public static string Exercise_YourProgressSessions => Service["Exercise_YourProgressSessions"];
    public static string Exercise_HzRange => Service["Exercise_HzRange"];
    public static string Exercise_ZeroSessions => Service["Exercise_ZeroSessions"];
    public static string Exercise_ProgressZero => Service["Exercise_ProgressZero"];
    public static string UI_MinutesShort => Service["UI_MinutesShort"];
    public static string UI_Okter => Service["UI_Okter"];
    public static string Exercise_MinutesOnly => Service["Exercise_MinutesOnly"];
    
    // Analyzer
    public static string Analyzer_Title => Service["Analyzer_Title"];
    public static string Analyzer_StartRecording => Service["Analyzer_StartRecording"];
    public static string Analyzer_StopRecording => Service["Analyzer_StopRecording"];
    public static string Analyzer_PlayRecording => Service["Analyzer_PlayRecording"];
    public static string Analyzer_AveragePitch => Service["Analyzer_AveragePitch"];
    public static string Analyzer_MinPitch => Service["Analyzer_MinPitch"];
    public static string Analyzer_MaxPitch => Service["Analyzer_MaxPitch"];
    public static string Analyzer_PitchVariation => Service["Analyzer_PitchVariation"];
    public static string Analyzer_IntonationScore => Service["Analyzer_IntonationScore"];
    public static string Analyzer_OverallScore => Service["Analyzer_OverallScore"];
    public static string Analyzer_Feedback => Service["Analyzer_Feedback"];
    public static string Analyzer_TargetRange => Service["Analyzer_TargetRange"];
    public static string Analyzer_Recording => Service["Analyzer_Recording"];
    public static string Analyzer_Ready => Service["Analyzer_Ready"];
    
    // Statistics
    public static string Statistics_Title => Service["Statistics_Title"];
    public static string Statistics_ThisWeek => Service["Statistics_ThisWeek"];
    public static string Statistics_ThisMonth => Service["Statistics_ThisMonth"];
    public static string Statistics_AllTime => Service["Statistics_AllTime"];
    public static string Statistics_TotalSessions => Service["Statistics_TotalSessions"];
    public static string Statistics_TotalMinutes => Service["Statistics_TotalMinutes"];
    public static string Statistics_AveragePitch => Service["Statistics_AveragePitch"];
    public static string Statistics_BestStreak => Service["Statistics_BestStreak"];
    public static string Statistics_CurrentStreak => Service["Statistics_CurrentStreak"];
    public static string Statistics_Days => Service["Statistics_Days"];
    public static string Statistics_Calendar => Service["Statistics_Calendar"];
    public static string Statistics_Achievements => Service["Statistics_Achievements"];
    
    // Settings
    public static string Settings_Title => Service["Settings_Title"];
    public static string Settings_Language => Service["Settings_Language"];
    public static string Settings_Norwegian => Service["Settings_Norwegian"];
    public static string Settings_English => Service["Settings_English"];
    public static string Settings_PitchRange => Service["Settings_PitchRange"];
    public static string Settings_MinPitch => Service["Settings_MinPitch"];
    public static string Settings_MaxPitch => Service["Settings_MaxPitch"];
    public static string Settings_VolumeThreshold => Service["Settings_VolumeThreshold"];
    public static string Settings_HearOwnVoice => Service["Settings_HearOwnVoice"];
    public static string Settings_AutoAdvance => Service["Settings_AutoAdvance"];
    public static string Settings_ResetProgress => Service["Settings_ResetProgress"];
    public static string Settings_ResetConfirm => Service["Settings_ResetConfirm"];
    public static string Settings_About => Service["Settings_About"];
    public static string Settings_Version => Service["Settings_Version"];
    
    // Feedback
    public static string Feedback_Excellent => Service["Feedback_Excellent"];
    public static string Feedback_Good => Service["Feedback_Good"];
    public static string Feedback_Nice => Service["Feedback_Nice"];
    public static string Feedback_Completed => Service["Feedback_Completed"];
    public static string Feedback_ExerciseStarted => Service["Feedback_ExerciseStarted"];
    public static string Feedback_TimeReached => Service["Feedback_TimeReached"];
    
    // Tips
    public static string Tip_Beginner => Service["Tip_Beginner"];
    public static string Tip_Intermediate => Service["Tip_Intermediate"];
    public static string Tip_Advanced => Service["Tip_Advanced"];
    public static string Tip_Consistency => Service["Tip_Consistency"];
    public static string Tip_Intonation => Service["Tip_Intonation"];
    
    // Common
    public static string Common_OK => Service["Common_OK"];
    public static string Common_Cancel => Service["Common_Cancel"];
    public static string Common_Save => Service["Common_Save"];
    public static string Common_Delete => Service["Common_Delete"];
    public static string Common_Yes => Service["Common_Yes"];
    public static string Common_No => Service["Common_No"];
    public static string Common_Error => Service["Common_Error"];
    public static string Common_Success => Service["Common_Success"];
    
    // Difficulty
    public static string Difficulty_Beginner => Service["Difficulty_Beginner"];
    public static string Difficulty_Intermediate => Service["Difficulty_Intermediate"];
    public static string Difficulty_Advanced => Service["Difficulty_Advanced"];
    
    // UI Strings
    public static string UI_StartSession => Service["UI_StartSession"];
    public static string UI_StopSession => Service["UI_StopSession"];
    public static string UI_StartRecording => Service["UI_StartRecording"];
    public static string UI_StopRecording => Service["UI_StopRecording"];
    public static string UI_StartExercise => Service["UI_StartExercise"];
    public static string UI_ClearDatabase => Service["UI_ClearDatabase"];
    public static string UI_MicReady => Service["UI_MicReady"];
    public static string UI_MicNotReady => Service["UI_MicNotReady"];
    public static string UI_Recording => Service["UI_Recording"];
    public static string UI_Ready => Service["UI_Ready"];
    public static string UI_CurrentLevel => Service["UI_CurrentLevel"];
    public static string UI_ProgressToNext => Service["UI_ProgressToNext"];
    public static string UI_StopOpptak => Service["UI_StopOpptak"];
    public static string Main_TimeSec => Service["Main_TimeSec"];
    public static string Main_FrequencyHz => Service["Main_FrequencyHz"];
    
    // Frequency
    public static string Frequency_Daily => Service["Frequency_Daily"];
    public static string Frequency_3xWeek => Service["Frequency_3xWeek"];
    public static string Frequency_2xWeek => Service["Frequency_2xWeek"];
    public static string Frequency_Weekly => Service["Frequency_Weekly"];
    
    // Goals
    public static string Goal_Pitch => Service["Goal_Pitch"];
    public static string Goal_Resonance => Service["Goal_Resonance"];
    public static string Goal_Intonation => Service["Goal_Intonation"];
    public static string Goal_Breathing => Service["Goal_Breathing"];
    public static string Goal_Combined => Service["Goal_Combined"];
}
