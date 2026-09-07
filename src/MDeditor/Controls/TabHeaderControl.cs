namespace MDeditor.Controls;

public sealed class TabHeaderControl : StackPanel
{
    private readonly TextBlock _title;
    private readonly Button _previewButton;
    private readonly Button _closeButton;
    private readonly LocalizationService _localization;
    private readonly DocumentViewState _view;

    public TabHeaderControl(DocumentViewState view, LocalizationService localization)
    {
        _view = view;
        _localization = localization;
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(2, 0, 0, 0);

        _title = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 220,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _previewButton = CreateButton("👁", "toolbar.preview");
        _previewButton.Click += (_, _) => PreviewRequested?.Invoke(this, _view);

        _closeButton = CreateButton("×", "dialog.close");
        _closeButton.Click += (_, _) => CloseRequested?.Invoke(this, _view);

        Children.Add(_title);
        Children.Add(_previewButton);
        Children.Add(_closeButton);
        Refresh();
    }

    public event EventHandler<DocumentViewState>? PreviewRequested;

    public event EventHandler<DocumentViewState>? CloseRequested;

    public bool IsPreviewOpen
    {
        set
        {
            _previewButton.Opacity = value ? 1 : 0.62;
            _previewButton.Foreground = value
                ? (Brush)Application.Current.FindResource("AccentBrush")
                : (Brush)Application.Current.FindResource("SecondaryTextBrush");
        }
    }

    public void Refresh()
    {
        var name = _view.Document.IsUntitled
            ? "Untitled"
            : Path.GetFileName(_view.Document.FilePath);
        _title.Text = _view.Document.IsModified ? $"{name} •" : name;
        _title.ToolTip = _view.Document.FilePath ?? name;
        var previewAvailable = !IsYamlFile(_view.Document.FilePath);
        _previewButton.IsEnabled = previewAvailable;
        _previewButton.ToolTip = _localization.Get(previewAvailable ? "toolbar.preview" : "toolbar.previewUnavailable");
        _closeButton.ToolTip = _localization.Get("dialog.close");
    }

    public void RefreshLanguage()
    {
        var previewAvailable = !IsYamlFile(_view.Document.FilePath);
        _previewButton.ToolTip = _localization.Get(previewAvailable ? "toolbar.preview" : "toolbar.previewUnavailable");
        _closeButton.ToolTip = _localization.Get("dialog.close");
    }

    private static Button CreateButton(string content, string tooltipKey)
    {
        return new Button
        {
            Content = content,
            ToolTip = tooltipKey,
            FontSize = content == "×" ? 16 : 13,
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(5, 0, 0, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush"),
            Focusable = false,
            VerticalContentAlignment = VerticalAlignment.Center
        };
    }

    private static bool IsYamlFile(string? path)
    {
        var extension = Path.GetExtension(path ?? string.Empty);
        return extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }
}
