using System.Text;
using MintedTextEditor.Core.Document;
using EditorDoc = MintedTextEditor.Core.Document.Document;

namespace MintedTextEditor.Core.Markdown;

/// <summary>
/// Convenience methods for exporting documents to Markdown and importing Markdown into documents.
/// </summary>
public static class MarkdownDocumentExtensions
{
    /// <summary>Exports the entire document as a Markdown string.</summary>
    public static string GetMarkdown(this EditorDoc doc, MarkdownExportOptions? options = null)
        => new MarkdownExporter(options).Export(doc);

    /// <summary>
    /// Appends Markdown content at the end of <paramref name="doc"/>.
    /// The imported blocks are added after the last existing block.
    /// </summary>
    public static void AppendMarkdown(this EditorDoc doc, string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return;
        var imported = new MarkdownImporter().Import(markdown);
        // Remove the single empty seed paragraph created by the Document ctor if present
        if (doc.Blocks.Count == 1 && doc.Blocks[0] is Paragraph { Length: 0 })
            doc.Blocks.Clear();
        foreach (var block in imported.Blocks)
        {
            block.Parent = doc;
            doc.Blocks.Add(block);
        }
        if (doc.Blocks.Count == 0)
            doc.Blocks.Add(new Paragraph { Parent = doc });
        doc.NotifyChanged(new DocumentChangedEventArgs(DocumentChangeType.TextInserted, TextRange.Empty));
    }

    /// <summary>
    /// Parses <paramref name="markdown"/> and returns a new <see cref="EditorDoc"/>.
    /// </summary>
    public static EditorDoc LoadMarkdown(string markdown)
        => new MarkdownImporter().Import(markdown);

    /// <summary>
    /// Reads all text from <paramref name="stream"/> and parses it as Markdown,
    /// returning a new <see cref="EditorDoc"/>.
    /// </summary>
    public static EditorDoc LoadMarkdown(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return LoadMarkdown(reader.ReadToEnd());
    }
}
