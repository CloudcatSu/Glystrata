using System.Runtime.InteropServices;

namespace Glystrata.Editing;

internal static class ImeNativeMethods
{
    public const int WM_IME_STARTCOMPOSITION = 0x010D;
    public const int WM_IME_ENDCOMPOSITION = 0x010E;
    public const int WM_IME_COMPOSITION = 0x010F;
    public const int WM_IME_SETCONTEXT = 0x0281;

    public const long ISC_SHOWUICOMPOSITIONWINDOW = 0x80000000L;

    public const int GCS_COMPSTR = 0x0008;
    public const int GCS_COMPATTR = 0x0010;
    public const int GCS_CURSORPOS = 0x0080;

    public const byte ATTR_TARGET_CONVERTED = 1;
    public const byte ATTR_TARGET_NOTCONVERTED = 3;

    public const int CFS_EXCLUDE = 0x0080;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CANDIDATEFORM
    {
        public int dwIndex;
        public int dwStyle;
        public POINT ptCurrentPos;
        public RECT rcArea;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("imm32.dll")]
    public static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    public static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    public static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[]? lpBuf, int dwBufLen);

    [DllImport("imm32.dll")]
    public static extern bool ImmSetCandidateWindow(IntPtr hIMC, ref CANDIDATEFORM candidateForm);
}
