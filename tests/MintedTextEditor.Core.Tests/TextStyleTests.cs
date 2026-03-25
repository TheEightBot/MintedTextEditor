using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Rendering;


namespace MintedTextEditor.Core.Tests;

public class TextStyleTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var style = TextStyle.Default;

        Assert.Equal("Default", style.FontFamily);
        Assert.Equal(14f, style.FontSize);
        Assert.False(style.IsBold);
        Assert.False(style.IsItalic);
        Assert.False(style.IsUnderline);
        Assert.False(style.IsStrikethrough);
        Assert.Equal(EditorColor.Black, style.TextColor);
        Assert.Equal(EditorColor.Transparent, style.HighlightColor);
    }

    [Fact]
    public void WithBold_CreatesNewInstance()
    {
        var original = TextStyle.Default;
        var bold = original.WithBold(true);

        Assert.False(original.IsBold);
        Assert.True(bold.IsBold);
        Assert.NotSame(original, bold);
    }

    [Fact]
    public void WithItalic_PreservesOtherProperties()
    {
        var bold = TextStyle.Default.WithBold(true);
        var boldItalic = bold.WithItalic(true);

        Assert.True(boldItalic.IsBold);
        Assert.True(boldItalic.IsItalic);
    }

    [Fact]
    public void WithFontFamily_SetsFamily()
    {
        var style = TextStyle.Default.WithFontFamily("Arial");
        Assert.Equal("Arial", style.FontFamily);
    }

    [Fact]
    public void WithFontSize_SetsSize()
    {
        var style = TextStyle.Default.WithFontSize(24f);
        Assert.Equal(24f, style.FontSize);
    }

    [Fact]
    public void WithTextColor_SetsColor()
    {
        var style = TextStyle.Default.WithTextColor(EditorColor.Red);
        Assert.Equal(EditorColor.Red, style.TextColor);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new TextStyle(fontFamily: "Arial", fontSize: 12, isBold: true);
        var b = new TextStyle(fontFamily: "Arial", fontSize: 12, isBold: true);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = TextStyle.Default;
        var b = TextStyle.Default.WithBold(true);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equality_NullCheck()
    {
        var style = TextStyle.Default;
        Assert.False(style.Equals(null));
        Assert.False(style == null);
        Assert.True(style != null);
    }

    [Fact]
    public void Immutability_OriginalUnchanged()
    {
        var original = TextStyle.Default;
        _ = original.WithBold(true);
        _ = original.WithItalic(true);
        _ = original.WithUnderline(true);
        _ = original.WithStrikethrough(true);
        _ = original.WithTextColor(EditorColor.Red);
        _ = original.WithHighlightColor(EditorColor.Yellow);
        _ = original.WithFontFamily("Arial");
        _ = original.WithFontSize(24f);

        // Original should be completely unchanged
        Assert.Equal(TextStyle.Default, original);
    }
}
