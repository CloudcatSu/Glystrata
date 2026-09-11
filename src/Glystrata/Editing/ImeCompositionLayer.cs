using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace Glystrata.Editing;

internal sealed record ImeCompositionState(string Text, int ClauseStart, int ClauseLength, int CursorIndex);

internal sealed class ImeCompositionLayer : UIElement
{
    private readonly record struct Segment(int Start, int End, Rect Bounds, string Text);

    private readonly TextEditor _editor;
    private readonly TextArea _textArea;
    private readonly TextView _textView;
    private readonly List<Segment> _segments = new();
    private ImeCompositionState? _state;

    public ImeCompositionLayer(TextEditor editor)
    {
        _editor = editor;
        _textArea = editor.TextArea;
        _textView = editor.TextArea.TextView;
        IsHitTestVisible = false;

        _textView.ScrollOffsetChanged += (_, _) => Recompute();
        _textView.VisualLinesChanged += (_, _) => Recompute();
        _textArea.Caret.PositionChanged += (_, _) => Recompute();
    }

    public Rect? CursorRect { get; private set; }

    public Rect? CursorSegmentRect { get; private set; }

    public void SetComposition(ImeCompositionState? state)
    {
        _state = state;
        Recompute();
    }

    private void Recompute()
    {
        _segments.Clear();
        CursorRect = null;
        CursorSegmentRect = null;

        if (_state is { Text.Length: > 0 } state)
        {
            try
            {
                BuildSegments(state);
            }
            catch
            {
                _segments.Clear();
                CursorRect = null;
                CursorSegmentRect = null;
            }
        }

        InvalidateVisual();
    }

    private void BuildSegments(ImeCompositionState state)
    {
        var dpi = VisualTreeHelper.GetDpi(_editor);
        var typeface = new Typeface(_editor.FontFamily, _editor.FontStyle, _editor.FontWeight, _editor.FontStretch);
        var fontSize = _editor.FontSize;
        var lineHeight = _textView.DefaultLineHeight;
        var caretPosition = _textView.GetVisualPosition(_textArea.Caret.Position, VisualYPosition.LineTop) - _textView.ScrollOffset;
        var maxWidth = Math.Max(1.0, _textView.ActualWidth);

        var text = state.Text;
        var charWidths = new double[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            charWidths[i] = CreateFormattedText(text[i].ToString(), typeface, fontSize, dpi).WidthIncludingTrailingWhitespace;
        }

        var index = 0;
        var lineNumber = 0;
        while (index < text.Length)
        {
            var x = lineNumber == 0 ? caretPosition.X : 0;
            var y = caretPosition.Y + lineNumber * lineHeight;
            var availableWidth = Math.Max(charWidths[index], maxWidth - x);

            var end = index;
            var width = 0.0;
            while (end < text.Length)
            {
                var nextWidth = width + charWidths[end];
                if (nextWidth > availableWidth && end > index)
                {
                    break;
                }
                width = nextWidth;
                end++;
            }

            var segmentText = text[index..end];
            var bounds = new Rect(x, y, Math.Max(width, 1.0), lineHeight);
            _segments.Add(new Segment(index, end, bounds, segmentText));

            index = end;
            lineNumber++;
        }

        foreach (var segment in _segments)
        {
            if (state.CursorIndex >= segment.Start && state.CursorIndex < segment.End)
            {
                var offset = SumWidths(charWidths, segment.Start, state.CursorIndex);
                CursorRect = new Rect(segment.Bounds.X + offset, segment.Bounds.Y, 1.0, segment.Bounds.Height);
                CursorSegmentRect = segment.Bounds;
                break;
            }
        }

        if (CursorRect is null && _segments.Count > 0)
        {
            var last = _segments[^1];
            CursorRect = new Rect(last.Bounds.Right, last.Bounds.Y, 1.0, last.Bounds.Height);
            CursorSegmentRect = last.Bounds;
        }
    }

    private static double SumWidths(double[] widths, int start, int end)
    {
        var sum = 0.0;
        for (var i = start; i < end && i < widths.Length; i++)
        {
            sum += widths[i];
        }
        return sum;
    }

    private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, DpiScale dpi)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            dpi.PixelsPerDip);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_state is not { Text.Length: > 0 } state || _segments.Count == 0)
        {
            return;
        }

        try
        {
            var background = _editor.Background ?? Brushes.White;
            var foreground = _editor.Foreground ?? Brushes.Black;
            var underlinePen = new Pen(foreground, 1.0);
            var activeUnderlinePen = new Pen(foreground, 2.0);
            var typeface = new Typeface(_editor.FontFamily, _editor.FontStyle, _editor.FontWeight, _editor.FontStretch);
            var dpi = VisualTreeHelper.GetDpi(_editor);

            foreach (var segment in _segments)
            {
                drawingContext.DrawRectangle(background, null, segment.Bounds);

                var formatted = CreateFormattedText(segment.Text, typeface, _editor.FontSize, dpi);
                formatted.SetForegroundBrush(foreground);
                drawingContext.DrawText(formatted, segment.Bounds.TopLeft);

                var underlineY = segment.Bounds.Bottom - 1;
                drawingContext.DrawLine(underlinePen, new Point(segment.Bounds.Left, underlineY), new Point(segment.Bounds.Right, underlineY));

                DrawActiveClause(drawingContext, segment, state, typeface, dpi, activeUnderlinePen);
            }

            if (CursorRect is { } cursorRect)
            {
                drawingContext.DrawLine(new Pen(foreground, 1.0), cursorRect.TopLeft, cursorRect.BottomLeft);
            }
        }
        catch
        {
        }
    }

    private void DrawActiveClause(DrawingContext drawingContext, Segment segment, ImeCompositionState state, Typeface typeface, DpiScale dpi, Pen pen)
    {
        if (state.ClauseLength <= 0)
        {
            return;
        }

        var clauseStart = Math.Max(state.ClauseStart, segment.Start);
        var clauseEnd = Math.Min(state.ClauseStart + state.ClauseLength, segment.End);
        if (clauseEnd <= clauseStart)
        {
            return;
        }

        var before = CreateFormattedText(segment.Text[..(clauseStart - segment.Start)], typeface, _editor.FontSize, dpi).WidthIncludingTrailingWhitespace;
        var clauseWidth = CreateFormattedText(segment.Text[(clauseStart - segment.Start)..(clauseEnd - segment.Start)], typeface, _editor.FontSize, dpi).WidthIncludingTrailingWhitespace;

        var y = segment.Bounds.Bottom - 1;
        var startX = segment.Bounds.Left + before;
        drawingContext.DrawLine(pen, new Point(startX, y), new Point(startX + clauseWidth, y));
    }
}
