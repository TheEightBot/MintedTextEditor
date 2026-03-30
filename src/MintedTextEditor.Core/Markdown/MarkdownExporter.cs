using System.Text;
using MintedTextEditor.Core.Document;
using EditorDoc = MintedTextEditor.Core.Document.Document;

namespace MintedTextEditor.Core.Markdown;

/// <summary>
/// Serializes a <see cref="Document"/> to CommonMark / GFM Markdown.
/// Only non-default inline styles are emitted.
/// </summary>
public class MarkdownExporter
{
    private readonly MarkdownExportOptions _options;

    public MarkdownExporter(MarkdownExportOptions? options = null)
        => _options = options ?? new MarkdownExportOptions();

    /// <summary>Exports the complete document as a Markdown string.</summary>
    public string Export(EditorDoc doc)
    {
        var sb = new StringBuilder();
        ExportBlocks(doc.Blocks, sb);
        return sb.ToString();
    }

    private void ExportBlocks(List<Block> blocks, StringBuilder sb)
    {
        bool needsBlankLine = false;
        int i = 0;

        while (i < blocks.Count)
        {
            var block = blocks[i];

            if (block is Paragraph lp && lp.Style.ListType != ListType.None)
            {
                if (needsBlankLine) sb.Append(_options.LineEnding);

                var listType = lp.Style.ListType;
                int itemNum = 1;
                while (i < blocks.Count
                       && blocks[i] is Paragraph p2
                       && p2.Style.ListType == listType)
                {
                    if (listType == ListType.Bullet)
                        sb.Append("- ");
                    else
                        sb.Append($"{itemNum}. ");
                    ExportInlines(p2.Inlines, sb);
                    sb.Append(_options.LineEnding);
                    itemNum++;
                    i++;
                }
                needsBlankLine = true;
            }
            else
            {
                if (needsBlankLine) sb.Append(_options.LineEnding);
                ExportBlock(block, sb);
                needsBlankLine = true;
                i++;
            }
        }
    }

    private void ExportBlock(Block block, StringBuilder sb)
    {
        if (block is Paragraph para)
            ExportParagraph(para, sb);
        else if (block is TableBlock table && _options.UseGfmExtensions)
            ExportTable(table, sb);
    }

    private void ExportParagraph(Paragraph para, StringBuilder sb)
    {
        var style = para.Style;

        if (style.HeadingLevel >= 1 && style.HeadingLevel <= 6)
        {
            sb.Append(new string('#', style.HeadingLevel));
            sb.Append(' ');
            ExportInlines(para.Inlines, sb);
            sb.Append(_options.LineEnding);
        }
        else if (style.IsBlockQuote)
        {
            sb.Append("> ");
            ExportInlines(para.Inlines, sb);
            sb.Append(_options.LineEnding);
        }
        else
        {
            ExportInlines(para.Inlines, sb);
            sb.Append(_options.LineEnding);
        }
    }

    private void ExportTable(TableBlock table, StringBuilder sb)
    {
        if (table.Rows.Count == 0) return;

        var le = _options.LineEnding;
        var headerRow = table.Rows[0];

        // Header row
        sb.Append('|');
        foreach (var cell in headerRow.Cells)
        {
            sb.Append(' ');
            ExportCellContent(cell.Blocks, sb);
            sb.Append(" |");
        }
        sb.Append(le);

        // Separator row
        sb.Append('|');
        foreach (var _ in headerRow.Cells)
            sb.Append(" --- |");
        sb.Append(le);

        // Data rows
        for (int i = 1; i < table.Rows.Count; i++)
        {
            sb.Append('|');
            foreach (var cell in table.Rows[i].Cells)
            {
                if (cell.IsMerged) continue;
                sb.Append(' ');
                ExportCellContent(cell.Blocks, sb);
                sb.Append(" |");
            }
            sb.Append(le);
        }
    }

    private void ExportCellContent(List<Block> blocks, StringBuilder sb)
    {
        foreach (var block in blocks)
        {
            if (block is Paragraph para)
                ExportInlines(para.Inlines, sb);
        }
    }

    private void ExportInlines(List<Inline> inlines, StringBuilder sb)
    {
        foreach (var inline in inlines)
            ExportInline(inline, sb);
    }

    private void ExportInline(Inline inline, StringBuilder sb)
    {
        if (inline is TextRun run)
        {
            if (string.IsNullOrEmpty(run.Text)) return;
            ExportTextRun(run, sb);
        }
        else if (inline is HyperlinkInline link)
        {
            sb.Append('[');
            foreach (var child in link.Children)
                ExportInline(child, sb);
            sb.Append("](");
            sb.Append(link.Url);
            if (!string.IsNullOrEmpty(link.Title))
            {
                sb.Append(" \"");
                sb.Append(link.Title);
                sb.Append('"');
            }
            sb.Append(')');
        }
        else if (inline is ImageInline img)
        {
            sb.Append("![");
            sb.Append(EscapeMarkdown(img.AltText));
            sb.Append("](");
            sb.Append(img.Source);
            sb.Append(')');
        }
        else if (inline is LineBreak)
        {
            sb.Append("  ");
            sb.Append(_options.LineEnding);
        }
    }

    private void ExportTextRun(TextRun run, StringBuilder sb)
    {
        var style = run.Style;
        var escaped = EscapeMarkdown(run.Text);

        // Build innermost to outermost; HTML tags are innermost.
        string content = escaped;

        if (style.IsSubscript)
            content = $"<sub>{escaped}</sub>";
        else if (style.IsSuperscript)
            content = $"<sup>{escaped}</sup>";

        if (style.IsStrikethrough && _options.UseGfmExtensions)
            content = $"~~{content}~~";

        if (style.IsBold && style.IsItalic)
            content = $"***{content}***";
        else if (style.IsBold)
            content = $"**{content}**";
        else if (style.IsItalic)
            content = $"*{content}*";

        sb.Append(content);
    }

    private static readonly char[] SpecialChars =
        { '\\', '`', '*', '_', '{', '}', '[', ']', '(', ')', '#', '+', '-', '.', '!', '|' };

    private static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new StringBuilder(text.Length + 4);
        foreach (var ch in text)
        {
            if (Array.IndexOf(SpecialChars, ch) >= 0)
                sb.Append('\\');
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
