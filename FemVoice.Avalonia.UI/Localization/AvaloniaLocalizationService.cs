using System.ComponentModel;
using FemVoiceStudio.Services;

namespace FemVoice.Avalonia.Localization;

/// <summary>Supplies the Avalonia UI language to shared services that accept ILocalizationService.</summary>
public sealed class AvaloniaLocalizationService : ILocalizationService
{
    public static AvaloniaLocalizationService Instance { get; } = new();

    private AvaloniaLocalizationService()
    {
        Localized.LanguageChanged += () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string CurrentLanguage => Localized.CurrentCulture.TwoLetterISOLanguageName;
    public bool IsEnglish => CurrentLanguage == "en";
    public bool IsNorwegian => CurrentLanguage is "nb" or "nn" or "no";
    public string this[string key] => GetString(key);
    public string GetString(string key) => Localized.Get(key);
    public string GetFormattedString(string key, params object[] args) =>
        string.Format(Localized.CurrentCulture, GetString(key), args);
    public void SetLanguage(string languageCode) => LanguageActivation.Apply(languageCode);
}
