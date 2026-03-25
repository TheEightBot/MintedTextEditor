using MintedTextEditor.Core.Events;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Tests;

public class EditorEventsTests
{
    [Fact]
    public void HyperlinkClickedEventArgs_Cancel_DefaultsToFalse_AndCanBeSet()
    {
        var args = new HyperlinkClickedEventArgs("https://example.com");

        Assert.False(args.Cancel);
        args.Cancel = true;
        Assert.True(args.Cancel);
    }

    [Fact]
    public void ImageEngine_InsertImage_FiresDocumentChanged()
    {
        var doc = new EditorDocument();
        DocumentChangedEventArgs? received = null;
        doc.Changed += (_, e) => received = e;

        ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png");

        Assert.NotNull(received);
        Assert.Equal(DocumentChangeType.TextInserted, received!.ChangeType);
    }

    [Fact]
    public void HyperlinkEngine_InsertHyperlink_FiresDocumentChanged()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("example");
        DocumentChangedEventArgs? received = null;
        doc.Changed += (_, e) => received = e;

        var range = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 7));
        HyperlinkEngine.InsertHyperlink(doc, range, "https://example.com");

        Assert.NotNull(received);
        Assert.Equal(DocumentChangeType.TextInserted, received!.ChangeType);
    }
}
