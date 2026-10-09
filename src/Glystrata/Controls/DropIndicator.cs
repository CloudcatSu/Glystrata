using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Glystrata.Controls;

public enum DropEdge
{
    Left,
    Right,
    Top,
    Bottom,
    /// <summary>The whole element: "drop it into this".</summary>
    Outline
}

/// <summary>
/// Shows where a dragged tab or group will land: an accent bar on the edge it will be inserted next
/// to, or an outline when it will be dropped into the element. Only one indicator exists at a time and it
/// fades in, so moving across a row of tabs reads as a bar that follows the pointer.
/// </summary>
public static class DropIndicator
{
    private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(90);

    private static IndicatorAdorner? _current;
    private static AdornerLayer? _layer;
    private static readonly DispatcherTimer HideTimer = CreateHideTimer();

    public static void Show(UIElement target, DropEdge edge)
    {
        HideTimer.Stop();
        if (_current is { } current && ReferenceEquals(current.AdornedElement, target) && current.Edge == edge)
        {
            return;
        }

        Remove();
        var layer = AdornerLayer.GetAdornerLayer(target);
        if (layer is null)
        {
            return;
        }

        var adorner = new IndicatorAdorner(target, edge);
        layer.Add(adorner);
        adorner.Opacity = 0;
        adorner.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
        _current = adorner;
        _layer = layer;
    }

    /// <summary>Hides right away (drop completed or drag ended).</summary>
    public static void Hide()
    {
        HideTimer.Stop();
        Remove();
    }

    /// <summary>
    /// For DragLeave. That event also fires when the pointer moves between an element's own children, so
    /// the removal is delayed and a following DragOver on the same element cancels it.
    /// </summary>
    public static void HideSoon(UIElement target)
    {
        if (_current is { } current && ReferenceEquals(current.AdornedElement, target))
        {
            HideTimer.Stop();
            HideTimer.Start();
        }
    }

    private static DispatcherTimer CreateHideTimer()
    {
        var timer = new DispatcherTimer { Interval = HideDelay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Remove();
        };
        return timer;
    }

    private static void Remove()
    {
        if (_current is not null)
        {
            _layer?.Remove(_current);
        }

        _current = null;
        _layer = null;
    }

    /// <summary>Dims the dragged element while the drag runs; always restores it and clears any indicator.</summary>
    public static void RunDrag(UIElement source, DataObject data, DragDropEffects effects)
    {
        var previous = source.Opacity;
        source.Opacity = 0.5;
        try
        {
            DragDrop.DoDragDrop(source, data, effects);
        }
        finally
        {
            source.Opacity = previous;
            Hide();
        }
    }

    private sealed class IndicatorAdorner : Adorner
    {
        private const double BarThickness = 3;

        public IndicatorAdorner(UIElement adorned, DropEdge edge) : base(adorned)
        {
            Edge = edge;
            IsHitTestVisible = false;
        }

        public DropEdge Edge { get; }

        protected override void OnRender(DrawingContext context)
        {
            var accent = Application.Current.TryFindResource("AccentBrush") as SolidColorBrush;
            var color = accent?.Color ?? Color.FromRgb(0x3B, 0x6F, 0xF5);
            var brush = new SolidColorBrush(color);
            var size = AdornedElement.RenderSize;
            if (Edge == DropEdge.Outline)
            {
                var fill = new SolidColorBrush(Color.FromArgb(0x26, color.R, color.G, color.B));
                context.DrawRoundedRectangle(fill, new Pen(brush, 1.5), new Rect(0.75, 0.75, Math.Max(0, size.Width - 1.5), Math.Max(0, size.Height - 1.5)), 4, 4);
                return;
            }

            var bar = Edge switch
            {
                DropEdge.Left => new Rect(-BarThickness / 2, 0, BarThickness, size.Height),
                DropEdge.Right => new Rect(size.Width - BarThickness / 2, 0, BarThickness, size.Height),
                DropEdge.Top => new Rect(0, -BarThickness / 2, size.Width, BarThickness),
                _ => new Rect(0, size.Height - BarThickness / 2, size.Width, BarThickness)
            };
            context.DrawRoundedRectangle(brush, null, bar, 1.5, 1.5);
        }
    }
}
