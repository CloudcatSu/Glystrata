using Glystrata.Controls;

namespace Glystrata.Views;

/// <summary>
/// A document taken out of the main window into a window of its own. It is only a shell: the editor
/// pane inside it is built and wired by <see cref="MainWindow"/> exactly like a pane of the main layout,
/// so saving, snapshots, groups, find and replace and the multi-view sync all go through the same code.
/// Its pane id is simply one that is not part of the main window's layout.
///
/// Deliberately not owned by the main window, so it has its own taskbar button and does not minimize
/// with it. It closes when the app exits.
/// </summary>
public sealed class FloatingEditorWindow : Window
{
    private readonly Border _paneHost = new();
    private readonly TextBlock _pathText;
    private readonly TextBlock _stateText;
    private readonly TextBlock _characterCountText;
    private readonly TextBlock _positionText;
    private readonly CustomTitleBar _titleBar;
    private bool _allowClose;
    private bool _closeRequestPending;

    public FloatingEditorWindow(Guid paneId, LocalizationService localization)
    {
        PaneId = paneId;
        Width = 820;
        Height = 720;
        MinWidth = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");

        _pathText = CreateStatusText(trim: true);
        _stateText = CreateStatusText();
        _characterCountText = CreateStatusText();
        _positionText = CreateStatusText();

        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(_stateText);
        right.Children.Add(new Separator());
        right.Children.Add(_characterCountText);
        right.Children.Add(new Separator());
        right.Children.Add(_positionText);
        var statusContent = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(right, Dock.Right);
        statusContent.Children.Add(right);
        statusContent.Children.Add(_pathText);
        var statusBar = new StatusBar { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        statusBar.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        statusBar.SetResourceReference(ForegroundProperty, "SecondaryTextBrush");
        statusBar.Items.Add(new StatusBarItem
        {
            Content = statusContent,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0)
        });
        var status = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = statusBar };
        status.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        var dock = new DockPanel();
        DockPanel.SetDock(status, Dock.Bottom);
        dock.Children.Add(status);
        dock.Children.Add(_paneHost);
        Content = dock;
        _titleBar = CustomTitleBar.Attach(this, localization);
    }

    public Guid PaneId { get; }

    public EditorPaneControl? Pane => _paneHost.Child as EditorPaneControl;

    /// <summary>The user asked to close the window (title bar, Alt+F4). The window stays open: the owner
    /// decides, because closing the last view of a modified document has to ask about saving first.</summary>
    public event EventHandler? CloseRequested;

    public void SetPane(EditorPaneControl pane) => _paneHost.Child = pane;

    public void SetStatus(string path, string state, string characters, string position)
    {
        _pathText.Text = path;
        _pathText.ToolTip = string.IsNullOrEmpty(path) ? null : path;
        _stateText.Text = state;
        _characterCountText.Text = characters;
        _positionText.Text = position;
    }

    public void RefreshLanguage() => _titleBar.RefreshLanguage();

    public void RefreshCompositionBackground() => _titleBar.RefreshCompositionBackground();

    /// <summary>Closes without asking; for when the owner has already settled the unsaved changes.</summary>
    public void CloseWithoutPrompt()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            // A window cannot be closed from inside its own Closing handler, and the save prompt is
            // modal, so hand the decision to the owner once this event has unwound.
            e.Cancel = true;
            // A second close (another click on X, Alt+F4, a close sent by another program) can arrive
            // while that prompt is still up: its modal loop keeps dispatching. Acting on it would close
            // this window from under its own dialog, which leaves the main window disabled for good.
            if (!_closeRequestPending)
            {
                _closeRequestPending = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        CloseRequested?.Invoke(this, EventArgs.Empty);
                    }
                    finally
                    {
                        _closeRequestPending = false;
                    }
                }));
            }
            return;
        }

        base.OnClosing(e);
    }

    private static TextBlock CreateStatusText(bool trim = false)
    {
        var text = new TextBlock
        {
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (trim)
        {
            text.TextTrimming = TextTrimming.CharacterEllipsis;
        }
        return text;
    }
}
