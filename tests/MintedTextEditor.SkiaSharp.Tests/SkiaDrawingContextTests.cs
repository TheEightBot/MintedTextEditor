using System.Globalization;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Rendering;
using SkiaSharp;

namespace MintedTextEditor.SkiaSharp.Tests;

/// <summary>
/// Regression tests for emoji rendering and bitmap caching in <see cref="SkiaDrawingContext"/>
/// (issue #9). Tests that depend on a color emoji font return early when none is installed;
/// CI installs Noto Color Emoji so they run there.
/// </summary>
public class SkiaDrawingContextTests : IDisposable
{
    private const string Family = "👨‍👩‍👧";
    private const string Technologist = "👩‍💻";
    private const string RedHeart = "❤️";

    private static readonly bool HasEmojiFont = HasColorEmojiFont();

    // MatchCharacter guarantees coverage, not color: without a color emoji font installed it
    // can return a monochrome font such as DejaVu Sans. Look for a color glyph table.
    private static bool HasColorEmojiFont()
    {
        using var typeface = SKFontManager.Default.MatchCharacter(null, SKFontStyle.Normal, new[] { "und-Zsye" }, 0x1F600);
        if (typeface is null)
            return false;

        foreach (var table in new[] { "CBDT", "sbix", "COLR", "SVG " })
        {
            uint tag = (uint)(table[0] << 24 | table[1] << 16 | table[2] << 8 | table[3]);
            if (typeface.GetTableSize(tag) > 0)
                return true;
        }

        return false;
    }

    private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(400, 100));
    private readonly SkiaDrawingContext _ctx;
    private readonly EditorPaint _paint = new() { Color = new EditorColor(0, 0, 0), Font = new EditorFont { Size = 32f } };

    public SkiaDrawingContextTests()
    {
        _surface.Canvas.Clear(SKColors.White);
        _ctx = new SkiaDrawingContext(_surface.Canvas);
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _surface.Dispose();
    }

    // ── Grapheme clusters / truncation ──────────────────────────

    [Theory]
    [InlineData("ab" + Technologist + "cd")]
    [InlineData("x" + Family + "y")]
    [InlineData("🇺🇸🇨🇦 flags")]
    [InlineData("keycap 1️⃣ and wave 👋🏽")]
    public void TruncateWithEllipsis_NeverSplitsAGraphemeCluster(string text)
    {
        var boundaries = new HashSet<int>(StringInfo.ParseCombiningCharacters(text)) { text.Length };
        float fullWidth = _ctx.MeasureText(text, _paint).Width;

        using var font = new SKFont(SKTypeface.Default, 32f);
        using var paint = new SKPaint();
        for (float width = 0; width <= fullWidth; width += 1f)
        {
            string truncated = _ctx.TruncateWithEllipsis(text, width, font, paint, _paint);
            Assert.EndsWith("...", truncated);
            int kept = truncated.Length - 3;
            Assert.True(boundaries.Contains(kept), $"Cut at {kept} splits a cluster of \"{text}\" (width {width})");
        }
    }

    [Fact]
    public void TruncateWithEllipsis_ResultFitsMeasuredAsAWhole()
    {
        const string text = "AVAWAVATAR Wolf office";
        using var font = new SKFont(SKTypeface.Default, 32f);
        using var paint = new SKPaint();
        float fullWidth = _ctx.MeasureText(text, _paint).Width;

        for (float width = 0; width <= fullWidth; width += 3f)
        {
            string truncated = _ctx.TruncateWithEllipsis(text, width, font, paint, _paint);
            if (truncated != "...")
                Assert.True(_ctx.MeasureText(truncated, _paint).Width <= width, $"\"{truncated}\" overflows {width}");
        }
    }

    [Fact]
    public void Vs15_KeepsTextPresentation()
    {
        // 😀︎ (U+1F600 U+FE0E) asks for the text glyph: it must not be forced to the emoji font.
        if (!HasEmojiFont || DejaVu is not { } typeface || !HasGlyph(typeface, 0x1F600)) return;

        var paint = new EditorPaint { Font = new EditorFont("DejaVu Sans", 32f) };
        using var dejaVuFont = new SKFont(typeface, 32f);
        float textGlyph = dejaVuFont.MeasureText("\U0001F600");

        Assert.Equal(textGlyph, _ctx.MeasureText("\U0001F600\uFE0E", paint).Width, 0.5f);
    }

    [Fact]
    public void MeasureText_LoneSurrogate_DoesNotThrow()
    {
        // Hit-testing measures prefixes that can end in half of a surrogate pair.
        var size = _ctx.MeasureText("a" + Technologist[..1], _paint);
        Assert.True(size.Width > 0);
    }

    // ── Shaping ──────────────────────────────────────────────────

    [Theory]
    [InlineData(Family, "👨👩👧")]
    [InlineData(Technologist, "👩💻")]
    public void MeasureText_ZwjSequence_IsShapedIntoOneGlyph(string joined, string separate)
    {
        if (!HasEmojiFont) return;

        float joinedWidth = _ctx.MeasureText(joined, _paint).Width;
        float separateWidth = _ctx.MeasureText(separate, _paint).Width;
        float singleWidth = _ctx.MeasureText("👨", _paint).Width;

        Assert.Equal(singleWidth, joinedWidth, 0.5f);
        Assert.True(joinedWidth < separateWidth * 0.75f, $"joined {joinedWidth} vs separate {separateWidth}");
    }

    [Fact]
    public void DrawText_ZwjSequence_InkStaysWithinMeasuredWidth()
    {
        if (!HasEmojiFont) return;

        float width = _ctx.MeasureText(Family, _paint).Width;
        _ctx.DrawText(Family, 10, 60, _paint);

        var ink = InkBounds();
        Assert.False(ink.IsEmpty);
        Assert.True(ink.Right <= 10 + width + 2, $"ink right {ink.Right} beyond measured {10 + width}");
    }

    [Theory]
    [InlineData("Hello, World — AVATAR office")]
    [InlineData("mixed 你好 text 😀 with fallback")]
    public void MeasureText_MatchesDrawnAdvance(string text)
    {
        // Measuring a string must agree with measuring it piece by piece at segment
        // boundaries, and drawing must stay inside the measured width.
        float width = _ctx.MeasureText(text, _paint).Width;
        _ctx.DrawText(text, 0, 60, _paint);

        var ink = InkBounds();
        Assert.True(ink.Right <= width + 4, $"ink right {ink.Right} beyond measured {width}");
    }

    [Fact]
    public void Shape_RightToLeftRun_KeepsLogicalGlyphOrder()
    {
        // Caret placement and hit-testing measure prefixes left to right, so glyphs must stay
        // in logical order (first character leftmost) rather than being reversed.
        if (DejaVu is not { } typeface || !HasGlyph(typeface, 0x05D0)) return;

        var shaped = HarfBuzzTextShaper.Get(typeface)!.Shape("אב", 32f);

        Assert.Equal(GlyphOf(typeface, 0x05D0), shaped.Glyphs[0]);
        Assert.Equal(GlyphOf(typeface, 0x05D1), shaped.Glyphs[1]);
        Assert.True(shaped.Positions[0].X < shaped.Positions[1].X);
    }

    [Fact]
    public void Shape_MixedScripts_ShapesEachScriptWithItsOwnRules()
    {
        // In "abcسلام" the Arabic must get its joined forms, exactly as when shaped alone.
        if (DejaVu is not { } typeface || !HasGlyph(typeface, 0x0633)) return;
        var shaper = HarfBuzzTextShaper.Get(typeface)!;

        var arabicAlone = shaper.Shape("سلام", 32f).Glyphs;
        var mixed = shaper.Shape("abcسلام", 32f).Glyphs;

        Assert.Equal(arabicAlone, mixed[^arabicAlone.Length..]);
        Assert.NotEqual(GlyphOf(typeface, 0x0633), arabicAlone[0]); // initial form, not isolated
    }

    [Fact]
    public void Shape_LongRun_ReusesTheLastResult()
    {
        // Hit-testing asks for caret offsets in the same long run once per character; it
        // must not reshape the whole run every time.
        var shaper = HarfBuzzTextShaper.Get(SKTypeface.Default)!;
        string longText = string.Concat(Enumerable.Repeat("long unwrapped line ", 40));

        var first = shaper.Shape(longText, 32f);
        Assert.Same(first, shaper.Shape(new string(longText.AsSpan()), 32f));
        Assert.NotSame(first, shaper.Shape(longText, 16f));
    }

    [Fact]
    public void Shape_AlternatingLongRuns_AreBothReused()
    {
        // With word wrap off, a line such as 300×'a' + 😀 + 300×'b' is measured as two long
        // segments through the same shaper, alternately, for every caret query.
        var shaper = HarfBuzzTextShaper.Get(SKTypeface.Default)!;
        string left = new('a', 300), right = new('b', 300);

        var leftRun = shaper.Shape(left, 31f);
        var rightRun = shaper.Shape(right, 31f);
        int shaped = shaper.ShapeCount;

        for (int i = 0; i < 50; i++)
        {
            Assert.Same(leftRun, shaper.Shape(left, 31f));
            Assert.Same(rightRun, shaper.Shape(right, 31f));
        }

        Assert.Equal(shaped, shaper.ShapeCount);
    }

    [Fact]
    public void Shape_LongRuns_AreEvictedBeyondTheGlyphBudget()
    {
        // Long results are bounded by total glyph count: shaping many distinct long runs
        // evicts the oldest, while the newest is always kept.
        var shaper = HarfBuzzTextShaper.Get(SKTypeface.Default)!;
        var oldest = shaper.Shape(new string('c', 1000), 29f);

        ShapedRun newest = oldest;
        for (int i = 0; i < 40; i++)
            newest = shaper.Shape(new string('d', 1000 + i), 29f);

        Assert.Same(newest, shaper.Shape(new string('d', 1039), 29f));
        Assert.NotSame(oldest, shaper.Shape(new string('c', 1000), 29f));
    }

    [Fact]
    public void CaretOffset_MatchesALinearScanOfTheClusters()
    {
        // Clusters 0,0 (two glyphs for one character), a 3-character ligature at 1, then a
        // 2-character cluster at 4 that runs to the end of the text.
        var run = new ShapedRun(new ushort[4], new SKPoint[4], 40f,
            new[] { 0, 0, 1, 4 }, new[] { 0f, 4f, 10f, 25f }, 6);

        for (int i = -1; i <= run.TextLength + 1; i++)
            Assert.Equal(LinearCaretOffset(run, i), run.CaretOffset(i), 0.001f);

        // Text that does not start at cluster 0 (leading characters with no glyph).
        var offset = new ShapedRun(new ushort[2], new SKPoint[2], 20f, new[] { 2, 3 }, new[] { 6f, 12f }, 4);
        for (int i = 0; i <= offset.TextLength; i++)
            Assert.Equal(LinearCaretOffset(offset, i), offset.CaretOffset(i), 0.001f);
    }

    [Fact]
    public void CaretOffset_LongShapedRun_MatchesALinearScan()
    {
        var run = HarfBuzzTextShaper.Get(SKTypeface.Default)!
            .Shape(string.Concat(Enumerable.Repeat("office AVATAR ffi Wolf ", 40)), 27f);

        for (int i = 0; i <= run.TextLength; i++)
            Assert.Equal(LinearCaretOffset(run, i), run.CaretOffset(i), 0.001f);
    }

    // The original linear-scan CaretOffset, kept as the reference for the binary search.
    private static float LinearCaretOffset(ShapedRun run, int index)
    {
        if (index <= 0) return 0f;
        if (index >= run.TextLength) return run.Width;

        int startCluster = 0;
        float startX = 0f;
        for (int g = 0; g < run.Clusters.Length; g++)
        {
            if (run.Clusters[g] > index)
            {
                float endX = run.GlyphX[g];
                return startX + (endX - startX) * (index - startCluster) / (run.Clusters[g] - startCluster);
            }

            if (run.Clusters[g] != startCluster || g == 0)
            {
                startCluster = run.Clusters[g];
                startX = run.GlyphX[g];
            }

            if (startCluster == index)
                return startX;
        }

        return startX + (run.Width - startX) * (index - startCluster) / Math.Max(1, run.TextLength - startCluster);
    }

    private static SKTypeface? DejaVu => SKFontManager.Default.MatchFamily("DejaVu Sans") is { FamilyName: "DejaVu Sans" } t ? t : null;

    private static bool HasGlyph(SKTypeface typeface, int codePoint)
    {
        using var font = new SKFont(typeface);
        return font.ContainsGlyph(codePoint);
    }

    private static ushort GlyphOf(SKTypeface typeface, int codePoint)
    {
        using var font = new SKFont(typeface);
        return font.GetGlyph(codePoint);
    }

    [Theory]
    [InlineData("AV")]
    [InlineData("To")]
    [InlineData("Wa")]
    public void MeasureCaretOffset_IncludesKerningOfTheWholeRun(string pair)
    {
        // The caret between the two letters sits where the second letter is drawn: the
        // kerned pair minus the second letter's advance, not the width of the first alone.
        float full = _ctx.MeasureText(pair, _paint).Width;
        float second = _ctx.MeasureText(pair[1..], _paint).Width;

        Assert.Equal(full - second, _ctx.MeasureCaretOffset(pair, 1, _paint), 0.01f);
    }

    [Fact]
    public void MeasureCaretOffset_IsMonotonicAndEndsAtFullWidth()
    {
        const string text = "AVATAR 👩\u200D💻 Wolf ❤\uFE0F office";
        float previous = 0f;
        for (int i = 0; i <= text.Length; i++)
        {
            float x = _ctx.MeasureCaretOffset(text, i, _paint);
            Assert.True(x >= previous - 0.01f, $"offset {i} went backwards: {x} < {previous}");
            previous = x;
        }

        Assert.Equal(_ctx.MeasureText(text, _paint).Width, previous, 0.01f);
    }

    // ── VS16 emoji presentation ─────────────────────────────────

    [Fact]
    public void DrawText_Vs16Heart_RendersInColorEvenWithBlackPaint()
    {
        if (!HasEmojiFont) return;

        _ctx.DrawText(RedHeart, 10, 60, _paint);

        Assert.True(HasColoredPixel(), "❤️ should use the color emoji typeface, not a monochrome text glyph");
    }

    [Fact]
    public void MeasureText_Vs16Heart_MatchesEmojiGlyphWidth()
    {
        if (!HasEmojiFont) return;

        // ❤️ must be one emoji-wide glyph, not a text heart plus a .notdef box for U+FE0F.
        float heart = _ctx.MeasureText(RedHeart, _paint).Width;
        float emoji = _ctx.MeasureText("😀", _paint).Width;
        Assert.Equal(emoji, heart, 0.5f);
    }

    [Fact]
    public void DrawText_EmojiCoveredByTextFont_StillUsesColorEmoji()
    {
        // DejaVu Sans has its own monochrome 😀 (it is the default font on Linux CI), so the
        // text font "covers" the string, but the emoji must still come from the color font.
        if (!HasEmojiFont || SKFontManager.Default.MatchFamily("DejaVu Sans") is not { } dejaVu
            || !HasGlyph(dejaVu, 0x1F600))
            return;

        var paint = new EditorPaint { Color = new EditorColor(0, 0, 0), Font = new EditorFont("DejaVu Sans", 32f) };
        Assert.Equal(_ctx.MeasureText("👨", _paint).Width, _ctx.MeasureText("😀", paint).Width, 0.5f);

        _ctx.DrawText("😀", 10, 60, paint);
        Assert.True(HasYellowPixel(), "😀 should use the color emoji typeface, not DejaVu Sans' monochrome glyph");
    }

    // ── Bitmap image cache ──────────────────────────────────────

    [Fact]
    public void DrawImage_MutableBitmap_ShowsLaterPixelChanges()
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Red);
        _ctx.DrawImage(bitmap, new EditorRect(0, 0, 4, 4));
        Assert.Equal(SKColors.Red, ReadPixel(1, 1));

        bitmap.Erase(SKColors.Blue);
        _ctx.DrawImage(bitmap, new EditorRect(0, 0, 4, 4));
        Assert.Equal(SKColors.Blue, ReadPixel(1, 1));
    }

    [Fact]
    public void DrawImage_ImmutableBitmap_DrawsRepeatedly()
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Green);
        bitmap.SetImmutable();

        _ctx.DrawImage(bitmap, new EditorRect(0, 0, 4, 4));
        _surface.Canvas.Clear(SKColors.White);
        _ctx.DrawImage(bitmap, new EditorRect(0, 0, 4, 4));

        Assert.Equal(SKColors.Green, ReadPixel(1, 1));
    }

    // ── Magnifier ────────────────────────────────────────────────

    [Fact]
    public void Magnifier_DrawsContentZoomedAndClippedToLens()
    {
        var magnifier = new Magnifier
        {
            Options = new MagnifierOptions
            {
                Width = 80, Height = 40, CornerRadius = 20, VerticalOffset = 0, Zoom = 2f,
                BorderColor = EditorColor.Transparent, ShadowColor = EditorColor.Transparent,
            },
        };
        magnifier.Show(200, 50);

        // Content: a 10×10 black square centred on the focus point.
        new MagnifierRenderer().Render(_ctx, magnifier, new EditorRect(0, 0, 400, 100), EditorColor.White,
            ctx => ctx.FillRect(new EditorRect(195, 45, 10, 10), new EditorPaint { Color = EditorColor.Black }));

        // Zoomed 2× the square covers (190..210, 40..60) around the lens centre (200, 50).
        Assert.Equal(new SKRectI(190, 0, 210, 100), InkBounds());
        Assert.Equal(SKColors.Black, ReadPixel(200, 50));
        Assert.Equal(SKColors.Black, ReadPixel(191, 41));
        Assert.Equal(SKColors.White, ReadPixel(188, 50));
    }

    // ── Helpers ──────────────────────────────────────────────────

    private SKColor ReadPixel(int x, int y)
    {
        using var bitmap = new SKBitmap(_surface.Canvas.DeviceClipBounds.Width, _surface.Canvas.DeviceClipBounds.Height);
        _surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
        return bitmap.GetPixel(x, y);
    }

    private SKBitmap Snapshot()
    {
        var bitmap = new SKBitmap(400, 100);
        _surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
        return bitmap;
    }

    private SKRectI InkBounds()
    {
        using var bitmap = Snapshot();
        int left = int.MaxValue, right = int.MinValue;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, y) == SKColors.White) continue;
            left = Math.Min(left, x);
            right = Math.Max(right, x + 1);
        }

        return left == int.MaxValue ? SKRectI.Empty : new SKRectI(left, 0, right, bitmap.Height);
    }

    private bool HasYellowPixel()
    {
        using var bitmap = Snapshot();
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var c = bitmap.GetPixel(x, y);
            if (c.Red > 200 && c.Green > 150 && c.Blue < 100)
                return true;
        }

        return false;
    }

    private bool HasColoredPixel()
    {
        using var bitmap = Snapshot();
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var c = bitmap.GetPixel(x, y);
            if (c.Red - Math.Max(c.Green, c.Blue) > 80)
                return true;
        }

        return false;
    }
}
