using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FemVoice.Avalonia.Views;

/// <summary>
/// Engine-backed Progression. RESPONSIVE: the flow cards live in two columns (LeftCol/RightCol) that fill the full
/// content width on desktop; on narrow/phone widths they reflow into a SINGLE column (interleaved in reading order)
/// so nothing is cramped. Display-only layout — no data/behaviour change.
/// </summary>
public partial class ProgressionView : UserControl
{
    // Captured once at load, in interleaved reading order (L0, R0, L1, R1, …). Used to redistribute on resize.
    private readonly List<Control> _ordered = new();
    private bool _single;   // current layout state (true = single column) to avoid redundant reflows

    public ProgressionView()
    {
        AvaloniaXamlLoader.Load(this);
        CaptureCards();
        SizeChanged += (_, e) => ApplyResponsive(e.NewSize.Width);
    }

    private void CaptureCards()
    {
        var left = LeftCol; var right = RightCol;
        if (left is null || right is null) return;
        var l = new List<Control>(left.Children.Count);
        foreach (var c in left.Children) if (c is Control ctl) l.Add(ctl);
        var r = new List<Control>(right.Children.Count);
        foreach (var c in right.Children) if (c is Control ctl) r.Add(ctl);
        int max = System.Math.Max(l.Count, r.Count);
        for (int i = 0; i < max; i++)
        {
            if (i < l.Count) _ordered.Add(l[i]);
            if (i < r.Count) _ordered.Add(r[i]);
        }
    }

    private void ApplyResponsive(double width)
    {
        var left = LeftCol; var right = RightCol;
        if (left is null || right is null || _ordered.Count == 0) return;

        bool single = width < 720;
        if (single == _single && left.Children.Count > 0) return;   // no change
        _single = single;

        left.Children.Clear();
        right.Children.Clear();

        if (single)
        {
            // One column: all cards stacked in reading order; the right column collapses.
            foreach (var c in _ordered) left.Children.Add(c);
            right.IsVisible = false;
            Grid.SetColumnSpan(left, 2);
            left.Margin = new Thickness(0);
        }
        else
        {
            // Two columns: even indices left, odd indices right (restores the authored split).
            for (int i = 0; i < _ordered.Count; i++)
                (i % 2 == 0 ? left : right).Children.Add(_ordered[i]);
            right.IsVisible = true;
            Grid.SetColumnSpan(left, 1);
            left.Margin = new Thickness(0, 0, 7, 0);
        }
    }
}
