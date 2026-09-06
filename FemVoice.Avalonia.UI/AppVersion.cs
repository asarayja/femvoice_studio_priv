using System;
using System.Reflection;

namespace FemVoice.Avalonia;

/// <summary>
/// The RUNNING APPLICATION's version, for anything user-facing (Settings → About, Diagnostics, support reports).
///
/// WHY THIS EXISTS: the view-models live in the shared FemVoice.Avalonia.UI library, which carries no
/// &lt;Version&gt; and therefore reports the SDK default 1.0.0. Reading
/// <c>typeof(SomeViewModel).Assembly.GetName().Version</c> — as Settings and Diagnostics both did — showed users
/// "1.0.0" while the app was actually 0.1.6. The version belongs to the HEAD (FemVoice.Studio / the Android or iOS
/// app), so it must be read from the entry assembly.
///
/// Resolution order: entry assembly's informational version (build metadata after '+' trimmed) → entry assembly
/// version → this library's own version as a last resort. Never throws.
/// </summary>
public static class AppVersion
{
    /// <summary>Display version of the running app, e.g. "0.1.6". Computed once.</summary>
    public static string Current { get; } = From(Assembly.GetEntryAssembly());

    /// <summary>Resolve a display version from <paramref name="assembly"/>. Pure, so it is unit-testable.</summary>
    public static string From(Assembly? assembly)
    {
        try
        {
            if (assembly is not null)
            {
                // Prefer the NUMERIC assembly version: it comes straight from <Version> and cannot drift into prose.
                // (This project previously shipped an InformationalVersion of
                // "0.1.0-dev (Avalonia desktop preview — display-only)" — stale text that would be shown to users.)
                var version = assembly.GetName().Version;
                if (version is not null && version != new Version(0, 0, 0, 0)) return version.ToString(3);

                // Only fall back to the informational string, and only when it actually looks like a version.
                var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(informational) && char.IsDigit(informational[0]))
                {
                    int plus = informational.IndexOf('+');   // strip source-link build metadata ("0.1.6+abc123")
                    return plus > 0 ? informational.Substring(0, plus) : informational;
                }
            }
        }
        catch { /* fall through to the library version */ }

        // No entry assembly (possible on some mobile hosts) → the shared library's own version. Honest, not a lie.
        return typeof(AppVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
