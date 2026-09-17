using Cursors = System.Windows.Input.Cursors;
using Ellipse = System.Windows.Shapes.Ellipse;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Glystrata.Controls;

namespace Glystrata.Dialogs;

/// <summary>A small HSV color picker styled with the app's own theme, in place of the OS <c>ColorDialog</c>.</summary>
public static class ColorPickerDialog
{
    private const double SvSize = 220;
    private const double HueHeight = 18;

    public static string? Show(Window? owner, string title, string initialHex, LocalizationService? localization = null)
    {
        var initial = TryParseHex(initialHex, out var parsedInitial) ? parsedInitial : Colors.Gray;
        var (hue, saturation, value) = RgbToHsv(initial);

        var window = new Window
        {
            Title = title,
            Width = 280,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner,
            Background = (Brush)Application.Current.FindResource("WindowBackgroundBrush"),
            ShowInTaskbar = false
        };

        var root = new StackPanel { Margin = new Thickness(18) };

        var svCanvas = new Canvas { Width = SvSize, Height = SvSize, ClipToBounds = true, Cursor = Cursors.Cross };
        var hueBase = new Rectangle { Width = SvSize, Height = SvSize, Fill = new SolidColorBrush(HsvToRgb(hue, 1, 1)) };
        var whiteFade = new Rectangle
        {
            Width = SvSize,
            Height = SvSize,
            Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(1, 0))
        };
        var blackFade = new Rectangle
        {
            Width = SvSize,
            Height = SvSize,
            Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, new Point(0, 0), new Point(0, 1))
        };
        var svThumb = new Ellipse { Width = 14, Height = 14, Stroke = Brushes.White, StrokeThickness = 2, Fill = Brushes.Transparent };
        svCanvas.Children.Add(hueBase);
        svCanvas.Children.Add(whiteFade);
        svCanvas.Children.Add(blackFade);
        svCanvas.Children.Add(svThumb);
        var svBorder = new Border
        {
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Child = svCanvas
        };
        root.Children.Add(svBorder);

        var hueCanvas = new Canvas { Width = SvSize, Height = HueHeight, Margin = new Thickness(0, 10, 0, 0), Cursor = Cursors.Hand };
        var hueStrip = new Rectangle { Width = SvSize, Height = HueHeight, RadiusX = 3, RadiusY = 3, Fill = CreateHueBrush() };
        var hueThumb = new Rectangle { Width = 6, Height = HueHeight + 4, Fill = Brushes.Transparent, Stroke = Brushes.White, StrokeThickness = 2 };
        hueCanvas.Children.Add(hueStrip);
        hueCanvas.Children.Add(hueThumb);
        root.Children.Add(hueCanvas);

        var previewRow = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        var swatch = new Border
        {
            Width = 32,
            Height = 28,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 8, 0),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1)
        };
        DockPanel.SetDock(swatch, Dock.Left);
        var hexBox = new TextBox { VerticalContentAlignment = VerticalAlignment.Center };
        previewRow.Children.Add(swatch);
        previewRow.Children.Add(hexBox);
        root.Children.Add(previewRow);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = localization?.Get("dialog.cancel") ?? "Cancel", Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(5, 0, 0, 0) };
        cancel.Click += (_, _) => window.DialogResult = false;
        var ok = new Button { Content = localization?.Get("dialog.ok") ?? "OK", Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(5, 0, 0, 0) };
        ok.Click += (_, _) => window.DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        root.Children.Add(buttons);

        window.Content = root;
        if (localization is not null)
        {
            CustomTitleBar.Attach(window, localization);
        }
        DialogKeys.AttachConfirmCancel(window, () => window.DialogResult = true);

        var updatingFromCode = false;

        void UpdateThumbs()
        {
            Canvas.SetLeft(svThumb, saturation * SvSize - svThumb.Width / 2);
            Canvas.SetTop(svThumb, (1 - value) * SvSize - svThumb.Height / 2);
            Canvas.SetLeft(hueThumb, hue / 360.0 * SvSize - hueThumb.Width / 2);
        }

        void UpdateFromHsv()
        {
            updatingFromCode = true;
            hueBase.Fill = new SolidColorBrush(HsvToRgb(hue, 1, 1));
            var rgb = HsvToRgb(hue, saturation, value);
            swatch.Background = new SolidColorBrush(rgb);
            hexBox.Text = $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}";
            UpdateThumbs();
            updatingFromCode = false;
        }

        void PickFromSv(System.Windows.Point point)
        {
            saturation = Math.Clamp(point.X / SvSize, 0, 1);
            value = Math.Clamp(1 - point.Y / SvSize, 0, 1);
            UpdateFromHsv();
        }

        void PickFromHue(System.Windows.Point point)
        {
            hue = Math.Clamp(point.X / SvSize, 0, 1) * 360;
            UpdateFromHsv();
        }

        svCanvas.MouseLeftButtonDown += (_, e) =>
        {
            svCanvas.CaptureMouse();
            PickFromSv(e.GetPosition(svCanvas));
        };
        svCanvas.MouseMove += (_, e) =>
        {
            if (svCanvas.IsMouseCaptured)
            {
                PickFromSv(e.GetPosition(svCanvas));
            }
        };
        svCanvas.MouseLeftButtonUp += (_, _) => svCanvas.ReleaseMouseCapture();

        hueCanvas.MouseLeftButtonDown += (_, e) =>
        {
            hueCanvas.CaptureMouse();
            PickFromHue(e.GetPosition(hueCanvas));
        };
        hueCanvas.MouseMove += (_, e) =>
        {
            if (hueCanvas.IsMouseCaptured)
            {
                PickFromHue(e.GetPosition(hueCanvas));
            }
        };
        hueCanvas.MouseLeftButtonUp += (_, _) => hueCanvas.ReleaseMouseCapture();

        hexBox.TextChanged += (_, _) =>
        {
            if (updatingFromCode || !TryParseHex(hexBox.Text, out var typed))
            {
                return;
            }
            (hue, saturation, value) = RgbToHsv(typed);
            UpdateFromHsv();
        };

        window.Loaded += (_, _) => UpdateFromHsv();

        return window.ShowDialog() == true && TryParseHex(hexBox.Text, out var final)
            ? $"#{final.R:X2}{final.G:X2}{final.B:X2}"
            : null;
    }

    private static LinearGradientBrush CreateHueBrush()
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        var hues = new[] { 0, 60, 120, 180, 240, 300, 360 };
        for (var i = 0; i < hues.Length; i++)
        {
            brush.GradientStops.Add(new GradientStop(HsvToRgb(hues[i], 1, 1), i / (double)(hues.Length - 1)));
        }
        return brush;
    }

    private static bool TryParseHex(string? value, out Color color)
    {
        color = Colors.Gray;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            if (new BrushConverter().ConvertFromString(value) is not SolidColorBrush brush)
            {
                return false;
            }
            color = brush.Color;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static (double Hue, double Saturation, double Value) RgbToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double hue;
        if (delta < 0.0001)
        {
            hue = 0;
        }
        else if (max == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / delta) + 4);
        }
        if (hue < 0)
        {
            hue += 360;
        }

        var saturation = max < 0.0001 ? 0 : delta / max;
        return (hue, saturation, max);
    }

    private static Color HsvToRgb(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60.0 % 2 - 1));
        var m = value - c;

        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
