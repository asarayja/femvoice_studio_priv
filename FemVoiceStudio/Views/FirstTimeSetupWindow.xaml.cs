using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Views
{
    public partial class FirstTimeSetupWindow : Window
    {
        public string SelectedLanguage { get; private set; } = "nb";
        public string SelectedTheme { get; private set; } = "System";
        
        public FirstTimeSetupWindow()
        {
            InitializeComponent();
            
            // Subscribe to theme changes
            ThemeManager.Instance.ThemeChanged += OnThemeChanged;
            
            // Detect system language and select appropriate option
            DetectSystemLanguage();
            
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
            
            // Apply settings
            ApplySettings();
            
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
            
            // Apply settings
            ApplySettings();
            
            // Mark setup as completed (so it doesn't show again)
            FirstTimeSetupService.Instance.MarkSetupCompleted();
            
            // Close and signal success
            DialogResult = true;
            Close();
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
