using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Shell;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Shape = System.Windows.Shapes.Shape;

namespace Glystrata.Controls;

public sealed class CustomTitleBar
{
    private const string MinimizeGlyph = "\uE921";
    private const string MaximizeGlyph = "\uE922";
    private const string RestoreGlyph = "\uE923";
    private const string CloseGlyph = "\uE8BB";

    private const int SmCxSizeFrame = 32;
    private const int SmCxPaddedBorder = 92;

    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCMOUSEMOVE = 0x00A0;
    private const int WM_NCMOUSELEAVE = 0x02A2;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCLBUTTONUP = 0x00A2;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int HTMAXBUTTON = 9;

    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_ALLCHILDREN = 0x0080;
    private const uint RDW_UPDATENOW = 0x0100;
    private const uint RDW_FRAME = 0x0400;

    private readonly Window _window;
    private readonly LocalizationService _localization;
    private readonly bool _resizable;
    private readonly bool _canMinimize;

    private DockPanel _dock = null!;
    private TextBlock _titleText = null!;
    private Rectangle _logo = null!;
    private Button? _minimizeButton;
    private Button? _maximizeRestoreButton;
    private Button _closeButton = null!;

    private CustomTitleBar(Window window, LocalizationService localization)
    {
        _window = window;
        _localization = localization;
        _resizable = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
        _canMinimize = window.ResizeMode != ResizeMode.NoResize;
    }

    public static CustomTitleBar Attach(Window window, LocalizationService localization)
    {
        var titleBar = new CustomTitleBar(window, localization);
        titleBar.Initialize();
        return titleBar;
    }

    private void Initialize()
    {
        WindowChrome.SetWindowChrome(_window, new WindowChrome
        {
            CaptionHeight = 32,
            ResizeBorderThickness = _resizable ? new Thickness(6) : new Thickness(0),
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(0, 0, 0, 1),
            UseAeroCaptionButtons = false
        });

        var existingContent = _window.Content as UIElement;
        _window.Content = null;

        _dock = new DockPanel();
        var bar = BuildBar();
        DockPanel.SetDock(bar, Dock.Top);
        _dock.Children.Add(bar);
        if (existingContent is not null)
        {
            _dock.Children.Add(existingContent);
        }
        _window.Content = _dock;

        _window.StateChanged += Window_StateChanged;
        _window.Activated += Window_ActivationChanged;
        _window.Deactivated += Window_ActivationChanged;
        _window.SourceInitialized += Window_SourceInitialized;
        _window.Loaded += Window_Loaded;
        UpdateMaximizeRestoreButton();
        UpdateOpacity();
    }

    private Grid BuildBar()
    {
        var bar = new Grid { Height = 32 };
        bar.SetResourceReference(Panel.BackgroundProperty, "TitleBarBackgroundBrush");
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _logo = new Rectangle
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(12, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            OpacityMask = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Resources/Icons/GlystrataGlyph.png")))
        };
        _logo.SetResourceReference(Shape.FillProperty, "MenuForegroundBrush");
        Grid.SetColumn(_logo, 0);
        bar.Children.Add(_logo);

        _titleText = new TextBlock
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        _titleText.SetResourceReference(TextBlock.ForegroundProperty, "MenuForegroundBrush");
        _titleText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Window.Title)) { Source = _window });
        Grid.SetColumn(_titleText, 1);
        bar.Children.Add(_titleText);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(buttons, 2);
        if (_canMinimize)
        {
            _minimizeButton = CreateCaptionButton(MinimizeGlyph, "titlebar.minimize", () => SystemCommands.MinimizeWindow(_window), isClose: false);
            buttons.Children.Add(_minimizeButton);
        }
        if (_resizable)
        {
            _maximizeRestoreButton = CreateCaptionButton(MaximizeGlyph, "titlebar.maximize", ToggleMaximizeRestore, isClose: false);
            buttons.Children.Add(_maximizeRestoreButton);
        }
        _closeButton = CreateCaptionButton(CloseGlyph, "titlebar.close", () => SystemCommands.CloseWindow(_window), isClose: true);
        buttons.Children.Add(_closeButton);
        bar.Children.Add(buttons);
        return bar;
    }

    private Button CreateCaptionButton(string glyph, string tooltipKey, Action action, bool isClose)
    {
        var button = new Button
        {
            Content = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 10,
            Width = 46,
            Height = 32,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            ToolTip = _localization.Get(tooltipKey),
            Style = (Style)Application.Current.FindResource(isClose ? "TitleBarCloseButtonStyle" : "TitleBarButtonStyle")
        };
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        button.Click += (_, _) => action();
        return button;
    }

    public void RefreshLanguage()
    {
        if (_minimizeButton is not null)
        {
            _minimizeButton.ToolTip = _localization.Get("titlebar.minimize");
        }
        _closeButton.ToolTip = _localization.Get("titlebar.close");
        UpdateMaximizeRestoreButton();
    }

    private void ToggleMaximizeRestore()
    {
        if (_window.WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(_window);
        }
        else
        {
            SystemCommands.MaximizeWindow(_window);
        }
    }

    private void UpdateMaximizeRestoreButton()
    {
        if (_maximizeRestoreButton is null)
        {
            return;
        }
        var maximized = _window.WindowState == WindowState.Maximized;
        _maximizeRestoreButton.Content = maximized ? RestoreGlyph : MaximizeGlyph;
        _maximizeRestoreButton.ToolTip = _localization.Get(maximized ? "titlebar.restore" : "titlebar.maximize");
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        _dock.Margin = _window.WindowState == WindowState.Maximized
            ? GetMaximizedFrameOverhang()
            : new Thickness(0);
        UpdateMaximizeRestoreButton();
    }

    // Maximized overhang = sizing frame + padded border; SystemParameters.WindowResizeBorderThickness omits the latter.
    private Thickness GetMaximizedFrameOverhang()
    {
        var dpi = VisualTreeHelper.GetDpi(_window);
        var dpiValue = (uint)Math.Round(dpi.PixelsPerInchX);
        var pixels = GetSystemMetricsForDpi(SmCxSizeFrame, dpiValue) + GetSystemMetricsForDpi(SmCxPaddedBorder, dpiValue);
        var overhang = pixels / dpi.DpiScaleX;
        return new Thickness(overhang);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

    private void Window_ActivationChanged(object? sender, EventArgs e) => UpdateOpacity();

    private void UpdateOpacity()
    {
        var opacity = _window.IsActive ? 1.0 : 0.6;
        _titleText.Opacity = opacity;
        _logo.Opacity = opacity;
    }

    // Subscribing here (after WindowChrome.SetWindowChrome above already queued its own SourceInitialized
    // handler) means our hook gets added to the HwndSource after WindowChrome's; HwndSource invokes hooks
    // most-recently-added-first, so ours runs before WindowChrome's and can override its WM_NCHITTEST result.
    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(_window).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
        ApplyCompositionBackground(source);
    }

    // WindowChrome extends the DWM frame into the client area and leaves the composition target
    // transparent so the frame can show through. On Windows 11 that frame is solid black, so anything
    // WPF has not painted reads as black rather than as an unfinished window. Painting the target with
    // the theme background keeps that worst case looking like an ordinary window.
    private void ApplyCompositionBackground(HwndSource? source)
    {
        if (source?.CompositionTarget is not { } target)
        {
            return;
        }
        if (_window.TryFindResource("WindowBackgroundBrush") is SolidColorBrush brush)
        {
            target.BackgroundColor = brush.Color;
        }
    }

    /// <summary>Re-reads the theme background into the composition target. Without this the colour
    /// picked when the window was created sticks: switch from light to dark and anything WPF has not
    /// painted keeps showing the light background.</summary>
    public void RefreshCompositionBackground()
    {
        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }
        ApplyCompositionBackground(HwndSource.FromHwnd(handle));
    }

    // A window that never animates - a dialog with no caret, no hover, no blinking anything - draws a
    // single frame when it opens. If that frame does not reach the screen nothing asks for another one
    // and the window stays blank (black, per the comment above) until it is reopened. Ask Win32 for one
    // paint once the content is up, and again when the dispatcher goes idle, so the frame is presented.
    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RepaintWindow));
        _window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(RepaintWindow));
    }

    private void RepaintWindow()
    {
        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero || !_window.IsVisible)
        {
            return;
        }
        RedrawWindow(handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_UPDATENOW | RDW_FRAME);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Checked before the maximize-button guard below: a window without a maximize button still
        // needs to hear about the system theme changing.
        if (msg == WM_SETTINGCHANGE &&
            string.Equals(Marshal.PtrToStringAuto(lParam), "ImmersiveColorSet", StringComparison.Ordinal))
        {
            SystemThemeWatcher.NotifyColorSchemeChanged();
            return IntPtr.Zero;
        }

        if (_maximizeRestoreButton is null)
        {
            return IntPtr.Zero;
        }

        switch (msg)
        {
            case WM_NCHITTEST:
                if (TryHitTestMaximizeButton(lParam))
                {
                    handled = true;
                    return new IntPtr(HTMAXBUTTON);
                }
                break;
            case WM_NCMOUSEMOVE:
                SetMaximizeButtonBackground(wParam.ToInt32() == HTMAXBUTTON ? "TitleBarButtonHoverBrush" : null);
                break;
            case WM_NCMOUSELEAVE:
                SetMaximizeButtonBackground(null);
                break;
            case WM_NCLBUTTONDOWN:
                if (wParam.ToInt32() == HTMAXBUTTON)
                {
                    SetMaximizeButtonBackground("TitleBarButtonPressedBrush");
                    handled = true;
                    return IntPtr.Zero;
                }
                break;
            case WM_NCLBUTTONUP:
                if (wParam.ToInt32() == HTMAXBUTTON)
                {
                    SetMaximizeButtonBackground("TitleBarButtonHoverBrush");
                    ToggleMaximizeRestore();
                    handled = true;
                    return IntPtr.Zero;
                }
                break;
        }
        return IntPtr.Zero;
    }

    private bool TryHitTestMaximizeButton(IntPtr lParam)
    {
        if (_maximizeRestoreButton is null || !_maximizeRestoreButton.IsVisible || PresentationSource.FromVisual(_maximizeRestoreButton) is null)
        {
            return false;
        }

        var raw = lParam.ToInt64();
        var x = unchecked((short)(raw & 0xFFFF));
        var y = unchecked((short)((raw >> 16) & 0xFFFF));
        var screenPoint = new Point(x, y);

        var topLeft = _maximizeRestoreButton.PointToScreen(new Point(0, 0));
        var bottomRight = _maximizeRestoreButton.PointToScreen(new Point(_maximizeRestoreButton.ActualWidth, _maximizeRestoreButton.ActualHeight));
        return new Rect(topLeft, bottomRight).Contains(screenPoint);
    }

    private void SetMaximizeButtonBackground(string? resourceKey)
    {
        if (_maximizeRestoreButton is null)
        {
            return;
        }
        if (resourceKey is null)
        {
            _maximizeRestoreButton.ClearValue(Button.BackgroundProperty);
        }
        else
        {
            _maximizeRestoreButton.SetResourceReference(Button.BackgroundProperty, resourceKey);
        }
    }
}
