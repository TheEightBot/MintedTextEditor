namespace MintedTextEditor.Core.Tests;

public class MintedTextEditorInfoTests
{
    [Fact]
    public void Version_ReturnsNonEmptyString()
    {
        var version = MintedTextEditorInfo.Version;

        Assert.False(string.IsNullOrWhiteSpace(version));
    }
}
