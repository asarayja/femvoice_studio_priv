using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Audio.Abstractions;   // AudioCaptureBackendFactory, AudioInputDevice
using FemVoice.Avalonia.Data;              // SettingsDataService (real backup/restore/clear)
using FemVoice.Avalonia.Localization;      // ScaffoldStrings.Cultures (Avalonia-owned culture list; no WPF)
using FemVoice.Avalonia.Preferences;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// FUNCTIONAL local settings editor. Binds every preference the Settings page shows and persists it to the
/// Avalonia-local <see cref="UiPreferencesStore"/>: theme, language and reduce-motion (applied LIVE on Save),
/// voice-goal style + focus, training frequency, the chosen microphone (routed to capture via
/// <see cref="UiPreferences.MicDeviceId"/>), hear-own-voice, accessibility toggles, and privacy consent. It also
/// drives the REAL database backup / restore / clear via <see cref="SettingsDataService"/> (with inline
/// confirmation), and exposes working action buttons (re-run onboarding, open mic calibration). Nothing here is a
/// disabled placeholder.
/// </summary>
public partial class UiPreferencesViewModel : ObservableObject
{
    private readonly UiPreferencesStore _store;
    private readonly SettingsDataService _data;

    private readonly Action? _openOnboarding;
    private readonly Action? _openMicCalibration;

    public UiPreferencesViewModel(UiPreferencesStore? store = null,
        Action? openOnboarding = null, Action? openMicCalibration = null,
        FemVoiceStudio.Data.IDatabaseService? database = null, SettingsDataService? dataService = null)
    {
        _store = store ?? new UiPreferencesStore();
        _data = dataService ?? new SettingsDataService(database);
        _openOnboarding = openOnboarding;
        _openMicCalibration = openMicCalibration;

        MicDeviceOptions = EnumerateMicDevices();
        RefreshBackups();

        var p = _store.Load();
        _theme = p.Theme;
        _language = p.Language;
        _reduceMotion = p.ReduceMotion;
        _setupCompleted = p.FirstTimeSetupCompleted;   // preserved verbatim on Save (not edited here)
        _selectedStyle = StyleOptions.FirstOrDefault(s => s.Token == p.VoiceGoalStyle) ?? StyleOptions[0];
        _selectedFocus = FocusOptions.FirstOrDefault(f => f.Token == p.VoiceGoalFocus) ?? FocusOptions[0];
        _selectedFrequency = FrequencyOptions.FirstOrDefault(f => f.Value == p.TrainingFrequency) ?? FrequencyOptions[1];
        _selectedMicDevice = MicDeviceOptions.FirstOrDefault(d => d.Id == p.MicDeviceId) ?? MicDeviceOptions[0];
        _hearOwnVoice = p.HearOwnVoice;
        _stressSensitive = p.StressSensitiveMode;
        _reducedVisualFeedback = p.ReducedVisualFeedback;
        _diagnosticsConsent = p.DiagnosticsConsent;
        _researchConsent = p.ResearchSharingConsent;
        _remindersEnabled = p.RemindersEnabled;
        _selectedReminderTime = MatchReminderTime(p.ReminderMinuteOfDay);
        _resonanceBaselineCentroidHz = p.ResonanceBaselineCentroidHz;   // not edited here; round-tripped so Save doesn't wipe it
    }

    // The mic-calibration resonance baseline is owned by MicCalibration, not this panel; kept verbatim so a Settings
    // Save never clears the per-user calibration (would otherwise reset the perceived-voice/resonance meter).
    private double _resonanceBaselineCentroidHz;

    // Map a stored minute-of-day to a preset option; a hand-edited/off-grid value falls back to 18:00.
    private ReminderTimeOption MatchReminderTime(int minuteOfDay)
        => ReminderTimeOptions.FirstOrDefault(t => t.MinuteOfDay == minuteOfDay)
           ?? ReminderTimeOptions.First(t => t.MinuteOfDay == 1080);

    // ── Voice-goal style + focus + training frequency (real persisted prefs, WPF Settings parity) ──────────────────
    public sealed record StyleOption(string Token, string Label) { public override string ToString() => Label; }
    public sealed record FocusOption(string Token, string Label) { public override string ToString() => Label; }
    public sealed record FrequencyOption(int Value, string Label) { public override string ToString() => Label; }
    public IReadOnlyList<StyleOption> StyleOptions { get; } = new[]
    {
        new StyleOption("soft_feminine", Localized.Get("VoiceGoalStyle_SoftFeminine", "Myk feminin")),
        new StyleOption("bright_neutral", Localized.Get("VoiceGoalStyle_BrightNeutral", "Lys nøytral")),
        new StyleOption("androgynous", Localized.Get("VoiceGoalStyle_Androgynous", "Androgyn")),
        new StyleOption("custom", Localized.Get("VoiceGoalStyle_Custom", "Egendefinert")),
    };
    public IReadOnlyList<FocusOption> FocusOptions { get; } = new[]
    {
        new FocusOption("balanced", Localized.Get("VoiceGoalFocus_Balanced", "Balansert")),
        new FocusOption("pitch", Localized.Get("Goal_Pitch", "Tonehøyde")),
        new FocusOption("resonance", Localized.Get("Goal_Resonance", "Resonans")),
        new FocusOption("intonation", Localized.Get("Goal_Intonation", "Intonasjon")),
    };
    public IReadOnlyList<FrequencyOption> FrequencyOptions { get; } = new[]
    {
        new FrequencyOption(2, Localized.Get("Settings_Accessibility_Frequency2", "2 dager")),
        new FrequencyOption(3, Localized.Get("Settings_Accessibility_Frequency3", "3 dager (anbefalt)")),
        new FrequencyOption(4, Localized.Get("Settings_Accessibility_Frequency4", "4 dager")),
        new FrequencyOption(5, Localized.Get("Settings_Accessibility_Frequency5", "5 eller flere dager")),
    };
    [ObservableProperty] private StyleOption _selectedStyle;
    [ObservableProperty] private FocusOption _selectedFocus;
    [ObservableProperty] private FrequencyOption _selectedFrequency;
    public string StyleLabel => Localized.Get("Settings_VoiceGoalStyle", "Stil");
    public string FocusLabel => Localized.Get("Settings_VoiceGoalFocus", "Fokus");
    public string FrequencyLabel => Localized.Get("Settings_TrainingFrequency", "Treningsfrekvens");

    // ── Microphone device (real enumeration + persisted routing) ──────────────────────────────────────────────────
    public sealed record MicDeviceOption(string? Id, string Label) { public override string ToString() => Label; }
    public IReadOnlyList<MicDeviceOption> MicDeviceOptions { get; }
    [ObservableProperty] private MicDeviceOption _selectedMicDevice;
    public string MicDeviceLabel => Localized.Get("Settings_Microphone", "Mikrofon");

    private static IReadOnlyList<MicDeviceOption> EnumerateMicDevices()
    {
        var list = new List<MicDeviceOption> { new(null, Localized.Get("Settings_MicDefault", "Systemstandard")) };
        try
        {
            var probe = AudioCaptureBackendFactory.CreateReal();
            foreach (var d in probe.GetInputDevices())
                if (!string.IsNullOrWhiteSpace(d.Id) && d.Id != "default")
                    list.Add(new MicDeviceOption(d.Id, d.Name));
            (probe as IDisposable)?.Dispose();
        }
        catch { /* enumeration is best-effort; the default entry always remains */ }
        return list;
    }

    // ── Daily training reminder (opt-in in-app nudge; time-of-day preset) ─────────────────────────────────────────
    public sealed record ReminderTimeOption(int MinuteOfDay, string Label) { public override string ToString() => Label; }
    public IReadOnlyList<ReminderTimeOption> ReminderTimeOptions { get; } = new[]
    {
        8 * 60, 12 * 60, 15 * 60, 17 * 60, 18 * 60, 19 * 60, 20 * 60, 21 * 60,
    }.Select(m => new ReminderTimeOption(m, $"{m / 60:00}:{m % 60:00}")).ToArray();
    [ObservableProperty] private bool _remindersEnabled;
    [ObservableProperty] private ReminderTimeOption _selectedReminderTime;
    public string RemindersHeading => Localized.Get("Settings_Reminders_Title", "Påminnelser");
    public string RemindersEnabledLabel => Localized.Get("Settings_Reminders_Enable", "Daglig påminnelse om å øve");
    public string ReminderTimeLabel => Localized.Get("Settings_Reminders_Time", "Påminn meg klokka");
    public string RemindersNote => Localized.Get("Settings_Reminders_Note",
        "Viser en vennlig påminnelse på forsiden når dagens økt gjenstår — kun etter valgt tidspunkt, aldri to ganger samme dag, og aldri når du har nådd ukemålet ditt.");

    // ── Hear own voice + accessibility + privacy (persisted prefs) ────────────────────────────────────────────────
    [ObservableProperty] private bool _hearOwnVoice;
    [ObservableProperty] private bool _stressSensitive;
    [ObservableProperty] private bool _reducedVisualFeedback;
    [ObservableProperty] private bool _diagnosticsConsent;
    [ObservableProperty] private bool _researchConsent;
    public string HearOwnVoiceLabel => Localized.Get("Settings_HearOwnVoice", "Hør egen stemme");
    public string StressSensitiveLabel => Localized.Get("Settings_Accessibility_StressSensitive", "Stressømfintlig modus");
    public string ReducedVisualLabel => Localized.Get("Settings_ReducedVisualFeedback", "Redusert visuell tilbakemelding");
    public string DiagnosticsConsentLabel => Localized.Get("Settings_Scaffold_PrivacyDiagnostics", "Diagnostikk-samtykke");
    public string ResearchConsentLabel => Localized.Get("Settings_Scaffold_PrivacyResearch", "Forskningsdeling (anonymisert)");

    // Working action buttons (WPF Settings has these; the Avalonia placeholders did nothing).
    public string RerunSetupLabel => Localized.Get("Settings_FirstRun", "Kjør førstegangsoppsett på nytt");
    public string OpenMicCalLabel => Localized.Get("MicCalibration_Open", "Åpne mikrofonkalibrering");
    public bool HasActions => _openOnboarding is not null || _openMicCalibration is not null;
    [RelayCommand] private void RerunSetup() => _openOnboarding?.Invoke();
    [RelayCommand] private void OpenMicCalibration() => _openMicCalibration?.Invoke();

    // Onboarding-completed flag is owned by FirstTimeSetup; this panel only round-trips it so a later Save here
    // does not wipe it.
    private readonly bool _setupCompleted;

    // Display-only option lists (Avalonia-owned; no WPF LocalizationService).
    public IReadOnlyList<ThemePreference> ThemeOptions { get; } =
        new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark };
    public IReadOnlyList<string> LanguageOptions { get; } = ScaffoldStrings.Cultures;

    [ObservableProperty] private ThemePreference _theme;
    [ObservableProperty] private string _language;
    [ObservableProperty] private bool _reduceMotion;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    /// <summary>Converter-free visibility flag for the status line.</summary>
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    public string Heading => Localized.Get("Settings_LocalPrefs_Title", "Lokale innstillinger");
    public string Note => Localized.Get("Settings_LocalPrefs_Note2",
        "Alle valg lagres lokalt på denne maskinen. Tema, språk og bevegelse brukes med en gang " +
        "(kun oversatt tekst følger språket). Mikrofonvalget brukes neste gang du starter en økt.");
    public string SaveLabel => Localized.Get("Settings_LocalPrefs_Save", "Lagre");
    public string ReloadLabel => Localized.Get("Settings_LocalPrefs_Reload", "Last på nytt");
    public string ThemeLabel => Localized.Get("Settings_ThemePreference", "Tema");
    public string LanguageLabel => Localized.Get("Settings_LanguageRow", "Språk");
    public string ReduceMotionLabel => Localized.Get("Settings_LocalPrefs_ReduceMotion", "Reduser bevegelse");

    /// <summary>The Avalonia-local file these preferences persist to (shown for transparency).</summary>
    public string FilePath => _store.FilePath;

    // ── Section headings (functional cards) ───────────────────────────────────────────────────────────────────────
    public string AudioHeading => Localized.Get("Settings_Scaffold_Audio", "Lyd og mikrofon");
    public string GoalHeading => Localized.Get("Settings_VoiceGoalTitle", "Øvelsespreferanser");
    public string AccessibilityHeading => Localized.Get("Settings_Accessibility_Title", "Tilgjengelighet");
    public string PrivacyHeading => Localized.Get("Privacy_Title", "Personvern");
    public string HearOwnVoiceNote => Localized.Get("Settings_HearOwnVoiceNote2",
        "Spiller mikrofonen din tilbake til høyttaleren mens du øver. Bruk hodetelefoner for å unngå tilbakekobling/hyl.");

    /// <summary>Current edited values as a model (no I/O).</summary>
    public UiPreferences Current() => new()
    {
        Theme = Theme, Language = Language, ReduceMotion = ReduceMotion, FirstTimeSetupCompleted = _setupCompleted,
        VoiceGoalStyle = SelectedStyle?.Token ?? "soft_feminine", TrainingFrequency = SelectedFrequency?.Value ?? 3,
        VoiceGoalFocus = SelectedFocus?.Token ?? "balanced",
        MicDeviceId = SelectedMicDevice?.Id,
        HearOwnVoice = HearOwnVoice,
        StressSensitiveMode = StressSensitive,
        ReducedVisualFeedback = ReducedVisualFeedback,
        DiagnosticsConsent = DiagnosticsConsent,
        ResearchSharingConsent = ResearchConsent,
        RemindersEnabled = RemindersEnabled,
        ReminderMinuteOfDay = SelectedReminderTime?.MinuteOfDay ?? 1080,
        ResonanceBaselineCentroidHz = _resonanceBaselineCentroidHz,
    };

    // Persist, then apply THEME + LANGUAGE + reduce-motion LIVE. Other prefs (mic device, focus, hear-own-voice,
    // accessibility, consent) are stored and read by their consumers (capture pipeline / live visuals). Fail-safe.
    [RelayCommand]
    private void Save()
    {
        bool ok = _store.Save(Current());
        if (ok)
        {
            FemVoice.Avalonia.Theming.ThemeActivation.Apply(Theme);                  // theme — live
            FemVoice.Avalonia.Localization.LanguageActivation.Apply(Language);       // language — live (raises LanguageChanged)
            FemVoice.Avalonia.Accessibility.MotionActivation.Apply(ReduceMotion);    // reduce-motion — live (Avalonia motion state)
        }
        Status = ok
            ? Localized.Get("Settings_LocalPrefs_Saved2",
                "Lagret. Tema, språk og bevegelse er oppdatert; mikrofon og øvrige valg brukes videre.")
            : Localized.Get("Settings_LocalPrefs_SaveFailed", "Kunne ikke lagre innstillingene lokalt.");
    }

    // Reload from disk (discards unsaved edits).
    [RelayCommand]
    private void Reload()
    {
        var p = _store.Load();
        Theme = p.Theme;
        Language = p.Language;
        ReduceMotion = p.ReduceMotion;
        SelectedStyle = StyleOptions.FirstOrDefault(s => s.Token == p.VoiceGoalStyle) ?? StyleOptions[0];
        SelectedFocus = FocusOptions.FirstOrDefault(f => f.Token == p.VoiceGoalFocus) ?? FocusOptions[0];
        SelectedFrequency = FrequencyOptions.FirstOrDefault(f => f.Value == p.TrainingFrequency) ?? FrequencyOptions[1];
        SelectedMicDevice = MicDeviceOptions.FirstOrDefault(d => d.Id == p.MicDeviceId) ?? MicDeviceOptions[0];
        HearOwnVoice = p.HearOwnVoice;
        StressSensitive = p.StressSensitiveMode;
        ReducedVisualFeedback = p.ReducedVisualFeedback;
        DiagnosticsConsent = p.DiagnosticsConsent;
        ResearchConsent = p.ResearchSharingConsent;
        RemindersEnabled = p.RemindersEnabled;
        SelectedReminderTime = MatchReminderTime(p.ReminderMinuteOfDay);
        _resonanceBaselineCentroidHz = p.ResonanceBaselineCentroidHz;
        Status = Localized.Get("Settings_LocalPrefs_Reloaded", "Lastet fra lagret fil.");
    }

    // ── Data: real backup / restore / clear (inline confirmation) ─────────────────────────────────────────────────
    public string DataHeading => Localized.Get("Settings_Database", "Data og sikkerhetskopi");
    public string DataNote => Localized.Get("Settings_DatabaseNote",
        "Ekte handlinger på den lokale databasen. Sikkerhetskopi er trygt; gjenoppretting og tømming endrer dataene dine.");
    public string BackupLabel => Localized.Get("Settings_CreateBackup", "Lag sikkerhetskopi");
    public string RestoreLabel => Localized.Get("Settings_RestoreBackup", "Gjenopprett valgt");
    public string ClearLabel => Localized.Get("UI_ClearDatabase", "Tøm database");
    public string ConfirmLabel => Localized.Get("Common_Confirm", "Bekreft");
    public string CancelLabel => Localized.Get("Common_Cancel", "Avbryt");
    public string NoBackupsLabel => Localized.Get("Settings_NoBackups", "Ingen sikkerhetskopier ennå.");

    public System.Collections.ObjectModel.ObservableCollection<BackupEntry> Backups { get; } = new();
    [ObservableProperty] private BackupEntry? _selectedBackup;
    public bool HasBackups => Backups.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDataStatus))]
    private string _dataStatus = string.Empty;
    public bool HasDataStatus => !string.IsNullOrEmpty(DataStatus);

    // Inline confirmation flags (converter-free visibility for the destructive actions).
    [ObservableProperty] private bool _confirmRestore;
    [ObservableProperty] private bool _confirmClear;

    private void RefreshBackups()
    {
        Backups.Clear();
        foreach (var b in _data.ListBackups()) Backups.Add(b);
        SelectedBackup = Backups.Count > 0 ? Backups[0] : null;
        OnPropertyChanged(nameof(HasBackups));
    }

    [RelayCommand]
    private void Backup()
    {
        var r = _data.Backup(DateTime.Now);
        DataStatus = r.Message;
        if (r.Ok) RefreshBackups();
    }

    [RelayCommand] private void Restore() { if (SelectedBackup is not null) { ConfirmClear = false; ConfirmRestore = true; } }
    [RelayCommand] private void Clear() { ConfirmRestore = false; ConfirmClear = true; }
    [RelayCommand] private void CancelConfirm() { ConfirmRestore = false; ConfirmClear = false; }

    [RelayCommand]
    private void ConfirmRestoreAction()
    {
        ConfirmRestore = false;
        if (SelectedBackup is null) { DataStatus = NoBackupsLabel; return; }
        DataStatus = _data.Restore(SelectedBackup.FilePath, DateTime.Now).Message;
        RefreshBackups();
    }

    [RelayCommand]
    private void ConfirmClearAction()
    {
        ConfirmClear = false;
        DataStatus = _data.Clear().Message;
        RefreshBackups();
    }
}
