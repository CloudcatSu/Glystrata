namespace MDeditor.Preview;

public partial class PreviewWindow : Window
{
    private readonly DocumentViewState _view;
    private readonly IMarkdownPreviewService _previewService;
    private readonly WpfMarkdownRenderer _renderer;
    private readonly LocalizationService _localization;
    private AppSettings _settings;
    private readonly DispatcherTimer _refreshTimer;

    public PreviewWindow(
        DocumentViewState view,
        IMarkdownPreviewService previewService,
        WpfMarkdownRenderer renderer,
        LocalizationService localization,
        AppSettings settings)
    {
        InitializeComponent();
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
        Refresh();
    }

    private void Refresh()
    {
        var sourceDirectory = _view.Document.FilePath is { } path
            ? Path.GetDirectoryName(path) ?? Environment.CurrentDirectory
            : Environment.CurrentDirectory;
        var parsed = _previewService.Parse(_view.Document.Text, sourceDirectory);
        Viewer.Document = _renderer.Render(parsed, _settings.PreviewTypography, _settings.Theme, _localization);
        Title = GetTitle();
    }

    private void ScheduleRefresh()
    {
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private string GetTitle()
    {
        var fileName = _view.Document.IsUntitled
            ? "Untitled"
            : Path.GetFileName(_view.Document.FilePath);
        return $"{fileName} — {_localization.Get("preview.reader")}";
    }
}
