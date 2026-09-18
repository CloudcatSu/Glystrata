using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using Glystrata.Editing;
using Glystrata.Syntax;
using Line = System.Windows.Shapes.Line;

namespace Glystrata.Controls;

public sealed class EditorPaneControl : Border
{
    private readonly LocalizationService _localization;
    private readonly SyntaxHighlightingService _syntax;
    private readonly TabControl _tabs;
    private readonly Border _formattingToolbar;
    private readonly TextBlock _emptyHint;
    private readonly Dictionary<Guid, TextEditor> _editors = new();
    private readonly Dictionary<Guid, TabHeaderControl> _headers = new();
    private readonly Dictionary<Guid, DocumentViewState> _views = new();
    private readonly Dictionary<Button, string> _formatButtonKeys = new();
    private readonly MarkdownFormattingService _formatting = new();
    private EditorColorPalette _palette;
    private Func<DocumentViewState, Brush?> _groupColorResolver = _ => null;
    private Brush _editorBackground = Brushes.White;
    private Brush _editorForeground = Brushes.Black;
    private double _zoom = 1.0;
    private ScrollViewer? _tabStripScrollViewer;

    private const double BaseFontSize = 14;

    public EditorPaneControl(
        Guid paneId,
        LocalizationService localization,
        SyntaxHighlightingService syntax,
        EditorColorPalette palette,
        bool showFormattingToolbar = false)
    {
        PaneId = paneId;
        _localization = localization;
        _syntax = syntax;
        _palette = palette;

        BorderBrush = (Brush)Application.Current.FindResource("BorderBrush");
        BorderThickness = new Thickness(1);
        Background = (Brush)Application.Current.FindResource("SurfaceBrush");

        _tabs = new TabControl
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        };
        _tabs.SelectionChanged += Tabs_SelectionChanged;
        _tabs.Loaded += (_, _) => AttachTabStripScrollViewer();
        _formattingToolbar = BuildFormattingToolbar();
        _formattingToolbar.Visibility = showFormattingToolbar ? Visibility.Visible : Visibility.Collapsed;
        _emptyHint = new TextBlock
        {
            Text = _localization.Get("editor.empty"),
            Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush"),
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(24)
        };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_formattingToolbar, 0);
        Grid.SetRow(_tabs, 1);
        Grid.SetRow(_emptyHint, 1);
        root.Children.Add(_formattingToolbar);
        root.Children.Add(_tabs);
        root.Children.Add(_emptyHint);
        Child = root;
        UpdateEmptyState();
    }

    public Guid PaneId { get; }

    public event EventHandler<DocumentViewState>? ViewSelected;

    public event EventHandler<DocumentViewState>? PreviewRequested;

    public event EventHandler<DocumentViewState>? CloseRequested;

    public event EventHandler<DocumentViewState>? TabContextRequested;

    public event EventHandler<DocumentViewState>? MoveRequested;

    public event EventHandler<DocumentViewState>? SnapshotRequested;

    public event EventHandler<DocumentViewState>? CreateSnapshotRequested;

    public event EventHandler<DocumentViewState>? RenameRequested;

    /// <summary>Fires after a tab drag-drop reorder; the payload is this pane's views in their new order.</summary>
    public event EventHandler<IReadOnlyList<Guid>>? TabsReordered;

    public event EventHandler<PaneSplitRequest>? OpenInNewPaneRequested;

    public event EventHandler<DocumentViewState>? ViewChanged;

    public event EventHandler<DocumentViewState>? ViewSelectionChanged;

    public event EventHandler<MarkdownFormatCommand>? FormattingRequested;

    public event EventHandler<double>? ZoomRequested;

    public DocumentViewState? SelectedView => (_tabs.SelectedItem as TabItem)?.Tag as DocumentViewState;

    public IReadOnlyCollection<DocumentViewState> Views => _views.Values;

    public void SetFormattingToolbarVisible(bool visible) =>
        _formattingToolbar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Supplies the colour bar for each tab. A pane has no access to the group list or the
    /// settings, so the owner resolves the colour and the pane only draws it.</summary>
    public void SetGroupColorResolver(Func<DocumentViewState, Brush?> resolver)
    {
        _groupColorResolver = resolver;
        RefreshGroupColors();
    }

    public void RefreshGroupColors()
    {
        foreach (var (viewId, header) in _headers)
        {
            if (_views.TryGetValue(viewId, out var view))
            {
                header.SetGroupColor(_groupColorResolver(view));
            }
        }
    }

    public void SetEditorColors(Brush background, Brush foreground, EditorColorPalette palette)
    {
        _editorBackground = background;
        _palette = palette;
        _editorForeground = CreatePaletteBrush(palette.Get("plain", "#24292F"), foreground);
        foreach (var pair in _editors)
        {
            pair.Value.Background = background;
            pair.Value.Foreground = foreground;
            foreach (var transformer in pair.Value.TextArea.TextView.LineTransformers.OfType<SyntaxColorizingTransformer>())
            {
                transformer.SetPalette(palette);
            }
        }
    }

    public void SetZoom(double zoom)
    {
        _zoom = zoom;
        foreach (var editor in _editors.Values)
        {
            editor.FontSize = BaseFontSize * _zoom;
            ApplyGutterSpacing(editor);
        }
    }

    public void SetViews(IEnumerable<DocumentViewState> views, Guid? selectedViewId = null)
    {
        _tabs.Items.Clear();
        _editors.Clear();
        _headers.Clear();
        _views.Clear();

        foreach (var view in views)
        {
            AddView(view);
        }

        if (_tabs.Items.Count == 0)
        {
            UpdateEmptyState();
            return;
        }

        var selected = selectedViewId.HasValue
            ? _tabs.Items.OfType<TabItem>().FirstOrDefault(item => ((DocumentViewState)item.Tag).ViewId == selectedViewId)
            : null;
        _tabs.SelectedItem = selected ?? _tabs.Items[0];
        UpdateEmptyState();
    }

    public void AddView(DocumentViewState view)
    {
        if (view.PaneId != PaneId || _views.ContainsKey(view.ViewId))
        {
            return;
        }

        var editor = CreateEditor(view);
        var header = new TabHeaderControl(view, _localization);
        header.PreviewRequested += (_, selectedView) => PreviewRequested?.Invoke(this, selectedView);
        header.CloseRequested += (_, selectedView) => CloseRequested?.Invoke(this, selectedView);
        header.ContextMenu = CreateContextMenu(view);
        header.MouseRightButtonUp += (_, _) => TabContextRequested?.Invoke(this, view);

        var tab = new TabItem
        {
            Tag = view,
            Header = header,
            Content = editor,
            Padding = new Thickness(8, 3, 8, 3)
        };
        tab.ContextMenu = header.ContextMenu;
        AttachTabDragDrop(tab, view);
        _tabs.Items.Add(tab);
        _views[view.ViewId] = view;
        _editors[view.ViewId] = editor;
        _headers[view.ViewId] = header;
        header.IsPreviewOpen = false;
        header.SetGroupColor(_groupColorResolver(view));

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => RestoreViewPosition(view, editor)));
        UpdateEmptyState();
        BringSelectedTabIntoView();
    }

    public void SelectView(Guid viewId)
    {
        var tab = _tabs.Items.OfType<TabItem>().FirstOrDefault(item => ((DocumentViewState)item.Tag).ViewId == viewId);
        if (tab is not null)
        {
            _tabs.SelectedItem = tab;
        }
    }

    public bool ContainsView(Guid viewId) => _views.ContainsKey(viewId);

    public void RefreshHeaders()
    {
        foreach (var header in _headers.Values)
        {
            header.Refresh();
        }
    }

    public void RefreshLanguage()
    {
        _emptyHint.Text = _localization.Get("editor.empty");
        foreach (var header in _headers.Values)
        {
            header.RefreshLanguage();
        }
        foreach (var (button, key) in _formatButtonKeys)
        {
            button.ToolTip = _localization.Get(key);
        }
    }

    public void SetPreviewState(Guid viewId, bool isOpen)
    {
        if (_headers.TryGetValue(viewId, out var header))
        {
            header.IsPreviewOpen = isOpen;
        }
    }

    public TextEditor? GetEditor(Guid viewId) => _editors.GetValueOrDefault(viewId);

    private TextEditor CreateEditor(DocumentViewState view)
    {
        var editor = new TextEditor
        {
            Document = view.Document.TextDocument,
            ShowLineNumbers = true,
            WordWrap = true,
            FontFamily = new FontFamily("Cascadia Code, Consolas, Segoe UI Emoji"),
            FontSize = BaseFontSize * _zoom,
            Padding = new Thickness(14, 10, 14, 10),
            Background = _editorBackground,
            Foreground = _editorForeground,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Options = { EnableHyperlinks = false, EnableEmailHyperlinks = false }
        };

        ImeComposition.Attach(editor);
        MarkdownTypingAssistant.Attach(editor);
        ApplyGutterSpacing(editor);
        editor.TextArea.LeftMargins.CollectionChanged += (_, _) => ApplyGutterSpacing(editor);
        editor.PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                return;
            }
            e.Handled = true;
            var step = e.Delta > 0 ? 0.1 : -0.1;
            ZoomRequested?.Invoke(this, Math.Clamp(_zoom + step, 0.5, 2.0));
        };

        var transformer = _syntax.CreateTransformer(view.Document, _palette, editor.TextArea.TextView);
        editor.TextArea.TextView.LineTransformers.Add(transformer);
        editor.TextChanged += (_, _) => ViewChanged?.Invoke(this, view);
        editor.TextArea.SelectionChanged += (_, _) => ViewSelectionChanged?.Invoke(this, view);
        editor.TextArea.Caret.PositionChanged += (_, _) => CaptureViewPosition(view, editor);
        editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => CaptureViewPosition(view, editor);
        editor.GotFocus += (_, _) =>
        {
            var tab = _tabs.Items.OfType<TabItem>().FirstOrDefault(item => ((DocumentViewState)item.Tag).ViewId == view.ViewId);
            if (tab is not null)
            {
                _tabs.SelectedItem = tab;
            }
            ViewSelected?.Invoke(this, view);
        };
        return editor;
    }

    private static void ApplyGutterSpacing(TextEditor editor)
    {
        var gap = new Thickness(0, 0, MeasureFullWidthSpace(editor), 0);
        foreach (var margin in editor.TextArea.LeftMargins)
        {
            if (margin is LineNumberMargin lineNumberMargin)
            {
                lineNumberMargin.Margin = gap;
            }
            else if (margin is Line line && DottedLineMargin.IsDottedLineMargin(line))
            {
                line.Margin = gap;
            }
        }
    }

    private static double MeasureFullWidthSpace(TextEditor editor)
    {
        var typeface = new Typeface(editor.FontFamily, editor.FontStyle, editor.FontWeight, editor.FontStretch);
        var formattedText = new FormattedText(
            "　",
            CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            editor.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(editor).PixelsPerDip);
        return formattedText.WidthIncludingTrailingWhitespace;
    }

    public bool ApplyFormatting(MarkdownFormatCommand command, string? linkUrl = null)
    {
        if (SelectedView is not { } view || !_editors.TryGetValue(view.ViewId, out var editor) || editor.Document is null)
        {
            return false;
        }

        var result = _formatting.Apply(
            editor.Document.Text,
            editor.SelectionStart,
            editor.SelectionLength,
            command,
            linkUrl);
        if (string.Equals(result.Text, editor.Document.Text, StringComparison.Ordinal))
        {
            editor.Focus();
            return false;
        }

        editor.Document.Replace(0, editor.Document.TextLength, result.Text);
        if (result.SelectionLength > 0)
        {
            editor.Select(result.SelectionStart, result.SelectionLength);
        }
        else
        {
            editor.CaretOffset = result.SelectionStart;
        }
        editor.Focus();
        CaptureViewPosition(view, editor);
        return true;
    }

    private void CaptureViewPosition(DocumentViewState view, TextEditor editor)
    {
        view.CaretOffset = editor.CaretOffset;
        view.HorizontalOffset = editor.HorizontalOffset;
        view.VerticalOffset = editor.VerticalOffset;
        ViewChanged?.Invoke(this, view);
    }

    private static void RestoreViewPosition(DocumentViewState view, TextEditor editor)
    {
        editor.CaretOffset = Math.Clamp(view.CaretOffset, 0, editor.Document?.TextLength ?? 0);
        editor.ScrollToHorizontalOffset(Math.Max(0, view.HorizontalOffset));
        editor.ScrollToVerticalOffset(Math.Max(0, view.VerticalOffset));
    }

    private ContextMenu CreateContextMenu(DocumentViewState view)
    {
        var menu = new ContextMenu();
        var close = new MenuItem { Header = _localization.Get("dialog.close") };
        close.Click += (_, _) => CloseRequested?.Invoke(this, view);
        menu.Items.Add(close);
        var preview = new MenuItem
        {
            Header = _localization.Get("preview.reader"),
            IsEnabled = !IsYamlFile(view.Document.FilePath)
        };
        preview.Click += (_, _) => PreviewRequested?.Invoke(this, view);
        menu.Items.Add(preview);
        var showInFolder = new MenuItem
        {
            Header = _localization.Get("tab.showInFolder"),
            IsEnabled = view.Document.FilePath is not null
        };
        showInFolder.Click += (_, _) => ShowInFolder(view.Document.FilePath);
        menu.Items.Add(showInFolder);
        var openInNewPane = new MenuItem { Header = _localization.Get("tab.openInNewPane") };
        var leftRight = new MenuItem { Header = _localization.Get("tab.openInNewPane.leftRight") };
        leftRight.Click += (_, _) => OpenInNewPaneRequested?.Invoke(this, new PaneSplitRequest(view, SplitOrientation.Horizontal));
        openInNewPane.Items.Add(leftRight);
        var topBottom = new MenuItem { Header = _localization.Get("tab.openInNewPane.topBottom") };
        topBottom.Click += (_, _) => OpenInNewPaneRequested?.Invoke(this, new PaneSplitRequest(view, SplitOrientation.Vertical));
        openInNewPane.Items.Add(topBottom);
        menu.Items.Add(openInNewPane);
        var move = new MenuItem { Header = _localization.Get("group.moveTo") };
        move.Click += (_, _) => MoveRequested?.Invoke(this, view);
        menu.Items.Add(move);
        var snapshot = new MenuItem { Header = _localization.Get("file.snapshotHistory") };
        snapshot.Click += (_, _) => SnapshotRequested?.Invoke(this, view);
        menu.Items.Add(snapshot);
        var createSnapshot = new MenuItem
        {
            Header = _localization.Get("file.createSnapshot"),
            IsEnabled = view.Document.FilePath is not null
        };
        createSnapshot.Click += (_, _) => CreateSnapshotRequested?.Invoke(this, view);
        menu.Items.Add(createSnapshot);
        var rename = new MenuItem { Header = _localization.Get("tab.rename") };
        rename.Click += (_, _) => RenameRequested?.Invoke(this, view);
        menu.Items.Add(rename);

        // The menu is built once when the tab is added and never rebuilt, so recompute
        // enabled state on open in case the document was untitled and got saved since.
        menu.Opened += (_, _) =>
        {
            var hasPath = view.Document.FilePath is not null;
            showInFolder.IsEnabled = hasPath;
            createSnapshot.IsEnabled = hasPath;
        };

        return menu;
    }

    // Wired on the TabItem itself (not just its header content) so the whole visible tab — including its
    // padding — is draggable and droppable, matching what a user expects when grabbing "the tab".
    private void AttachTabDragDrop(TabItem tab, DocumentViewState view)
    {
        Point? dragStart = null;
        tab.PreviewMouseLeftButtonDown += (_, e) => dragStart = StartedOnTab(tab, e) ? e.GetPosition(null) : null;
        tab.PreviewMouseMove += (_, e) =>
        {
            if (dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }
            var current = e.GetPosition(null);
            if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            dragStart = null;
            DragDrop.DoDragDrop(tab, new DataObject(TabHeaderControl.TabDragFormat, view.ViewId.ToString()), DragDropEffects.Move);
        };

        tab.AllowDrop = true;
        tab.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(TabHeaderControl.TabDragFormat) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
        tab.Drop += (_, e) => HandleTabDrop(e, view);
    }

    /// <summary>
    /// The editor is the TabItem's <see cref="ContentControl.Content"/>, which makes it a *logical* child
    /// of the tab even though the TabControl hosts it visually elsewhere — so mouse events from the whole
    /// editing area route through the TabItem as well. Without this check, dragging to select text in the
    /// editor starts a tab drag, and the drag loop swallows the moves AvalonEdit needs to extend the
    /// selection. Only a gesture that starts on the tab's own visual subtree may begin a tab drag.
    /// </summary>
    private static bool StartedOnTab(TabItem tab, RoutedEventArgs e) =>
        e.OriginalSource is Visual source && tab.IsAncestorOf(source);

    private void HandleTabDrop(DragEventArgs e, DocumentViewState targetView)
    {
        e.Handled = true;
        if (e.Data.GetData(TabHeaderControl.TabDragFormat) is not string viewIdText ||
            !Guid.TryParse(viewIdText, out var draggedViewId) ||
            draggedViewId == targetView.ViewId ||
            !_views.ContainsKey(draggedViewId))
        {
            return;
        }

        var draggedTab = _tabs.Items.OfType<TabItem>().FirstOrDefault(item => ((DocumentViewState)item.Tag).ViewId == draggedViewId);
        var targetTab = _tabs.Items.OfType<TabItem>().FirstOrDefault(item => ((DocumentViewState)item.Tag).ViewId == targetView.ViewId);
        if (draggedTab is null || targetTab is null)
        {
            return;
        }

        var targetIndex = _tabs.Items.IndexOf(targetTab);
        _tabs.Items.Remove(draggedTab);
        _tabs.Items.Insert(targetIndex, draggedTab);
        _tabs.SelectedItem = draggedTab;

        var orderedIds = _tabs.Items.OfType<TabItem>().Select(item => ((DocumentViewState)item.Tag).ViewId).ToArray();
        TabsReordered?.Invoke(this, orderedIds);
    }

    private static void ShowInFolder(string? filePath)
    {
        if (filePath is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // 開啟檔案總管失敗時不應影響編輯器。
        }
    }

    private Border BuildFormattingToolbar()
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        AddFormatButton(buttons, "¶", "format.paragraph", MarkdownFormatCommand.Paragraph);
        AddFormatButton(buttons, "H1", "format.heading1", MarkdownFormatCommand.Heading1);
        AddFormatButton(buttons, "H2", "format.heading2", MarkdownFormatCommand.Heading2);
        AddFormatButton(buttons, "H3", "format.heading3", MarkdownFormatCommand.Heading3);
        AddFormatButton(buttons, "H4", "format.heading4", MarkdownFormatCommand.Heading4);
        AddFormatButton(buttons, "H5", "format.heading5", MarkdownFormatCommand.Heading5);
        AddFormatButton(buttons, "H6", "format.heading6", MarkdownFormatCommand.Heading6);
        AddFormatButton(buttons, "B", "format.bold", MarkdownFormatCommand.Bold);
        AddFormatButton(buttons, "I", "format.italic", MarkdownFormatCommand.Italic);
        AddFormatButton(buttons, "S", "format.strikethrough", MarkdownFormatCommand.Strikethrough);
        AddFormatButton(buttons, "`", "format.inlineCode", MarkdownFormatCommand.InlineCode);
        AddFormatButton(buttons, "↗", "format.link", MarkdownFormatCommand.Link);
        AddFormatButton(buttons, "•", "format.unorderedList", MarkdownFormatCommand.UnorderedList);
        AddFormatButton(buttons, "1.", "format.orderedList", MarkdownFormatCommand.OrderedList);
        AddFormatButton(buttons, "❯", "format.quote", MarkdownFormatCommand.Quote);
        AddFormatButton(buttons, "```", "format.codeBlock", MarkdownFormatCommand.CodeBlock);

        var scroll = new ScrollViewer
        {
            Content = buttons,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(5, 2, 5, 2),
            ToolTip = _localization.Get("toolbar.formatting")
        };
        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = scroll
        };
        border.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return border;
    }

    private void AddFormatButton(StackPanel panel, string content, string localizationKey, MarkdownFormatCommand command)
    {
        var button = new Button
        {
            Content = content,
            ToolTip = _localization.Get(localizationKey),
            MinWidth = content.Length > 2 ? 38 : 30,
            Height = 27,
            Padding = new Thickness(5, 0, 5, 0),
            Margin = new Thickness(1, 0, 1, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontWeight = command is MarkdownFormatCommand.Bold or MarkdownFormatCommand.Heading1
                ? FontWeights.SemiBold
                : FontWeights.Normal
        };
        button.SetResourceReference(Button.ForegroundProperty, "PrimaryTextBrush");
        button.Click += (_, _) => FormattingRequested?.Invoke(this, command);
        _formatButtonKeys[button] = localizationKey;
        panel.Children.Add(button);
    }

    private static bool IsYamlFile(string? path)
    {
        var extension = Path.GetExtension(path ?? string.Empty);
        return extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }

    private static Brush CreatePaletteBrush(string value, Brush fallback)
    {
        try
        {
            var brush = (Brush?)new BrushConverter().ConvertFromString(value);
            if (brush is not null)
            {
                brush.Freeze();
                return brush;
            }
        }
        catch (FormatException)
        {
            // 使用主題前景色作為失效自訂色的安全回退。
        }
        return fallback;
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != _tabs || SelectedView is not { } view)
        {
            return;
        }

        ViewSelected?.Invoke(this, view);
        UpdateEmptyState();
        BringSelectedTabIntoView();
    }

    private void BringSelectedTabIntoView()
    {
        if (_tabs.SelectedItem is not TabItem tab)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => tab.BringIntoView()));
    }

    private void AttachTabStripScrollViewer()
    {
        if (_tabs.Template?.FindName("TabStripScrollViewer", _tabs) is not ScrollViewer scrollViewer ||
            ReferenceEquals(scrollViewer, _tabStripScrollViewer))
        {
            return;
        }

        _tabStripScrollViewer = scrollViewer;
        scrollViewer.PreviewMouseWheel += TabStripScrollViewer_PreviewMouseWheel;
    }

    private static void TabStripScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableWidth <= 0)
        {
            return;
        }

        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void UpdateEmptyState() => _emptyHint.Visibility = _tabs.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
}
