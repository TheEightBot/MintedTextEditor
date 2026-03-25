using MintedTextEditor.Core.Rendering;
using SkiaSharp;

namespace MintedTextEditor.SkiaSharp;

/// <summary>
/// SkiaSharp implementation of <see cref="IDrawingContext"/>.
/// Wraps an <see cref="SKCanvas"/> and caches SKPaint/SKFont/SKTypeface per frame.
/// </summary>
public class SkiaDrawingContext : IDrawingContext, IDisposable
{
    private readonly SKCanvas _canvas;

    // Per-frame caching
    private readonly Dictionary<PaintKey, SKPaint> _paintCache = new();
    private readonly Dictionary<FontKey, SKFont> _fontCache = new();
    private readonly Dictionary<TypefaceKey, SKTypeface> _typefaceCache = new();

    public SkiaDrawingContext(SKCanvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
    }

    // ── Rectangles ───────────────────────────────────────────────

    public void DrawRect(EditorRect rect, EditorPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Stroke);
        _canvas.DrawRect(ToSKRect(rect), skPaint);
    }

    public void FillRect(EditorRect rect, EditorPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        _canvas.DrawRect(ToSKRect(rect), skPaint);
    }

    public void DrawRoundRect(EditorRect rect, float cornerRadius, EditorPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Stroke);
        _canvas.DrawRoundRect(ToSKRect(rect), cornerRadius, cornerRadius, skPaint);
    }

    public void FillRoundRect(EditorRect rect, float cornerRadius, EditorPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        _canvas.DrawRoundRect(ToSKRect(rect), cornerRadius, cornerRadius, skPaint);
    }

    // ── Lines ────────────────────────────────────────────────────

    public void DrawLine(float x1, float y1, float x2, float y2, EditorPaint paint)
    {
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Stroke);
        _canvas.DrawLine(x1, y1, x2, y2, skPaint);
    }

    // ── Text ─────────────────────────────────────────────────────

    public void DrawText(string text, float x, float y, EditorPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return;

        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        var skFont = GetOrCreateFont(paint);
        _canvas.DrawText(text, x, y, SKTextAlign.Left, skFont, skPaint);
    }

    public void DrawTextInRect(string text, EditorRect rect, EditorPaint paint,
        TextAlignment hAlign = TextAlignment.Left,
        VerticalAlignment vAlign = VerticalAlignment.Center,
        bool clip = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        var skFont = GetOrCreateFont(paint);

        // Measure the text
        float textWidth = skFont.MeasureText(text, out _, skPaint);
        var metrics = skFont.Metrics;

        // Truncate with ellipsis if needed
        string displayText = text;
        if (textWidth > rect.Width)
        {
            displayText = TruncateWithEllipsis(text, rect.Width, skFont, skPaint);
            textWidth = skFont.MeasureText(displayText, out _, skPaint);
        }

        // Horizontal alignment
        float x = hAlign switch
        {
            TextAlignment.Center => rect.X + (rect.Width - textWidth) / 2f,
            TextAlignment.Right => rect.Right - textWidth,
            _ => rect.X
        };

        // Vertical alignment
        float textHeight = metrics.Descent - metrics.Ascent;
        float y = vAlign switch
        {
            VerticalAlignment.Top => rect.Y - metrics.Ascent,
            VerticalAlignment.Bottom => rect.Bottom - metrics.Descent,
            _ => rect.Y + (rect.Height - textHeight) / 2f - metrics.Ascent // Center
        };

        if (clip)
        {
            _canvas.Save();
            _canvas.ClipRect(ToSKRect(rect));
        }

        _canvas.DrawText(displayText, x, y, SKTextAlign.Left, skFont, skPaint);

        if (clip)
        {
            _canvas.Restore();
        }
    }

    // ── Text measuring ───────────────────────────────────────────

    public EditorSize MeasureText(string text, EditorPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return EditorSize.Empty;

        var skFont = GetOrCreateFont(paint);
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);

        float width = skFont.MeasureText(text, out _, skPaint);
        var metrics = skFont.Metrics;
        float height = metrics.Descent - metrics.Ascent;

        return new EditorSize(width, height);
    }

    public EditorFontMetrics GetFontMetrics(EditorPaint paint)
    {
        var skFont = GetOrCreateFont(paint);
        var metrics = skFont.Metrics;
        return new EditorFontMetrics(metrics.Ascent, metrics.Descent, metrics.Leading);
    }

    // ── Clipping & transforms ────────────────────────────────────

    public void ClipRect(EditorRect rect)
    {
        _canvas.ClipRect(ToSKRect(rect));
    }

    public void Save()
    {
        _canvas.Save();
    }

    public void Restore()
    {
        _canvas.Restore();
    }

    public void Translate(float dx, float dy)
    {
        _canvas.Translate(dx, dy);
    }

    // ── Images ───────────────────────────────────────────────────

    public void DrawImage(object image, EditorRect destRect)
    {
        // Use a high-quality sampling paint so bitmaps are never blurry when the
        // source and destination sizes differ (e.g. HiDPI SVG icons drawn at small
        // logical sizes, or user-inserted images scaled to fit their column width).
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
        };

        if (image is SKImage skImage)
        {
            _canvas.DrawImage(skImage, ToSKRect(destRect), paint);
        }
        else if (image is SKBitmap skBitmap)
        {
            _canvas.DrawBitmap(skBitmap, ToSKRect(destRect), paint);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Applies <c>SKColorFilter.CreateBlendMode(tint, SrcIn)</c> so the icon alpha mask is
    /// preserved while every opaque pixel is replaced by <paramref name="tint"/>.  This
    /// lets a single black-ink rasterized SVG render correctly in any theme color.
    /// </remarks>
    public void DrawTintedImage(object image, EditorRect destRect, EditorColor tint)
    {
        using var colorFilter = SKColorFilter.CreateBlendMode(
            new SKColor(tint.R, tint.G, tint.B, tint.A),
            SKBlendMode.SrcIn);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
            ColorFilter = colorFilter,
        };

        if (image is SKImage skImage)
            _canvas.DrawImage(skImage, ToSKRect(destRect), paint);
        else if (image is SKBitmap skBitmap)
            _canvas.DrawBitmap(skBitmap, ToSKRect(destRect), paint);
    }

    // ── Caching ──────────────────────────────────────────────────

    private SKPaint GetOrCreatePaint(EditorPaint paint, SKPaintStyle style)
    {
        var key = new PaintKey(paint.Color.ToUint32(), paint.StrokeWidth, style, paint.IsAntiAlias);

        if (!_paintCache.TryGetValue(key, out var skPaint))
        {
            skPaint = new SKPaint
            {
                Color = new SKColor(paint.Color.R, paint.Color.G, paint.Color.B, paint.Color.A),
                StrokeWidth = paint.StrokeWidth,
                Style = style,
                IsAntialias = paint.IsAntiAlias,
            };
            _paintCache[key] = skPaint;
        }

        return skPaint;
    }

    private SKFont GetOrCreateFont(EditorPaint paint)
    {
        var font = paint.Font ?? new EditorFont();
        var key = new FontKey(font.Family, font.Size, font.IsBold, font.IsItalic);

        if (!_fontCache.TryGetValue(key, out var skFont))
        {
            var typeface = GetOrCreateTypeface(font);
            skFont = new SKFont(typeface, font.Size)
            {
                Edging = SKFontEdging.SubpixelAntialias,
                Subpixel = true,
            };
            _fontCache[key] = skFont;
        }

        return skFont;
    }

    private SKTypeface GetOrCreateTypeface(EditorFont font)
    {
        var key = new TypefaceKey(font.Family, font.IsBold, font.IsItalic);

        if (!_typefaceCache.TryGetValue(key, out var typeface))
        {
            var weight = font.IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var width = SKFontStyleWidth.Normal;
            var slant = font.IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

            typeface = SKTypeface.FromFamilyName(font.Family, weight, width, slant)
                       ?? SKTypeface.Default;
            _typefaceCache[key] = typeface;
        }

        return typeface;
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static SKRect ToSKRect(EditorRect rect)
        => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private static string TruncateWithEllipsis(string text, float maxWidth, SKFont font, SKPaint paint)
    {
        const string ellipsis = "...";
        float ellipsisWidth = font.MeasureText(ellipsis, out _, paint);
        float available = maxWidth - ellipsisWidth;

        if (available <= 0) return ellipsis;

        // Binary search for the maximum characters that fit
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            float w = font.MeasureText(text[..mid], out _, paint);
            if (w <= available)
                lo = mid;
            else
                hi = mid - 1;
        }

        return lo > 0 ? text[..lo] + ellipsis : ellipsis;
    }

    // ── Disposal ─────────────────────────────────────────────────

    public void Dispose()
    {
        foreach (var paint in _paintCache.Values)
            paint.Dispose();
        _paintCache.Clear();

        foreach (var font in _fontCache.Values)
            font.Dispose();
        _fontCache.Clear();

        foreach (var typeface in _typefaceCache.Values)
        {
            if (typeface != SKTypeface.Default)
                typeface.Dispose();
        }
        _typefaceCache.Clear();
    }

    // ── Cache keys ───────────────────────────────────────────────

    private readonly record struct PaintKey(uint Color, float StrokeWidth, SKPaintStyle Style, bool IsAntialias);
    private readonly record struct FontKey(string Family, float Size, bool IsBold, bool IsItalic);
    private readonly record struct TypefaceKey(string Family, bool IsBold, bool IsItalic);
}
