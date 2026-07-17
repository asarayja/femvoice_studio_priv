using System;
using Avalonia.Data;

namespace FemVoice.Avalonia.Localization;

/// <summary>
/// Avalonia markup extension for safe, display-only localized text: <c>{loc:Tr Key=Some_Key, Fallback=...}</c>.
/// It returns a one-way binding to a reactive <see cref="LocalizedValue"/>, so the label resolves through the
/// shared LocalizationService and updates if the language ever changes. It introduces no WPF dependency, is
/// not modelled on the WPF localization markup, and changes no resource keys or localization semantics.
///
/// USAGE CONTRACT: each <see cref="LocalizedValue"/> subscribes to the app-lifetime LocalizationService
/// singleton and is not disposed, so use <c>{loc:Tr}</c> only on STATIC, app-lifetime chrome (realized once).
/// For transient/churned content (DataTemplates re-realized on navigation, virtualized item rows), resolve via
/// the non-subscribing <see cref="Localized.Get"/> on the view-model instead (as DeferredSurfaceViewModel does),
/// to avoid accruing orphaned subscribers.
/// </summary>
public sealed class TrExtension
{
    public TrExtension() { }
    public TrExtension(string key) { Key = key; }

    /// <summary>Resource key to resolve.</summary>
    public string Key { get; set; } = "";
    /// <summary>Readable fallback used when the key is missing (defaults to the key).</summary>
    public string Fallback { get; set; } = "";

    public object ProvideValue(IServiceProvider serviceProvider)
    {
        var source = new LocalizedValue(Key, string.IsNullOrEmpty(Fallback) ? Key : Fallback);
        return new Binding(nameof(LocalizedValue.Value)) { Source = source, Mode = BindingMode.OneWay };
    }
}
