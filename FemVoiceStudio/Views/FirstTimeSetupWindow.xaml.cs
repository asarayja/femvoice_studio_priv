using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Views
{
    public partial class FirstTimeSetupWindow : Window
    {
        public string SelectedLanguage { get; private set; } = "nb";
        public string SelectedTheme { get; private set; } = "System";

        // Onboarding-valg: stilmål + treningsfrekvens. Defaultene speiler Settings:
        // soft_feminine / 3 dager — trygge standardvalg ved Skip.
        public string SelectedStyleGoal { get; private set; } = "soft_feminine";
        public int SelectedTrainingFrequency { get; private set; } = 3;

        public FirstTimeSetupWindow()
        {
            InitializeComponent();

            // Subscribe to theme changes
            ThemeManager.Instance.ThemeChanged += OnThemeChanged;

            // Detect system language and select appropriate option
            DetectSystemLanguage();

            // Default-valg i de nye seksjonene (speiler Settings-defaultene).
            SelectComboBoxByTag(StyleGoalComboBox, "soft_feminine");
            SelectComboBoxByTag(FrequencyComboBox, "3");

            UpdateLocalizedText();
        }
        
        private void DetectSystemLanguage()
        {
            // Get current system culture
            var systemCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            
            // Select the appropriate item in the ComboBox
            for (int i = 0; i < LanguageComboBox.Items.Count; i++)
            {
                if (LanguageComboBox.Items[i] is ComboBoxItem item)
                {
                    var tag = item.Tag?.ToString();
                    if (tag == systemCulture)
                    {
                        LanguageComboBox.SelectedIndex = i;
                        SelectedLanguage = tag;
                        return;
                    }
                }
            }
            
            // Default to Norwegian
            LanguageComboBox.SelectedIndex = 0;
            SelectedLanguage = "nb";
        }
        
        private void OnThemeChanged(object? sender, EventArgs e)
        {
            // Force re-evaluation of dynamic resources
            // Clear and re-add the resource dictionary to force refresh
            var resources = this.Resources;
            if (resources != null)
            {
                // Trigger resource refresh by accessing MergedDictionaries
                var mergedDicts = this.Resources.MergedDictionaries;
                if (mergedDicts != null)
                {
                    // Force update by reassigning
                    var temp = mergedDicts.Count;
                }
            }
            
            // Also invalidate visual to redraw
            InvalidateVisual();
            UpdateLayout();
        }
        
        private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Skip if controls are not yet loaded
            if (LanguageComboBox?.SelectedItem == null)
                return;
            
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var languageCode = selectedItem.Tag?.ToString() ?? "nb";
                SelectedLanguage = languageCode;
                
                // Temporarily set language to update UI
                LocalizationService.Instance.SetLanguage(languageCode);
                UpdateLocalizedText();
            }
        }
        
        private void UpdateLocalizedText()
        {
            // Update texts based on currently selected language
            var loc = LocalizationService.Instance;
            
            try
            {
                Title = loc["FirstTimeSetup_Title"];
                WelcomeText.Text = loc["FirstTimeSetup_Welcome"];
                LanguageLabel.Text = loc["FirstTimeSetup_LanguageLabel"];
                LanguageDesc.Text = loc["FirstTimeSetup_LanguageDesc"];
                ThemeLabel.Text = loc["FirstTimeSetup_ThemeLabel"];
                ThemeDesc.Text = loc["FirstTimeSetup_ThemeDesc"];
                StyleGoalLabel.Text = loc["FirstSetup_StyleGoalLabel"];
                StyleGoalDesc.Text = loc["FirstSetup_StyleGoalDesc"];
                FrequencyLabel.Text = loc["FirstSetup_FrequencyLabel"];
                FrequencyDesc.Text = loc["FirstSetup_FrequencyDesc"];
                SkipButton.Content = loc["FirstTimeSetup_Skip"];
                ContinueButton.Content = loc["FirstTimeSetup_Continue"];
            }
            catch
            {
                // Use defaults if localization fails
            }
        }
        
        private void OnContinueClick(object sender, RoutedEventArgs e)
        {
            // Get selected language from ComboBox
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                SelectedLanguage = selectedItem.Tag?.ToString() ?? "nb";
            }
            
            // Get selected theme
            if (ThemeLight.IsChecked == true)
                SelectedTheme = "Light";
            else if (ThemeDark.IsChecked == true)
                SelectedTheme = "Dark";
            else
                SelectedTheme = "System";

            // Capture style goal + training frequency selections.
            var styleTag = GetSelectedTag(StyleGoalComboBox);
            if (!string.IsNullOrEmpty(styleTag))
                SelectedStyleGoal = styleTag;

            if (int.TryParse(GetSelectedTag(FrequencyComboBox), out var frequency))
                SelectedTrainingFrequency = frequency;

            // Apply settings (language/theme) + persist profile (style/frequency).
            ApplySettings();
            PersistProfileSelections();

            // Mark setup as completed
            FirstTimeSetupService.Instance.MarkSetupCompleted();

            // Close and signal success
            DialogResult = true;
            Close();
        }
        
        private void OnSkipClick(object sender, RoutedEventArgs e)
        {
            // Use default settings
            SelectedLanguage = "nb";
            SelectedTheme = "System";

            // Trygge profil-defaulter (samme som Settings): soft_feminine / 3 dager.
            SelectedStyleGoal = "soft_feminine";
            SelectedTrainingFrequency = 3;

            // Apply settings + persist default-profilen så identiteten finnes fra start.
            ApplySettings();
            PersistProfileSelections();

            // Mark setup as completed (so it doesn't show again)
            FirstTimeSetupService.Instance.MarkSetupCompleted();

            // Close and signal success
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Persisterer onboarding-valgene (stilmål + treningsfrekvens) til BÅDE
        /// UserVoiceProfile og VoiceGoalProfile — speiler SettingsWindow så de to
        /// modellene aldri driver fra hverandre. Bevarer alle andre felter (hent-eller-
        /// opprett). Feiler stille i design-/testkontekst der DI ikke finnes.
        /// </summary>
        private void PersistProfileSelections()
        {
            try
            {
                var database = App.Services?.GetService(typeof(DatabaseService)) as DatabaseService;
                if (database != null)
                {
                    // Hent-eller-opprett: bevarer baselines/komfortsone om de finnes.
                    var profile = database.GetUserVoiceProfile(1) ?? new UserVoiceProfile { UserId = 1 };
                    profile.PreferredVoiceStyle = UserVoiceProfile.FromGoalStyleKey(SelectedStyleGoal);
                    profile.TrainingFrequencyPerWeek = SelectedTrainingFrequency;
                    database.SaveUserVoiceProfile(profile);
                }

                // VoiceGoalProfile speiler stilmålet via GoalStyleKey (samme som Settings).
                var goalProvider = ResolveVoiceGoalProfileProvider();
                var goalProfile = goalProvider.GetProfile(1) ?? new VoiceGoalProfile { UserId = 1 };
                goalProfile.GoalStyleKey = SelectedStyleGoal;
                goalProvider.SaveProfile(goalProfile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error persisting onboarding profile: {ex.Message}");
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
                // First-time setup can be constructed before App.Services exists.
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
                    string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }

            comboBox.SelectedIndex = 0;
        }
        
        private void ApplySettings()
        {
            try
            {
                // Apply language
                LocalizationService.Instance.SetLanguage(SelectedLanguage);
                
                // Apply theme
                var appTheme = SelectedTheme switch
                {
                    "Light" => AppTheme.Light,
                    "Dark" => AppTheme.Dark,
                    _ => AppTheme.System
                };
                
                ThemeManager.Instance.SwitchTheme(appTheme);
                
                // Save settings
                ThemeManager.Instance.SaveSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying settings: {ex.Message}");
            }
        }
        
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            // Allow dragging the window
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from theme changes
            ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
            base.OnClosed(e);
        }
    }
}
