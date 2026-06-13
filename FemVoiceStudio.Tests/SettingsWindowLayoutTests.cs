using System.Text.RegularExpressions;
using Xunit;

namespace FemVoiceStudio.Tests;

public sealed class SettingsWindowLayoutTests
{
    [Fact]
    public void SettingsWindow_UsesUsableResizableDefaultSize()
    {
        var xaml = ReadView("SettingsWindow.xaml");

        Assert.Contains("Height=\"720\" Width=\"720\"", xaml);
        Assert.Contains("MinHeight=\"560\" MinWidth=\"600\"", xaml);
        Assert.Contains("ResizeMode=\"CanResize\"", xaml);
        Assert.DoesNotContain("ResizeMode=\"NoResize\"", xaml);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", xaml);
    }

    [Fact]
    public void SettingsWindow_DatabaseButtonsWrapInsteadOfClippingNorwegianLabels()
    {
        var xaml = ReadView("SettingsWindow.xaml");

        Assert.Contains("<WrapPanel Margin=\"0,12,0,0\">", xaml);
        Assert.Contains("Content=\"{loc:Loc Settings_CreateBackup}\"", xaml);
        Assert.Contains("Content=\"{loc:Loc Settings_RestoreBackup}\"", xaml);
        Assert.Contains("MinWidth=\"190\"", xaml);
        Assert.Contains("MinWidth=\"230\"", xaml);
        Assert.DoesNotContain("<StackPanel Orientation=\"Horizontal\" Margin=\"0,12,0,0\">", xaml);
    }

    [Fact]
    public void SettingsWindow_SelectionControlsAvoidFixedNarrowWidths()
    {
        var xaml = ReadView("SettingsWindow.xaml");

        Assert.DoesNotMatch(new Regex("ComboBox[^>]+Width=\"(?:200|230)\"", RegexOptions.Singleline), xaml);
        Assert.True(Regex.Matches(xaml, "ComboBox[^>]+MinWidth=\"260\"", RegexOptions.Singleline).Count >= 4);
        Assert.True(Regex.Matches(xaml, "ComboBox[^>]+MaxWidth=\"360\"", RegexOptions.Singleline).Count >= 4);
    }

    private static string ReadView(string fileName)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), "FemVoiceStudio", "Views", fileName));

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
