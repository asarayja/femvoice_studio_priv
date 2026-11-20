using FemVoice.Avalonia.Localization;
using FemVoiceStudio.Services.VoiceLoad;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Turns the time-based rest advice (<see cref="PracticeLoadGuard"/>) plus the exercise's own recommended duration
/// into the ONE line shown in the rest banner. Empty string = nothing to show. The wording is deliberately about
/// TIME ("you have practised a while"), never about strain or stability — nothing here measures those.
/// </summary>
public static class RestAdviceDisplay
{
    public static string Text(RestAdviceLevel level, bool durationReached)
    {
        if (level == RestAdviceLevel.Now)
            return Localized.Get("Rest_PauseNow", "Du har øvd lenge uten pause. Ta en pause nå og la stemmen hvile.");
        if (durationReached)
            return Localized.Get("Rest_DurationReached",
                "Du har nådd anbefalt varighet for denne øvelsen. Avslutt gjerne her eller ta en pause.");
        if (level == RestAdviceLevel.Soon)
            return Localized.Get("VoiceLoad_PauseSoon", "Du har øvd en stund. Ta gjerne en kort pause snart.");
        return "";
    }
}
