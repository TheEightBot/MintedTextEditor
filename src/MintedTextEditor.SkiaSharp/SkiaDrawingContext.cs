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

    // Per-frame caches for font-fallback resolution (emoji / non-Latin glyphs the
    // run's primary typeface cannot render). Fallback typefaces returned by
    // SKFontManager.MatchCharacter are new references we own and must dispose; the
    // SKFonts built on them are tracked separately so they are disposed first.
    private readonly Dictionary<FallbackTypefaceKey, SKTypeface?> _fallbackTypefaceCache = new();
    private readonly Dictionary<FallbackFontKey, SKFont> _fallbackFontCache = new();

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
        DrawSegmented(text, x, y, skFont, skPaint, paint);
    }

    public void DrawTextInRect(string text, EditorRect rect, EditorPaint paint,
        TextAlignment hAlign = TextAlignment.Left,
        VerticalAlignment vAlign = VerticalAlignment.Center,
        bool clip = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);
        var skFont = GetOrCreateFont(paint);

        // Measure the text (font-fallback aware so emoji/non-Latin widths are correct)
        float textWidth = MeasureSegmented(text, skFont, skPaint, paint);
        var metrics = skFont.Metrics;

        // Truncate with ellipsis if needed
        string displayText = text;
        if (textWidth > rect.Width)
        {
            displayText = TruncateWithEllipsis(text, rect.Width, skFont, skPaint, paint);
            textWidth = MeasureSegmented(displayText, skFont, skPaint, paint);
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

        DrawSegmented(displayText, x, y, skFont, skPaint, paint);

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

        // Sum segment widths so font-fallback glyphs (emoji, non-Latin) are measured
        // with the same typefaces they will be drawn with — keeping layout and
        // rendering in agreement.
        float width = MeasureSegmented(text, skFont, skPaint, paint);
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
        using var paint = new SKPaint { IsAntialias = true };
        DrawImageCore(image, destRect, paint);
    }

    // SkiaSharp 3+/4 removed SKPaint.FilterQuality; sampling is now passed explicitly.
    // Mipmapped linear sampling matches the old "High" quality for downscaled bitmaps.
    private static readonly SKSamplingOptions HighQualitySampling =
        new(SKFilterMode.Linear, SKMipmapMode.Linear);

    private void DrawImageCore(object image, EditorRect destRect, SKPaint paint)
    {
        var dest = ToSKRect(destRect);
        if (image is SKImage skImage)
        {
            _canvas.DrawImage(skImage, dest, HighQualitySampling, paint);
        }
        else if (image is SKBitmap skBitmap)
        {
            // DrawBitmap has no SKSamplingOptions overload, so draw via an SKImage.
            // Cache the wrapper per bitmap: toolbar icons are redrawn every frame and
            // SKImage.FromBitmap copies pixels of mutable bitmaps. The entry is collected
            // with the bitmap (SKImage releases its native handle via its own finalizer).
            var img = BitmapImageCache.GetValue(skBitmap, static b => SKImage.FromBitmap(b));
            if (img is not null)
                _canvas.DrawImage(img, dest, HighQualitySampling, paint);
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SKBitmap, SKImage>
        BitmapImageCache = new();

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
            ColorFilter = colorFilter,
        };

        DrawImageCore(image, destRect, paint);
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
                // Synthesize the style when the family has no real bold/italic face.
                Embolden = font.IsBold && typeface.FontWeight < (int)SKFontStyleWeight.SemiBold,
                SkewX = font.IsItalic && typeface.FontSlant == SKFontStyleSlant.Upright ? -0.2f : 0f,
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

            // SkiaSharp 4: FromFamilyName with an unknown name ("Default") or null returns
            // the default face without applying the requested style, so bold/italic were
            // silently lost. Resolve the platform default family name first, then match the
            // style within that family.
            string family = ResolveFamilyName(font.Family);
            using var style = new SKFontStyle(weight, width, slant);
            typeface = SKFontManager.Default.MatchFamily(family, style)
                       ?? SKTypeface.FromFamilyName(family, weight, width, slant)
                       ?? SKTypeface.Default;
            _typefaceCache[key] = typeface;
        }

        return typeface;
    }

    // ── Font fallback ────────────────────────────────────────────
    //
    // The low-level SKCanvas.DrawText / SKFont.MeasureText APIs perform NO font
    // fallback: any code point the chosen typeface lacks (all color emoji, and any
    // script the family does not cover) maps to glyph 0 (.notdef) and renders as a
    // blank/tofu box while being mis-measured. To fix this we split each string into
    // consecutive segments that share a typeface — the run's primary typeface where it
    // has a glyph, otherwise a system fallback resolved via SKFontManager — and draw /
    // measure each segment with an SKFont built on its own typeface. A matched color-emoji
    // typeface renders color glyphs automatically and ignores the paint's color, so no
    // SKColorFilter is ever applied; the run's paint is reused unchanged for every segment.
    //
    // Segmentation is GRAPHEME-CLUSTER aware. Multi-code-point sequences such as
    //   base + U+FE0F (variation selector-16, emoji presentation)
    //   base + skin-tone modifier (U+1F3FB–U+1F3FF)
    //   ZWJ sequences (U+200D between emoji)
    //   regional-indicator letter pairs (flags)
    //   enclosing keycap (U+20E3)
    //   general Unicode combining marks (categories Mn / Me / Mc)
    // are always kept together in one cluster so no combining/modifier/selector code
    // point ever ends up in its own segment. When rendered alone such code points have
    // no glyph and produce .notdef boxes. The cluster is drawn with the base code point
    // only (variation selectors and other zero-width formatting code points are stripped
    // from the drawn string) because color emoji fonts map the color glyph to the BASE
    // code point — U+FE0F is merely a presentation hint.

    /// <summary>
    /// Draws <paramref name="text"/> starting at the baseline point
    /// (<paramref name="x"/>, <paramref name="y"/>), switching to a fallback typeface for
    /// any code point the primary font cannot render so emoji and non-Latin glyphs paint.
    /// </summary>
    private void DrawSegmented(string text, float x, float y, SKFont primaryFont, SKPaint paint, EditorPaint editorPaint)
    {
        // Fast path: the primary font covers every code point (the common case for
        // plain Latin text). Avoid all per-segment allocation and caching work.
        if (PrimaryFontCoversAll(text, primaryFont))
        {
            _canvas.DrawText(text, x, y, SKTextAlign.Left, primaryFont, paint);
            return;
        }

        float penX = x;
        foreach (var (drawString, font) in SegmentByTypeface(text, primaryFont, editorPaint))
        {
            // The run's paint is reused for every segment. Color-emoji typefaces carry their
            // own color tables and ignore the paint's color (verified: an emoji drawn with a
            // red paint still renders in its native colors), while monochrome fallback glyphs
            // (e.g. CJK from a system font) correctly honour the requested color. No
            // SKColorFilter is applied to either.
            _canvas.DrawText(drawString, penX, y, SKTextAlign.Left, font, paint);
            penX += font.MeasureText(drawString, paint);
        }
    }

    /// <summary>
    /// Measures <paramref name="text"/> using the same per-segment typeface selection as
    /// <see cref="DrawSegmented"/>, so measured widths always match what is drawn.
    /// </summary>
    private float MeasureSegmented(string text, SKFont primaryFont, SKPaint paint, EditorPaint editorPaint)
    {
        if (PrimaryFontCoversAll(text, primaryFont))
            return primaryFont.MeasureText(text, paint);

        float width = 0f;
        foreach (var (drawString, font) in SegmentByTypeface(text, primaryFont, editorPaint))
            width += font.MeasureText(drawString, paint);

        return width;
    }

    /// <summary>
    /// Returns <c>true</c> when every code point in <paramref name="text"/> has a glyph in
    /// <paramref name="primaryFont"/> (so no fallback is needed). Correctly walks surrogate
    /// pairs so astral-plane code points (most emoji) are tested as single code points.
    /// </summary>
    private static bool PrimaryFontCoversAll(string text, SKFont primaryFont)
    {
        for (int i = 0; i < text.Length;)
        {
            int codePoint = NextCodePoint(text, i, out int charCount);
            if (!primaryFont.ContainsGlyph(codePoint))
                return false;
            i += charCount;
        }

        return true;
    }

    // ── Grapheme-cluster helpers ──────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when <paramref name="codePoint"/> is a cluster-extending
    /// code point — it must always be attached to the preceding base and must never
    /// start a new segment or be drawn as a standalone glyph.
    /// </summary>
    private static bool IsClusterExtender(int codePoint)
    {
        // Variation selectors (text/emoji presentation selectors U+FE00–U+FE0F)
        if (codePoint is >= 0xFE00 and <= 0xFE0F) return true;
        // Zero-width joiner (joins emoji in ZWJ sequences like 👨‍👩‍👧)
        if (codePoint == 0x200D) return true;
        // Emoji skin-tone modifiers (Fitzpatrick scale U+1F3FB–U+1F3FF)
        if (codePoint is >= 0x1F3FB and <= 0x1F3FF) return true;
        // Combining enclosing keycap (e.g. 1️⃣ = '1' + VS16 + U+20E3)
        if (codePoint == 0x20E3) return true;
        // General Unicode combining marks (Mn = Non-spacing, Me = Enclosing, Mc = Spacing combining)
        // CharUnicodeInfo takes the scalar directly; unlike char.ConvertFromUtf32 it does not
        // throw for a lone surrogate (reachable when hit-testing measures half an emoji).
        var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(codePoint);
        return cat is System.Globalization.UnicodeCategory.NonSpacingMark
                   or System.Globalization.UnicodeCategory.EnclosingMark
                   or System.Globalization.UnicodeCategory.SpacingCombiningMark;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="codePoint"/> is a zero-width formatting
    /// code point that should be stripped from the drawn string (the underlying glyph in
    /// a color-emoji font is addressed by the base code point alone; these are hints only).
    /// </summary>
    private static bool IsZeroWidthFormatting(int codePoint)
    {
        // Variation selectors U+FE00–U+FE0F
        if (codePoint is >= 0xFE00 and <= 0xFE0F) return true;
        // Zero-width joiner
        if (codePoint == 0x200D) return true;
        // Zero-width non-joiner
        if (codePoint == 0x200C) return true;
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="codePoint"/> is a Regional Indicator letter
    /// (U+1F1E6–U+1F1FF) used to form flag emoji pairs.
    /// </summary>
    private static bool IsRegionalIndicator(int codePoint)
        => codePoint is >= 0x1F1E6 and <= 0x1F1FF;

    /// <summary>
    /// Collects a full grapheme cluster starting at <paramref name="index"/> in
    /// <paramref name="text"/> and returns the number of UTF-16 chars consumed.
    /// The cluster always contains at least one code point (the base); cluster-extending
    /// code points that immediately follow are absorbed. Regional-indicator letters are
    /// paired so flag emoji stay together.
    /// <para/>
    /// <paramref name="hasEmojiPresentation"/> is set to <c>true</c> when the cluster
    /// contains U+FE0F (VARIATION SELECTOR-16), indicating the base should be rendered
    /// with the emoji (color) typeface even if the primary font has a monochrome glyph.
    /// </summary>
    private static int ReadGraphemeCluster(string text, int index,
        out bool hasEmojiPresentation)
    {
        hasEmojiPresentation = false;
        int start = index;

        // Consume the base code point
        int baseCodePoint = NextCodePoint(text, index, out int baseCharCount);
        index += baseCharCount;

        // Regional-indicator pair: consume exactly one more RI if present
        if (IsRegionalIndicator(baseCodePoint))
        {
            if (index < text.Length)
            {
                int next = NextCodePoint(text, index, out int nextCount);
                if (IsRegionalIndicator(next))
                    index += nextCount;
            }
            return index - start;
        }

        // Consume any trailing cluster-extending code points
        while (index < text.Length)
        {
            int extender = NextCodePoint(text, index, out int extCount);
            if (!IsClusterExtender(extender))
                break;
            if (extender == 0xFE0F)
                hasEmojiPresentation = true;
            index += extCount;
        }

        return index - start;
    }

    /// <summary>
    /// Strips zero-width formatting code points (variation selectors, ZWJ, ZWNJ) from
    /// <paramref name="cluster"/> and returns the drawable string. For color-emoji fonts
    /// the color glyph is indexed by the base code point; VS16 / ZWJ are hints only and
    /// produce .notdef when drawn directly.
    /// </summary>
    private static string StripZeroWidthFormatting(string cluster)
    {
        // Fast path: scan first before allocating
        bool needsStrip = false;
        for (int i = 0; i < cluster.Length;)
        {
            int cp = NextCodePoint(cluster, i, out int cc);
            if (IsZeroWidthFormatting(cp)) { needsStrip = true; break; }
            i += cc;
        }
        if (!needsStrip) return cluster;

        var sb = new System.Text.StringBuilder(cluster.Length);
        for (int i = 0; i < cluster.Length;)
        {
            int cp = NextCodePoint(cluster, i, out int cc);
            if (!IsZeroWidthFormatting(cp))
                sb.Append(cluster, i, cc);
            i += cc;
        }
        return sb.ToString();
    }

    // ── Segmentation ─────────────────────────────────────────────

    /// <summary>
    /// Splits <paramref name="text"/> into consecutive drawable segments, each tagged with
    /// the <see cref="SKFont"/> that should render it.
    /// <para/>
    /// Segmentation is grapheme-cluster aware: combining/modifier/variation-selector code
    /// points are always grouped with their base cluster and never produce standalone
    /// segments. When a cluster contains U+FE0F (emoji presentation selector) the whole
    /// cluster is resolved against the emoji fallback typeface so color rendering is
    /// preferred even if the primary font has a monochrome glyph for the base. Zero-width
    /// formatting code points (VS, ZWJ) are stripped from the drawn string because
    /// color-emoji fonts address their color glyph via the base code point alone.
    /// </summary>
    private List<(string DrawString, SKFont Font)> SegmentByTypeface(
        string text, SKFont primaryFont, EditorPaint editorPaint)
    {
        var segments = new List<(string, SKFont)>();
        float size = primaryFont.Size;

        SKFont? currentFont = null;
        var currentDraw = new System.Text.StringBuilder();

        int i = 0;
        while (i < text.Length)
        {
            // Read a full grapheme cluster (base + any combining/modifier/selector code points)
            int clusterLen = ReadGraphemeCluster(text, i, out bool hasEmojiPresentation);
            string clusterRaw = text.Substring(i, clusterLen);

            // An unpaired surrogate (a substring that splits an emoji, e.g. during hit-testing)
            // has no glyph, and Skia rejects the whole run if it is included: skip it.
            if (char.IsSurrogate(clusterRaw[0]) && (clusterRaw.Length < 2 || !char.IsSurrogatePair(clusterRaw[0], clusterRaw[1])))
            {
                i += clusterLen;
                continue;
            }

            string clusterDraw = StripZeroWidthFormatting(clusterRaw);

            // Resolve which font to use for this cluster.
            // If the cluster carries U+FE0F we force the emoji fallback typeface (color) even
            // when the primary typeface has a monochrome glyph for the base code point.
            int baseCodePoint = NextCodePoint(clusterRaw, 0, out _);
            SKFont resolvedFont;
            if (hasEmojiPresentation)
            {
                // Force emoji (color) typeface for variation-selector clusters.
                // Fall back to primary if no emoji font is installed (e.g. CI host).
                resolvedFont = GetFallbackFont(editorPaint, baseCodePoint, size) ?? primaryFont;
            }
            else if (primaryFont.ContainsGlyph(baseCodePoint))
            {
                resolvedFont = primaryFont;
            }
            else
            {
                resolvedFont = GetFallbackFont(editorPaint, baseCodePoint, size) ?? primaryFont;
            }

            if (currentFont is null)
            {
                currentFont = resolvedFont;
                currentDraw.Append(clusterDraw);
            }
            else if (ReferenceEquals(resolvedFont, currentFont))
            {
                // Same font — accumulate into current segment
                currentDraw.Append(clusterDraw);
            }
            else
            {
                // Font changed — flush the accumulated segment
                if (currentDraw.Length > 0)
                    segments.Add((currentDraw.ToString(), currentFont));
                currentFont = resolvedFont;
                currentDraw.Clear();
                currentDraw.Append(clusterDraw);
            }

            i += clusterLen;
        }

        // Flush the final segment
        if (currentFont is not null && currentDraw.Length > 0)
            segments.Add((currentDraw.ToString(), currentFont));

        return segments;
    }

    /// <summary>
    /// Resolves (and caches) an <see cref="SKFont"/> at <paramref name="size"/> built on a
    /// system typeface that can render <paramref name="codePoint"/>, or <c>null</c> if no
    /// fallback font is installed.
    /// </summary>
    private SKFont? GetFallbackFont(EditorPaint editorPaint, int codePoint, float size)
    {
        var font = editorPaint.Font ?? new EditorFont();
        var typeface = GetFallbackTypeface(font, codePoint);
        if (typeface is null)
            return null;

        var key = new FallbackFontKey(typeface, size);
        if (!_fallbackFontCache.TryGetValue(key, out var skFont))
        {
            skFont = new SKFont(typeface, size)
            {
                Edging = SKFontEdging.SubpixelAntialias,
                Subpixel = true,
            };
            _fallbackFontCache[key] = skFont;
        }

        return skFont;
    }

    /// <summary>
    /// Resolves (and caches) a system fallback typeface that contains a glyph for
    /// <paramref name="codePoint"/>, matching the requested family's style. Returns
    /// <c>null</c> when no font on the system covers the code point. The returned
    /// typeface is owned by this context and disposed in <see cref="Dispose"/>.
    /// </summary>
    private SKTypeface? GetFallbackTypeface(EditorFont font, int codePoint)
    {
        var key = new FallbackTypefaceKey(font.Family, font.IsBold, font.IsItalic, codePoint);
        if (!_fallbackTypefaceCache.TryGetValue(key, out var typeface))
        {
            var weight = font.IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = font.IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            using var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);

            // null bcp47 lets Skia pick the best system font for the code point — this
            // returns Apple Color Emoji on macOS/iOS and Noto Color Emoji on Android.
            typeface = SKFontManager.Default.MatchCharacter(ResolveFamilyName(font.Family), style, null, codePoint);
            _fallbackTypefaceCache[key] = typeface;
        }

        return typeface;
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>Maps the editor's "Default" (or empty) family to the platform default family name.</summary>
    private static string ResolveFamilyName(string? family)
        => string.IsNullOrWhiteSpace(family) || string.Equals(family, "Default", StringComparison.OrdinalIgnoreCase)
            ? SKTypeface.Default.FamilyName
            : family;

    private static SKRect ToSKRect(EditorRect rect)
        => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    /// <summary>
    /// Reads the Unicode code point at <paramref name="index"/>, advancing by two UTF-16
    /// units for a valid surrogate pair and one otherwise. Lone/unpaired surrogates (which
    /// can occur when callers pass a substring that splits a pair) are returned as-is rather
    /// than throwing, so glyph lookup simply fails over to fallback.
    /// </summary>
    private static int NextCodePoint(string text, int index, out int charCount)
    {
        char c = text[index];
        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            charCount = 2;
            return char.ConvertToUtf32(c, text[index + 1]);
        }

        charCount = 1;
        return c;
    }

    private string TruncateWithEllipsis(string text, float maxWidth, SKFont font, SKPaint paint, EditorPaint editorPaint)
    {
        const string ellipsis = "...";
        float ellipsisWidth = font.MeasureText(ellipsis, out _, paint);
        float available = maxWidth - ellipsisWidth;

        if (available <= 0) return ellipsis;

        // Walk grapheme clusters so multi-code-point sequences (emoji + VS16, skin-tone
        // modifiers, ZWJ sequences) are never split mid-cluster, and measure with font
        // fallback so widths match what is drawn. Accumulate clusters greedily until the
        // next one would overflow.
        int cut = 0;
        float consumed = 0f;
        int i = 0;
        while (i < text.Length)
        {
            int clusterLen = ReadGraphemeCluster(text, i, out _);
            string clusterRaw = text.Substring(i, clusterLen);
            string clusterDraw = StripZeroWidthFormatting(clusterRaw);
            float w = MeasureSegmented(clusterDraw, font, paint, editorPaint);
            if (consumed + w > available)
                break;
            consumed += w;
            i += clusterLen;
            cut = i;
        }

        return cut > 0 ? text[..cut] + ellipsis : ellipsis;
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

        // Dispose fallback fonts before their typefaces. Each fallback typeface is a fresh
        // reference returned by SKFontManager.MatchCharacter that we own — except when the
        // manager hands back SKTypeface.Default, which (like the primary cache) must not be
        // disposed.
        foreach (var font in _fallbackFontCache.Values)
            font.Dispose();
        _fallbackFontCache.Clear();

        // Several code points routinely resolve to the SAME fallback typeface instance
        // (every emoji maps to one color-emoji font), so dedupe by reference identity to
        // avoid double-disposing.
        var disposedFallbacks = new HashSet<SKTypeface>(ReferenceEqualityComparer.Instance);
        foreach (var typeface in _fallbackTypefaceCache.Values)
        {
            if (typeface is not null && typeface != SKTypeface.Default && disposedFallbacks.Add(typeface))
                typeface.Dispose();
        }
        _fallbackTypefaceCache.Clear();
    }

    // ── Cache keys ───────────────────────────────────────────────

    private readonly record struct PaintKey(uint Color, float StrokeWidth, SKPaintStyle Style, bool IsAntialias);
    private readonly record struct FontKey(string Family, float Size, bool IsBold, bool IsItalic);
    private readonly record struct TypefaceKey(string Family, bool IsBold, bool IsItalic);
    private readonly record struct FallbackTypefaceKey(string Family, bool IsBold, bool IsItalic, int CodePoint);
    private readonly record struct FallbackFontKey(SKTypeface Typeface, float Size);
}
