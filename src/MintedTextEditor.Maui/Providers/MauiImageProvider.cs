using MintedTextEditor.Core.Formatting;
using SkiaSharp;
using Svg.Skia;

namespace MintedTextEditor.Maui.Providers;

/// <summary>
/// Default MAUI implementation for loading image sources into SKBitmap handles.
/// </summary>
public sealed class MauiImageProvider : IImageProvider
{
    private const int DefaultSvgRasterSize = 512;

    /// <summary>
    /// Loads an image source from app assets, file paths, or HTTP(S) URLs.
    /// </summary>
    public async Task<object> LoadImageAsync(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Image source is required.", nameof(source));

        SKBitmap? bmp = null;
        bool isSvg = IsSvgSource(source);

        if (!source.Contains('/') && !source.Contains('\\'))
        {
            try
            {
                await using var stream = await FileSystem.OpenAppPackageFileAsync(source).ConfigureAwait(false);
                bmp = isSvg
                    ? DecodeSvgBitmap(stream)
                    : SKBitmap.Decode(stream);
            }
            catch
            {
                // Ignore and continue with path/URL resolvers.
            }
        }

        if (bmp is null && (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                            || Path.IsPathRooted(source)))
        {
            var path = source.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(source).LocalPath
                : source;
            await using var stream = File.OpenRead(path);
            bmp = IsSvgPath(path)
                ? DecodeSvgBitmap(stream)
                : SKBitmap.Decode(stream);
        }

        if (bmp is null && (source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                            || source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
        {
            using var httpClient = new HttpClient();
            var bytes = await httpClient.GetByteArrayAsync(source).ConfigureAwait(false);
            if (isSvg || LooksLikeSvg(bytes))
            {
                using var mem = new MemoryStream(bytes, writable: false);
                bmp = DecodeSvgBitmap(mem);
            }
            else
            {
                bmp = SKBitmap.Decode(bytes);
            }
        }

        return bmp ?? throw new InvalidOperationException($"Unable to load image source '{source}'.");
    }

    private static bool IsSvgSource(string source)
    {
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
                return IsSvgPath(uri.AbsolutePath);

            return source.Contains(".svg", StringComparison.OrdinalIgnoreCase);
        }

        return IsSvgPath(source);
    }

    private static bool IsSvgPath(string path)
        => path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSvg(byte[] bytes)
    {
        int probe = Math.Min(bytes.Length, 1024);
        if (probe == 0)
            return false;

        var head = System.Text.Encoding.UTF8.GetString(bytes, 0, probe);
        return head.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static SKBitmap? DecodeSvgBitmap(Stream svgStream)
    {
        var svg = new SKSvg();
        var picture = svg.Load(svgStream);
        if (picture is null)
            return null;

        var cull = picture.CullRect;
        float width = cull.Width > 1f ? cull.Width : DefaultSvgRasterSize;
        float height = cull.Height > 1f ? cull.Height : DefaultSvgRasterSize;

        int outWidth = Math.Max(1, (int)Math.Ceiling(width));
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
