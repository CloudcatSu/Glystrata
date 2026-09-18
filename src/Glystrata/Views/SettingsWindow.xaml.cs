using System.Text.Json;
using System.Text.Json.Nodes;
using Glystrata.Controls;

namespace Glystrata.Views;

public partial class SettingsWindow : Window
{
    private const string SettingsFileKind = "glystrata-settings";
    private const int SupportedSettingsSchemaVersion = 1;

    private readonly LocalizationService _localization;
    private AppSettings _working;
    private readonly Dictionary<string, TextBox> _colorBoxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _readerColorBoxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _typographyBoxes = new(StringComparer.Ordinal);
    private readonly List<TextBlock> _paletteScopeNotes = new();

    // Display order of the theme dropdown, which is deliberately not the enum's numeric order:
    // ThemePreference's values are pinned for settings.json compatibility.
    private static readonly ThemePreference[] ThemeOrder =
    {
        ThemePreference.System,
        ThemePreference.Light,
        ThemePreference.Dark
    };

    // Display order of the character-count dropdown; kept explicit rather than casting the selected
    // index, for the same settings.json compatibility reason as ThemeOrder above.
    private static readonly CharacterCountMode[] CharacterCountModeOrder =
    {
        CharacterCountMode.IncludeWhitespace,
        CharacterCountMode.ExcludeWhitespace,
        CharacterCountMode.ExcludeLineBreaks
    };

    private static readonly JsonSerializerOptions CloneOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ImportOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private ComboBox _languageBox = null!;
    private ComboBox _themeBox = null!;
    private ComboBox _characterCountModeBox = null!;
    private System.Windows.Controls.CheckBox _formattingToolbarBox = null!;
    private TextBox _snapshotIntervalBox = null!;
    private TextBox _maxSnapshotsBox = null!;
    private System.Windows.Controls.CheckBox _hideSnapshotFilesBox = null!;

    public SettingsWindow(AppSettings settings, LocalizationService localization)
    {
        InitializeComponent();
        CustomTitleBar.Attach(this, localization);
        DialogKeys.AttachEscapeToClose(this);
        _localization = localization;
        _working = CloneSettings(settings);
        BuildUi();
        ApplyThemeResources();
        _localization.LanguageChanged += Localization_LanguageChanged;
    }

    public event EventHandler<AppSettings>? SettingsApplied;

    protected override void OnClosed(EventArgs e)
    {
        _localization.LanguageChanged -= Localization_LanguageChanged;
        base.OnClosed(e);
    }

    private void BuildUi()
    {
        RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var tabs = new TabControl();
        tabs.Items.Add(CreateGeneralTab());
        tabs.Items.Add(CreateColorsTab());
        tabs.Items.Add(CreateReaderColorsTab());
        tabs.Items.Add(CreateSnapshotsTab());
        Grid.SetRow(tabs, 0);
        RootGrid.Children.Add(tabs);

        var buttons = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };

        var leftButtons = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(leftButtons, Dock.Left);
        var reset = CreateButton(_localization.Get("settings.reset"));
        reset.Click += (_, _) =>
        {
            _working = CloneSettings(new AppSettings());
            LoadFields();
        };
        var export = CreateButton(_localization.Get("settings.export"));
        export.Click += (_, _) => ExportSettings();
        var import = CreateButton(_localization.Get("settings.import"));
        import.Click += (_, _) => ImportSettings();
        leftButtons.Children.Add(reset);
        leftButtons.Children.Add(export);
        leftButtons.Children.Add(import);
        buttons.Children.Add(leftButtons);

        var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = CreateButton(_localization.Get("dialog.cancel"));
        cancel.Click += (_, _) => Close();
        var apply = CreateButton(_localization.Get("settings.apply"), true);
        apply.Click += (_, _) => ApplyAndClose();
        rightButtons.Children.Add(cancel);
        rightButtons.Children.Add(apply);
        buttons.Children.Add(rightButtons);

        Grid.SetRow(buttons, 1);
        RootGrid.Children.Add(buttons);

        Title = _localization.Get("settings.title");
        LoadFields();
    }

    private TabItem CreateGeneralTab()
    {
        var panel = CreateScrollPanel();
        _languageBox = new ComboBox { Width = 220 };
        _languageBox.Items.Add(_localization.Get("settings.language.zh"));
        _languageBox.Items.Add(_localization.Get("settings.language.en"));
        _languageBox.SelectionChanged += (_, _) =>
        {
            if (_languageBox.SelectedIndex >= 0)
            {
                _working.Language = _languageBox.SelectedIndex == 0 ? AppLanguage.TraditionalChinese : AppLanguage.English;
            }
        };
        AddLabeledControl(panel, _localization.Get("settings.language"), _languageBox);

        _themeBox = new ComboBox { Width = 220 };
        _themeBox.Items.Add(_localization.Get("settings.theme.system"));
        _themeBox.Items.Add(_localization.Get("settings.light"));
        _themeBox.Items.Add(_localization.Get("settings.dark"));
        _themeBox.SelectionChanged += (_, _) =>
        {
            if (_themeBox.SelectedIndex < 0)
            {
                return;
            }

            var preference = ThemeOrder[_themeBox.SelectedIndex];
            if (preference == _working.Theme)
            {
                return;
            }

            // The colour tabs edit whichever palette the chosen theme resolves to, so anything typed
            // so far belongs to the previous one and has to be stored before the fields are reloaded.
            var previous = ThemeService.Resolve(_working.Theme);
            _working.Theme = preference;
            if (ThemeService.Resolve(preference) != previous)
            {
                SavePaletteFields(previous);
                SaveReaderPaletteFields(previous);
                LoadPaletteFields();
                LoadReaderPaletteFields();
            }
            UpdatePaletteScopeNotes();
        };
        AddLabeledControl(panel, _localization.Get("settings.theme"), _themeBox);

        _formattingToolbarBox = new System.Windows.Controls.CheckBox
        {
            IsThreeState = false,
            Padding = new Thickness(0)
        };
        AddLabeledControl(panel, _localization.Get("settings.showFormattingToolbar"), _formattingToolbarBox);

        _characterCountModeBox = new ComboBox { Width = 220 };
        _characterCountModeBox.Items.Add(_localization.Get("status.countMode.includeWhitespace"));
        _characterCountModeBox.Items.Add(_localization.Get("status.countMode.excludeWhitespace"));
        _characterCountModeBox.Items.Add(_localization.Get("status.countMode.excludeLineBreaks"));
        AddLabeledControl(panel, _localization.Get("settings.characterCountMode"), _characterCountModeBox);
        return new TabItem { Header = _localization.Get("settings.general"), Content = WrapPanel(panel) };
    }

    private TabItem CreateColorsTab()
    {
        var panel = CreateScrollPanel();
        AddPaletteScopeNote(panel);
        var note = new TextBlock
        {
            Text = "HEX",
            Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        panel.Children.Add(note);
        foreach (var key in new[] { "plain", "heading", "emphasis", "link", "list", "quote", "code", "yaml-key", "yaml-value", "yaml-comment", "front-matter" })
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var swatch = new Border { Width = 28, Height = 24, Margin = new Thickness(0, 0, 8, 0), CornerRadius = new CornerRadius(3) };
            var box = new TextBox { Width = 110, HorizontalContentAlignment = HorizontalAlignment.Center };
            box.TextChanged += (_, _) => UpdateSwatch(box, swatch);
            _colorBoxes[key] = box;
            var choose = new Button { Content = "…", Width = 30, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(0) };
            choose.Click += (_, _) => ChooseColor(box, swatch);
            row.Children.Add(swatch);
            row.Children.Add(new TextBlock
            {
                Text = _localization.Get($"settings.color.{key}"),
                Width = 170,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush")
            });
            row.Children.Add(box);
            row.Children.Add(choose);
            panel.Children.Add(row);
        }

        return new TabItem { Header = _localization.Get("settings.colors"), Content = WrapPanel(panel) };
    }

    private TabItem CreateReaderColorsTab()
    {
        var panel = CreateScrollPanel();
        AddPaletteScopeNote(panel);
        var note = new TextBlock
        {
            Text = _localization.Get("settings.readerColors.help"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        panel.Children.Add(note);
        foreach (var key in new[] { "text", "heading", "link", "quote", "code" })
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var swatch = new Border { Width = 28, Height = 24, Margin = new Thickness(0, 0, 8, 0), CornerRadius = new CornerRadius(3) };
            var box = new TextBox { Width = 110, HorizontalContentAlignment = HorizontalAlignment.Center };
            box.TextChanged += (_, _) => UpdateSwatch(box, swatch);
            _readerColorBoxes[key] = box;
            var choose = new Button { Content = "…", Width = 30, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(0) };
            choose.Click += (_, _) => ChooseColor(box, swatch);
            row.Children.Add(swatch);
            row.Children.Add(new TextBlock
            {
                Text = _localization.Get($"settings.readerColor.{key}"),
                Width = 170,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush")
            });
            row.Children.Add(box);
            row.Children.Add(choose);
            panel.Children.Add(row);
        }

        var previewHeader = new TextBlock
        {
            Text = _localization.Get("settings.preview"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 6),
            Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush")
        };
        panel.Children.Add(previewHeader);
        foreach (var (key, label) in new[]
        {
            ("h1", "settings.heading.h1"), ("h2", "settings.heading.h2"), ("h3", "settings.heading.h3"),
            ("h4", "settings.heading.h4"), ("h5", "settings.heading.h5"), ("h6", "settings.heading.h6"),
            ("line", "settings.lineSpacing"), ("paragraph", "settings.paragraphSpacing"), ("heading", "settings.headingSpacing")
        })
        {
            var box = new TextBox { Width = 90 };
            _typographyBoxes[key] = box;
            AddLabeledControl(panel, _localization.Get(label), box);
        }

        return new TabItem { Header = _localization.Get("settings.readerColors"), Content = WrapPanel(panel) };
    }

    private TabItem CreateSnapshotsTab()
    {
        var panel = CreateScrollPanel();

        _snapshotIntervalBox = new TextBox { Width = 90 };
        AddLabeledControl(panel, _localization.Get("settings.snapshotInterval"), _snapshotIntervalBox);
        AddHelpText(panel, "settings.snapshotInterval.help");

        _maxSnapshotsBox = new TextBox { Width = 90 };
        AddLabeledControl(panel, _localization.Get("settings.maxSnapshots"), _maxSnapshotsBox);
        AddHelpText(panel, "settings.maxSnapshots.help");

        _hideSnapshotFilesBox = new System.Windows.Controls.CheckBox
        {
            IsThreeState = false,
            Padding = new Thickness(0)
        };
        AddLabeledControl(panel, _localization.Get("settings.hideSnapshotFiles"), _hideSnapshotFilesBox);
        AddHelpText(panel, "settings.hideSnapshotFiles.help");

        return new TabItem { Header = _localization.Get("settings.snapshots"), Content = WrapPanel(panel) };
    }

    private void LoadFields()
    {
        _languageBox.SelectedIndex = _working.Language == AppLanguage.English ? 1 : 0;
        var themeIndex = Array.IndexOf(ThemeOrder, _working.Theme);
        _themeBox.SelectedIndex = themeIndex >= 0 ? themeIndex : 0;
        _formattingToolbarBox.IsChecked = _working.ShowFormattingToolbar;
        var countModeIndex = Array.IndexOf(CharacterCountModeOrder, _working.CharacterCountMode);
        _characterCountModeBox.SelectedIndex = countModeIndex >= 0 ? countModeIndex : 0;
        _snapshotIntervalBox.Text = _working.SnapshotIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        _maxSnapshotsBox.Text = _working.MaxSnapshotsPerFile.ToString(CultureInfo.InvariantCulture);
        _hideSnapshotFilesBox.IsChecked = _working.HideSnapshotFiles;
        _typographyBoxes["h1"].Text = _working.PreviewTypography.H1Size.ToString(CultureInfo.InvariantCulture);
        _typographyBoxes["h2"].Text = _working.PreviewTypography.H2Size.ToString(CultureInfo.InvariantCulture);
        _typographyBoxes["h3"].Text = _working.PreviewTypography.H3Size.ToString(CultureInfo.InvariantCulture);
        _typographyBoxes["h4"].Text = _working.PreviewTypography.H4Size.ToString(CultureInfo.InvariantCulture);
        _typographyBoxes["h5"].Text = _working.PreviewTypography.H5Size.ToString(CultureInfo.InvariantCulture);
        _typographyBoxes["h6"].Text = _working.PreviewTypography.H6Size.ToString(CultureInfo.InvariantCulture);
        _typographyBoxes["line"].Text = _working.PreviewTypography.LineSpacing.ToString(CultureInfo.InvariantCulture);
        _typographyBoxes["paragraph"].Text = _working.PreviewTypography.ParagraphSpacing.ToString(CultureInfo.InvariantCulture);
        _typographyBoxes["heading"].Text = _working.PreviewTypography.HeadingSpacing.ToString(CultureInfo.InvariantCulture);
        LoadPaletteFields();
        LoadReaderPaletteFields();
        UpdatePaletteScopeNotes();
    }

    /// <summary>The palette the colour tabs are currently editing. "Follow the system" is resolved
    /// first, so the tabs always edit the palette that is actually in effect.</summary>
    private ThemeKind EditingTheme => ThemeService.Resolve(_working.Theme);

    private void AddPaletteScopeNote(Panel panel)
    {
        var note = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush")
        };
        _paletteScopeNotes.Add(note);
        panel.Children.Add(note);
    }

    private void UpdatePaletteScopeNotes()
    {
        var themeName = _localization.Get(EditingTheme == ThemeKind.Dark ? "settings.dark" : "settings.light");
        if (_working.Theme == ThemePreference.System)
        {
            themeName = string.Format(CultureInfo.CurrentCulture, _localization.Get("settings.theme.systemSuffix"), themeName);
        }
        var text = string.Format(CultureInfo.CurrentCulture, _localization.Get("settings.paletteScope"), themeName);
        foreach (var note in _paletteScopeNotes)
        {
            note.Text = text;
        }
    }

    private void LoadPaletteFields()
    {
        var palette = EditingTheme == ThemeKind.Dark ? _working.DarkEditorPalette : _working.LightEditorPalette;
        foreach (var (key, box) in _colorBoxes)
        {
            box.Text = palette.Get(key, "#808080");
        }
    }

    private void SavePaletteFields(ThemeKind theme)
    {
        var palette = theme == ThemeKind.Dark ? _working.DarkEditorPalette : _working.LightEditorPalette;
        foreach (var (key, box) in _colorBoxes)
        {
            if (TryNormalizeColor(box.Text, out var color))
            {
                palette.Colors[key] = color;
            }
        }
    }

    private void LoadReaderPaletteFields()
    {
        var palette = EditingTheme == ThemeKind.Dark ? _working.DarkReaderPalette : _working.LightReaderPalette;
        foreach (var (key, box) in _readerColorBoxes)
        {
            box.Text = palette.Get(key, "#808080");
        }
    }

    private void SaveReaderPaletteFields(ThemeKind theme)
    {
        var palette = theme == ThemeKind.Dark ? _working.DarkReaderPalette : _working.LightReaderPalette;
        foreach (var (key, box) in _readerColorBoxes)
        {
            if (TryNormalizeColor(box.Text, out var color))
            {
                palette.Colors[key] = color;
            }
        }
    }

    private void ApplyAndClose()
    {
        if (!TryReadSettings())
        {
            return;
        }

        SettingsApplied?.Invoke(this, _working);
        Close();
    }

    private bool TryReadSettings()
    {
        if (!int.TryParse(_snapshotIntervalBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval) ||
            !int.TryParse(_maxSnapshotsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maximum))
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), _localization.Get("settings.error.invalidInteger"));
            return false;
        }

        _working.SnapshotIntervalMinutes = interval;
        _working.MaxSnapshotsPerFile = maximum;
        _working.ShowFormattingToolbar = _formattingToolbarBox.IsChecked == true;
        _working.HideSnapshotFiles = _hideSnapshotFilesBox.IsChecked == true;
        if (_characterCountModeBox.SelectedIndex >= 0)
        {
            _working.CharacterCountMode = CharacterCountModeOrder[_characterCountModeBox.SelectedIndex];
        }
        var typography = _working.PreviewTypography;
        if (!TryDouble("h1", value => typography.H1Size = value) ||
            !TryDouble("h2", value => typography.H2Size = value) ||
            !TryDouble("h3", value => typography.H3Size = value) ||
            !TryDouble("h4", value => typography.H4Size = value) ||
            !TryDouble("h5", value => typography.H5Size = value) ||
            !TryDouble("h6", value => typography.H6Size = value) ||
            !TryDouble("line", value => typography.LineSpacing = value) ||
            !TryDouble("paragraph", value => typography.ParagraphSpacing = value) ||
            !TryDouble("heading", value => typography.HeadingSpacing = value))
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), _localization.Get("settings.error.invalidNumber"));
            return false;
        }

        var palette = EditingTheme == ThemeKind.Dark ? _working.DarkEditorPalette : _working.LightEditorPalette;
        foreach (var (key, box) in _colorBoxes)
        {
            if (!TryNormalizeColor(box.Text, out var color))
            {
                MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), string.Format(_localization.Get("settings.error.invalidColor"), box.Text));
                return false;
            }
            palette.Colors[key] = color;
        }

        var readerPalette = EditingTheme == ThemeKind.Dark ? _working.DarkReaderPalette : _working.LightReaderPalette;
        foreach (var (key, box) in _readerColorBoxes)
        {
            if (!TryNormalizeColor(box.Text, out var color))
            {
                MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), string.Format(_localization.Get("settings.error.invalidColor"), box.Text));
                return false;
            }
            readerPalette.Colors[key] = color;
        }

        _working.Normalize();
        return true;
    }

    private bool TryDouble(string key, Action<double> assign)
    {
        if (!double.TryParse(_typographyBoxes[key].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            return false;
        }
        assign(value);
        return true;
    }

    private static void AddLabeledControl(Panel panel, string label, FrameworkElement control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Left;
        control.VerticalAlignment = VerticalAlignment.Center;
        var row = new DockPanel { Margin = new Thickness(0, 6, 0, 6) };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Width = 250,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush")
        });
        row.Children.Add(control);
        panel.Children.Add(row);
    }

    private void AddHelpText(Panel panel, string localizationKey)
    {
        panel.Children.Add(new TextBlock
        {
            Text = _localization.Get(localizationKey),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(250, -2, 0, 10),
            Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush")
        });
    }

    private static StackPanel CreateScrollPanel() => new() { Margin = new Thickness(6) };

    private static ScrollViewer WrapPanel(StackPanel panel)
    {
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static Button CreateButton(string text, bool accent = false) => new()
    {
        Content = text,
        Padding = new Thickness(16, 7, 16, 7),
        Margin = new Thickness(6, 0, 0, 0),
        Background = accent ? (Brush)Application.Current.FindResource("AccentBrush") : (Brush)Application.Current.FindResource("SurfaceBrush"),
        Foreground = accent ? Brushes.White : (Brush)Application.Current.FindResource("PrimaryTextBrush")
    };

    private void ApplyThemeResources()
    {
        Background = (Brush)Application.Current.FindResource("WindowBackgroundBrush");
        RootGrid.Background = Background;
    }

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        Title = _localization.Get("settings.title");
    }

    private static void UpdateSwatch(TextBox box, Border swatch)
    {
        if (TryNormalizeColor(box.Text, out var value))
        {
            swatch.Background = (Brush)new BrushConverter().ConvertFromString(value)!;
        }
    }

    private void ChooseColor(TextBox box, Border swatch)
    {
        var current = TryNormalizeColor(box.Text, out var value) ? value : "#808080";
        var picked = ColorPickerDialog.Show(this, _localization.Get("settings.pickColor"), current, _localization);
        if (picked is not null)
        {
            box.Text = picked;
            UpdateSwatch(box, swatch);
        }
    }

    private static bool TryNormalizeColor(string? value, out string color)
    {
        color = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var converted = new BrushConverter().ConvertFromString(value) as SolidColorBrush;
            if (converted is null)
            {
                return false;
            }
            color = $"#{converted.Color.R:X2}{converted.Color.G:X2}{converted.Color.B:X2}";
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // A hand-written property-by-property copy silently drops whatever AppSettings gains next, so
    // cloning goes through the same serializer that persists settings.json instead.
    private static AppSettings CloneSettings(AppSettings source)
    {
        var clone = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(source, CloneOptions), CloneOptions)
                    ?? new AppSettings();
        clone.Normalize();
        return clone;
    }

    private void ExportSettings()
    {
        if (!TryReadSettings())
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = $"{_localization.Get("settings.fileFilter")}|*.gss",
            FileName = "glystrata-settings.gss",
            OverwritePrompt = true,
            Title = _localization.Get("settings.export")
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            // .gss is already the extension of the per-document snapshot sidecar
            // (SnapshotSidecarStore.GetSidecarPath), so an exported settings file and a snapshot file
            // share an extension; "kind" is what lets import tell the two apart.
            var node = JsonSerializer.SerializeToNode(_working, ExportOptions)!.AsObject();
            node["kind"] = SettingsFileKind;
            File.WriteAllText(dialog.FileName, node.ToJsonString(ExportOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), exception.Message);
        }
    }

    private void ImportSettings()
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{_localization.Get("settings.fileFilter")}|*.gss",
            CheckFileExists = true,
            Title = _localization.Get("settings.import")
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            var documentOptions = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            if (JsonNode.Parse(json, documentOptions: documentOptions) is not JsonObject node)
            {
                MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), _localization.Get("settings.import.invalid"));
                return;
            }

            // A snapshot sidecar shares the .gss extension with an exported settings file (see
            // ExportSettings), so its shape is ruled out before trusting the "kind" field.
            if (node.ContainsKey("snapshots") || node.ContainsKey("notice"))
            {
                MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), _localization.Get("settings.import.notSettings"));
                return;
            }

            if (node["kind"]?.GetValue<string>() != SettingsFileKind)
            {
                MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), _localization.Get("settings.import.notSettings"));
                return;
            }

            if (node["schemaVersion"] is { } schemaVersionNode && schemaVersionNode.GetValue<int>() > SupportedSettingsSchemaVersion)
            {
                MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), _localization.Get("settings.import.tooNew"));
                return;
            }

            var imported = JsonSerializer.Deserialize<AppSettings>(node.ToJsonString(), ImportOptions);
            if (imported is null)
            {
                MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), _localization.Get("settings.import.invalid"));
                return;
            }

            // Import only replaces the in-memory working copy; the user still has to press Apply, so
            // Cancel backs the import out.
            imported.Normalize();
            _working = imported;
            LoadFields();
            MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), _localization.Get("settings.import.success"));
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // InvalidOperationException also lands here: GetValue<T>() throws it when a field above
            // turns out to be the wrong JSON type.
            MessageDialogs.Inform(this, _localization, _localization.Get("settings.title"), _localization.Get("settings.import.invalid"));
        }
    }
}
