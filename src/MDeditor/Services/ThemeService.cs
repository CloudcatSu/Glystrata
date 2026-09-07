namespace MDeditor.Services;

public sealed class ThemeService
{
    private ResourceDictionary? _activeDictionary;

    public ThemeKind CurrentTheme { get; private set; } = ThemeKind.Light;

    public event EventHandler? ThemeChanged;

    public void Apply(ThemeKind theme)
    {
        var dictionaryName = theme == ThemeKind.Dark ? "Dark.xaml" : "Light.xaml";
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Resources/Themes/{dictionaryName}", UriKind.Relative)
        };

        var resources = Application.Current.Resources;
        if (_activeDictionary is not null)
        {
            resources.MergedDictionaries.Remove(_activeDictionary);
        }

        resources.MergedDictionaries.Add(dictionary);
        _activeDictionary = dictionary;
        CurrentTheme = theme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
