using Forms = System.Windows.Forms;
using Microsoft.Win32;
using Glystrata.Controls;
using Glystrata.Preview;
using Glystrata.Syntax;
using Glystrata.Views;
using Ellipse = System.Windows.Shapes.Ellipse;
using Shape = System.Windows.Shapes.Shape;
using WpfDataObject = System.Windows.IDataObject;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfDragEventArgs = System.Windows.DragEventArgs;

namespace Glystrata;

public partial class MainWindow : Window
{
    private readonly DocumentManager _documents = new();
    private readonly GroupManager _groups = new();
    private readonly JsonStateStore _stateStore = new();
    private readonly ISnapshotService _snapshots = new SnapshotService();
    private readonly IMarkdownPreviewService _markdown = new MarkdownPreviewService();
    private readonly TextDiffService _diff = new();
    private readonly FileCodec _codec = new();
    private readonly LocalizationService _localization = new();
    private readonly ThemeService _theme = new();
    private readonly SyntaxHighlightingService _syntax = new();
    private readonly WpfMarkdownRenderer _renderer = new();
    private readonly PreviewWindowManager _previewWindows;
    private readonly string[] _startupPaths;
    private readonly HashSet<Guid> _attachedDocuments = new();
    private readonly Dictionary<Guid, DispatcherTimer> _autoSaveTimers = new();
    private readonly Dictionary<Guid, DispatcherTimer> _externalCheckTimers = new();
    private readonly Dictionary<Guid, FileSystemWatcher> _watchers = new();
    private readonly Dictionary<Guid, FileFingerprint> _knownExternalFingerprints = new();
    private readonly Dictionary<Guid, DateTime> _lastChangedUtc = new();

    private AppSettings _settings = new();
    private PaneLayoutNode _layoutRoot;
    private Guid _activePaneId;
    private Guid? _activeViewId;
    private Guid? _selectedGroupId;
    private bool _loaded;
    private bool _isClosing;
    private bool _isApplyingSettings;
    private bool _suppressGroupSelection;
    private double _editorZoom = 1.0;

    private Grid _paneHost = null!;
    private Border _sidebar = null!;
    private TreeView _groupTree = null!;
    private Button _allTabsFilter = null!;
    private TextBlock _sidebarTitle = null!;
    private TextBlock _statusText = null!;
    private TextBlock _positionText = null!;
    private Button _characterCountButton = null!;
    private ContextMenu _characterCountMenu = null!;
    private Slider _zoomSlider = null!;
    private TextBlock _zoomPercentText = null!;
    private readonly Dictionary<Guid, Ellipse> _groupIndicators = new();
    private readonly Dictionary<Guid, EditorPaneControl> _paneControls = new();

    public MainWindow(IEnumerable<string>? startupPaths = null)
    {
        _startupPaths = startupPaths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .ToArray() ?? Array.Empty<string>();
        InitializeComponent();
        AllowDrop = true;
        PreviewDragOver += Window_PreviewDragOver;
        PreviewDrop += Window_PreviewDrop;
        _layoutRoot = new PaneLayoutNode.EditorPane(Guid.NewGuid());
        _activePaneId = _layoutRoot.Id;
        _previewWindows = new PreviewWindowManager(_markdown, _renderer, _localization, () => _settings);
        _previewWindows.StateChanged += (_, view) => RefreshPanePreviewState(view);
        _localization.LanguageChanged += Localization_LanguageChanged;
        _theme.ThemeChanged += Theme_ThemeChanged;

        _settings = _stateStore.LoadSettingsForStartup();
        _localization.Apply(_settings.Language);
        _theme.Apply(_settings.Theme);

        BuildShell();
    }

    private DocumentViewState? ActiveView => _activeViewId is { } id ? _documents.FindView(id) : null;

    private ICSharpCode.AvalonEdit.TextEditor? ActiveEditor
    {
        get
        {
            if (_activeViewId is not { } viewId)
            {
                return null;
            }
            return _paneControls.Values.Select(pane => pane.GetEditor(viewId)).FirstOrDefault(editor => editor is not null);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _ = LoadWorkspaceAsync();
    }

    private void Window_PreviewDragOver(object sender, WpfDragEventArgs e)
    {
        e.Effects = GetDroppedFiles(e.Data).Length > 0
            ? WpfDragDropEffects.Copy
            : WpfDragDropEffects.None;
        e.Handled = true;
    }

    private void Window_PreviewDrop(object sender, WpfDragEventArgs e)
    {
        var paths = GetDroppedFiles(e.Data);
        if (paths.Length == 0)
        {
            e.Effects = WpfDragDropEffects.None;
            e.Handled = true;
            return;
        }

        var group = ResolveCurrentGroup();
        var opened = 0;
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            _groups.AddPath(group.Id, path, GroupItemKind.File);
            OpenPath(path, group.Id, selectGroup: false);
            opened++;
        }

        if (opened > 0)
        {
            RebuildGroupsTree();
            ScheduleSessionSave();
        }

        e.Effects = opened > 0 ? WpfDragDropEffects.Copy : WpfDragDropEffects.None;
        e.Handled = true;
    }

    private void OpenStartupFiles()
    {
        if (_startupPaths.Length == 0)
        {
            return;
        }

        var group = ResolveCurrentGroup();
        foreach (var path in _startupPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            OpenPath(path, group.Id, selectGroup: false);
        }

        RebuildGroupsTree();
        ScheduleSessionSave();
    }

    private Group ResolveCurrentGroup()
    {
        if (GetSelectedGroup() is { } selected)
        {
            return selected;
        }

        if (ActiveView?.SourceGroupId is { } sourceGroupId && _groups.Find(sourceGroupId) is { } activeGroup)
        {
            return activeGroup;
        }

        return EnsureDefaultGroup();
    }

    private static string[] GetDroppedFiles(WpfDataObject data)
    {
        if (!data.GetDataPresent(WpfDataFormats.FileDrop, true))
        {
            return Array.Empty<string>();
        }

        return (data.GetData(WpfDataFormats.FileDrop, true) as string[] ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control && Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    NewUntitled();
                    e.Handled = true;
                    break;
                case Key.O:
                    OpenFiles();
                    e.Handled = true;
                    break;
                case Key.S:
                    _ = SaveActiveAsync();
                    e.Handled = true;
                    break;
                case Key.W:
                    if (ActiveView is { } view)
                    {
                        CloseView(view);
                        e.Handled = true;
                    }
                    break;
                case Key.F:
                    FindText();
                    e.Handled = true;
                    break;
                case Key.H:
                    ReplaceText();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == Key.S)
        {
            _ = SaveActiveAsAsync();
            e.Handled = true;
        }
    }

    private async Task LoadWorkspaceAsync()
    {
        var groupsState = await _stateStore.LoadGroupsAsync();
        groupsState.ApplyTo(_groups);
        if (_groups.Groups.Count == 0)
        {
            _groups.CreateGroup(_localization.Get("sidebar.groups"));
        }

        var session = await _stateStore.LoadSessionAsync();
        var restoredLayout = BuildLayoutFromState(session);
        if (restoredLayout is not null)
        {
            _layoutRoot = restoredLayout;
        }
        _activePaneId = PaneLayoutOperations.GetFirstPaneId(_layoutRoot);
        BuildShell();
        RestoreViews(session);
        PruneEmptyPanes();
        RebuildPaneLayout();
        StartTimers();
        UpdateStatus();
        OpenStartupFiles();
    }

    private void BuildShell()
    {
        RootGrid.Children.Clear();
        _paneControls.Clear();

        var dock = new DockPanel();
        var menu = BuildMenu();
        DockPanel.SetDock(menu, Dock.Top);
        dock.Children.Add(menu);

        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        dock.Children.Add(toolbar);

        var status = BuildStatusBar();
        DockPanel.SetDock(status, Dock.Bottom);
        dock.Children.Add(status);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(258) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _sidebar = BuildSidebar();
        Grid.SetColumn(_sidebar, 0);
        body.Children.Add(_sidebar);
        var sidebarSplitter = new GridSplitter
        {
            Width = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = (Brush)Application.Current.FindResource("BorderBrush"),
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext
        };
        Grid.SetColumn(sidebarSplitter, 1);
        body.Children.Add(sidebarSplitter);

        _paneHost = new Grid { Background = (Brush)Application.Current.FindResource("WindowBackgroundBrush") };
        Grid.SetColumn(_paneHost, 2);
        body.Children.Add(_paneHost);
        dock.Children.Add(body);
        RootGrid.Children.Add(dock);

        RebuildGroupsTree();
        RebuildPaneLayout();
        UpdateStatus();
    }

    private Grid BuildMenu()
    {
        var menu = new Menu
        {
            Background = (Brush)Application.Current.FindResource("MenuBackgroundBrush"),
            Foreground = (Brush)Application.Current.FindResource("MenuForegroundBrush"),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var file = CreateTopLevelMenuItem("menu.file");
        file.Items.Add(CreateMenuItem("file.new", NewUntitled, "Ctrl+N"));
        file.Items.Add(CreateMenuItem("file.open", OpenFiles, "Ctrl+O"));
        file.Items.Add(CreateMenuItem("file.save", () => _ = SaveActiveAsync(), "Ctrl+S"));
        file.Items.Add(CreateMenuItem("file.saveAs", () => _ = SaveActiveAsAsync(), "Ctrl+Shift+S"));
        file.Items.Add(CreateMenuItem("file.snapshotHistory", () => OpenSnapshotHistory()));
        file.Items.Add(CreateMenuItem("file.createSnapshot", () => CreateSnapshotNow()));
        file.Items.Add(CreateMenuItem("file.exit", Close));
        file.Items.Add(CreateMenuItem("settings.open", OpenSettings));
        menu.Items.Add(file);

        var edit = CreateTopLevelMenuItem("menu.edit");
        edit.Items.Add(CreateMenuItem("edit.undo", Undo, "Ctrl+Z"));
        edit.Items.Add(CreateMenuItem("edit.redo", Redo, "Ctrl+Y"));
        edit.Items.Add(CreateMenuItem("edit.find", FindText, "Ctrl+F"));
        edit.Items.Add(CreateMenuItem("edit.replace", ReplaceText, "Ctrl+H"));
        menu.Items.Add(edit);

        var view = CreateTopLevelMenuItem("menu.view");
        view.Items.Add(CreateMenuItem("view.splitHorizontal", () => SplitActivePane(SplitOrientation.Horizontal)));
        view.Items.Add(CreateMenuItem("view.splitVertical", () => SplitActivePane(SplitOrientation.Vertical)));
        view.Items.Add(CreateMenuItem("view.closePane", CloseActivePane));
        view.Items.Add(CreateMenuItem("view.resetLayout", CollapseToSinglePane));
        view.Items.Add(CreateMenuItem("view.toggleSidebar", ToggleSidebar));
        view.Items.Add(CreateMenuItem("view.newView", OpenNewView));
        menu.Items.Add(view);

        var groups = CreateTopLevelMenuItem("menu.group");
        groups.Items.Add(CreateMenuItem("group.new", NewGroup));
        groups.Items.Add(CreateMenuItem("group.addFile", () => AddFilesToGroup()));
        groups.Items.Add(CreateMenuItem("group.addFolder", () => AddFolderToGroup()));
        menu.Items.Add(groups);

        var help = CreateTopLevelMenuItem("menu.help");
        help.Items.Add(CreateMenuItem("help.about", ShowAbout));
        menu.Items.Add(help);

        var host = new Grid
        {
            Height = 32,
            Background = (Brush)Application.Current.FindResource("MenuBackgroundBrush")
        };
        host.Children.Add(menu);
        return host;
    }

    private MenuItem CreateTopLevelMenuItem(string resourceKey) => new()
    {
        Header = _localization.Get(resourceKey),
        Height = 32,
        MinWidth = 0,
        Padding = new Thickness(7, 0, 7, 0),
        Margin = new Thickness(0),
        HorizontalAlignment = HorizontalAlignment.Left,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private DockPanel BuildToolbar()
    {
        var toolbar = new DockPanel
        {
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            LastChildFill = true,
            Height = 38
        };
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 3, 5, 3)
        };
        left.Children.Add(CreateToolbarButton("＋", "toolbar.new", NewUntitled));
        left.Children.Add(CreateToolbarButton("↥", "toolbar.open", OpenFiles));
        left.Children.Add(CreateToolbarButton("⇔", "toolbar.splitHorizontal", () => SplitActivePane(SplitOrientation.Horizontal)));
        left.Children.Add(CreateToolbarButton("⇕", "toolbar.splitVertical", () => SplitActivePane(SplitOrientation.Vertical)));
        left.Children.Add(CreateToolbarButton("▣", "toolbar.closePane", CloseActivePane));
        left.Children.Add(CreateToolbarButton("1", "toolbar.resetLayout", CollapseToSinglePane));
        toolbar.Children.Add(left);

        return toolbar;
    }

    private StatusBar BuildStatusBar()
    {
        var status = new StatusBar
        {
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush"),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        _statusText = new TextBlock { Margin = new Thickness(8, 0, 20, 0) };
        _positionText = new TextBlock { Margin = new Thickness(8, 0, 8, 0) };
        _characterCountButton = new Button
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _characterCountButton.SetResourceReference(Button.ForegroundProperty, "SecondaryTextBrush");
        _characterCountMenu = BuildCharacterCountMenu();
        _characterCountButton.ContextMenu = _characterCountMenu;
        _characterCountButton.Click += (_, _) =>
        {
            _characterCountMenu.PlacementTarget = _characterCountButton;
            _characterCountMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            _characterCountMenu.IsOpen = true;
        };

        _zoomSlider = new Slider
        {
            Minimum = 50,
            Maximum = 200,
            TickFrequency = 10,
            IsSnapToTickEnabled = true,
            SmallChange = 10,
            LargeChange = 10,
            Width = 110,
            VerticalAlignment = VerticalAlignment.Center,
            Value = _editorZoom * 100,
            ToolTip = _localization.Get("status.zoom")
        };
        _zoomSlider.ValueChanged += (_, e) => SetEditorZoom(e.NewValue / 100.0);
        _zoomPercentText = new TextBlock
        {
            Text = $"{Math.Round(_editorZoom * 100)}%",
            Margin = new Thickness(6, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = _localization.Get("status.zoom")
        };
        _zoomPercentText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
        _zoomPercentText.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                SetEditorZoom(1.0);
            }
        };

        var content = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(_statusText, Dock.Left);
        content.Children.Add(_statusText);
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        right.Children.Add(_zoomSlider);
        right.Children.Add(_zoomPercentText);
        right.Children.Add(new Separator());
        right.Children.Add(_characterCountButton);
        right.Children.Add(new Separator());
        right.Children.Add(_positionText);
        DockPanel.SetDock(right, Dock.Right);
        content.Children.Add(right);
        status.Items.Add(new StatusBarItem
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0)
        });
        return status;
    }

    private void SetEditorZoom(double zoom)
    {
        zoom = Math.Clamp(zoom, 0.5, 2.0);
        _editorZoom = zoom;
        if (_zoomSlider is not null)
        {
            _zoomSlider.Value = zoom * 100;
        }
        if (_zoomPercentText is not null)
        {
            _zoomPercentText.Text = $"{Math.Round(zoom * 100)}%";
        }
        foreach (var pane in _paneControls.Values)
        {
            pane.SetZoom(zoom);
        }
    }

    private ContextMenu BuildCharacterCountMenu()
    {
        var menu = new ContextMenu();
        AddCharacterCountOption(menu, CharacterCountMode.IncludeWhitespace, "status.countMode.includeWhitespace");
        AddCharacterCountOption(menu, CharacterCountMode.ExcludeWhitespace, "status.countMode.excludeWhitespace");
        AddCharacterCountOption(menu, CharacterCountMode.ExcludeLineBreaks, "status.countMode.excludeLineBreaks");
        return menu;
    }

    private void AddCharacterCountOption(ContextMenu menu, CharacterCountMode mode, string resourceKey)
    {
        var item = new MenuItem
        {
            Header = _localization.Get(resourceKey),
            Tag = mode,
            IsCheckable = true,
            IsChecked = _settings.CharacterCountMode == mode
        };
        item.Click += (_, _) =>
        {
            _settings.CharacterCountMode = mode;
            RefreshCharacterCountMenu();
            UpdateStatus();
            ScheduleSessionSave();
            _ = _stateStore.SaveSettingsAsync(_settings);
        };
        menu.Items.Add(item);
    }

    private void RefreshCharacterCountMenu()
    {
        if (_characterCountMenu is null)
        {
            return;
        }

        foreach (var item in _characterCountMenu.Items.OfType<MenuItem>())
        {
            item.IsChecked = item.Tag is CharacterCountMode mode && mode == _settings.CharacterCountMode;
        }
    }

    private Border BuildSidebar()
    {
        var panel = new DockPanel();
        var header = new Grid { Margin = new Thickness(12, 10, 10, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _sidebarTitle = new TextBlock
        {
            Text = _localization.Get("sidebar.groups"),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_sidebarTitle, 0);
        header.Children.Add(_sidebarTitle);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(CreateSmallButton("＋", _localization.Get("group.new"), NewGroup));
        buttons.Children.Add(CreateSmallButton("▤", _localization.Get("group.addFile"), () => AddFilesToGroup()));
        buttons.Children.Add(CreateSmallButton("□", _localization.Get("group.addFolder"), () => AddFolderToGroup()));
        Grid.SetColumn(buttons, 1);
        header.Children.Add(buttons);
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        _allTabsFilter = new Button
        {
            Content = _localization.Get("sidebar.allTabs"),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Height = 30,
            Margin = new Thickness(8, 0, 8, 6),
            Padding = new Thickness(10, 4, 10, 4),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush"),
            Focusable = false
        };
        _allTabsFilter.Click += (_, _) => SelectAllTabs();
        DockPanel.SetDock(_allTabsFilter, Dock.Top);
        panel.Children.Add(_allTabsFilter);

        _groupTree = new TreeView
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush")
        };
        _groupTree.SelectedItemChanged += GroupTree_SelectedItemChanged;
        panel.Children.Add(_groupTree);
        return new Border
        {
            Background = (Brush)Application.Current.FindResource("SidebarBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = panel
        };
    }

    private MenuItem CreateMenuItem(string resourceKey, Action action, string? gesture = null)
    {
        var item = new MenuItem { Header = _localization.Get(resourceKey), InputGestureText = gesture };
        item.Click += (_, _) => action();
        return item;
    }

    private Button CreateToolbarButton(string content, string tooltipKey, Action action)
    {
        var button = new Button
        {
            Content = content,
            ToolTip = _localization.Get(tooltipKey),
            FontSize = 16,
            Width = 34,
            Height = 30,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 0, 2, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush")
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Button CreateSmallButton(string content, string tooltip, Action action)
    {
        var button = new Button
        {
            Content = content,
            ToolTip = tooltip,
            FontSize = 14,
            Width = 25,
            Height = 24,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 0, 0, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush")
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void RebuildGroupsTree()
    {
        if (_groupTree is null)
        {
            return;
        }

        _suppressGroupSelection = true;
        try
        {
            _groupTree.Items.Clear();
            _groupIndicators.Clear();

            TreeViewItem? selectedNode = null;
            if (_selectedGroupId is { } selectedGroupId && !_groups.Groups.Any(group => group.Id == selectedGroupId))
            {
                _selectedGroupId = null;
            }
            RefreshAllTabsFilter();

            foreach (var group in _groups.Groups)
            {
                var indicator = new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Margin = new Thickness(1, 0, 7, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0
                };
                indicator.SetResourceReference(Shape.FillProperty, "GroupIndicatorBrush");
                var groupHeader = new StackPanel { Orientation = Orientation.Horizontal };
                groupHeader.Children.Add(indicator);
                var groupLabel = new TextBlock
                {
                    Text = group.Name,
                    VerticalAlignment = VerticalAlignment.Center
                };
                groupLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
                groupHeader.Children.Add(groupLabel);
                _groupIndicators[group.Id] = indicator;

                var groupNode = new TreeViewItem
                {
                    Header = groupHeader,
                    Tag = group,
                    IsExpanded = true,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush")
                };
                groupNode.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "PrimaryTextBrush");
                groupNode.ContextMenu = CreateGroupContextMenu(group);
                _groupTree.Items.Add(groupNode);
                if (_selectedGroupId == group.Id)
                {
                    selectedNode = groupNode;
                }
            }

            if (selectedNode is not null)
            {
                selectedNode.IsSelected = true;
            }
            RefreshGroupSelectionIndicators();
        }
        finally
        {
            _suppressGroupSelection = false;
        }
    }

    private void SelectAllTabs()
    {
        _selectedGroupId = null;
        RebuildGroupsTree();
        RebuildPaneLayout();
        UpdateStatus();
    }

    private void RefreshAllTabsFilter()
    {
        if (_allTabsFilter is null)
        {
            return;
        }

        _allTabsFilter.Content = _localization.Get("sidebar.allTabs");
        _allTabsFilter.Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush");
        _allTabsFilter.Background = _selectedGroupId is null
            ? (Brush)Application.Current.FindResource("TabActiveBrush")
            : Brushes.Transparent;
    }

    private void RefreshGroupSelectionIndicators()
    {
        foreach (var pair in _groupIndicators)
        {
            pair.Value.Opacity = _selectedGroupId == pair.Key ? 1 : 0;
        }
    }

    private ContextMenu CreateGroupContextMenu(Group group)
    {
        var menu = new ContextMenu();
        var addFile = new MenuItem { Header = _localization.Get("group.addFile") };
        addFile.Click += (_, _) => AddFilesToGroup(group);
        var addFolder = new MenuItem { Header = _localization.Get("group.addFolder") };
        addFolder.Click += (_, _) => AddFolderToGroup(group);
        var rename = new MenuItem { Header = _localization.Get("group.rename") };
        rename.Click += (_, _) => RenameGroup(group);
        var delete = new MenuItem { Header = _localization.Get("group.delete") };
        delete.Click += (_, _) => DeleteGroup(group);
        menu.Items.Add(addFile);
        menu.Items.Add(addFolder);
        menu.Items.Add(rename);
        menu.Items.Add(delete);
        return menu;
    }

    private void GroupTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressGroupSelection || e.NewValue is not TreeViewItem node)
        {
            return;
        }

        switch (node.Tag)
        {
            case Group group:
                _selectedGroupId = group.Id;
                RefreshAllTabsFilter();
                RefreshGroupSelectionIndicators();
                RebuildPaneLayout();
                UpdateStatus();
                break;
        }
    }

    private void RebuildPaneLayout()
    {
        if (_paneHost is null)
        {
            return;
        }

        NormalizeActiveViewForFilter();
        _paneHost.Children.Clear();
        _paneControls.Clear();
        _paneHost.Children.Add(CreateLayoutVisual(_layoutRoot));
        if (!_paneControls.ContainsKey(_activePaneId))
        {
            _activePaneId = PaneLayoutOperations.GetFirstPaneId(_layoutRoot);
        }
        SyncPreviewStates();
    }

    private void SyncPreviewStates()
    {
        foreach (var pane in _paneControls.Values)
        {
            foreach (var view in _documents.Views.Where(candidate => candidate.PaneId == pane.PaneId))
            {
                pane.SetPreviewState(view.ViewId, _previewWindows.IsOpen(view));
            }
        }
    }

    private bool IsViewVisible(DocumentViewState view) =>
        _selectedGroupId is not { } groupId || view.SourceGroupId == groupId;

    private void NormalizeActiveViewForFilter()
    {
        var active = ActiveView;
        if (active is not null && IsViewVisible(active) && PaneLayoutOperations.ContainsPane(_layoutRoot, active.PaneId))
        {
            return;
        }

        var replacement = _documents.Views.FirstOrDefault(IsViewVisible);
        _activeViewId = replacement?.ViewId;
        _activePaneId = replacement?.PaneId ?? PaneLayoutOperations.GetFirstPaneId(_layoutRoot);
    }

    private UIElement CreateLayoutVisual(PaneLayoutNode node)
    {
        if (node is PaneLayoutNode.EditorPane editorPane)
        {
            var pane = new EditorPaneControl(
                editorPane.PaneId,
                _localization,
                _syntax,
                GetActivePalette(),
                _settings.ShowFormattingToolbar);
            pane.SetEditorColors(
                (Brush)Application.Current.FindResource("EditorBackgroundBrush"),
                (Brush)Application.Current.FindResource("EditorForegroundBrush"),
                GetActivePalette());
            pane.SetZoom(_editorZoom);
            pane.ViewSelected += Pane_ViewSelected;
            pane.ViewChanged += Pane_ViewChanged;
            pane.ViewSelectionChanged += Pane_ViewSelectionChanged;
            pane.FormattingRequested += Pane_FormattingRequested;
            pane.PreviewRequested += Pane_PreviewRequested;
            pane.CloseRequested += Pane_CloseRequested;
            pane.MoveRequested += Pane_MoveRequested;
            pane.SnapshotRequested += Pane_SnapshotRequested;
            pane.ZoomRequested += (_, zoom) => SetEditorZoom(zoom);
            pane.SetViews(
                _documents.Views.Where(view => view.PaneId == editorPane.PaneId && IsViewVisible(view)),
                _activeViewId);
            _paneControls[editorPane.PaneId] = pane;
            return pane;
        }

        var split = (PaneLayoutNode.Split)node;
        var grid = new Grid();
        var ratio = Math.Clamp(split.Ratio, 0.1, 0.9);
        if (split.Orientation == SplitOrientation.Horizontal)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - ratio, GridUnitType.Star) });
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ratio, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - ratio, GridUnitType.Star) });
        }

        var first = CreateLayoutVisual(split.First);
        var second = CreateLayoutVisual(split.Second);
        var splitter = new GridSplitter
        {
            Background = (Brush)Application.Current.FindResource("BorderBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ResizeDirection = split.Orientation == SplitOrientation.Horizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            ShowsPreview = false
        };
        if (split.Orientation == SplitOrientation.Horizontal)
        {
            Grid.SetColumn(first, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(second, 2);
        }
        else
        {
            Grid.SetRow(first, 0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(second, 2);
        }
        grid.Children.Add(first);
        grid.Children.Add(splitter);
        grid.Children.Add(second);
        splitter.DragCompleted += (_, _) =>
        {
            var actual = split.Orientation == SplitOrientation.Horizontal ? grid.ActualWidth : grid.ActualHeight;
            var firstSize = split.Orientation == SplitOrientation.Horizontal ? grid.ColumnDefinitions[0].ActualWidth : grid.RowDefinitions[0].ActualHeight;
            if (actual > 0)
            {
                _layoutRoot = UpdateSplitRatio(_layoutRoot, split.SplitId, Math.Clamp(firstSize / actual, 0.1, 0.9));
                PruneEmptyPanes();
                ScheduleSessionSave();
            }
        };
        return grid;
    }

    private void Pane_ViewSelected(object? sender, DocumentViewState view)
    {
        _activePaneId = view.PaneId;
        _activeViewId = view.ViewId;
        UpdateStatus();
    }

    private void Pane_ViewChanged(object? sender, DocumentViewState view)
    {
        if (sender is EditorPaneControl pane && pane.SelectedView?.ViewId == view.ViewId)
        {
            _activePaneId = view.PaneId;
            _activeViewId = view.ViewId;
        }
        RefreshPaneHeaders();
        ScheduleSessionSave();
        UpdateStatus();
    }

    private void Pane_ViewSelectionChanged(object? sender, DocumentViewState view)
    {
        if (sender is EditorPaneControl pane && pane.SelectedView?.ViewId == view.ViewId)
        {
            _activePaneId = view.PaneId;
            _activeViewId = view.ViewId;
        }
        UpdateStatus();
    }

    private void Pane_FormattingRequested(object? sender, MarkdownFormatCommand command)
    {
        if (sender is not EditorPaneControl pane || pane.SelectedView is null)
        {
            return;
        }

        if (command == MarkdownFormatCommand.Link)
        {
            var urlResult = InputDialogs.Prompt(
                this,
                _localization.Get("format.link"),
                _localization.Get("format.linkUrl"),
                "https://",
                _localization);
            if (!urlResult.Ok)
            {
                return;
            }
            pane.ApplyFormatting(command, urlResult.Value);
            return;
        }

        pane.ApplyFormatting(command);
    }

    private void Pane_PreviewRequested(object? sender, DocumentViewState view)
    {
        var extension = Path.GetExtension(view.Document.FilePath ?? string.Empty);
        if (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        _previewWindows.Toggle(view, this);
        RefreshPanePreviewState(view);
    }

    private void Pane_CloseRequested(object? sender, DocumentViewState view) => CloseView(view);

    private void Pane_MoveRequested(object? sender, DocumentViewState view) => MoveViewToGroup(view);

    private void Pane_SnapshotRequested(object? sender, DocumentViewState view) => OpenSnapshotHistory(view);

    private void RefreshPaneHeaders()
    {
        foreach (var pane in _paneControls.Values)
        {
            pane.RefreshHeaders();
        }
    }

    private void RefreshPanePreviewState(DocumentViewState view)
    {
        foreach (var pane in _paneControls.Values)
        {
            pane.SetPreviewState(view.ViewId, _previewWindows.IsOpen(view));
        }
    }

    private void SplitActivePane(SplitOrientation orientation)
    {
        var oldPaneId = _activePaneId;
        if (!TryFindPane(_layoutRoot, oldPaneId, out _))
        {
            oldPaneId = PaneLayoutOperations.GetFirstPaneId(_layoutRoot);
        }

        var newPaneId = Guid.NewGuid();
        var active = ActiveView;
        if (active is not null)
        {
            var duplicate = _documents.CreateView(active.Document, active.SourceGroupId, newPaneId);
            AttachDocument(active.Document);
            _activeViewId = duplicate.ViewId;
        }

        _layoutRoot = ReplacePaneWithSplit(_layoutRoot, oldPaneId, orientation, newPaneId);
        _activePaneId = newPaneId;
        RebuildPaneLayout();
        ScheduleSessionSave();
        FocusActiveView();
    }

    private void CloseActivePane()
    {
        if (RemovePaneAndMerge(_activePaneId))
        {
            ScheduleSessionSave();
            FocusActiveView();
        }
    }

    private void CollapseToSinglePane()
    {
        var paneId = PaneLayoutOperations.GetFirstPaneId(_layoutRoot);
        if (_layoutRoot is PaneLayoutNode.EditorPane)
        {
            return;
        }

        foreach (var view in _documents.Views)
        {
            view.PaneId = paneId;
        }

        _layoutRoot = PaneLayoutOperations.CollapseToSinglePane(_layoutRoot);
        _activePaneId = paneId;
        RebuildPaneLayout();
        ScheduleSessionSave();
        FocusActiveView();
    }

    private bool RemovePaneAndMerge(Guid paneId)
    {
        var paneIds = PaneLayoutOperations.EnumeratePaneIds(_layoutRoot).ToArray();
        if (paneIds.Length <= 1 || !paneIds.Contains(paneId))
        {
            return false;
        }

        var replacement = paneIds.First(id => id != paneId);
        foreach (var view in _documents.Views.Where(view => view.PaneId == paneId).ToArray())
        {
            view.PaneId = replacement;
        }

        var nextRoot = PaneLayoutOperations.RemovePane(_layoutRoot, paneId, out var removed);
        if (!removed || nextRoot is null)
        {
            return false;
        }

        _layoutRoot = nextRoot;
        _activePaneId = ActiveView?.PaneId ?? replacement;
        RebuildPaneLayout();
        return true;
    }

    private void PruneEmptyPanes()
    {
        while (PaneLayoutOperations.EnumeratePaneIds(_layoutRoot).Count() > 1)
        {
            var emptyPaneId = PaneLayoutOperations.EnumeratePaneIds(_layoutRoot)
                .FirstOrDefault(paneId => !_documents.Views.Any(view => view.PaneId == paneId));
            if (emptyPaneId == Guid.Empty || !RemovePaneAndMerge(emptyPaneId))
            {
                return;
            }
        }
    }

    private void NewUntitled()
    {
        var view = _documents.CreateUntitled(_activePaneId, _selectedGroupId);
        AttachDocument(view.Document);
        _activeViewId = view.ViewId;
        RebuildPaneLayout();
        FocusActiveView();
        ScheduleSessionSave();
    }

    private void OpenFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Markdown and text|*.md;*.markdown;*.mdown;*.yaml;*.yml;*.txt|All files|*.*",
            CheckFileExists = true,
            Title = _localization.Get("file.open")
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var path in dialog.FileNames)
        {
            OpenPath(path, GetSelectedGroup()?.Id);
        }
    }

    private void OpenPath(string path, Guid? sourceGroupId = null, bool newView = false, bool selectGroup = true)
    {
        try
        {
            if (selectGroup && sourceGroupId is { } groupId)
            {
                _selectedGroupId = groupId;
                RefreshAllTabsFilter();
            }
            var view = _documents.OpenView(path, _activePaneId, sourceGroupId, newView);
            AttachDocument(view.Document);
            _activePaneId = view.PaneId;
            _activeViewId = view.ViewId;
            RebuildPaneLayout();
            FocusActiveView();
            UpdateStatus();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or UnsupportedTextEncodingException)
        {
            MessageBox.Show(this, exception.Message, _localization.Get("error.open"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenNewView()
    {
        var active = ActiveView;
        if (active is null)
        {
            NewUntitled();
            return;
        }

        var view = _documents.CreateView(active.Document, active.SourceGroupId, active.PaneId);
        AttachDocument(view.Document);
        _activeViewId = view.ViewId;
        _activePaneId = view.PaneId;
        RebuildPaneLayout();
        FocusActiveView();
    }

    private async Task SaveActiveAsync()
    {
        try
        {
            if (ActiveView is not { } view)
            {
                return;
            }

            if (view.Document.FilePath is null)
            {
                await SaveActiveAsAsync();
                return;
            }
            await SaveDocumentAsync(view.Document);
        }
        catch (Exception exception)
        {
            ShowSaveError(exception);
        }
    }

    private async Task SaveActiveAsAsync()
    {
        try
        {
            if (ActiveView is not { } view)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Markdown|*.md|YAML|*.yaml;*.yml|All files|*.*",
                FileName = view.Document.IsUntitled ? "untitled.md" : Path.GetFileName(view.Document.FilePath),
                Title = _localization.Get("file.saveAs"),
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var result = await _documents.SaveAsAsync(view.Document, dialog.FileName!);
            if (!result.Success)
            {
                ShowSaveError(result.ErrorMessage);
                return;
            }

            AttachDocument(view.Document);
            RebuildGroupsTree();
            RefreshPaneHeaders();
            ScheduleSessionSave();
            UpdateStatus();
        }
        catch (Exception exception)
        {
            ShowSaveError(exception);
        }
    }

    private async Task SaveDocumentAsync(DocumentSession document)
    {
        try
        {
            var result = await _documents.SaveAsync(document);
            if (!result.Success)
            {
                ShowSaveError(result.ErrorMessage);
                return;
            }
            _knownExternalFingerprints.Remove(document.SessionId);
            RefreshPaneHeaders();
            UpdateStatus(_localization.Get("status.autoSaved"));
            ScheduleSessionSave();
        }
        catch (Exception exception)
        {
            ShowSaveError(exception);
        }
    }

    private void ShowSaveError(Exception? exception)
    {
        var message = exception?.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = _localization.Get("error.save");
        }
        ShowSaveError(message);
    }

    private void ShowSaveError(string? message)
    {
        if (_isClosing || Dispatcher.HasShutdownStarted)
        {
            return;
        }
        MessageBox.Show(this, message ?? _localization.Get("error.save"), _localization.Get("error.save"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void CloseView(DocumentViewState view)
    {
        var closedPaneId = view.PaneId;
        if (view.Document.IsModified)
        {
            var result = MessageBox.Show(
                this,
                _localization.Get("dialog.unsavedMessage"),
                _localization.Get("dialog.unsavedTitle"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Cancel)
            {
                return;
            }
            if (result == MessageBoxResult.Yes)
            {
                if (!SaveDocumentSynchronously(view.Document))
                {
                    return;
                }
            }
        }

        _previewWindows.Close(view);
        _documents.CloseView(view.ViewId);
        DetachDocumentIfUnused(view.Document);
        if (_activeViewId == view.ViewId)
        {
            var replacement = _documents.Views.FirstOrDefault(candidate => candidate.PaneId == view.PaneId) ?? _documents.Views.FirstOrDefault();
            _activeViewId = replacement?.ViewId;
            if (replacement is not null)
            {
                _activePaneId = replacement.PaneId;
            }
        }

        var paneRemoved = !_documents.Views.Any(candidate => candidate.PaneId == closedPaneId) &&
                          RemovePaneAndMerge(closedPaneId);
        if (!paneRemoved)
        {
            RebuildPaneLayout();
        }
        ScheduleSessionSave();
        UpdateStatus();
    }

    private void DetachDocumentIfUnused(DocumentSession document)
    {
        if (_documents.Views.Any(candidate => candidate.Document.SessionId == document.SessionId))
        {
            return;
        }

        if (_autoSaveTimers.Remove(document.SessionId, out var autoSaveTimer))
        {
            autoSaveTimer.Stop();
        }
        if (_externalCheckTimers.Remove(document.SessionId, out var externalCheckTimer))
        {
            externalCheckTimer.Stop();
        }
        if (_watchers.Remove(document.SessionId, out var watcher))
        {
            watcher.Dispose();
        }
        _knownExternalFingerprints.Remove(document.SessionId);
        _lastChangedUtc.Remove(document.SessionId);
        if (_attachedDocuments.Remove(document.SessionId))
        {
            document.TextChanged -= Document_TextChanged;
            document.PropertyChanged -= Document_PropertyChanged;
        }
    }

    private void MoveViewToGroup(DocumentViewState view)
    {
        var target = InputDialogs.SelectGroup(this, _localization.Get("group.moveTo"), _groups.Groups, view.SourceGroupId, _localization);
        if (target is null || target.Id == view.SourceGroupId)
        {
            return;
        }

        var sourceGroupId = view.SourceGroupId;
        view.SourceGroupId = target.Id;
        if (view.Document.FilePath is { } path)
        {
            if (sourceGroupId is { } sourceId && sourceId != target.Id)
            {
                _groups.RemovePath(sourceId, path);
            }
            _groups.AddPath(target.Id, path, GroupItemKind.File);
        }
        RebuildGroupsTree();
        RebuildPaneLayout();
        ScheduleSessionSave();
    }

    private void OpenSnapshotHistory(DocumentViewState? requestedView = null)
    {
        if (requestedView is null && ActiveView is null)
        {
            MessageBox.Show(this, _localization.Get("dialog.noFile"), _localization.Get("snapshot.title"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var view = requestedView ?? ActiveView!;
        if (view.Document.FilePath is null)
        {
            MessageBox.Show(this, _localization.Get("dialog.noFile"), _localization.Get("snapshot.title"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var window = new SnapshotHistoryWindow(view, _snapshots, _localization, _settings.MaxSnapshotsPerFile) { Owner = this };
        window.CompareRequested += async (_, snapshot) =>
        {
            try
            {
                var snapshots = await _snapshots.ListAsync(view.Document.FilePath!);
                var diffWindow = new DiffWindow(snapshots, snapshot, () => view.Document.Text, _diff, _localization) { Owner = this };
                diffWindow.Show();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                MessageBox.Show(this, exception.Message, _localization.Get("snapshot.title"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        window.RestoreRequested += (_, request) => RestoreSnapshot(view, request.Snapshot, request.Mode);
        window.Show();
    }

    private async void CreateSnapshotNow()
    {
        if (ActiveView is not { } view || view.Document.FilePath is null)
        {
            MessageBox.Show(this, _localization.Get("dialog.noFile"), _localization.Get("snapshot.title"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var snapshot = await _snapshots.CreateAsync(view.Document, _settings.MaxSnapshotsPerFile);
            UpdateStatus(_localization.Get(snapshot is null ? "snapshot.unchanged" : "snapshot.created"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, _localization.Get("snapshot.title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RestoreSnapshot(DocumentViewState view, SnapshotInfo snapshot, RestoreMode mode)
    {
        if (mode == RestoreMode.ReplaceCurrent)
        {
            view.Document.ApplyRestoredContent(snapshot.Text, snapshot.Encoding, snapshot.LineEnding);
            _lastChangedUtc[view.Document.SessionId] = DateTime.UtcNow;
            RefreshPaneHeaders();
            RebuildPaneLayout();
            ScheduleSessionSave();
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown|*.md|YAML|*.yaml;*.yml|All files|*.*",
            FileName = "restored.md",
            Title = _localization.Get("snapshot.saveAsNew"),
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var newView = _documents.CreateUntitled(_activePaneId, _selectedGroupId);
        newView.Document.ApplyRestoredContent(snapshot.Text, snapshot.Encoding, snapshot.LineEnding);
        var result = await _documents.SaveAsAsync(newView.Document, dialog.FileName);
        if (!result.Success)
        {
            _documents.CloseView(newView.ViewId);
            MessageBox.Show(this, result.ErrorMessage, _localization.Get("error.save"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        AttachDocument(newView.Document);
        _activeViewId = newView.ViewId;
        RebuildPaneLayout();
        FocusActiveView();
    }

    private void Undo()
    {
        if (ActiveEditor?.CanUndo == true)
        {
            ActiveEditor.Undo();
        }
    }

    private void Redo()
    {
        if (ActiveEditor?.CanRedo == true)
        {
            ActiveEditor.Redo();
        }
    }

    private void FindText()
    {
        if (ActiveView is not { } view || ActiveEditor is not { } editor)
        {
            return;
        }

        var queryResult = InputDialogs.Prompt(this, _localization.Get("edit.find"), _localization.Get("edit.find"), localization: _localization);
        if (!queryResult.Ok || string.IsNullOrEmpty(queryResult.Value))
        {
            return;
        }
        var query = queryResult.Value;
        var start = Math.Min(editor.CaretOffset + 1, view.Document.Text.Length);
        var index = view.Document.Text.IndexOf(query, start, StringComparison.CurrentCultureIgnoreCase);
        index = index < 0 ? view.Document.Text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) : index;
        if (index >= 0)
        {
            editor.Select(index, query.Length);
            editor.ScrollToLine(editor.Document.GetLineByOffset(index).LineNumber);
            editor.Focus();
        }
    }

    private void ReplaceText()
    {
        if (ActiveView is not { } view || ActiveEditor is not { } editor)
        {
            return;
        }

        var queryResult = InputDialogs.Prompt(this, _localization.Get("edit.replace"), _localization.Get("edit.find"), localization: _localization);
        if (!queryResult.Ok || string.IsNullOrEmpty(queryResult.Value))
        {
            return;
        }
        var query = queryResult.Value;
        var replacementResult = InputDialogs.Prompt(this, _localization.Get("edit.replace"), _localization.Get("edit.replace"), string.Empty, _localization);
        if (!replacementResult.Ok)
        {
            return;
        }
        var replacement = replacementResult.Value;
        var text = view.Document.Text.Replace(query, replacement, StringComparison.CurrentCultureIgnoreCase);
        view.Document.TextDocument.Text = text;
        editor.Focus();
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_settings, _localization) { Owner = this };
        window.SettingsApplied += (_, settings) => ApplySettings(settings);
        window.ShowDialog();
    }

    private void ApplySettings(AppSettings settings)
    {
        settings.Normalize();
        _settings = settings;
        _isApplyingSettings = true;
        try
        {
            _localization.Apply(settings.Language);
            _theme.Apply(settings.Theme);
        }
        finally
        {
            _isApplyingSettings = false;
        }
        BuildShell();
        _previewWindows.UpdateSettings(_settings);
        _previewWindows.ApplyTheme();
        ScheduleSessionSave();
        _ = _stateStore.SaveSettingsAsync(_settings);
    }

    private void ToggleSidebar()
    {
        _sidebar.Visibility = _sidebar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowAbout()
    {
        MessageBox.Show(this, _localization.Get("about.message"), _localization.Get("help.about"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void NewGroup()
    {
        var nameResult = InputDialogs.Prompt(this, _localization.Get("group.new"), _localization.Get("group.new"), localization: _localization);
        if (!nameResult.Ok || string.IsNullOrWhiteSpace(nameResult.Value))
        {
            return;
        }
        _groups.CreateGroup(nameResult.Value);
        RebuildGroupsTree();
        ScheduleSessionSave();
    }

    private void RenameGroup(Group? group = null)
    {
        group ??= GetSelectedGroup();
        if (group is null)
        {
            return;
        }
        var nameResult = InputDialogs.Prompt(this, _localization.Get("group.rename"), _localization.Get("group.rename"), group.Name, _localization);
        if (nameResult.Ok && !string.IsNullOrWhiteSpace(nameResult.Value) && _groups.RenameGroup(group.Id, nameResult.Value))
        {
            RebuildGroupsTree();
            ScheduleSessionSave();
        }
    }

    private void DeleteGroup(Group? group = null)
    {
        group ??= GetSelectedGroup();
        if (group is null)
        {
            return;
        }
        if (MessageBox.Show(this, group.Name, _localization.Get("group.delete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        foreach (var view in _documents.Views.Where(view => view.SourceGroupId == group.Id))
        {
            view.SourceGroupId = null;
        }
        _groups.DeleteGroup(group.Id);
        if (_selectedGroupId == group.Id)
        {
            _selectedGroupId = null;
        }
        if (_groups.Groups.Count == 0)
        {
            _groups.CreateGroup(_localization.Get("sidebar.groups"));
        }
        RebuildGroupsTree();
        RebuildPaneLayout();
        ScheduleSessionSave();
    }

    private void AddFilesToGroup(Group? group = null)
    {
        group ??= GetSelectedGroup() ?? EnsureDefaultGroup();
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Markdown and text|*.md;*.markdown;*.mdown;*.yaml;*.yml;*.txt|All files|*.*",
            Title = _localization.Get("group.addFile")
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        foreach (var path in dialog.FileNames)
        {
            _groups.AddPath(group.Id, path, GroupItemKind.File);
        }
        RebuildGroupsTree();
        ScheduleSessionSave();
    }

    private void AddFolderToGroup(Group? group = null)
    {
        group ??= GetSelectedGroup() ?? EnsureDefaultGroup();
        using var dialog = new Forms.FolderBrowserDialog { Description = _localization.Get("group.addFolder") };
        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }
        _groups.AddPath(group.Id, dialog.SelectedPath, GroupItemKind.Folder);
        RebuildGroupsTree();
        ScheduleSessionSave();
    }

    private Group EnsureDefaultGroup() => _groups.Groups.FirstOrDefault() ?? _groups.CreateGroup(_localization.Get("sidebar.groups"));

    private Group? GetSelectedGroup()
    {
        return _groupTree.SelectedItem switch
        {
            TreeViewItem { Tag: Group group } => group,
            _ => null
        };
    }

    private void RefreshUiLanguage()
    {
        Title = _localization.Get("app.title");
        if (_sidebarTitle is not null)
        {
            _sidebarTitle.Text = _localization.Get("sidebar.groups");
        }
    }

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        if (_loaded && !_isApplyingSettings)
        {
            BuildShell();
        }
        RefreshUiLanguage();
        _previewWindows.RefreshLanguage();
    }

    private void Theme_ThemeChanged(object? sender, EventArgs e)
    {
        if (!_loaded)
        {
            return;
        }
        foreach (var pane in _paneControls.Values)
        {
            pane.SetEditorColors(
                (Brush)Application.Current.FindResource("EditorBackgroundBrush"),
                (Brush)Application.Current.FindResource("EditorForegroundBrush"),
                GetActivePalette());
        }
        _previewWindows.ApplyTheme();
        RefreshAllTabsFilter();
        RefreshGroupSelectionIndicators();
        RefreshUiLanguage();
    }

    private EditorColorPalette GetActivePalette() => _settings.Theme == ThemeKind.Dark ? _settings.DarkEditorPalette : _settings.LightEditorPalette;

    private void StartTimers()
    {
        _snapshotTimer ??= new DispatcherTimer();
        _snapshotTimer.Interval = TimeSpan.FromSeconds(30);
        _snapshotTimer.Tick -= SnapshotTimer_Tick;
        _snapshotTimer.Tick += SnapshotTimer_Tick;
        _snapshotTimer.Start();
    }

    private DispatcherTimer? _snapshotTimer;

    private async void SnapshotTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        foreach (var document in _documents.Documents.ToArray())
        {
            if (document.FilePath is null || !_lastChangedUtc.TryGetValue(document.SessionId, out var changedAt))
            {
                continue;
            }
            if (now - changedAt < TimeSpan.FromMinutes(_settings.SnapshotIntervalMinutes) ||
                string.Equals(document.Text, document.LastSnapshotText, StringComparison.Ordinal))
            {
                continue;
            }
            try
            {
                await _snapshots.CreateAsync(document, _settings.MaxSnapshotsPerFile);
                document.LastSnapshotText = document.Text;
                document.LastSnapshotUtc = now;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 快照失敗不應中斷編輯器。
            }
        }
    }

    private void AttachDocument(DocumentSession document)
    {
        if (_attachedDocuments.Add(document.SessionId))
        {
            document.TextChanged += Document_TextChanged;
            document.PropertyChanged += Document_PropertyChanged;
        }
        ConfigureWatcher(document);
    }

    private void Document_TextChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            if (!Dispatcher.HasShutdownStarted)
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(() => Document_TextChanged(sender, e)));
                }
                catch (InvalidOperationException)
                {
                    // Dispatcher 正在關閉時，略過晚到的文件事件。
                }
            }
            return;
        }

        if (sender is not DocumentSession document || _isClosing)
        {
            return;
        }
        _lastChangedUtc[document.SessionId] = DateTime.UtcNow;
        RefreshPaneHeaders();
        UpdateStatus();
        ScheduleAutoSave(document);
        ScheduleSessionSave();
    }

    private void Document_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            if (!Dispatcher.HasShutdownStarted)
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(() => Document_PropertyChanged(sender, e)));
                }
                catch (InvalidOperationException)
                {
                    // Dispatcher 正在關閉時，略過晚到的文件事件。
                }
            }
            return;
        }

        if (sender is DocumentSession document && e.PropertyName == nameof(DocumentSession.FilePath))
        {
            ConfigureWatcher(document);
            RefreshPaneHeaders();
        }
    }

    private void ScheduleAutoSave(DocumentSession document)
    {
        if (document.FilePath is null)
        {
            return;
        }

        if (!_autoSaveTimers.TryGetValue(document.SessionId, out var timer))
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += async (_, _) =>
            {
                timer.Stop();
                _autoSaveTimers.Remove(document.SessionId);
                if (document.IsModified && document.FilePath is not null)
                {
                    await SaveDocumentAsync(document);
                }
            };
            _autoSaveTimers[document.SessionId] = timer;
        }
        timer.Stop();
        timer.Start();
    }

    private void ConfigureWatcher(DocumentSession document)
    {
        if (_watchers.TryGetValue(document.SessionId, out var oldWatcher))
        {
            oldWatcher.Dispose();
            _watchers.Remove(document.SessionId);
        }
        if (document.FilePath is not { } path || !File.Exists(path))
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        if (directory is null)
        {
            return;
        }
        try
        {
            var watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };
            watcher.Changed += (_, _) => ScheduleExternalCheck(document);
            watcher.Created += (_, _) => ScheduleExternalCheck(document);
            watcher.Renamed += (_, _) => ScheduleExternalCheck(document);
            _watchers[document.SessionId] = watcher;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 無法監看時仍可正常編輯與儲存。
        }
    }

    private void ScheduleExternalCheck(DocumentSession document)
    {
        if (_isClosing || Dispatcher.HasShutdownStarted)
        {
            return;
        }
        if (!Dispatcher.CheckAccess())
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() => ScheduleExternalCheck(document)));
            }
            catch (InvalidOperationException)
            {
                // Dispatcher 正在關閉時，略過晚到的檔案監看事件。
            }
            return;
        }
        if (!_externalCheckTimers.TryGetValue(document.SessionId, out var timer))
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                CheckExternalChange(document);
            };
            _externalCheckTimers[document.SessionId] = timer;
        }
        timer.Stop();
        timer.Start();
    }

    private void CheckExternalChange(DocumentSession document)
    {
        if (document.FilePath is not { } path || !File.Exists(path))
        {
            return;
        }
        FileFingerprint current;
        try
        {
            current = FileFingerprint.Read(path);
        }
        catch (IOException)
        {
            return;
        }
        if (Equals(current, document.LastSavedFingerprint) ||
            (_knownExternalFingerprints.TryGetValue(document.SessionId, out var known) && Equals(known, current)))
        {
            return;
        }
        _knownExternalFingerprints[document.SessionId] = current;
        var result = MessageBox.Show(
            this,
            _localization.Get("dialog.externalChangeMessage"),
            _localization.Get("dialog.externalChangeTitle"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            ReloadDocument(document);
        }
        else if (result == MessageBoxResult.No)
        {
            CompareExternalDocument(document);
        }
    }

    private void ReloadDocument(DocumentSession document)
    {
        if (document.FilePath is not { } path)
        {
            return;
        }
        try
        {
            var decoded = _codec.Read(path);
            document.ApplyLoadedContent(decoded.Text, decoded.Encoding, decoded.LineEnding, decoded.Fingerprint);
            _lastChangedUtc.Remove(document.SessionId);
            RefreshPaneHeaders();
            RebuildPaneLayout();
        }
        catch (IOException exception)
        {
            MessageBox.Show(this, exception.Message, _localization.Get("error.open"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CompareExternalDocument(DocumentSession document)
    {
        if (document.FilePath is not { } path)
        {
            return;
        }
        try
        {
            var decoded = _codec.Read(path);
            var window = new DiffWindow(document.Text, decoded.Text, DateTime.UtcNow, _diff, _localization) { Owner = this };
            window.Show();
        }
        catch (IOException exception)
        {
            MessageBox.Show(this, exception.Message, _localization.Get("error.open"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FocusActiveView()
    {
        if (_activeViewId is not { } viewId)
        {
            return;
        }
        if (_paneControls.TryGetValue(_activePaneId, out var pane))
        {
            pane.SelectView(viewId);
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => pane.GetEditor(viewId)?.Focus()));
        }
    }

    private void UpdateStatus(string? overrideText = null)
    {
        if (_statusText is null || _positionText is null || _characterCountButton is null)
        {
            return;
        }
        if (ActiveView is not { } view)
        {
            _statusText.Text = overrideText ?? _localization.Get("status.noFile");
            _positionText.Text = string.Empty;
            _characterCountButton.Content = $"{_localization.Get("status.characters")} —";
            _characterCountButton.ToolTip = GetCharacterCountModeLabel();
            return;
        }
        var name = view.Document.IsUntitled ? _localization.Get("document.untitled") : Path.GetFileName(view.Document.FilePath);
        var state = view.Document.IsModified ? _localization.Get("status.modified") : (overrideText ?? _localization.Get("status.saved"));
        _statusText.Text = $"{name} · {state} · {view.Document.Encoding} · {view.Document.LineEnding}";
        var offset = Math.Clamp(view.CaretOffset, 0, view.Document.TextDocument.TextLength);
        var line = view.Document.TextDocument.GetLineByOffset(offset).LineNumber;
        var column = offset - view.Document.TextDocument.GetLineByNumber(line).Offset + 1;
        _positionText.Text = $"Ln {line}, Col {column}";

        var total = TextMetrics.CountCharacters(view.Document.Text, _settings.CharacterCountMode);
        var selected = ActiveEditor is { SelectionLength: > 0 } editor
            ? TextMetrics.CountCharacters(editor.SelectedText, _settings.CharacterCountMode)
            : (int?)null;
        _characterCountButton.Content = selected is { } selectedCount
            ? $"{_localization.Get("status.characters")} {total} · {_localization.Get("status.selectedCharacters")} {selectedCount}"
            : $"{_localization.Get("status.characters")} {total}";
        _characterCountButton.ToolTip = GetCharacterCountModeLabel();
    }

    private string GetCharacterCountModeLabel() => _settings.CharacterCountMode switch
    {
        CharacterCountMode.ExcludeWhitespace => _localization.Get("status.countMode.excludeWhitespace"),
        CharacterCountMode.ExcludeLineBreaks => _localization.Get("status.countMode.excludeLineBreaks"),
        _ => _localization.Get("status.countMode.includeWhitespace")
    };

    private void ScheduleSessionSave()
    {
        if (!_loaded || _isClosing)
        {
            return;
        }
        _sessionSaveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _sessionSaveTimer.Stop();
        _sessionSaveTimer.Tick -= SessionSaveTimer_Tick;
        _sessionSaveTimer.Tick += SessionSaveTimer_Tick;
        _sessionSaveTimer.Start();
    }

    private DispatcherTimer? _sessionSaveTimer;

    private async void SessionSaveTimer_Tick(object? sender, EventArgs e)
    {
        _sessionSaveTimer?.Stop();
        await SaveWorkspaceAsync();
    }

    private async Task SaveWorkspaceAsync()
    {
        var settings = _settings;
        var groups = GroupsState.FromGroups(_groups.Groups);
        var session = CreateSessionState();
        try
        {
            await _stateStore.SaveSettingsAsync(settings).ConfigureAwait(false);
            await _stateStore.SaveGroupsAsync(groups).ConfigureAwait(false);
            await _stateStore.SaveSessionAsync(session).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 工作階段狀態儲存失敗不應讓程式退出。
        }
    }

    private SessionState CreateSessionState()
    {
        var state = new SessionState { RootNodeId = _layoutRoot.Id, ActiveViewId = _activeViewId };
        AppendPaneState(_layoutRoot, state.Nodes);
        foreach (var view in _documents.Views.Where(view => view.Document.FilePath is not null))
        {
            state.Views.Add(new SessionViewState
            {
                ViewId = view.ViewId,
                Path = view.Document.FilePath!,
                SourceGroupId = view.SourceGroupId,
                PaneId = view.PaneId,
                CaretOffset = view.CaretOffset,
                HorizontalOffset = view.HorizontalOffset,
                VerticalOffset = view.VerticalOffset
            });
        }
        return state;
    }

    private void RestoreViews(SessionState state)
    {
        var paneIds = PaneLayoutOperations.EnumeratePaneIds(_layoutRoot).ToHashSet();
        var pathCounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var idMap = new Dictionary<Guid, Guid>();
        foreach (var saved in state.Views)
        {
            if (string.IsNullOrWhiteSpace(saved.Path) || !File.Exists(saved.Path))
            {
                continue;
            }
            var paneId = paneIds.Contains(saved.PaneId) ? saved.PaneId : PaneLayoutOperations.GetFirstPaneId(_layoutRoot);
            var canonical = DocumentManager.CanonicalizePath(saved.Path);
            var newView = !pathCounts.Add(canonical);
            try
            {
                var view = _documents.OpenView(saved.Path, paneId, saved.SourceGroupId, newView);
                view.CaretOffset = saved.CaretOffset;
                view.HorizontalOffset = saved.HorizontalOffset;
                view.VerticalOffset = saved.VerticalOffset;
                AttachDocument(view.Document);
                idMap[saved.ViewId] = view.ViewId;
            }
            catch (IOException)
            {
                // 工作階段中已不存在的檔案略過，其他檔案照常還原。
            }
        }

        if (state.ActiveViewId is { } activeId && idMap.TryGetValue(activeId, out var restoredId))
        {
            _activeViewId = restoredId;
            if (_documents.FindView(restoredId) is { } view)
            {
                _activePaneId = view.PaneId;
            }
        }
    }

    private static void AppendPaneState(PaneLayoutNode node, ICollection<PaneNodeState> states)
    {
        if (node is PaneLayoutNode.EditorPane pane)
        {
            states.Add(new PaneNodeState { Id = pane.PaneId, IsSplit = false });
            return;
        }
        var split = (PaneLayoutNode.Split)node;
        states.Add(new PaneNodeState
        {
            Id = split.SplitId,
            IsSplit = true,
            Orientation = split.Orientation,
            Ratio = split.Ratio,
            FirstNodeId = split.First.Id,
            SecondNodeId = split.Second.Id
        });
        AppendPaneState(split.First, states);
        AppendPaneState(split.Second, states);
    }

    private static PaneLayoutNode? BuildLayoutFromState(SessionState state)
    {
        if (state.RootNodeId == Guid.Empty || state.Nodes.Count == 0)
        {
            return null;
        }
        var nodes = state.Nodes.ToDictionary(node => node.Id);
        return Build(state.RootNodeId, nodes, new HashSet<Guid>());

        static PaneLayoutNode? Build(Guid id, IReadOnlyDictionary<Guid, PaneNodeState> nodes, ISet<Guid> visiting)
        {
            if (!nodes.TryGetValue(id, out var state) || !visiting.Add(id))
            {
                return null;
            }
            if (!state.IsSplit)
            {
                return new PaneLayoutNode.EditorPane(state.Id);
            }
            if (state.FirstNodeId is not { } firstId || state.SecondNodeId is not { } secondId)
            {
                return null;
            }
            var first = Build(firstId, nodes, visiting);
            var second = Build(secondId, nodes, visiting);
            return first is null || second is null
                ? null
                : new PaneLayoutNode.Split(state.Id, state.Orientation, Math.Clamp(state.Ratio, 0.1, 0.9), first, second);
        }
    }

    private static bool TryFindPane(PaneLayoutNode node, Guid paneId, out PaneLayoutNode.EditorPane? pane)
    {
        if (node is PaneLayoutNode.EditorPane editor && editor.PaneId == paneId)
        {
            pane = editor;
            return true;
        }
        if (node is PaneLayoutNode.Split split && TryFindPane(split.First, paneId, out pane))
        {
            return true;
        }
        if (node is PaneLayoutNode.Split secondSplit && TryFindPane(secondSplit.Second, paneId, out pane))
        {
            return true;
        }
        pane = null;
        return false;
    }

    private static PaneLayoutNode ReplacePaneWithSplit(PaneLayoutNode node, Guid paneId, SplitOrientation orientation, Guid newPaneId)
    {
        if (node is PaneLayoutNode.EditorPane pane)
        {
            return pane.PaneId == paneId
                ? new PaneLayoutNode.Split(Guid.NewGuid(), orientation, 0.5, pane, new PaneLayoutNode.EditorPane(newPaneId))
                : pane;
        }
        var split = (PaneLayoutNode.Split)node;
        return new PaneLayoutNode.Split(
            split.SplitId,
            split.Orientation,
            split.Ratio,
            ReplacePaneWithSplit(split.First, paneId, orientation, newPaneId),
            ReplacePaneWithSplit(split.Second, paneId, orientation, newPaneId));
    }

    private static PaneLayoutNode UpdateSplitRatio(PaneLayoutNode node, Guid splitId, double ratio)
    {
        if (node is PaneLayoutNode.EditorPane)
        {
            return node;
        }
        var split = (PaneLayoutNode.Split)node;
        if (split.SplitId == splitId)
        {
            return split with { Ratio = Math.Clamp(ratio, 0.1, 0.9) };
        }
        return split with
        {
            First = UpdateSplitRatio(split.First, splitId, ratio),
            Second = UpdateSplitRatio(split.Second, splitId, ratio)
        };
    }

    private bool SaveDocumentSynchronously(DocumentSession document)
    {
        if (document.FilePath is null)
        {
            return false;
        }

        try
        {
            var result = _documents.SaveAsync(document).GetAwaiter().GetResult();
            if (!result.Success)
            {
                ShowSaveError(result.ErrorMessage);
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            ShowSaveError(exception);
            return false;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }
        foreach (var document in _documents.Documents.Where(document => document.IsModified).ToArray())
        {
            var result = MessageBox.Show(
                this,
                _localization.Get("dialog.unsavedMessage"),
                _localization.Get("dialog.unsavedTitle"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (result == MessageBoxResult.Yes)
            {
                if (!SaveDocumentSynchronously(document) || document.IsModified)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        _isClosing = true;
        _snapshotTimer?.Stop();
        _sessionSaveTimer?.Stop();
        foreach (var timer in _autoSaveTimers.Values)
        {
            timer.Stop();
        }
        foreach (var timer in _externalCheckTimers.Values)
        {
            timer.Stop();
        }
        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }
        _previewWindows.CloseAll();
        SaveWorkspaceAsync().GetAwaiter().GetResult();
        _documents.Dispose();
    }

}
