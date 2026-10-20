using MintedTextEditor.Core.Input;

namespace MintedTextEditor.Core.Tests;

public class DeadKeyComposerTests
{
    private const int Acute = 0x0301;

    // Minimal stand-in for Android's KeyCharacterMap.getDeadChar.
    private static int Combine(int accent, int c) => (accent, c) switch
    {
        (Acute, 'e') => 'é',
        (Acute, 'a') => 'á',
        _ => 0,
    };

    private static int Dead(int accent) => accent | DeadKeyComposer.CombiningAccentFlag;

    [Fact]
    public void PlainCharacter_IsReturned()
    {
        var c = new DeadKeyComposer(Combine);
        Assert.Equal("x", c.Process('x'));
        Assert.Equal("😀", c.Process(0x1F600));
    }

    [Fact]
    public void Zero_IsNotHandled()
    {
        Assert.Null(new DeadKeyComposer(Combine).Process(0));
    }

    [Fact]
    public void DeadKey_DoesNotThrow_AndCombinesWithNext()
    {
        var c = new DeadKeyComposer(Combine);

        Assert.Equal(string.Empty, c.Process(Dead(Acute)));
        Assert.True(c.HasPendingAccent);
        Assert.Equal("é", c.Process('e'));
        Assert.False(c.HasPendingAccent);
    }

    [Fact]
    public void DeadKey_ThatDoesNotCombine_EmitsAccentThenCharacter()
    {
        var c = new DeadKeyComposer(Combine);
        c.Process(Dead(Acute));

        Assert.Equal("́x", c.Process('x'));
    }

    [Fact]
    public void TwoDeadKeys_FlushFirstAccent()
    {
        var c = new DeadKeyComposer(Combine);
        c.Process(Dead(Acute));

        Assert.Equal("́", c.Process(Dead(Acute)));
        Assert.Equal("á", c.Process('a'));
    }

    [Fact]
    public void InvalidScalar_ProducesNoText()
    {
        var c = new DeadKeyComposer(Combine);
        Assert.Equal(string.Empty, c.Process(0xD800));
        Assert.Equal(string.Empty, c.Process(0x110000));
    }
}
