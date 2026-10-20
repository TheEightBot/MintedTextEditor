using System.Globalization;
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

    private static readonly bool HasEmojiFont =
        SKFontManager.Default.MatchCharacter(null, SKFontStyle.Normal, new[] { "und-Zsye" }, 0x1F600) is not null;

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
