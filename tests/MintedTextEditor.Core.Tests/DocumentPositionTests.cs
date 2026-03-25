using MintedTextEditor.Core.Document;


namespace MintedTextEditor.Core.Tests;

public class DocumentPositionTests
{
    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new DocumentPosition(1, 2, 3);
        var b = new DocumentPosition(1, 2, 3);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var a = new DocumentPosition(1, 2, 3);
        var b = new DocumentPosition(1, 2, 4);
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void CompareTo_SameBlock_DifferentInline()
    {
        var a = new DocumentPosition(0, 0, 5);
        var b = new DocumentPosition(0, 1, 0);
        Assert.True(a < b);
        Assert.True(b > a);
    }

    [Fact]
    public void CompareTo_DifferentBlock()
    {
        var a = new DocumentPosition(0, 5, 10);
        var b = new DocumentPosition(1, 0, 0);
        Assert.True(a < b);
    }

    [Fact]
    public void CompareTo_SamePosition()
    {
        var a = new DocumentPosition(1, 2, 3);
        var b = new DocumentPosition(1, 2, 3);
        Assert.True(a <= b);
        Assert.True(a >= b);
        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        var a = new DocumentPosition(1, 2, 3);
        var b = new DocumentPosition(1, 2, 3);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var pos = new DocumentPosition(1, 2, 3);
        Assert.Equal("(1, 2, 3)", pos.ToString());
    }

    [Fact]
    public void TextRange_Normalizes_StartAndEnd()
    {
        var start = new DocumentPosition(1, 0, 0);
        var end = new DocumentPosition(0, 0, 0);
        var range = new TextRange(start, end);

        Assert.True(range.Start <= range.End);
        Assert.Equal(end, range.Start); // end is the smaller position
    }

    [Fact]
    public void TextRange_IsEmpty_WhenStartEqualsEnd()
    {
        var pos = new DocumentPosition(0, 0, 0);
        var range = new TextRange(pos, pos);
        Assert.True(range.IsEmpty);
    }

    [Fact]
    public void TextRange_IsNotEmpty_WhenDifferentPositions()
    {
        var range = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, 5));
        Assert.False(range.IsEmpty);
    }

    [Fact]
    public void TextRange_Contains_PositionInside()
    {
        var range = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, 10));

        Assert.True(range.Contains(new DocumentPosition(0, 0, 5)));
        Assert.True(range.Contains(new DocumentPosition(0, 0, 0)));
        Assert.True(range.Contains(new DocumentPosition(0, 0, 10)));
    }

    [Fact]
    public void TextRange_DoesNotContain_PositionOutside()
    {
        var range = new TextRange(
            new DocumentPosition(0, 0, 5),
            new DocumentPosition(0, 0, 10));

        Assert.False(range.Contains(new DocumentPosition(0, 0, 0)));
        Assert.False(range.Contains(new DocumentPosition(0, 0, 11)));
    }
}
