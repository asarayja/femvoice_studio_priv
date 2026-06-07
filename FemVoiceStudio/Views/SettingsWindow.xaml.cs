using System.Windows;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using System.Windows.Controls;

namespace FemVoiceStudio.Views;

public partial class SettingsWindow : Window
{
    private readonly DatabaseService _database;
    private readonly ILocalizationService _localization;
    private readonly IVoiceGoalProfileProvider _voiceGoalProfiles;
    private bool _languageChanged = false;
    private bool _themeChanged = false;
    
    public SettingsWindow()
    {
        InitializeComponent();
        // DatabaseService er DI-singleton; manuelle new re-kjørte skjema-init (integrasjonsaudit-funn).
        _database = App.Services?.GetService(typeof(DatabaseService)) as DatabaseService
                    ?? new DatabaseService();
        _localization = LocalizationService.Instance;
        _voiceGoalProfiles = ResolveVoiceGoalProfileProvider();
        LoadSettings();
    }
    
    private void LoadSettings()
    {
        var settings = _database.GetUserSettings();
        HearOwnVoiceCheckBox.IsChecked = settings.HearOwnVoice;
        
        // Load current language
        var currentLang = LocalizationService.Instance.CurrentLanguage;
        for (int i = 0; i < LanguageComboBox.Items.Count; i++)
        {
            if (LanguageComboBox.Items[i] is ComboBoxItem item && item.Tag is string langCode)
            {
                if (langCode.StartsWith(currentLang) || currentLang.StartsWith(langCode))
                {
                    LanguageComboBox.SelectedIndex = i;
                    break;
                }
            }
        }
        
        // Load current theme
        var currentTheme = ThemeManager.Instance.CurrentThemeMode;
        ThemeSystemRadio.IsChecked = currentTheme == AppTheme.System;
        ThemeLightRadio.IsChecked = currentTheme == AppTheme.Light;
        ThemeDarkRadio.IsChecked = currentTheme == AppTheme.Dark;

        LoadVoiceGoalProfile();
        LoadUserVoiceProfile();
    }
    
    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string languageCode)
        {
            LocalizationService.Instance.SetLanguage(languageCode);
            _languageChanged = true;
        }
    }
    
    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (ThemeSystemRadio.IsChecked == true)
        {
            ThemeManager.Instance.SwitchTheme(AppTheme.System);
        }
        else if (ThemeLightRadio.IsChecked == true)
        {
            ThemeManager.Instance.SwitchTheme(AppTheme.Light);
        }
        else if (ThemeDarkRadio.IsChecked == true)
        {
            ThemeManager.Instance.SwitchTheme(AppTheme.Dark);
        }
        _themeChanged = true;
    }
    
    private void OnSaveSettings(object sender, RoutedEventArgs e)
    {
        var settings = _database.GetUserSettings();
        settings.HearOwnVoice = HearOwnVoiceCheckBox.IsChecked ?? false;
        settings.Theme = ThemeManager.Instance.CurrentThemeMode.ToString();
        _database.UpdateUserSettings(settings);
        SaveVoiceGoalProfile();
        SaveUserVoiceProfile();
    }
    
    private void OnResetDatabase(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            _localization.GetString("Settings_ResetDatabaseConfirmMessage"),
            _localization.GetString("Settings_ResetDatabaseConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // Close all database connections first
                DatabaseService.CloseAllConnections();
                
                // Then reset the database
                _database.ResetDatabase();
                
                MessageBox.Show(
                    _localization.GetString("Settings_ResetDatabaseSuccessMessage"),
                    _localization.GetString("Settings_ResetDatabaseSuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    _localization.GetFormattedString("Settings_ResetDatabaseErrorMessage", ex.Message),
                    _localization.GetString("Settings_ResetDatabaseErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void OnOpenMicrophoneCalibration(object sender, RoutedEventArgs e)
    {
        var window = new MicrophoneCalibrationWindow
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void LoadVoiceGoalProfile()
    {
        var profile = _voiceGoalProfiles.GetProfile(1);
        var focus = string.IsNullOrWhiteSpace(profile?.PrimaryFocus)
            ? "balanced"
            : profile.PrimaryFocus;
        var style = string.IsNullOrWhiteSpace(profile?.GoalStyleKey)
            ? "soft_feminine"
            : profile.GoalStyleKey;

        SelectComboBoxByTag(VoiceGoalFocusComboBox, focus);
        SelectComboBoxByTag(VoiceGoalStyleComboBox, style);
    }

    private void SaveVoiceGoalProfile()
    {
        var profile = _voiceGoalProfiles.GetProfile(1) ?? new VoiceGoalProfile { UserId = 1 };
        var selectedFocus = GetSelectedTag(VoiceGoalFocusComboBox);

        profile.PrimaryFocus = selectedFocus == "balanced" ? string.Empty : selectedFocus;
        profile.GoalStyleKey = GetSelectedTag(VoiceGoalStyleComboBox);
        _voiceGoalProfiles.SaveProfile(profile);
    }

    private void LoadUserVoiceProfile()
    {
        var profile = _database.GetUserVoiceProfile(1);

        StressSensitiveModeCheckBox.IsChecked = profile?.StressSensitiveMode ?? false;
        ReducedVisualFeedbackCheckBox.IsChecked = profile?.ReducedVisualFeedback ?? false;

        var frequency = profile?.TrainingFrequencyPerWeek ?? 3;
        SelectComboBoxByTag(TrainingFrequencyComboBox, frequency.ToString());
    }

    private void SaveUserVoiceProfile()
    {
        // Hent-eller-opprett: bevarer baselines/komfortsone som andre systemer kan ha kalibrert.
        var profile = _database.GetUserVoiceProfile(1) ?? new UserVoiceProfile { UserId = 1 };

        profile.StressSensitiveMode = StressSensitiveModeCheckBox.IsChecked ?? false;
        profile.ReducedVisualFeedback = ReducedVisualFeedbackCheckBox.IsChecked ?? false;

        if (int.TryParse(GetSelectedTag(TrainingFrequencyComboBox), out var frequency))
        {
            profile.TrainingFrequencyPerWeek = frequency;
        }

        // A7-funn (critical): stilvalget MÅ speiles inn i UserVoiceProfile —
        // TargetProfileAdapter leser utelukkende PreferredVoiceStyle. Uten denne
        // koblingen sto feltet på modelldefault (Feminine) for alle brukere uansett
        // valg, og en DarkFeminine-/Androgynous-bruker fikk HEVET resonansmål.
        var styleTag = GetSelectedTag(VoiceGoalStyleComboBox);
        if (!string.IsNullOrEmpty(styleTag))
        {
            profile.PreferredVoiceStyle = UserVoiceProfile.FromGoalStyleKey(styleTag);
        }

        _database.SaveUserVoiceProfile(profile);

        // A7-funn: StressSensitiveExperience er en singleton som cacher profilen —
        // uten Refresh() tok endrede tilgjengelighetsflagg først effekt ved neste
        // app-start.
        try
        {
            (App.Services?.GetService(typeof(StressSensitiveExperience))
                as StressSensitiveExperience)?.Refresh();
        }
        catch
        {
            // Settings kan konstrueres i design-/testkontekst før App.Services finnes.
        }
    }

    private static IVoiceGoalProfileProvider ResolveVoiceGoalProfileProvider()
    {
        try
        {
            if (App.Services?.GetService(typeof(IVoiceGoalProfileProvider)) is IVoiceGoalProfileProvider provider)
                return provider;
        }
        catch
        {
            // Settings can be constructed in design/test contexts before App.Services exists.
        }

        return new LocalVoiceGoalProfileStore();
    }

    private static string GetSelectedTag(ComboBox comboBox)
        => comboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag
            ? tag
            : string.Empty;

    private static void SelectComboBoxByTag(ComboBox comboBox, string tag)
    {
        for (var i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item &&
                item.Tag is string itemTag &&
                string.Equals(itemTag, tag, System.StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }
    
    private void OnClose(object sender, RoutedEventArgs e)
    {
        // Save settings before closing
        OnSaveSettings(sender, e);
        
        // If language or theme changed, refresh main window
        if (_languageChanged || _themeChanged)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.RefreshUI();
            }
        }
        
        Close();
    }
}
