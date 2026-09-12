using System.IO.Compression;

namespace Glystrata.Preview;

/// <summary>
/// Draws emoji as images in the reader. WPF renders colour fonts as flat monochrome glyphs, so the
/// Markdown reader swaps emoji sequences for the bundled Twemoji artwork instead.
/// </summary>
public static class EmojiRenderer
{
    private const string ArchiveUri = "pack://application:,,,/Resources/Emoji/twemoji-72.zip";

    private static readonly Dictionary<string, BitmapSource?> Cache = new(StringComparer.Ordinal);
    private static readonly EmojiScaleConverter ScaleConverter = new();
    private static ZipArchive? _archive;
    private static bool _archiveLoadFailed;

    /// <summary>Appends <paramref name="text"/>, replacing emoji sequences with images where artwork exists.</summary>
    public static void AddText(InlineCollection target, string text, Brush foreground)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var index = 0;
        var runStart = 0;
        while (index < text.Length)
        {
            var sequenceLength = EmojiScanner.MatchSequence(text, index);
            if (sequenceLength == 0)
            {
                index++;
                continue;
            }

            var image = TryCreateImage(text.Substring(index, sequenceLength));
            if (image is null)
            {
                index += sequenceLength;
                continue;
            }

            if (index > runStart)
            {
                target.Add(new Run(text[runStart..index]) { Foreground = foreground });
            }
            target.Add(image);
            index += sequenceLength;
            runStart = index;
        }

        if (runStart < text.Length)
        {
            target.Add(new Run(text[runStart..]) { Foreground = foreground });
        }
    }

    private static InlineUIContainer? TryCreateImage(string sequence)
    {
        var source = Load(sequence);
        if (source is null)
        {
            return null;
        }

        var image = new Image
        {
            Source = source,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        var container = new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.TextBottom };
        // The artwork tracks whatever font size the surrounding text ends up with.
        image.SetBinding(FrameworkElement.HeightProperty, new System.Windows.Data.Binding(nameof(Inline.FontSize))
        {
            Source = container,
            Converter = ScaleConverter
        });
        image.SetBinding(FrameworkElement.WidthProperty, new System.Windows.Data.Binding(nameof(Inline.FontSize))
        {
            Source = container,
            Converter = ScaleConverter
        });
        return container;
    }

    private static BitmapSource? Load(string sequence)
    {
        foreach (var key in EmojiScanner.CandidateKeys(sequence))
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                if (cached is not null)
                {
                    return cached;
                }
                continue;
            }

            var loaded = LoadFromArchive(key);
            Cache[key] = loaded;
            if (loaded is not null)
            {
                return loaded;
            }
        }
        return null;
    }

    private static BitmapSource? LoadFromArchive(string key)
    {
        var archive = OpenArchive();
        var entry = archive?.GetEntry($"{key}.png");
        if (entry is null)
        {
            return null;
        }

        try
        {
            using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            entryStream.CopyTo(buffer);
            buffer.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = buffer;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException)
        {
            return null;
        }
    }

    private static ZipArchive? OpenArchive()
    {
        if (_archive is not null || _archiveLoadFailed)
        {
            return _archive;
        }

        try
        {
            var stream = Application.GetResourceStream(new Uri(ArchiveUri))?.Stream;
            _archive = stream is null ? null : new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException)
        {
            _archive = null;
        }
        _archiveLoadFailed = _archive is null;
        return _archive;
    }

    private sealed class EmojiScaleConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double fontSize && fontSize > 0 ? fontSize * 1.15 : 16.0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            System.Windows.Data.Binding.DoNothing;
    }
}
