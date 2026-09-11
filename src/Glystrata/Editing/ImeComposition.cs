using System.Runtime.CompilerServices;
using System.Windows.Interop;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using Point = System.Windows.Point;

namespace Glystrata.Editing;

public static class ImeComposition
{
    private static readonly ConditionalWeakTable<TextEditor, ImeEditorController> Controllers = new();

    public static void Attach(TextEditor editor)
    {
        Controllers.GetValue(editor, static e => new ImeEditorController(e));
    }
}

internal sealed class ImeEditorController
{
    private readonly TextEditor _editor;
    private ImeHwndHook? _hook;
    private ImeCompositionLayer? _layer;
    private bool _layerInserted;

    public ImeEditorController(TextEditor editor)
    {
        _editor = editor;
        _editor.Loaded += OnLoaded;
        _editor.Unloaded += OnUnloaded;
    }

    public bool IsFocused => _editor.TextArea.IsKeyboardFocusWithin;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            EnsureLayer();
            if (PresentationSource.FromVisual(_editor) is HwndSource source)
            {
                _hook = ImeHwndHook.GetOrCreate(source);
                _hook.Register(this);
            }
        }
        catch
        {
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            _hook?.Unregister(this);
            _hook = null;
            _layer?.SetComposition(null);
        }
        catch
        {
        }
    }

    private void EnsureLayer()
    {
        if (_layerInserted)
        {
            return;
        }

        try
        {
            _layer = new ImeCompositionLayer(_editor);
            _editor.TextArea.TextView.InsertLayer(_layer, KnownLayer.Caret, LayerInsertionPosition.Above);
            _layerInserted = true;
        }
        catch
        {
            _layer = null;
        }
    }

    public void UpdateComposition(string text, int clauseStart, int clauseLength, int cursorIndex)
    {
        _layer?.SetComposition(new ImeCompositionState(text, clauseStart, clauseLength, cursorIndex));
    }

    public void ClearComposition()
    {
        _layer?.SetComposition(null);
    }

    public void UpdateCandidateWindow(IntPtr hwnd)
    {
        if (_layer is not { CursorSegmentRect: { } segmentRect, CursorRect: { } cursorRect })
        {
            return;
        }

        var himc = IntPtr.Zero;
        try
        {
            if (PresentationSource.FromVisual(_editor) is not HwndSource { RootVisual: { } root } source)
            {
                return;
            }

            var transform = _layer.TransformToAncestor(root);
            var topLeft = source.CompositionTarget.TransformToDevice.Transform(transform.Transform(segmentRect.TopLeft));
            var bottomRight = source.CompositionTarget.TransformToDevice.Transform(transform.Transform(segmentRect.BottomRight));
            var cursorBottom = source.CompositionTarget.TransformToDevice.Transform(transform.Transform(new Point(cursorRect.Left, segmentRect.Bottom)));

            himc = ImeNativeMethods.ImmGetContext(hwnd);
            if (himc == IntPtr.Zero)
            {
                return;
            }

            var form = new ImeNativeMethods.CANDIDATEFORM
            {
                dwIndex = 0,
                dwStyle = ImeNativeMethods.CFS_EXCLUDE,
                ptCurrentPos = new ImeNativeMethods.POINT { X = (int)Math.Round(cursorBottom.X), Y = (int)Math.Round(cursorBottom.Y) },
                rcArea = new ImeNativeMethods.RECT
                {
                    Left = (int)Math.Round(topLeft.X),
                    Top = (int)Math.Round(topLeft.Y),
                    Right = (int)Math.Round(bottomRight.X),
                    Bottom = (int)Math.Round(bottomRight.Y)
                }
            };
            ImeNativeMethods.ImmSetCandidateWindow(himc, ref form);
        }
        catch
        {
        }
        finally
        {
            if (himc != IntPtr.Zero)
            {
                try
                {
                    ImeNativeMethods.ImmReleaseContext(hwnd, himc);
                }
                catch
                {
                }
            }
        }
    }
}
