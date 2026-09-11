using System.Runtime.InteropServices;
using System.Windows.Shell;
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
}
