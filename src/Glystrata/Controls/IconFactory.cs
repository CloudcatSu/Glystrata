using Rectangle = System.Windows.Shapes.Rectangle;
using Shape = System.Windows.Shapes.Shape;

namespace Glystrata.Controls;

/// <summary>
/// Builds line-art icons from black-on-transparent PNGs, used as an opacity mask so the
/// stroke takes whatever brush the caller supplies and follows the theme.
/// </summary>
public static class IconFactory
{
    public static Rectangle Create(string resourcePath, double size)
    {
        var mask = new ImageBrush(new BitmapImage(new Uri($"pack://application:,,,/Resources/Icons/{resourcePath}")));
        RenderOptions.SetBitmapScalingMode(mask, BitmapScalingMode.HighQuality);
        return new Rectangle
        {
            Width = size,
            Height = size,
            OpacityMask = mask,
            SnapsToDevicePixels = true
        };
    }

    public static Rectangle Create(string resourcePath, double size, string fillResourceKey)
    {
        var icon = Create(resourcePath, size);
        icon.SetResourceReference(Shape.FillProperty, fillResourceKey);
        return icon;
    }
}
