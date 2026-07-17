using CommunityToolkit.Mvvm.ComponentModel;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// One Exercise Guide category-filter chip (display-only). <see cref="Label"/> is the category text shown on the
/// chip ("Alle" or an exercise goal); <see cref="IsSelected"/> drives the selected visual state (bound to a
/// <c>selected</c> style class — converter-free). Selecting a chip filters the in-memory display list only; no
/// persistence, analytics, or session writes are involved.
/// </summary>
public partial class CategoryChipViewModel : ObservableObject
{
    public CategoryChipViewModel(string label, bool isSelected)
    {
        Label = label;
        _isSelected = isSelected;
    }

    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;
}
