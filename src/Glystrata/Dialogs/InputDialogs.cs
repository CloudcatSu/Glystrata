using Glystrata.Controls;

namespace Glystrata.Dialogs;

public readonly record struct PromptResult(bool Ok, string Value)
{
    public static readonly PromptResult Cancelled = new(false, string.Empty);
}

public static class InputDialogs
{
    public static PromptResult Prompt(Window? owner, string title, string label, string initialValue = "", LocalizationService? localization = null)
    {
        var window = new Window
        {
            Title = title,
            Width = 390,
            Height = 170,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner,
            Background = (Brush)Application.Current.FindResource("WindowBackgroundBrush")
        };

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var text = new TextBlock
        {
            Text = label,
            Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(text, 0);
        root.Children.Add(text);
        var input = new TextBox { Text = initialValue, MinWidth = 300 };
        Grid.SetRow(input, 1);
        root.Children.Add(input);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var cancel = new Button { Content = localization?.Get("dialog.cancel") ?? "Cancel", Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(5, 0, 0, 0) };
        cancel.Click += (_, _) => window.DialogResult = false;
        var ok = new Button { Content = localization?.Get("dialog.ok") ?? "OK", Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(5, 0, 0, 0) };
        ok.Click += (_, _) => window.DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        window.Content = root;
        if (localization is not null)
        {
            CustomTitleBar.Attach(window, localization);
        }
        DialogKeys.AttachConfirmCancel(window, () => window.DialogResult = true);
        window.Loaded += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };
        return window.ShowDialog() == true ? new PromptResult(true, input.Text.Trim()) : PromptResult.Cancelled;
    }

    public static Group? SelectGroup(Window? owner, string title, IEnumerable<Group> groups, Guid? selectedId = null, LocalizationService? localization = null)
    {
        var candidates = groups.ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var window = new Window
        {
            Title = title,
            Width = 360,
            Height = 160,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner,
            Background = (Brush)Application.Current.FindResource("WindowBackgroundBrush")
        };
        var root = new StackPanel { Margin = new Thickness(18) };
        var box = new ComboBox
        {
            ItemsSource = candidates.Select(group => group.Name).ToArray(),
            SelectedIndex = Math.Max(0, Array.FindIndex(candidates, group => group.Id == selectedId))
        };
        root.Children.Add(box);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
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
        return window.ShowDialog() == true && box.SelectedIndex >= 0 && box.SelectedIndex < candidates.Length
            ? candidates[box.SelectedIndex]
            : null;
    }
}
