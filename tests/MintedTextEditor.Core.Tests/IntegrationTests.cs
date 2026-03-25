using System.Diagnostics;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Html;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Integration tests that exercise multiple subsystems together
/// and performance baselines for the layout engine.
/// </summary>
public class IntegrationTests
{
    private readonly MockDrawingContext _context = new();
    private readonly TextLayoutEngine _engine = new();

    // ── Full editing flow ─────────────────────────────────────────────────────

    [Fact]
    public void FullFlow_TypeFormatUndoRedoExportImport_RoundTrips()
    {
        // ── 1. Build initial document via TestDocumentBuilder ─────────────────
        var doc = TestDocumentBuilder.SingleParagraph("Hello world");
        _engine.Layout(doc, 800f, _context);

        // ── 2. Apply bold to "Hello" (chars 0..5) via UndoManager ─────────────
        //    UndoManager.Push() calls Execute() internally — no separate apply needed.
        var boldRange = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, 5));

        var undo = new UndoManager();
        var applyAction = new ApplyStyleAction(doc, boldRange, s => s.WithBold(true));
        undo.Push(applyAction);
        Assert.True(undo.CanUndo);

        var para = (Paragraph)doc.Blocks[0];
        Assert.True(((TextRun)para.Inlines[0]).Style.IsBold,
            "First run should be bold after Push");

        // ── 3. Undo ───────────────────────────────────────────────────────────
        undo.Undo();
        var runAfterUndo = (TextRun)((Paragraph)doc.Blocks[0]).Inlines[0];
        Assert.False(runAfterUndo.Style.IsBold, "Bold should be removed after undo");
        Assert.True(undo.CanRedo);

        // ── 4. Redo ───────────────────────────────────────────────────────────
        undo.Redo();
        var runAfterRedo = (TextRun)((Paragraph)doc.Blocks[0]).Inlines[0];
        Assert.True(runAfterRedo.Style.IsBold, "Bold should be restored after redo");

        // ── 5. Export to HTML ─────────────────────────────────────────────────
        var html = new HtmlExporter().Export(doc);
        Assert.Contains("<strong>", html);
        Assert.Contains("Hello", html);

        // ── 6. Import HTML back into a new document ───────────────────────────
        var reimported = new HtmlImporter().Import(html);

        // ── 7. Compare key properties ─────────────────────────────────────────
        Assert.Equal(doc.Blocks.Count, reimported.Blocks.Count);
        var reimportedPara = (Paragraph)reimported.Blocks[0];
        var boldRun = reimportedPara.Inlines.OfType<TextRun>().FirstOrDefault(r => r.Style.IsBold);
        Assert.NotNull(boldRun);
        Assert.Contains("Hello", boldRun.Text);
    }

    [Fact]
    public void FullFlow_MultiParagraph_ExportImport_PreservesStructure()
    {
        var doc = TestDocumentBuilder.MultiParagraph(
            "First paragraph",
            "Second paragraph",
            "Third paragraph");

        var html = new HtmlExporter().Export(doc);
        var reimported = new HtmlImporter().Import(html);

        Assert.Equal(3, reimported.Blocks.Count);
        Assert.Equal("First paragraph",
            string.Concat(((Paragraph)reimported.Blocks[0]).Inlines.OfType<TextRun>().Select(r => r.Text)));
        Assert.Equal("Second paragraph",
            string.Concat(((Paragraph)reimported.Blocks[1]).Inlines.OfType<TextRun>().Select(r => r.Text)));
        Assert.Equal("Third paragraph",
            string.Concat(((Paragraph)reimported.Blocks[2]).Inlines.OfType<TextRun>().Select(r => r.Text)));
    }

    [Fact]
    public void FullFlow_InsertAndDelete_ViaUndoManager_Roundtrips()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();
        var pos = new DocumentPosition(0, 0, 0);

        // Push executes the action and adds to undo stack
        undo.Push(new InsertTextAction(doc, pos, "Hello"));

        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal("Hello", ((TextRun)para.Inlines[0]).Text);

        // Undo insert → text should disappear
        undo.Undo();
        para = (Paragraph)doc.Blocks[0];
        var text = string.Concat(para.Inlines.OfType<TextRun>().Select(r => r.Text));
        Assert.Equal("", text);

        // Redo insert → text should reappear
        undo.Redo();
        para = (Paragraph)doc.Blocks[0];
        Assert.Equal("Hello", ((TextRun)para.Inlines[0]).Text);
    }

    // ── TestDocumentBuilder tests ─────────────────────────────────────────────

    [Fact]
    public void TestDocumentBuilder_SingleParagraph_HasOneBlock()
    {
        var doc = TestDocumentBuilder.SingleParagraph("Hello");
        Assert.Single(doc.Blocks);
        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal("Hello", ((TextRun)para.Inlines[0]).Text);
    }

    [Fact]
    public void TestDocumentBuilder_MultiParagraph_HasCorrectBlockCount()
    {
        var doc = TestDocumentBuilder.MultiParagraph("A", "B", "C");
        Assert.Equal(3, doc.Blocks.Count);
    }

    [Fact]
    public void TestDocumentBuilder_Paragraph_StyleConfiguration_IsApplied()
    {
        var doc = new TestDocumentBuilder()
            .Paragraph("Heading", configureStyle: s => s.HeadingLevel = 1)
            .Build();

        var style = ((Paragraph)doc.Blocks[0]).Style;
        Assert.Equal(1, style.HeadingLevel);
    }

    [Fact]
    public void TestDocumentBuilder_ParagraphWithRuns_MultipleRunsInlined()
    {
        var doc = new TestDocumentBuilder()
            .ParagraphWithRuns(
                ("Bold text", new TextStyle(isBold: true)),
                (" normal text", null))
            .Build();

        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal(2, para.Inlines.Count);
        Assert.True(((TextRun)para.Inlines[0]).Style.IsBold);
        Assert.False(((TextRun)para.Inlines[1]).Style.IsBold);
    }

    // ── Performance baseline ──────────────────────────────────────────────────

    [Fact]
    public void Performance_LargeDocument_LayoutCompletes_within_5_Seconds()
    {
        // Build a document with 10,000 paragraphs
        const int paragraphCount = 10_000;
        var builder = new TestDocumentBuilder();
        for (int i = 0; i < paragraphCount; i++)
            builder.Paragraph($"Paragraph {i}: The quick brown fox jumps over the lazy dog.");
        var doc = builder.Build();

        Assert.Equal(paragraphCount, doc.Blocks.Count);

        var sw = Stopwatch.StartNew();
        var layout = _engine.Layout(doc, 800f, _context);
        sw.Stop();

        Assert.Equal(paragraphCount, layout.Blocks.Count);
        Assert.True(sw.Elapsed.TotalSeconds < 5.0,
            $"Layout of {paragraphCount} paragraphs took {sw.Elapsed.TotalSeconds:F2}s (limit: 5s)");
    }

    [Fact]
    public void Performance_LargeDocument_RelayoutAfterSingleEdit_IsFast()
    {
        // Build medium document
        const int paragraphCount = 1_000;
        var builder = new TestDocumentBuilder();
        for (int i = 0; i < paragraphCount; i++)
            builder.Paragraph($"Line {i}: some text content here.");
        var doc = builder.Build();

        // Initial layout
        _engine.Layout(doc, 800f, _context);

        // Single-paragraph edit: modify the last paragraph
        var lastPara = (Paragraph)doc.Blocks[^1];
        lastPara.Inlines.Clear();
        lastPara.Inlines.Add(new TextRun("Updated content."));

        var sw = Stopwatch.StartNew();
        var relayout = _engine.Layout(doc, 800f, _context);
        sw.Stop();

        Assert.Equal(paragraphCount, relayout.Blocks.Count);
        Assert.True(sw.Elapsed.TotalSeconds < 3.0,
            $"Relayout took {sw.Elapsed.TotalSeconds:F2}s (limit: 3s)");
    }
}
