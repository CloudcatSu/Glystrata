namespace MDeditor.Preview;

public sealed class PreviewWindowManager
{
    private readonly Dictionary<Guid, PreviewWindow> _windows = new();
    private readonly IMarkdownPreviewService _previewService;
    private readonly WpfMarkdownRenderer _renderer;
    private readonly LocalizationService _localization;
    private readonly Func<AppSettings> _settingsProvider;

    public PreviewWindowManager(
        IMarkdownPreviewService previewService,
        WpfMarkdownRenderer renderer,
        LocalizationService localization,
        Func<AppSettings> settingsProvider)
    {
        _previewService = previewService;
        _renderer = renderer;
        _localization = localization;
        _settingsProvider = settingsProvider;
    }

    public bool IsOpen(DocumentViewState view) => _windows.ContainsKey(view.ViewId);

    public event EventHandler<DocumentViewState>? StateChanged;

    public void Toggle(DocumentViewState view, Window owner)
    {
        if (_windows.TryGetValue(view.ViewId, out var existing))
        {
            existing.Close();
            return;
        }

        var window = new PreviewWindow(view, _previewService, _renderer, _localization, _settingsProvider())
        {
            Owner = owner
        };
        window.PreviewClosed += (_, _) =>
        {
            _windows.Remove(view.ViewId);
            StateChanged?.Invoke(this, view);
        };
        _windows[view.ViewId] = window;
        window.Show();
    }

    public void Close(DocumentViewState view)
    {
        if (_windows.TryGetValue(view.ViewId, out var window))
        {
            window.Close();
        }
    }

    public void UpdateSettings(AppSettings settings)
    {
        foreach (var window in _windows.Values)
        {
            window.UpdateSettings(settings);
        }
    }

    public void ApplyTheme()
    {
        foreach (var window in _windows.Values)
        {
            window.ApplyTheme();
        }
    }

    public void RefreshLanguage()
    {
        foreach (var window in _windows.Values)
        {
            window.UpdateSettings(_settingsProvider());
        }
    }

    public void CloseAll()
    {
        foreach (var window in _windows.Values.ToArray())
        {
            window.Close();
        }
        _windows.Clear();
    }
}
