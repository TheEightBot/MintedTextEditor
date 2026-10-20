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
    private SKTypeface? _emojiTypeface;
    private bool _emojiTypefaceResolved;

    // Shapers are shared process-wide (see HarfBuzzTextShaper); this only saves the
    // lookup for fonts already seen this frame.
    private readonly Dictionary<SKFont, HarfBuzzTextShaper?> _shaperCache = new();

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

    /// <inheritdoc />
    /// <remarks>
    /// Measured within the whole shaped text, the same way it is drawn, so the caret and
    /// hit-testing include the kerning and contextual forms between neighbours.
    /// </remarks>
    public float MeasureCaretOffset(string text, int index, EditorPaint paint)
    {
        if (string.IsNullOrEmpty(text) || index <= 0) return 0f;
        if (index >= text.Length) return MeasureText(text, paint).Width;

        var skFont = GetOrCreateFont(paint);
        var skPaint = GetOrCreatePaint(paint, SKPaintStyle.Fill);

        if (PrimaryFontCoversAll(text, skFont))
            return CaretOffsetInRun(text, index, skFont, skPaint, ShapeAllText);

        float x = 0f;
        foreach (var (start, runText, font) in SegmentByTypeface(text, skFont, paint))
        {
            if (index <= start)
                return x;

            bool shape = ShapeAllText || !ReferenceEquals(font, skFont);
            if (index < start + runText.Length)
                return x + CaretOffsetInRun(runText, index - start, font, skPaint, shape);

            x += MeasureRun(runText, font, skPaint, shape);
        }

        return x;
    }

    private float CaretOffsetInRun(string text, int index, SKFont font, SKPaint paint, bool shape)
    {
        var shaped = shape ? Shape(text, font) : null;
        return shaped?.CaretOffset(index) ?? font.MeasureText(StripZeroWidthFormatting(text[..index]), paint);
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

    /// <inheritdoc />
    public void Scale(float sx, float sy)
    {
        _canvas.Scale(sx, sy);
    }

    /// <inheritdoc />
    public void ClipRoundRect(EditorRect rect, float cornerRadius)
    {
        using var roundRect = new SKRoundRect(ToSKRect(rect), cornerRadius);
        _canvas.ClipRoundRect(roundRect, antialias: true);
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
            // SKImage.FromBitmap copies the pixels of a mutable bitmap, so a cached image
            // would never show later changes to it. Only immutable bitmaps (toolbar icons
            // and loaded images, redrawn every frame) are cached; the entry is collected with
            // the bitmap (SKImage releases its native handle via its own finalizer). A
            // mutable bitmap gets a fresh snapshot on every draw.
            if (skBitmap.IsImmutable)
            {
                var img = BitmapImageCache.GetValue(skBitmap, static b => SKImage.FromBitmap(b));
                if (img is not null)
                    _canvas.DrawImage(img, dest, HighQualitySampling, paint);
            }
            else
            {
                using var img = SKImage.FromBitmap(skBitmap);
                if (img is not null)
                    _canvas.DrawImage(img, dest, HighQualitySampling, paint);
            }
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

    // ── Font fallback & shaping ──────────────────────────────────
    //
    // The low-level SKCanvas.DrawText / SKFont.MeasureText APIs perform NO font
    // fallback: any code point the chosen typeface lacks (all color emoji, and any
    // script the family does not cover) maps to glyph 0 (.notdef) and renders as a
    // blank/tofu box while being mis-measured. To fix this we split each string into
    // consecutive segments that share a typeface — the run's primary typeface where it
    // covers the cluster, otherwise a system fallback resolved via SKFontManager — and
    // draw / measure each segment with an SKFont built on its own typeface. A matched
    // color-emoji typeface renders color glyphs automatically and ignores the paint's
    // color, so no SKColorFilter is ever applied; the run's paint is reused unchanged.
    //
    // Segmentation works on extended grapheme clusters (StringInfo text elements), so
    // emoji ZWJ sequences (👩‍💻), skin-tone and VS16 sequences, flags, keycaps and
    // combining marks always stay in one segment with one typeface.
    //
    // Segments are then SHAPED with HarfBuzz rather than drawn code point by code point:
    // shaping applies the font's own substitution rules, which is what joins a ZWJ
    // sequence into a single glyph and selects the color glyph for base + U+FE0F. The
    // same shaped glyphs and advances are used for drawing and measuring, so layout and
    // rendering always agree.

    /// <summary>
    /// When <c>true</c> (the default) all text is shaped with HarfBuzz, giving kerning,
    /// ligatures and complex-script shaping. When <c>false</c>, only segments drawn with a
    /// fallback typeface (emoji and scripts the run's font cannot render) are shaped and
    /// everything else uses Skia's faster unshaped text drawing. Changing this affects
    /// text measurement, so set it once at startup, before any layout happens.
    /// </summary>
    public static bool ShapeAllText { get; set; } = true;

    /// <summary>
    /// Draws <paramref name="text"/> starting at the baseline point
    /// (<paramref name="x"/>, <paramref name="y"/>), switching to a fallback typeface for
    /// any cluster the primary font cannot render so emoji and non-Latin glyphs paint.
    /// </summary>
    private void DrawSegmented(string text, float x, float y, SKFont primaryFont, SKPaint paint, EditorPaint editorPaint)
    {
        // Fast path: the primary font covers every code point (the common case for
        // plain Latin text). Avoid all per-segment allocation and caching work.
        if (PrimaryFontCoversAll(text, primaryFont))
        {
            DrawRun(text, x, y, primaryFont, paint, ShapeAllText);
            return;
        }

        float penX = x;
        foreach (var (_, runText, font) in SegmentByTypeface(text, primaryFont, editorPaint))
        {
            // The run's paint is reused for every segment. Color-emoji typefaces carry their
            // own color tables and ignore the paint's color, while monochrome fallback glyphs
            // (e.g. CJK from a system font) correctly honour the requested color.
            penX += DrawRun(runText, penX, y, font, paint, ShapeAllText || !ReferenceEquals(font, primaryFont));
        }
    }

    /// <summary>
    /// Measures <paramref name="text"/> using the same per-segment typeface selection and
    /// shaping as <see cref="DrawSegmented"/>, so measured widths always match what is drawn.
    /// </summary>
    private float MeasureSegmented(string text, SKFont primaryFont, SKPaint paint, EditorPaint editorPaint)
    {
        if (PrimaryFontCoversAll(text, primaryFont))
            return MeasureRun(text, primaryFont, paint, ShapeAllText);

        float width = 0f;
        foreach (var (_, runText, font) in SegmentByTypeface(text, primaryFont, editorPaint))
            width += MeasureRun(runText, font, paint, ShapeAllText || !ReferenceEquals(font, primaryFont));

        return width;
    }

    /// <summary>
    /// Draws a single-typeface run at the baseline point and returns its advance width.
    /// </summary>
    private float DrawRun(string text, float x, float y, SKFont font, SKPaint paint, bool shape)
    {
        var shaped = shape ? Shape(text, font) : null;
        if (shaped is null)
        {
            string drawable = StripZeroWidthFormatting(text);
            _canvas.DrawText(drawable, x, y, SKTextAlign.Left, font, paint);
            return font.MeasureText(drawable, paint);
        }

        if (shaped.Glyphs.Length > 0)
        {
            using var builder = new SKTextBlobBuilder();
            builder.AddPositionedRun(shaped.Glyphs, font, shaped.Positions);
            using var blob = builder.Build();
            if (blob is not null)
                _canvas.DrawText(blob, x, y, paint);
        }

        return shaped.Width;
    }

    /// <summary>Measures a single-typeface run exactly as <see cref="DrawRun"/> draws it.</summary>
    private float MeasureRun(string text, SKFont font, SKPaint paint, bool shape)
    {
        var shaped = shape ? Shape(text, font) : null;
        return shaped?.Width ?? font.MeasureText(StripZeroWidthFormatting(text), paint);
    }

    /// <summary>
    /// Shapes <paramref name="text"/> with <paramref name="font"/>'s typeface, or returns
    /// <c>null</c> when no shaper is available for it (the caller then draws unshaped).
    /// </summary>
    private ShapedRun? Shape(string text, SKFont font)
    {
        if (!_shaperCache.TryGetValue(font, out var shaper))
        {
            shaper = HarfBuzzTextShaper.Get(font.Typeface);
            _shaperCache[font] = shaper;
        }

        return shaper?.Shape(text, font.Size);
    }

    /// <summary>
    /// Returns <c>true</c> when every code point in <paramref name="text"/> has a glyph in
    /// <paramref name="primaryFont"/> (so no fallback is needed) and nothing requests emoji
    /// presentation. Correctly walks surrogate pairs so astral-plane code points (most
    /// emoji) are tested as single code points.
    /// </summary>
    private static bool PrimaryFontCoversAll(string text, SKFont primaryFont)
    {
        for (int i = 0; i < text.Length;)
        {
            int codePoint = NextCodePoint(text, i, out int charCount);
            // Emoji go to the color emoji font even where the text font has its own
            // monochrome glyph (❤️, or 😀 in DejaVu Sans) — that needs the segmented path.
            if (PrefersEmojiPresentation(codePoint) || !primaryFont.ContainsGlyph(codePoint))
                return false;
            i += charCount;
        }

        return true;
    }

    // ── Grapheme-cluster helpers ──────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when <paramref name="codePoint"/> is a zero-width formatting
    /// code point (variation selector, ZWJ, ZWNJ). Shaping handles these; the unshaped
    /// fallback strips them because drawn directly they produce .notdef boxes.
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
    /// Returns the length in UTF-16 chars of the extended grapheme cluster starting at
    /// <paramref name="index"/> (at least 1). Uses .NET's UAX #29 implementation, so ZWJ
    /// sequences such as 👩‍💻 or 👨‍👩‍👧, flags, keycaps, skin-tone and VS16 sequences and
    /// combining marks are each returned whole.
    /// <para/>
    /// <paramref name="prefersEmoji"/> is set when the cluster should be drawn with the color
    /// emoji typeface even if another font has a (monochrome) glyph for it — fonts such as
    /// DejaVu Sans carry their own 😀 — because it contains a code point for which
    /// <see cref="PrefersEmojiPresentation"/> is true.
    /// </summary>
    private static int ReadGraphemeCluster(string text, int index, out bool prefersEmoji)
    {
        int length = Math.Max(1, System.Globalization.StringInfo.GetNextTextElementLength(text, index));

        prefersEmoji = false;
        for (int i = index; i < index + length;)
        {
            int codePoint = NextCodePoint(text, i, out int charCount);
            // VS15 explicitly asks for text presentation (🖥︎): never force the emoji font.
            if (codePoint == 0xFE0E)
            {
                prefersEmoji = false;
                break;
            }
            prefersEmoji |= PrefersEmojiPresentation(codePoint);
            i += charCount;
        }

        return length;
    }

    /// <summary>
    /// Returns <c>true</c> for a code point that makes its cluster an emoji: one in the emoji
    /// planes (U+1F000–U+1FAFF, default emoji presentation; this includes flags and skin-tone
    /// modifiers), VS16 (U+FE0F), ZWJ (U+200D) or the keycap mark (U+20E3).
    /// </summary>
    private static bool PrefersEmojiPresentation(int codePoint)
        => codePoint is (>= 0x1F000 and <= 0x1FAFF) or 0xFE0F or 0x200D or 0x20E3;

    /// <summary>
    /// Returns <c>true</c> when <paramref name="font"/> has a glyph for every code point in
    /// <paramref name="cluster"/>, ignoring zero-width formatting code points.
    /// </summary>
    private static bool FontCoversCluster(SKFont font, string cluster)
    {
        for (int i = 0; i < cluster.Length;)
        {
            int codePoint = NextCodePoint(cluster, i, out int charCount);
            if (!IsZeroWidthFormatting(codePoint) && !font.ContainsGlyph(codePoint))
                return false;
            i += charCount;
        }

        return true;
    }

    /// <summary>
    /// Strips zero-width formatting code points (variation selectors, ZWJ, ZWNJ) from
    /// <paramref name="text"/> for unshaped drawing. Without shaping, a color-emoji font
    /// only maps its glyph from the base code point; VS16 / ZWJ drawn directly produce
    /// .notdef boxes.
    /// </summary>
    private static string StripZeroWidthFormatting(string text)
    {
        // Fast path: scan first before allocating
        bool needsStrip = false;
        for (int i = 0; i < text.Length;)
        {
            int cp = NextCodePoint(text, i, out int cc);
            if (IsZeroWidthFormatting(cp)) { needsStrip = true; break; }
            i += cc;
        }
        if (!needsStrip) return text;

        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length;)
        {
            int cp = NextCodePoint(text, i, out int cc);
            if (!IsZeroWidthFormatting(cp))
                sb.Append(text, i, cc);
            i += cc;
        }
        return sb.ToString();
    }

    // ── Segmentation ─────────────────────────────────────────────

    /// <summary>
    /// Splits <paramref name="text"/> into consecutive segments, each tagged with the
    /// <see cref="SKFont"/> that should render it. Segment text is left intact (joiners
    /// and selectors included) so it can be shaped.
    /// <para/>
    /// Each grapheme cluster picks its typeface in this order: the color emoji typeface for
    /// emoji clusters (see <see cref="ReadGraphemeCluster"/>), the primary typeface when it
    /// covers the whole cluster, then a system fallback for the cluster's base code point.
    /// </summary>
    private List<(int Start, string Text, SKFont Font)> SegmentByTypeface(
        string text, SKFont primaryFont, EditorPaint editorPaint)
    {
        var segments = new List<(int, string, SKFont)>();
        float size = primaryFont.Size;

        SKFont? currentFont = null;
        int segmentStart = 0;

        int i = 0;
        while (i < text.Length)
        {
            int clusterLen = ReadGraphemeCluster(text, i, out bool prefersEmoji);

            // An unpaired surrogate (a substring that splits an emoji, e.g. during hit-testing)
            // has no glyph, and Skia rejects the whole run if it is included: skip it.
            if (char.IsSurrogate(text[i]) && (i + 1 >= text.Length || !char.IsSurrogatePair(text[i], text[i + 1])))
            {
                if (currentFont is not null && i > segmentStart)
                    segments.Add((segmentStart, text[segmentStart..i], currentFont));
                currentFont = null;
                i += clusterLen;
                segmentStart = i;
                continue;
            }

            string cluster = text.Substring(i, clusterLen);
            SKFont resolvedFont = ResolveClusterFont(cluster, prefersEmoji, primaryFont, editorPaint, size);

            if (currentFont is null)
            {
                currentFont = resolvedFont;
                segmentStart = i;
            }
            else if (!ReferenceEquals(resolvedFont, currentFont))
            {
                // Font changed — flush the accumulated segment
                segments.Add((segmentStart, text[segmentStart..i], currentFont));
                currentFont = resolvedFont;
                segmentStart = i;
            }

            i += clusterLen;
        }

        // Flush the final segment
        if (currentFont is not null && text.Length > segmentStart)
            segments.Add((segmentStart, text[segmentStart..], currentFont));

        return segments;
    }

    private SKFont ResolveClusterFont(string cluster, bool prefersEmoji, SKFont primaryFont,
        EditorPaint editorPaint, float size)
    {
        // Emoji clusters go to the color emoji typeface even when the primary font has a
        // monochrome glyph for the base (❤️), so VS16 really selects emoji presentation.
        if (prefersEmoji && GetEmojiFont(size) is { } emojiFont && FontCoversCluster(emojiFont, cluster))
            return emojiFont;

        if (FontCoversCluster(primaryFont, cluster))
            return primaryFont;

        int baseCodePoint = NextCodePoint(cluster, 0, out _);
        return GetFallbackFont(editorPaint, baseCodePoint, size) ?? primaryFont;
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
        return typeface is null ? null : GetFallbackFont(typeface, size);
    }

    private SKFont GetFallbackFont(SKTypeface typeface, float size)
    {
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
    /// Returns an <see cref="SKFont"/> at <paramref name="size"/> on the system's color emoji
    /// typeface, or <c>null</c> when none is installed.
    /// </summary>
    private SKFont? GetEmojiFont(float size)
    {
        if (!_emojiTypefaceResolved)
        {
            // Ask for the font that renders 😀 (which no text font covers) with the emoji
            // script tag "und-Zsye". This finds Apple Color Emoji, Noto Color Emoji or Segoe
            // UI Emoji without hard-coding family names, on every platform's font manager.
            _emojiTypeface = SKFontManager.Default.MatchCharacter(null, SKFontStyle.Normal, EmojiLanguageTags, 0x1F600);
            _emojiTypefaceResolved = true;
        }

        return _emojiTypeface is null ? null : GetFallbackFont(_emojiTypeface, size);
    }

    private static readonly string[] EmojiLanguageTags = { "und-Zsye" };

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

    internal string TruncateWithEllipsis(string text, float maxWidth, SKFont font, SKPaint paint, EditorPaint editorPaint)
    {
        const string ellipsis = "...";

        // Cut only at grapheme-cluster boundaries so multi-code-point sequences (emoji +
        // VS16, skin-tone modifiers, ZWJ sequences) are never split, and measure each
        // candidate exactly as it will be drawn: shaped widths are not additive (kerning,
        // contextual forms), so the whole prefix plus the ellipsis is measured together.
        string result = ellipsis;
        int i = 0;
        while (i < text.Length)
        {
            i += ReadGraphemeCluster(text, i, out _);
            string candidate = text[..i] + ellipsis;
            if (MeasureSegmented(candidate, font, paint, editorPaint) > maxWidth)
                break;
            result = candidate;
        }

        return result;
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
        _shaperCache.Clear();

        // Several code points routinely resolve to the SAME fallback typeface instance
        // (every emoji maps to one color-emoji font, which is also the emoji typeface), so
        // dedupe by reference identity to avoid double-disposing.
        var disposedFallbacks = new HashSet<SKTypeface>(ReferenceEqualityComparer.Instance);
        foreach (var typeface in _fallbackTypefaceCache.Values.Append(_emojiTypeface))
        {
            if (typeface is not null && typeface != SKTypeface.Default && disposedFallbacks.Add(typeface))
                typeface.Dispose();
        }
        _fallbackTypefaceCache.Clear();
        _emojiTypeface = null;
        _emojiTypefaceResolved = false;
    }

    // ── Cache keys ───────────────────────────────────────────────

    private readonly record struct PaintKey(uint Color, float StrokeWidth, SKPaintStyle Style, bool IsAntialias);
    private readonly record struct FontKey(string Family, float Size, bool IsBold, bool IsItalic);
    private readonly record struct TypefaceKey(string Family, bool IsBold, bool IsItalic);
    private readonly record struct FallbackTypefaceKey(string Family, bool IsBold, bool IsItalic, int CodePoint);
    private readonly record struct FallbackFontKey(SKTypeface Typeface, float Size);
}
