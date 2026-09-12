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

        var result = -1;
        var window = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.Height,
            Width = 420,
            MinHeight = 150,
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
        root.Children.Add(text);

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
        CustomTitleBar.Attach(window, localization);
        window.ShowDialog();
        return result;
    }

    public static bool Confirm(Window? owner, LocalizationService localization, string title, string message, string confirmLabel) =>
        Show(owner, localization, title, message, confirmLabel, localization.Get("dialog.cancel")) == 0;

    public static void Inform(Window? owner, LocalizationService localization, string title, string message) =>
        Show(owner, localization, title, message, localization.Get("dialog.ok"));
}
