using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Xunit;

namespace FemVoiceStudio.Tests;

public sealed class FrontPageProgressTests
{
    [Fact]
    public void FrontPageProgress_LoadsFromExistingSessions()
    {
        var status = Status(
            level: DifficultyLevel.Middels,
            sessionsAtLevel: 2,
            totalSessions: 9);

        var snapshot = MainViewModel.BuildFrontPageProgressSnapshot(status);

        Assert.Equal(40.0, snapshot.ProgressPercentage);
        Assert.Equal("2/5", snapshot.CounterText);
        Assert.False(snapshot.HasInsufficientData);
    }

    [Fact]
    public void FrontPageProgress_UpdatesAfterCompletedSession()
    {
        var db = new TestDatabaseService();
        db.UpdateUserSettings(new UserSettings
        {
            CurrentDifficulty = DifficultyLevel.Nybegynner,
            SessionsAtCurrentLevel = 0,
            TotalSessionsCompleted = 0,
            AutoAdvanceLevel = true
        });

        var progression = new ProgressionService(db);
        progression.EvaluateProgressionWithSafety(Session(score: 82));

        var snapshot = MainViewModel.BuildFrontPageProgressSnapshot(progression.GetProgressionStatus());

        Assert.Equal(20.0, snapshot.ProgressPercentage);
        Assert.Equal("1/5", snapshot.CounterText);
        Assert.False(snapshot.HasInsufficientData);
    }

    [Fact]
    public void FrontPageCurrentLevel_LoadsFromAnalytics()
    {
        LocalizationService.Instance.SetLanguage("nb-NO");
        var snapshot = MainViewModel.BuildFrontPageProgressSnapshot(Status(
            level: DifficultyLevel.Middels,
            sessionsAtLevel: 1,
            totalSessions: 4));

        Assert.Equal(DifficultyLevel.Middels, snapshot.CurrentLevel);
        Assert.Equal("Middels", snapshot.CurrentLevelText);
    }

    [Fact]
    public void FrontPageCurrentLevel_UpdatesAfterCompletedSession()
    {
        LocalizationService.Instance.SetLanguage("nb-NO");
        var db = new TestDatabaseService();
        db.UpdateUserSettings(new UserSettings
        {
            CurrentDifficulty = DifficultyLevel.Nybegynner,
            SessionsAtCurrentLevel = 4,
            TotalSessionsCompleted = 4,
            AutoAdvanceLevel = true
        });

        var progression = new ProgressionService(db);
        progression.EvaluateProgressionWithSafety(Session(score: 90));

        var snapshot = MainViewModel.BuildFrontPageProgressSnapshot(progression.GetProgressionStatus());

        Assert.Equal(DifficultyLevel.Middels, snapshot.CurrentLevel);
        Assert.Equal("Middels", snapshot.CurrentLevelText);
        Assert.Equal("0/5", snapshot.CounterText);
    }

    [Fact]
    public void FrontPageProgress_RaisesPropertyChanged()
    {
        var source = ReadSource("FemVoiceStudio", "ViewModels", "MainViewModel.cs");

        Assert.Contains("private double _frontPageProgressPercentage;", source);
        Assert.Contains("FrontPageProgressPercentage = snapshot.ProgressPercentage;", source);
    }

    [Fact]
    public void FrontPageCurrentLevel_RaisesPropertyChanged()
    {
        var source = ReadSource("FemVoiceStudio", "ViewModels", "MainViewModel.cs");

        Assert.Contains("private DifficultyLevel _frontPageCurrentLevel", source);
        Assert.Contains("private string _frontPageCurrentLevelText", source);
        Assert.Contains("FrontPageCurrentLevel = snapshot.CurrentLevel;", source);
        Assert.Contains("FrontPageCurrentLevelText = snapshot.CurrentLevelText;", source);
    }

    [Fact]
    public void FrontPageProgress_NotPlaceholderWhenDataExists()
    {
        var snapshot = MainViewModel.BuildFrontPageProgressSnapshot(Status(
            level: DifficultyLevel.Nybegynner,
            sessionsAtLevel: 3,
            totalSessions: 3));

        Assert.False(snapshot.HasInsufficientData);
        Assert.DoesNotContain("Insight_InsufficientData", snapshot.StateText);
        Assert.Contains("3", snapshot.StateText);
    }

    [Fact]
    public void FrontPageCurrentLevel_NotPlaceholderWhenDataExists()
    {
        LocalizationService.Instance.SetLanguage("nb-NO");
        var snapshot = MainViewModel.BuildFrontPageProgressSnapshot(Status(
            level: DifficultyLevel.Avansert,
            sessionsAtLevel: 2,
            totalSessions: 12));

        Assert.Equal(DifficultyLevel.Avansert, snapshot.CurrentLevel);
        Assert.Equal("Avansert", snapshot.CurrentLevelText);
        Assert.DoesNotContain("CurrentLevel", snapshot.CurrentLevelText);
    }

    [Fact]
    public void FrontPageProgress_ShowsInsufficientDataWhenNeeded()
    {
        LocalizationService.Instance.SetLanguage("nb-NO");
        var snapshot = MainViewModel.BuildFrontPageProgressSnapshot(Status(
            level: DifficultyLevel.Nybegynner,
            sessionsAtLevel: 0,
            totalSessions: 0));

        Assert.True(snapshot.HasInsufficientData);
        Assert.Contains("ikke nok data", snapshot.StateText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FrontPageCurrentLevel_ShowsInsufficientDataWhenNeeded()
    {
        LocalizationService.Instance.SetLanguage("nb-NO");
        var snapshot = MainViewModel.BuildFrontPageProgressSnapshot(Status(
            level: DifficultyLevel.Nybegynner,
            sessionsAtLevel: 0,
            totalSessions: 0));

        Assert.Equal(DifficultyLevel.Nybegynner, snapshot.CurrentLevel);
        Assert.Equal("Nybegynner", snapshot.CurrentLevelText);
        Assert.True(snapshot.HasInsufficientData);
    }

    private static ProgressionStatus Status(
        DifficultyLevel level,
        int sessionsAtLevel,
        int totalSessions,
        int required = 5)
        => new()
        {
            CurrentLevel = level,
            SessionsAtCurrentLevel = sessionsAtLevel,
            SessionsRequiredForPromotion = required,
            TotalSessions = totalSessions,
            CurrentStreak = totalSessions > 0 ? 1 : 0
        };

    private static TrainingSession Session(double score)
        => new()
        {
            StartTime = DateTime.Now.AddMinutes(-10),
            EndTime = DateTime.Now,
            OverallScore = score,
            AveragePitch = 190,
            PitchVariation = 10,
            DifficultyLevel = DifficultyLevel.Nybegynner
        };

    private static string ReadSource(params string[] segments)
        => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FemVoiceStudio.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
