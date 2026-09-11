using Forms = System.Windows.Forms;
using Glystrata.Controls;

namespace Glystrata.Views;

public partial class SettingsWindow : Window
{
    private readonly LocalizationService _localization;
    private readonly AppSettings _working;
    private readonly Dictionary<string, TextBox> _colorBoxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _typographyBoxes = new(StringComparer.Ordinal);
    private readonly StackPanel _content = new();
    private ComboBox _languageBox = null!;
    private ComboBox _themeBox = null!;
    private System.Windows.Controls.CheckBox _formattingToolbarBox = null!;
    private TextBox _snapshotIntervalBox = null!;
    private TextBox _maxSnapshotsBox = null!;
    private System.Windows.Controls.CheckBox _hideSnapshotFilesBox = null!;

    public SettingsWindow(AppSettings settings, LocalizationService localization)
    {
        InitializeComponent();
        CustomTitleBar.Attach(this, localization);
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
        tabs.Items.Add(CreatePreviewTab());
        tabs.Items.Add(CreateSnapshotsTab());
        Grid.SetRow(tabs, 0);
        RootGrid.Children.Add(tabs);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var reset = CreateButton(_localization.Get("settings.reset"));
        reset.Click += (_, _) =>
        {
            CopySettings(new AppSettings(), _working);
            LoadFields();
        };
        var cancel = CreateButton(_localization.Get("dialog.cancel"));
        cancel.Click += (_, _) => Close();
        var apply = CreateButton(_localization.Get("settings.apply"), true);
        apply.Click += (_, _) => ApplyAndClose();
        buttons.Children.Add(reset);
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
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
        _themeBox.Items.Add(_localization.Get("settings.light"));
        _themeBox.Items.Add(_localization.Get("settings.dark"));
        _themeBox.SelectionChanged += (_, _) =>
        {
            if (_themeBox.SelectedIndex >= 0)
            {
                var newTheme = _themeBox.SelectedIndex == 0 ? ThemeKind.Light : ThemeKind.Dark;
                if (newTheme != _working.Theme)
                {
                    SavePaletteFields(_working.Theme);
                    _working.Theme = newTheme;
                    LoadPaletteFields();
                }
            }
        };
        AddLabeledControl(panel, _localization.Get("settings.theme"), _themeBox);

        _formattingToolbarBox = new System.Windows.Controls.CheckBox
        {
            IsThreeState = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        AddLabeledControl(panel, _localization.Get("settings.showFormattingToolbar"), _formattingToolbarBox);
        return new TabItem { Header = _localization.Get("settings.general"), Content = WrapPanel(panel) };
    }

    private TabItem CreateColorsTab()
    {
        var panel = CreateScrollPanel();
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

    private TabItem CreatePreviewTab()
    {
        var panel = CreateScrollPanel();
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
        return new TabItem { Header = _localization.Get("settings.preview"), Content = WrapPanel(panel) };
    }

    private TabItem CreateSnapshotsTab()
    {
        var panel = CreateScrollPanel();
        _snapshotIntervalBox = new TextBox { Width = 90 };
        _maxSnapshotsBox = new TextBox { Width = 90 };
        AddLabeledControl(panel, _localization.Get("settings.snapshotInterval"), _snapshotIntervalBox);
        AddLabeledControl(panel, _localization.Get("settings.maxSnapshots"), _maxSnapshotsBox);
        _hideSnapshotFilesBox = new System.Windows.Controls.CheckBox
        {
            IsThreeState = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        AddLabeledControl(panel, _localization.Get("settings.hideSnapshotFiles"), _hideSnapshotFilesBox);
        return new TabItem { Header = _localization.Get("settings.snapshots"), Content = WrapPanel(panel) };
    }

    private void LoadFields()
    {
        _languageBox.SelectedIndex = _working.Language == AppLanguage.English ? 1 : 0;
        _themeBox.SelectedIndex = _working.Theme == ThemeKind.Dark ? 1 : 0;
        _formattingToolbarBox.IsChecked = _working.ShowFormattingToolbar;
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
    }

    private void LoadPaletteFields()
    {
        var palette = _working.Theme == ThemeKind.Dark ? _working.DarkEditorPalette : _working.LightEditorPalette;
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
            MessageBox.Show(this, _localization.Get("settings.error.invalidInteger"), _localization.Get("settings.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _working.SnapshotIntervalMinutes = interval;
        _working.MaxSnapshotsPerFile = maximum;
        _working.ShowFormattingToolbar = _formattingToolbarBox.IsChecked == true;
        _working.HideSnapshotFiles = _hideSnapshotFilesBox.IsChecked == true;
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
            MessageBox.Show(this, _localization.Get("settings.error.invalidNumber"), _localization.Get("settings.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var palette = _working.Theme == ThemeKind.Dark ? _working.DarkEditorPalette : _working.LightEditorPalette;
        foreach (var (key, box) in _colorBoxes)
        {
            if (!TryNormalizeColor(box.Text, out var color))
            {
                MessageBox.Show(this, string.Format(_localization.Get("settings.error.invalidColor"), box.Text), _localization.Get("settings.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            palette.Colors[key] = color;
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

    private static void AddLabeledControl(Panel panel, string label, UIElement control)
    {
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

    private static void ChooseColor(TextBox box, Border swatch)
    {
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = TryNormalizeColor(box.Text, out var value)
                ? System.Drawing.ColorTranslator.FromHtml(value)
                : System.Drawing.Color.Gray
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            var color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            box.Text = color;
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

    private static AppSettings CloneSettings(AppSettings source)
    {
        var clone = new AppSettings
        {
            SchemaVersion = source.SchemaVersion,
            Language = source.Language,
            Theme = source.Theme,
            SnapshotIntervalMinutes = source.SnapshotIntervalMinutes,
            MaxSnapshotsPerFile = source.MaxSnapshotsPerFile,
            CharacterCountMode = source.CharacterCountMode,
            ShowFormattingToolbar = source.ShowFormattingToolbar,
            HideSnapshotFiles = source.HideSnapshotFiles,
            LightEditorPalette = source.LightEditorPalette.Clone(),
            DarkEditorPalette = source.DarkEditorPalette.Clone(),
            PreviewTypography = new PreviewTypography
            {
                H1Size = source.PreviewTypography.H1Size,
                H2Size = source.PreviewTypography.H2Size,
                H3Size = source.PreviewTypography.H3Size,
                H4Size = source.PreviewTypography.H4Size,
                H5Size = source.PreviewTypography.H5Size,
                H6Size = source.PreviewTypography.H6Size,
                LineSpacing = source.PreviewTypography.LineSpacing,
                ParagraphSpacing = source.PreviewTypography.ParagraphSpacing,
                HeadingSpacing = source.PreviewTypography.HeadingSpacing
            }
        };
        clone.Normalize();
        return clone;
    }

    private static void CopySettings(AppSettings source, AppSettings target)
    {
        var clone = CloneSettings(source);
        target.SchemaVersion = clone.SchemaVersion;
        target.Language = clone.Language;
        target.Theme = clone.Theme;
        target.SnapshotIntervalMinutes = clone.SnapshotIntervalMinutes;
        target.MaxSnapshotsPerFile = clone.MaxSnapshotsPerFile;
        target.CharacterCountMode = clone.CharacterCountMode;
        target.ShowFormattingToolbar = clone.ShowFormattingToolbar;
        target.HideSnapshotFiles = clone.HideSnapshotFiles;
        target.LightEditorPalette = clone.LightEditorPalette;
        target.DarkEditorPalette = clone.DarkEditorPalette;
        target.PreviewTypography = clone.PreviewTypography;
    }
}
