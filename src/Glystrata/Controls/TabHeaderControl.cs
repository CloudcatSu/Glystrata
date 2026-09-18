namespace Glystrata.Controls;

public sealed class TabHeaderControl : StackPanel
{
    /// <summary>Drag-data format used to carry a tab's <see cref="DocumentViewState.ViewId"/> when reordering
    /// tabs or dropping one onto a sidebar group.</summary>
    public const string TabDragFormat = "GlystrataTabViewId";

    private readonly Border _groupColorBar;
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

        _groupColorBar = new Border
        {
            Width = 3,
            Height = 14,
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };

        _title = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 220,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var eye = IconFactory.Create("Eye.png", 15);
        _previewButton = CreateButton(eye, "toolbar.preview");
        // The icon follows the button's foreground, which switches to the accent colour while the reader is open.
        eye.SetBinding(System.Windows.Shapes.Shape.FillProperty, new System.Windows.Data.Binding(nameof(Button.Foreground)) { Source = _previewButton });
        _previewButton.Click += (_, _) => PreviewRequested?.Invoke(this, _view);

        _closeButton = CreateButton("×", "dialog.close");
        _closeButton.Click += (_, _) => CloseRequested?.Invoke(this, _view);

        Children.Add(_groupColorBar);
        Children.Add(_title);
        Children.Add(_previewButton);
        Children.Add(_closeButton);
        Refresh();
    }

    public DocumentViewState View => _view;

    /// <summary>Shows the owning group's colour beside the file name, or hides the bar when the tab
    /// belongs to no group or to one with no colour.</summary>
    public void SetGroupColor(Brush? brush)
    {
        _groupColorBar.Background = brush;
        _groupColorBar.Visibility = brush is null ? Visibility.Collapsed : Visibility.Visible;
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
            ? _localization.Get("document.untitled")
            : Path.GetFileName(_view.Document.FilePath);
        _title.Text = _view.Document.IsModified ? $"{name} •" : name;
        _title.ToolTip = _view.Document.FilePath ?? name;
        var previewAvailable = !IsYamlFile(_view.Document.FilePath);
        _previewButton.IsEnabled = previewAvailable;
        _previewButton.ToolTip = _localization.Get(previewAvailable ? "toolbar.preview" : "toolbar.previewUnavailable");
        _closeButton.ToolTip = _localization.Get("dialog.close");
    }

    public void RefreshLanguage() => Refresh();

    private static Button CreateButton(object content, string tooltipKey)
    {
        return new Button
        {
            Content = content,
            ToolTip = tooltipKey,
            FontSize = 16,
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
