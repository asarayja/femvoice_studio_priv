using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public sealed class PackagingReadinessTests
    {
        [Fact]
        public void SettingsAndDiagnosticsPaths_AreStableUserPaths_NotDebugOrTemp()
        {
            Assert.EndsWith(
                Path.Combine("Documents", "FemVoiceStudio", "settings.json"),
                ThemeManager.SettingsPath,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                Path.Combine("FemVoiceStudio", "Diagnostics"),
                DiagnosticsNaming.PrimaryRoot,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                DiagnosticsNaming.PrimaryRoot,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                Path.Combine("FemVoiceStudio", "RuntimeDiagnostics"),
                Rc0RuntimeLog.CurrentLogPath,
                StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain(Path.GetTempPath(), ThemeManager.SettingsPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Path.GetTempPath(), DiagnosticsNaming.PrimaryRoot, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Debug", ThemeManager.SettingsPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CodexBuild", ThemeManager.SettingsPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DiagnosticsRuntimeLog_RecreatesMissingFolder()
        {
            Rc0RuntimeLog.Write("PackagingReadiness", "folder creation smoke test");

            Assert.True(Directory.Exists(Path.GetDirectoryName(Rc0RuntimeLog.CurrentLogPath)!));
        }

        [Fact]
        public void Project_DeclaresSplashLogoAsPackagedOutputContent()
        {
            var projectPath = FindRepoFile(Path.Combine("FemVoiceStudio", "FemVoiceStudio.csproj"));
            var doc = XDocument.Load(projectPath);

            var logoContent = doc
                .Descendants("Content")
                .SingleOrDefault(e =>
                    string.Equals((string?)e.Attribute("Link"), "logo.png", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(logoContent);
            Assert.Equal("..\\logo.png", (string?)logoContent!.Attribute("Include"));
            Assert.Equal("PreserveNewest", (string?)logoContent.Attribute("CopyToOutputDirectory"));
        }

        [Fact]
        public void PackagedOutput_ContainsSplashLogoAfterBuild()
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "logo.png");
            Assert.True(File.Exists(logoPath), $"Expected packaged logo at {logoPath}");
            Assert.True(new FileInfo(logoPath).Length > 0);
        }

        [Fact]
        public void RuntimeSource_DoesNotDependOnCodexOrValidationHarnessPaths()
        {
            var sourceRoot = FindRepoDirectory("FemVoiceStudio");
            var forbidden = new[]
            {
                "CodexBuild",
                "FemVoiceRcCloseoutValidation",
                "FemVoiceRcCloseoutOutput",
                "AppData\\Local\\Temp",
                "..\", \"..\", \"..\", \"logo.png"
            };

            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !path.EndsWith(".old", StringComparison.OrdinalIgnoreCase)))
            {
                var text = File.ReadAllText(file);
                foreach (var token in forbidden)
                    Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ReportExportRuntime_DoesNotForceTempOrDebugOutputPath()
        {
            var file = FindRepoFile(Path.Combine("FemVoiceStudio", "ViewModels", "ReportExportViewModel.cs"));
            var text = File.ReadAllText(file);

            Assert.Contains("SaveFileDialog", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Path.GetTempPath", text, StringComparison.Ordinal);
            Assert.DoesNotContain("CodexBuild", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Debug", text, StringComparison.OrdinalIgnoreCase);
        }

        private static string FindRepoFile(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new FileNotFoundException($"Could not find {relativePath} from test output path.");
        }

        private static string FindRepoDirectory(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException($"Could not find {relativePath} from test output path.");
        }
    }
}
