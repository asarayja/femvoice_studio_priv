using System.Collections.Generic;
using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// FUNCTIONAL Settings page. It is a thin host around <see cref="UiPreferencesViewModel"/>, which holds every
/// working control — theme / language / reduce-motion (applied live), voice-goal style + focus, training frequency,
/// microphone selection (routed to capture), hear-own-voice, accessibility toggles, privacy consent, and REAL
/// database backup / restore / clear — all persisted locally. There are no disabled "deferred" placeholders. This
/// host adds only the page title, the About block (real version), the privacy consent paragraphs, and a safety note.
/// </summary>
public sealed class SettingsViewModel
{
    private readonly System.Action? _openOnboarding;
    private readonly System.Action? _openMicCalibration;
    private readonly FemVoiceStudio.Data.IDatabaseService? _database;

    public SettingsViewModel(System.Action? openOnboarding = null, System.Action? openMicCalibration = null,
        FemVoiceStudio.Data.IDatabaseService? database = null)
    {
        _openOnboarding = openOnboarding;
        _openMicCalibration = openMicCalibration;
        _database = database;

        Title = Localized.Get("Settings_Title", "Innstillinger");
        SafetyNote = Localized.Get("Settings_ScaffoldSafety3",
            "Alle valg lagres lokalt på denne maskinen · ingen klinisk endring.");
    }

    private UiPreferencesViewModel? _preferences;
    /// <summary>The functional preferences editor (lazily constructed so no file I/O or device enumeration happens
    /// until the Settings page is actually shown).</summary>
    public UiPreferencesViewModel Preferences =>
        _preferences ??= new UiPreferencesViewModel(null, _openOnboarding, _openMicCalibration, _database);

    public string Title { get; }
    public string SafetyNote { get; }

    // ── About (real, read-only) ───────────────────────────────────────────────────────────────────────────────────
    public string AboutHeading => Localized.Get("Settings_About", "Om");
    public string AppName => "FemVoice Studio";
    public string VersionLabel => Localized.Get("Settings_Version", "Versjon");
    // Read from the HEAD assembly, not this shared library: the library carries no <Version> and reported the SDK
    // default 1.0.0, so About showed "1.0.0" while the app was 0.1.6.
    public string VersionValue => FemVoice.Avalonia.AppVersion.Current;
    public string PlatformValue => "Avalonia · Windows / macOS / Linux / Android / iOS";

    // ── Privacy consent paragraphs (informational; real shared Privacy_* keys) ────────────────────────────────────
    public string ConsentHeading => Localized.Get("Privacy_Title", "Personvern");
    public IReadOnlyList<string> ConsentParagraphs { get; } = new[]
    {
        Localized.Get("Privacy_DiagnosticsConsent", "Diagnostikk eksporteres bare når du selv aktiverer det."),
        Localized.Get("Privacy_ResearchWarning", "Forskningsdata anonymiseres som standard."),
        Localized.Get("Privacy_ProfessionalNotesWarning", "Profesjonelle notater kan inneholde sensitiv fritekst."),
    };
}
