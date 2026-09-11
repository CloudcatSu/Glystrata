using System.Runtime.CompilerServices;
using System.Windows.Interop;

namespace Glystrata.Editing;

internal sealed class ImeHwndHook
{
    private static readonly ConditionalWeakTable<HwndSource, ImeHwndHook> Registry = new();

    private readonly HwndSource _source;
    private readonly HwndSourceHook _hook;
    private readonly List<ImeEditorController> _controllers = new();

    private ImeHwndHook(HwndSource source)
    {
        _source = source;
        _hook = WndProc;
        _source.AddHook(_hook);
    }

    public static ImeHwndHook GetOrCreate(HwndSource source) => Registry.GetValue(source, static s => new ImeHwndHook(s));

    public void Register(ImeEditorController controller)
    {
        if (!_controllers.Contains(controller))
        {
            _controllers.Add(controller);
        }
    }

    public void Unregister(ImeEditorController controller)
    {
        _controllers.Remove(controller);
        if (_controllers.Count == 0)
        {
            try
            {
                _source.RemoveHook(_hook);
            }
            catch
            {
            }
            Registry.Remove(_source);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        try
        {
            switch (msg)
            {
                case ImeNativeMethods.WM_IME_SETCONTEXT:
                    return HandleSetContext(hwnd, msg, wParam, lParam, ref handled);
                case ImeNativeMethods.WM_IME_COMPOSITION:
                    HandleComposition(hwnd);
                    break;
                case ImeNativeMethods.WM_IME_ENDCOMPOSITION:
                    GetFocusedController()?.ClearComposition();
                    break;
            }
        }
        catch
        {
        }
        return IntPtr.Zero;
    }

    private static IntPtr HandleSetContext(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (wParam != IntPtr.Zero)
        {
            var flags = lParam.ToInt64() & ~ImeNativeMethods.ISC_SHOWUICOMPOSITIONWINDOW;
            lParam = new IntPtr(flags);
        }
        handled = true;
        return ImeNativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void HandleComposition(IntPtr hwnd)
    {
        var controller = GetFocusedController();
        if (controller is null)
        {
            return;
        }

        var himc = IntPtr.Zero;
        try
        {
            himc = ImeNativeMethods.ImmGetContext(hwnd);
            if (himc == IntPtr.Zero)
            {
                return;
            }

            var text = ReadCompositionString(himc);
            if (string.IsNullOrEmpty(text))
            {
                controller.ClearComposition();
                return;
            }

            var attributes = ReadCompositionAttributes(himc);
            var cursor = Math.Clamp(ImeNativeMethods.ImmGetCompositionStringW(himc, ImeNativeMethods.GCS_CURSORPOS, null, 0), 0, text.Length);
            var (clauseStart, clauseLength) = FindActiveClause(attributes, text.Length);
            controller.UpdateComposition(text, clauseStart, clauseLength, cursor);
        }
        catch
        {
            controller.ClearComposition();
            return;
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

        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => controller.UpdateCandidateWindow(hwnd)));
    }

    private ImeEditorController? GetFocusedController()
    {
        foreach (var controller in _controllers)
        {
            if (controller.IsFocused)
            {
                return controller;
            }
        }
        return null;
    }

    private static string ReadCompositionString(IntPtr himc)
    {
        var length = ImeNativeMethods.ImmGetCompositionStringW(himc, ImeNativeMethods.GCS_COMPSTR, null, 0);
        if (length <= 0)
        {
            return string.Empty;
        }
        var buffer = new byte[length];
        var written = ImeNativeMethods.ImmGetCompositionStringW(himc, ImeNativeMethods.GCS_COMPSTR, buffer, length);
        return written <= 0 ? string.Empty : Encoding.Unicode.GetString(buffer, 0, written);
    }

    private static byte[] ReadCompositionAttributes(IntPtr himc)
    {
        var length = ImeNativeMethods.ImmGetCompositionStringW(himc, ImeNativeMethods.GCS_COMPATTR, null, 0);
        if (length <= 0)
        {
            return Array.Empty<byte>();
        }
        var buffer = new byte[length];
        var written = ImeNativeMethods.ImmGetCompositionStringW(himc, ImeNativeMethods.GCS_COMPATTR, buffer, length);
        return written <= 0 ? Array.Empty<byte>() : buffer;
    }

    private static (int Start, int Length) FindActiveClause(byte[] attributes, int textLength)
    {
        var start = -1;
        var end = -1;
        var count = Math.Min(attributes.Length, textLength);
        for (var i = 0; i < count; i++)
        {
            if (attributes[i] is ImeNativeMethods.ATTR_TARGET_CONVERTED or ImeNativeMethods.ATTR_TARGET_NOTCONVERTED)
            {
                if (start < 0)
                {
                    start = i;
                }
                end = i + 1;
            }
        }
        return start < 0 ? (0, 0) : (start, end - start);
    }
}
