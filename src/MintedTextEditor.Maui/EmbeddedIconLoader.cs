using MintedTextEditor.Core.Toolbar;
using SkiaSharp;
using Svg.Skia;

namespace MintedTextEditor.Maui;

/// <summary>
/// Rasterizes the built-in SVG icons from <see cref="ToolbarIconResources"/> into
/// <see cref="SKBitmap"/> instances at a requested minimum pixel size.
/// Icons are rendered with their native black ink so that draw-time colour filtering
/// (see <see cref="MintedTextEditor.Core.Rendering.IDrawingContext.DrawTintedImage"/>)
/// can recolour them to the current theme colour automatically.
/// </summary>
internal static class EmbeddedIconLoader
{
    private const int DefaultSvgRasterSize = 512;

    /// <summary>
    /// Loads and rasterizes all embedded icons for <paramref name="pack"/> at the
    /// given <paramref name="pixelSize"/>.
    /// The returned bitmaps are owned by the caller.
    /// </summary>
    public static Dictionary<string, SKBitmap> LoadAll(ToolbarIconPack pack, int pixelSize)
    {
        var result = new Dictionary<string, SKBitmap>(StringComparer.Ordinal);

        foreach (var key in ToolbarIconResources.Keys)
        {
            using var stream = ToolbarIconResources.OpenIconStream(key, pack);
            if (stream is null)
                continue;

            var bmp = Rasterize(stream, pixelSize);
            if (bmp is not null)
                result[key] = bmp;
        }

        return result;
    }

    private static SKBitmap? Rasterize(Stream svgStream, int minDimension)
    {
        var svg = new SKSvg();
        var picture = svg.Load(svgStream);
        if (picture is null)
            return null;

        var cull = picture.CullRect;
        float width  = cull.Width  > 1f ? cull.Width  : DefaultSvgRasterSize;
        float height = cull.Height > 1f ? cull.Height : DefaultSvgRasterSize;

        if (minDimension > 0)
        {
            float scale = Math.Max(minDimension / width, minDimension / height);
            if (scale > 1f)
            {
                width  *= scale;
                height *= scale;
            }
        }

        int outWidth  = Math.Max(1, (int)Math.Ceiling(width));
        int outHeight = Math.Max(1, (int)Math.Ceiling(height));

        var bitmap = new SKBitmap(outWidth, outHeight, true);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        if (cull.Width > 1f && cull.Height > 1f)
        {
            var matrix = SKMatrix.CreateScale(outWidth / cull.Width, outHeight / cull.Height);
            canvas.SetMatrix(matrix);
            canvas.DrawPicture(picture, -cull.Left, -cull.Top);
        }
        else
        {
            canvas.DrawPicture(picture);
        }

        canvas.Flush();
        return bitmap;
    }
}
