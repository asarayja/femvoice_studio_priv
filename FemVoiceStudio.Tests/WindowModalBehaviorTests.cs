using System.Text.RegularExpressions;
using Xunit;

namespace FemVoiceStudio.Tests;

public sealed class WindowModalBehaviorTests
{
    private static readonly string[] MainWindowModelessFields =
    [
        "_calendarWindow",
        "_statisticsWindow",
        "_exerciseWindow",
        "_analyzerWindow",
        "_smartCoachWindow",
        "_resonanceWindow",
        "_progressionWindow",
        "_analysisWindow",
        "_settingsWindow",
        "_clinicianDashboardWindow",
        "_coachDashboardWindow",
        "_reportExportWindow",
        "_manualOverrideWindow",
        "_caseReviewWindow"
    ];

    [Fact]
    public void WindowService_InformationalWindows_AreModeless()
    {
        var mainWindow = ReadSource("FemVoiceStudio", "Views", "MainWindow.xaml.cs");
        var calendarViewModel = ReadSource("FemVoiceStudio", "ViewModels", "CalendarViewModel.cs");
        var settingsWindow = ReadSource("FemVoiceStudio", "Views", "SettingsWindow.xaml.cs");

        Assert.DoesNotContain("ShowDialog(", mainWindow);
        Assert.Contains("ShowOrActivateModelessWindow", mainWindow);
        Assert.Contains("_dayDetailsWindow.Show();", calendarViewModel);
        Assert.DoesNotContain("window.ShowDialog();", calendarViewModel);
        Assert.Contains("_microphoneCalibrationWindow.Show();", settingsWindow);
        Assert.DoesNotContain("window.ShowDialog();", settingsWindow);
    }

    [Fact]
    public void WindowService_FocusExistingWindowInsteadOfDuplicate()
    {
        var mainWindow = ReadSource("FemVoiceStudio", "Views", "MainWindow.xaml.cs");

        Assert.Contains("current is { IsVisible: true }", mainWindow);
        Assert.Contains("RestoreAndFocus(current)", mainWindow);
        foreach (var field in MainWindowModelessFields)
        {
            Assert.Contains(field, mainWindow);
        }

        var calls = Regex.Matches(mainWindow, "ShowOrActivateModelessWindow\\(").Count;
        Assert.True(calls >= MainWindowModelessFields.Length);
    }

    [Fact]
    public void WindowService_MainWindowNotDisabledForHelperWindows()
    {
        var mainWindow = ReadSource("FemVoiceStudio", "Views", "MainWindow.xaml.cs");

        Assert.DoesNotContain("IsEnabled = false", mainWindow);
        Assert.DoesNotContain("IsEnabled=false", mainWindow);
    }

    [Fact]
    public void WindowService_DestructiveConfirmationsRemainModal()
    {
        var settingsWindow = ReadSource("FemVoiceStudio", "Views", "SettingsWindow.xaml.cs");

        Assert.Contains("Settings_ResetDatabaseConfirmMessage", settingsWindow);
        Assert.Contains("Settings_RestoreConfirmMessage", settingsWindow);
        Assert.Contains("MessageBox.Show", settingsWindow);
        Assert.Contains("new OpenFileDialog", settingsWindow);
        Assert.Contains("dialog.ShowDialog(this)", settingsWindow);
    }

    [Fact]
    public void WindowService_PrivacyConsentRemainsModalIfRequired()
    {
        var app = ReadSource("FemVoiceStudio", "App.xaml.cs");

        Assert.Contains("FirstTimeSetupWindow", app);
        Assert.Contains("setupWindow.ShowDialog()", app);
    }

    [Fact]
    public void WindowService_ChildWindowsCloseWithMainWindow()
    {
        var mainWindow = ReadSource("FemVoiceStudio", "Views", "MainWindow.xaml.cs");

        Assert.Contains("Closing += OnWindowClosing", mainWindow);
        Assert.Contains("CloseModelessChildWindows();", mainWindow);
        foreach (var field in MainWindowModelessFields)
        {
            Assert.Contains(field, mainWindow);
        }
    }

    [Fact]
    public void ReportExportWindow_UsesUsableResizableLayout()
    {
        var xaml = ReadSource("FemVoiceStudio", "Views", "ReportExportWindow.xaml");

        Assert.Contains("Width=\"540\"", xaml);
        Assert.Contains("Height=\"460\"", xaml);
        Assert.Contains("MinWidth=\"520\"", xaml);
        Assert.Contains("MinHeight=\"430\"", xaml);
        Assert.Contains("ResizeMode=\"CanResize\"", xaml);
        Assert.DoesNotContain("ResizeMode=\"NoResize\"", xaml);
    }

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
