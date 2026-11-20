using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace FemVoice.Tests.Portable
{
    /// <summary>
    /// Keeps the test run away from the user's REAL data. The code under test resolves per-user folders in many places
    /// (the persisted Core language, logs, calibration profiles, settings…). Before this, a plain <c>dotnet test</c>
    /// left <c>~/.local/share/FemVoiceStudio/language.txt</c> set to "en" — which then made 22 of the app's headless
    /// smokes fail — and appended to a log under the real Documents folder.
    ///
    /// On Linux/macOS .NET resolves those folders from the XDG_* variables on every call, so pointing them at a private
    /// temp folder for this test process is enough. It runs as a module initializer, i.e. before any test (or static
    /// constructor of the code under test) can touch a path. No-op on Windows, where these folders are not
    /// environment-driven.
    /// </summary>
    internal static class TestUserDataSandbox
    {
#pragma warning disable CA2255 // a module initializer is exactly what is wanted in a test assembly
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RedirectUserFolders()
        {
            if (!(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())) return;
            try
            {
                string root = Path.Combine(Path.GetTempPath(), $"femvoice-tests-{Environment.ProcessId}-{Guid.NewGuid():N}");
                foreach (var (variable, sub) in new[] { ("XDG_DATA_HOME", "data"), ("XDG_DOCUMENTS_DIR", "docs"), ("XDG_CONFIG_HOME", "config") })
                {
                    string dir = Path.Combine(root, sub);
                    Directory.CreateDirectory(dir);
                    Environment.SetEnvironmentVariable(variable, dir);
                }
                AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { Directory.Delete(root, true); } catch { } };
            }
            catch { /* an unsandboxed run is still a valid test run */ }
        }
    }
}
