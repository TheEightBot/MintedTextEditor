using System.Text;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Rendering;
using EditorDoc = MintedTextEditor.Core.Document.Document;

namespace MintedTextEditor.Core.Html;

/// <summary>
/// Controls how <see cref="HtmlExporter"/> serializes a document.
/// </summary>
public class HtmlExportOptions
{
    /// <summary>When true, wraps the output in &lt;!DOCTYPE html&gt;&lt;html&gt;&lt;body&gt;…&lt;/body&gt;&lt;/html&gt;.</summary>
    public bool IncludeDocumentWrapper { get; set; } = false;
}

/// <summary>
/// Serializes a <see cref="Document"/> to clean, semantic HTML5.
/// Only non-default inline styles are emitted.
/// </summary>
public class HtmlExporter
{
    private readonly HtmlExportOptions _options;

    public HtmlExporter(HtmlExportOptions? options = null)
        => _options = options ?? new HtmlExportOptions();

    /// <summary>Exports the complete document as an HTML string.</summary>
    public string Export(EditorDoc doc)
    {
        var sb = new StringBuilder();
        if (_options.IncludeDocumentWrapper)
            sb.Append("<!DOCTYPE html><html><body>");

        ExportBlocks(doc.Blocks, sb);

        if (_options.IncludeDocumentWrapper)
            sb.Append("</body></html>");

        return sb.ToString();
    }

    /// <summary>Exports a subset of the document (e.g. table cell content).</summary>
    private void ExportBlocks(List<Block> blocks, StringBuilder sb)
    {
        int i = 0;
        while (i < blocks.Count)
        {
            // Group consecutive same-type list items into ul/ol
            if (blocks[i] is Paragraph lp && lp.Style.ListType != ListType.None)
            {
                var listType = lp.Style.ListType;
                var listTag = listType == ListType.Bullet ? "ul" : "ol";
                sb.Append($"<{listTag}>");
                while (i < blocks.Count
                       && blocks[i] is Paragraph p2
                       && p2.Style.ListType == listType)
                {
                    sb.Append("<li>");
                    ExportInlines(p2.Inlines, sb);
                    sb.Append("</li>");
                    i++;
                }
                sb.Append($"</{listTag}>");
            }
            else
            {
                ExportBlock(blocks[i], sb);
                i++;
            }
        }
    }

    private void ExportBlock(Block block, StringBuilder sb)
    {
        if (block is Paragraph para)
            ExportParagraph(para, sb);
        else if (block is TableBlock table)
            ExportTable(table, sb);
    }

    private void ExportParagraph(Paragraph para, StringBuilder sb)
    {
        var style = para.Style;

        if (style.IsBlockQuote)
        {
            sb.Append("<blockquote>");
            var pStyleAttr = BuildParaStyleAttr(style);
            sb.Append(string.IsNullOrEmpty(pStyleAttr) ? "<p>" : $"<p style=\"{pStyleAttr}\">");
            ExportInlines(para.Inlines, sb);
            sb.Append("</p></blockquote>");
            return;
        }

        string tag = (style.HeadingLevel >= 1 && style.HeadingLevel <= 6)
            ? $"h{style.HeadingLevel}"
            : "p";

        var styleAttr = BuildParaStyleAttr(style);
        sb.Append(string.IsNullOrEmpty(styleAttr) ? $"<{tag}>" : $"<{tag} style=\"{styleAttr}\">");
        ExportInlines(para.Inlines, sb);
        sb.Append($"</{tag}>");
    }

    private static string BuildParaStyleAttr(ParagraphStyle style)
    {
        var parts = new List<string>(2);
        if (style.Alignment != TextAlignment.Left)
            parts.Add($"text-align:{AlignmentToCss(style.Alignment)}");
        if (style.Direction == TextDirection.RightToLeft)
            parts.Add("direction:rtl");
        return string.Join(";", parts);
    }

    private static string AlignmentToCss(TextAlignment a) => a switch
    {
        TextAlignment.Center => "center",
        TextAlignment.Right => "right",
        TextAlignment.Justify => "justify",
        _ => "left"
    };

    private void ExportTable(TableBlock table, StringBuilder sb)
    {
        sb.Append("<table>");
        foreach (var row in table.Rows)
        {
            sb.Append("<tr>");
            foreach (var cell in row.Cells)
            {
                if (cell.IsMerged) continue;
                var spanAtts = "";
                if (cell.ColumnSpan > 1) spanAtts += $" colspan=\"{cell.ColumnSpan}\"";
                if (cell.RowSpan > 1) spanAtts += $" rowspan=\"{cell.RowSpan}\"";
                sb.Append($"<td{spanAtts}>");
                ExportBlocks(cell.Blocks, sb);
                sb.Append("</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</table>");
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
            ExportTextRun(run.Text, run.Style, sb);
        }
        else if (inline is HyperlinkInline link)
        {
            var titleAttr = string.IsNullOrEmpty(link.Title) ? "" : $" title=\"{HtmlEncode(link.Title)}\"";
            sb.Append($"<a href=\"{HtmlEncodeAttr(link.Url)}\"{titleAttr}>");
            foreach (var child in link.Children)
                ExportInline(child, sb);
            sb.Append("</a>");
        }
        else if (inline is ImageInline img)
        {
            var wAttr = img.Width > 0 ? $" width=\"{(int)img.Width}\"" : "";
            var hAttr = img.Height > 0 ? $" height=\"{(int)img.Height}\"" : "";
            sb.Append($"<img src=\"{HtmlEncodeAttr(img.Source)}\" alt=\"{HtmlEncode(img.AltText)}\"{wAttr}{hAttr} />");
        }
        else if (inline is LineBreak)
        {
            sb.Append("<br />");
        }
    }

    private static void ExportTextRun(string text, TextStyle style, StringBuilder sb)
    {
        // Collect span-level styles (font, color) — these need a <span> wrapper
        var spanParts = new List<string>(4);
        if (style.FontFamily != "Default")
            spanParts.Add($"font-family:{QuoteFontFamily(style.FontFamily)}");
        if (style.FontSize != 14f)
            spanParts.Add($"font-size:{style.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture)}pt");
        if (style.TextColor != EditorColor.Black)
            spanParts.Add($"color:{ColorToCss(style.TextColor)}");
        if (style.HighlightColor != EditorColor.Transparent)
            spanParts.Add($"background-color:{ColorToCss(style.HighlightColor)}");

        // Open tags (outermost first)
        if (spanParts.Count > 0)
            sb.Append($"<span style=\"{string.Join(";", spanParts)}\">");
        if (style.IsBold) sb.Append("<strong>");
        if (style.IsItalic) sb.Append("<em>");
        if (style.IsUnderline) sb.Append("<u>");
        if (style.IsStrikethrough) sb.Append("<s>");
        if (style.IsSubscript) sb.Append("<sub>");
        if (style.IsSuperscript) sb.Append("<sup>");

        sb.Append(HtmlEncode(text));

        // Close tags (reverse order)
        if (style.IsSuperscript) sb.Append("</sup>");
        if (style.IsSubscript) sb.Append("</sub>");
        if (style.IsStrikethrough) sb.Append("</s>");
        if (style.IsUnderline) sb.Append("</u>");
        if (style.IsItalic) sb.Append("</em>");
        if (style.IsBold) sb.Append("</strong>");
        if (spanParts.Count > 0) sb.Append("</span>");
    }

    private static string ColorToCss(EditorColor c)
        => c.A == 255
            ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
            : $"rgba({c.R},{c.G},{c.B},{c.A / 255.0:F3})";

    private static string QuoteFontFamily(string family)
        => family.Contains(' ') ? $"'{family}'" : family;

    internal static string HtmlEncode(string text)
        => text.Replace("&", "&amp;")
               .Replace("<", "&lt;")
               .Replace(">", "&gt;");

    internal static string HtmlEncodeAttr(string text)
        => HtmlEncode(text).Replace("\"", "&quot;");
}
