using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Fluent builder for constructing <see cref="EditorDocument"/> instances in tests.
/// </summary>
internal sealed class TestDocumentBuilder
{
    private readonly EditorDocument _doc = new();
    private bool _firstParagraph = true;

    /// <summary>
    /// Appends a paragraph with the given text, optionally configuring its style.
    /// The first call reuses the default empty paragraph that every new document begins with.
    /// </summary>
    public TestDocumentBuilder Paragraph(
        string text = "",
        Action<ParagraphStyle>? configureStyle = null,
        TextStyle? textStyle = null)
    {
        Paragraph para;
        if (_firstParagraph)
        {
            // Reuse the default paragraph so Blocks[0] is always this call's paragraph.
            para = (Paragraph)_doc.Blocks[0];
            _firstParagraph = false;
        }
        else
        {
            para = new Paragraph { Parent = _doc };
            _doc.Blocks.Add(para);
        }

        if (text.Length > 0)
            para.Inlines.Add(new TextRun(text, textStyle));

        configureStyle?.Invoke(para.Style);
        return this;
    }

    /// <summary>
    /// Appends a paragraph whose single text run spans multiple style regions.
    /// Each element is a (text, style) pair.
    /// </summary>
    public TestDocumentBuilder ParagraphWithRuns(params (string Text, TextStyle? Style)[] runs)
    {
        Paragraph para;
        if (_firstParagraph)
        {
            para = (Paragraph)_doc.Blocks[0];
            _firstParagraph = false;
        }
        else
        {
            para = new Paragraph { Parent = _doc };
            _doc.Blocks.Add(para);
        }

        foreach (var (text, style) in runs)
            para.Inlines.Add(new TextRun(text, style));

        return this;
    }

    /// <summary>Returns the constructed document.</summary>
    public EditorDocument Build() => _doc;

    // ── Convenience factory ───────────────────────────────────────────────────

    /// <summary>Creates a document with a single paragraph containing <paramref name="text"/>.</summary>
    public static EditorDocument SingleParagraph(string text = "")
        => new TestDocumentBuilder().Paragraph(text).Build();

    /// <summary>Creates a document with the given lines as separate paragraphs.</summary>
    public static EditorDocument MultiParagraph(params string[] lines)
    {
        var builder = new TestDocumentBuilder();
        foreach (var line in lines)
            builder.Paragraph(line);
        return builder.Build();
    }
}
