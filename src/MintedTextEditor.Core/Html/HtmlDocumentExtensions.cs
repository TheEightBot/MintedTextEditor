using System.Text;
using MintedTextEditor.Core.Document;
using EditorDoc = MintedTextEditor.Core.Document.Document;

namespace MintedTextEditor.Core.Html;

/// <summary>
/// Convenience methods for exporting documents to HTML and importing HTML into documents.
/// </summary>
public static class HtmlDocumentExtensions
{
    /// <summary>Exports the entire document as an HTML string.</summary>
    public static string GetHtml(this EditorDoc doc, HtmlExportOptions? options = null)
        => new HtmlExporter(options).Export(doc);

    /// <summary>
    /// Appends HTML content at the end of <paramref name="doc"/>.
    /// The imported blocks are added after the last existing block.
    /// </summary>
    public static void AppendHtml(this EditorDoc doc, string html)
    {
        if (string.IsNullOrEmpty(html)) return;
        var imported = new HtmlImporter().Import(html);
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
    /// Exports the content within <paramref name="range"/> as an HTML string.
    /// Each paragraph boundary within the range becomes a separate <c>&lt;p&gt;</c> element.
    /// </summary>
    public static string GetHtml(this EditorDoc doc, TextRange range)
    {
        if (range.IsEmpty) return string.Empty;

        // Build a temporary document containing only the blocks spanned by the range,
        // trimmed to the range boundaries.
        var temp = new EditorDoc();
        temp.Blocks.Clear();

        var start = range.Start;
        var end   = range.End;

        for (int bi = start.BlockIndex; bi <= end.BlockIndex && bi < doc.Blocks.Count; bi++)
        {
            if (doc.Blocks[bi] is not Paragraph srcPara) continue;

            var para = new Paragraph();
            para.Style.Alignment     = srcPara.Style.Alignment;
            para.Style.HeadingLevel  = srcPara.Style.HeadingLevel;
            para.Style.IndentLevel   = srcPara.Style.IndentLevel;
            para.Style.ListType      = srcPara.Style.ListType;
            para.Style.IsBlockQuote  = srcPara.Style.IsBlockQuote;
            para.Style.Direction     = srcPara.Style.Direction;

            int startInline = bi == start.BlockIndex ? start.InlineIndex : 0;
            int endInline   = bi == end.BlockIndex   ? end.InlineIndex   : srcPara.Inlines.Count - 1;

            for (int ii = startInline; ii <= endInline && ii < srcPara.Inlines.Count; ii++)
            {
                var inline = srcPara.Inlines[ii];

                if (inline is TextRun run)
                {
                    int startOff = bi == start.BlockIndex && ii == startInline ? start.Offset : 0;
                    int endOff   = bi == end.BlockIndex   && ii == endInline   ? end.Offset   : run.Text.Length;
                    startOff = Math.Clamp(startOff, 0, run.Text.Length);
                    endOff   = Math.Clamp(endOff,   0, run.Text.Length);
                    if (startOff < endOff)
                        para.AddInline(new TextRun(run.Text[startOff..endOff], run.Style));
                }
                else
                {
                    // Non-text inlines (HyperlinkInline, ImageInline, LineBreak) are included only when
                    // they are fully within the range (not at the cut-off boundary).
                    bool atStartCut = bi == start.BlockIndex && ii == startInline;
                    bool atEndCut   = bi == end.BlockIndex   && ii == endInline;
                    if (!atStartCut || start.Offset == 0)
                    {
                        if (!atEndCut || end.Offset == 0)
                            para.AddInline(inline);
                    }
                }
            }

            temp.Blocks.Add(para);
        }

        if (temp.Blocks.Count == 0) return string.Empty;
        return new HtmlExporter().Export(temp);
    }

    /// <summary>
    /// Parses <paramref name="html"/> and returns a new <see cref="EditorDoc"/>.
    /// </summary>
    public static EditorDoc LoadHtml(string html)
        => new HtmlImporter().Import(html);

    /// <summary>
    /// Reads all text from <paramref name="stream"/> and parses it as HTML,
    /// returning a new <see cref="EditorDoc"/>.
    /// </summary>
    public static EditorDoc LoadHtml(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return LoadHtml(reader.ReadToEnd());
    }
}
