using MintedTextEditor.Core.Document;


namespace MintedTextEditor.Core.Tests;

public class TextRunTests
{
    [Fact]
    public void Constructor_SetsTextAndStyle()
    {
        var style = TextStyle.Default.WithBold(true);
        var run = new TextRun("Hello", style);

        Assert.Equal("Hello", run.Text);
        Assert.Same(style, run.Style);
        Assert.Equal(5, run.Length);
    }

    [Fact]
    public void Constructor_NullText_BecomesEmpty()
    {
        var run = new TextRun(null!);
        Assert.Equal("", run.Text);
        Assert.Equal(0, run.Length);
    }

    [Fact]
    public void GetText_ReturnsText()
    {
        var run = new TextRun("test");
        Assert.Equal("test", run.GetText());
    }

    [Fact]
    public void Split_AtMiddle_ReturnsRightPart()
    {
        var style = TextStyle.Default;
        var run = new TextRun("HelloWorld", style);

        var right = run.Split(5);

        Assert.Equal("Hello", run.Text);
        Assert.Equal("World", right.Text);
        Assert.Same(style, right.Style);
    }

    [Fact]
    public void Split_AtStart_OriginalEmpty()
    {
        var run = new TextRun("Hello");
        var right = run.Split(0);

        Assert.Equal("", run.Text);
        Assert.Equal("Hello", right.Text);
    }

    [Fact]
    public void Split_AtEnd_RightPartEmpty()
    {
        var run = new TextRun("Hello");
        var right = run.Split(5);

        Assert.Equal("Hello", run.Text);
        Assert.Equal("", right.Text);
    }

    [Fact]
    public void Split_InvalidOffset_Throws()
    {
        var run = new TextRun("Hello");
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Split(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Split(6));
    }

    [Fact]
    public void Merge_SameStyle_ConcatenatesText()
    {
        var style = TextStyle.Default;
        var a = new TextRun("Hello", style);
        var b = new TextRun(" World", style);

        a.Merge(b);

        Assert.Equal("Hello World", a.Text);
    }

    [Fact]
    public void Merge_DifferentStyles_Throws()
    {
        var a = new TextRun("Hello", TextStyle.Default);
        var b = new TextRun("World", TextStyle.Default.WithBold(true));

        Assert.Throws<InvalidOperationException>(() => a.Merge(b));
    }
}
