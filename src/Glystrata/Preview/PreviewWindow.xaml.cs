using Glystrata.Controls;

namespace Glystrata.Preview;

public partial class PreviewWindow : Window
{
    private readonly DocumentViewState _view;
    private readonly IMarkdownPreviewService _previewService;
    private readonly WpfMarkdownRenderer _renderer;
    private readonly LocalizationService _localization;
    private AppSettings _settings;
    private readonly DispatcherTimer _refreshTimer;
    private int _refreshGeneration;
    private bool _closed;
    private double _zoomPercent = 100;
    private bool _updatingZoom;
    private readonly MenuItem _copyMenuItem = new();
    private readonly MenuItem _selectAllMenuItem = new();

    public PreviewWindow(
        DocumentViewState view,
        IMarkdownPreviewService previewService,
        WpfMarkdownRenderer renderer,
        LocalizationService localization,
        AppSettings settings)
    {
        InitializeComponent();
        CustomTitleBar.Attach(this, localization);
        _view = view;
        _previewService = previewService;
        _renderer = renderer;
        _localization = localization;
        _settings = settings;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            Refresh();
        };
        _view.Document.TextChanged += Document_TextChanged;
        _localization.LanguageChanged += Localization_LanguageChanged;
        Refresh();

        Viewer.MinZoom = 50;
        Viewer.MaxZoom = 200;
        Viewer.ZoomIncrement = 10;
        ZoomSlider.ValueChanged += ZoomSlider_ValueChanged;
        ZoomPercentText.MouseLeftButtonDown += ZoomPercentText_MouseLeftButtonDown;
        Viewer.PreviewMouseWheel += Viewer_PreviewMouseWheel;

        _copyMenuItem.Command = ApplicationCommands.Copy;
        _selectAllMenuItem.Command = ApplicationCommands.SelectAll;
        Viewer.ContextMenu = new ContextMenu
        {
            Items = { _copyMenuItem, _selectAllMenuItem }
        };

        RefreshZoomTooltip();
        RefreshContextMenuText();
        ApplyZoom(100);
    }

    public DocumentViewState View => _view;

    public event EventHandler? PreviewClosed;

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        ScheduleRefresh();
    }

    public void ApplyTheme()
    {
        Background = (Brush)Application.Current.FindResource("PreviewBackgroundBrush");
        RootGrid.Background = Background;
        ScheduleRefresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _view.Document.TextChanged -= Document_TextChanged;
        _localization.LanguageChanged -= Localization_LanguageChanged;
        _refreshTimer.Stop();
        PreviewClosed?.Invoke(this, EventArgs.Empty);
        base.OnClosed(e);
    }

    private void Document_TextChanged(object? sender, EventArgs e) => ScheduleRefresh();

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        Title = GetTitle();
        RefreshZoomTooltip();
        RefreshContextMenuText();
        Refresh();
    }

    private async void Refresh()
    {
        var generation = ++_refreshGeneration;
        var text = _view.Document.Text;
        var sourceDirectory = _view.Document.FilePath is { } path
            ? Path.GetDirectoryName(path) ?? Environment.CurrentDirectory
            : Environment.CurrentDirectory;

        MarkdownPreviewDocument parsed;
        try
        {
            parsed = await Task.Run(() => _previewService.Parse(text, sourceDirectory));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        if (_closed || generation != _refreshGeneration)
        {
            return;
        }

        var readerPalette = _settings.Theme == ThemeKind.Dark ? _settings.DarkReaderPalette : _settings.LightReaderPalette;
        Viewer.Document = _renderer.Render(parsed, _settings.PreviewTypography, _settings.Theme, _localization, readerPalette);
        Title = GetTitle();
        ApplyZoom(_zoomPercent);
    }

    private void ScheduleRefresh()
    {
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private string GetTitle()
    {
        var fileName = _view.Document.IsUntitled
            ? _localization.Get("document.untitled")
            : Path.GetFileName(_view.Document.FilePath);
        return $"{fileName} — {_localization.Get("preview.reader")}";
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingZoom)
        {
            return;
        }
        ApplyZoom(e.NewValue);
    }

    private void ZoomPercentText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ApplyZoom(100);
        }
    }

    private void Viewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }
        ApplyZoom(_zoomPercent + (e.Delta > 0 ? 10 : -10));
        e.Handled = true;
    }

    private void ApplyZoom(double percent)
    {
        percent = Math.Clamp(percent, 50, 200);
        _zoomPercent = percent;
        _updatingZoom = true;
        try
        {
            Viewer.Zoom = percent;
            ZoomSlider.Value = percent;
            ZoomPercentText.Text = $"{Math.Round(percent)}%";
        }
        finally
        {
            _updatingZoom = false;
        }
    }

    private void RefreshZoomTooltip()
    {
        var tooltip = _localization.Get("preview.zoom");
        ZoomSlider.ToolTip = tooltip;
        ZoomPercentText.ToolTip = tooltip;
    }

    private void RefreshContextMenuText()
    {
        _copyMenuItem.Header = _localization.Get("edit.copy");
        _selectAllMenuItem.Header = _localization.Get("edit.selectAll");
    }
}
