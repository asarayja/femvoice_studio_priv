using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FemVoice.Avalonia.Views;

/// <summary>
/// Engine-backed Progression. Single-column, full-width cards — nothing is squeezed on phone widths, and it reads
/// cleanly on desktop too (like Settings/Reports). Display-only layout.
/// </summary>
public partial class ProgressionView : UserControl
{
    public ProgressionView() => AvaloniaXamlLoader.Load(this);
}
