using System.ComponentModel;
using FemVoiceStudio.Services;   // LocalizationService

namespace FemVoice.Avalonia.Localization;

/// <summary>
/// Reactive, display-only localized-string holder used by the <c>{loc:Tr}</c> markup extension. It resolves
/// through <see cref="Localized.Get"/> and re-raises <see cref="Value"/> when the shared LocalizationService
/// signals a language change (it raises "Item[]" / "CurrentCulture" PropertyChanged), so bound labels update
/// without changing any localization semantics. Strictly read-only: it never calls SetLanguage and writes nothing.
/// </summary>
public sealed class LocalizedValue : INotifyPropertyChanged
{
    private readonly string _key;
    private readonly string _fallback;

    public LocalizedValue(string key, string fallback)
    {
        _key = key;
        _fallback = fallback;
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>The resolved localized string (or the readable fallback when the key is missing).</summary>
    public string Value => Localized.Get(_key, _fallback);

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The shared service raises "Item[]" (and "CurrentCulture") on a language switch; null/empty means "all".
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is "Item[]" or "CurrentCulture")
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
