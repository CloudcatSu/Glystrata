using Glystrata.Controls;

namespace Glystrata.Dialogs;

/// <summary>
/// Themed replacement for <see cref="MessageBox"/> so prompts match the rest of the app.
/// Returns the index of the chosen button, or -1 when the dialog was dismissed.
/// </summary>
public static class MessageDialogs
{
    public static int Show(Window? owner, LocalizationService localization, string title, string message, params string[] buttons)
    {
        if (buttons.Length == 0)
        {
            buttons = new[] { localization.Get("dialog.ok") };
        }

        const double dialogWidth = 420;
        var result = -1;
        var window = new Window
        {
            Title = title,
            Width = dialogWidth,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner
        };
        window.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");

        var root = new DockPanel { Margin = new Thickness(20, 18, 20, 16), LastChildFill = true };
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        DockPanel.SetDock(buttonRow, Dock.Bottom);
        root.Children.Add(buttonRow);

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
        var textScroll = new ScrollViewer
        {
            Content = text,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            BorderThickness = new Thickness(0)
        };
        root.Children.Add(textScroll);

        for (var index = 0; index < buttons.Length; index++)
        {
            var choice = index;
            var isDefault = index == 0;
            var button = new Button
            {
                Content = buttons[index],
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(6, 0, 0, 0),
                MinWidth = 88,
                IsDefault = isDefault,
                IsCancel = index == buttons.Length - 1
            };
            if (isDefault)
            {
                button.SetResourceReference(Button.BackgroundProperty, "AccentBrush");
                button.Foreground = Brushes.White;
            }
            button.Click += (_, _) =>
            {
                result = choice;
                window.DialogResult = true;
            };
            buttonRow.Children.Add(button);
        }

        window.Content = root;
        // The window opens at its final size on purpose: letting SizeToContent resize it after the
        // custom chrome is up leaves the whole window painted black until something forces a redraw.
        window.Height = MeasureHeight(root, dialogWidth);
        CustomTitleBar.Attach(window, localization);
        window.ShowDialog();
        return result;
    }

    private static double MeasureHeight(FrameworkElement content, double dialogWidth)
    {
        const double titleBarHeight = 32;
        content.Measure(new System.Windows.Size(dialogWidth, double.PositiveInfinity));
        var available = SystemParameters.WorkArea.Height * 0.7;
        return Math.Clamp(Math.Ceiling(titleBarHeight + content.DesiredSize.Height) + 2, 150, Math.Max(200, available));
    }

    public static bool Confirm(Window? owner, LocalizationService localization, string title, string message, string confirmLabel) =>
        Show(owner, localization, title, message, confirmLabel, localization.Get("dialog.cancel")) == 0;

    public static void Inform(Window? owner, LocalizationService localization, string title, string message) =>
        Show(owner, localization, title, message, localization.Get("dialog.ok"));
}
