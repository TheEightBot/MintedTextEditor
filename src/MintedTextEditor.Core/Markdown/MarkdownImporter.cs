using System.Text;
using System.Text.RegularExpressions;
using MintedTextEditor.Core.Document;
using EditorDoc = MintedTextEditor.Core.Document.Document;

namespace MintedTextEditor.Core.Markdown;

/// <summary>
/// Parses a CommonMark / GFM Markdown string into a <see cref="Document"/> model.
/// No external dependencies — uses hand-written block and inline parsers.
/// </summary>
public class MarkdownImporter
{
    /// <summary>Parses <paramref name="markdown"/> and returns a new <see cref="Document"/>.</summary>
    public EditorDoc Import(string markdown)
    {
        var doc = new EditorDoc();
        doc.Blocks.Clear();

        if (string.IsNullOrEmpty(markdown))
        {
            doc.Blocks.Add(new Paragraph { Parent = doc });
            return doc;
        }

        var lines = markdown.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].EndsWith('\r'))
                lines[i] = lines[i][..^1];
        }

        ParseBlocks(doc, lines);

        if (doc.Blocks.Count == 0)
            doc.Blocks.Add(new Paragraph { Parent = doc });

        return doc;
    }

    // ──────────────────────────────────────────────────────────────────
    // Block-level parser
    // ──────────────────────────────────────────────────────────────────

    private static readonly Regex HeadingRegex =
        new(@"^(#{1,6})\s+(.*?)(?:\s+#+\s*)?$", RegexOptions.Compiled);
    private static readonly Regex BulletRegex =
        new(@"^[-*+]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex OrderedRegex =
        new(@"^\d+\.\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex HrRegex =
        new(@"^(\-{3,}|\*{3,}|_{3,})\s*$", RegexOptions.Compiled);
    private static readonly Regex TableCellRegex =
        new(@"^\|.*\|$", RegexOptions.Compiled);
    private static readonly Regex TableSepRegex =
        new(@"^\|[\s\-:|]+\|$", RegexOptions.Compiled);

    private void ParseBlocks(EditorDoc doc, string[] lines)
    {
        int i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];

            // Blank line: paragraph separator
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // ATX heading: # heading
            var headingMatch = HeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                int level = headingMatch.Groups[1].Length;
                string content = headingMatch.Groups[2].Value;
                var para = new Paragraph { Parent = doc };
                para.Style.HeadingLevel = level;
                AddInlines(para, ParseInlines(content, TextStyle.Default));
                doc.Blocks.Add(para);
                i++;
                continue;
            }

            // Blockquote: > text
            if (line.StartsWith("> ") || line == ">")
            {
                var content = line.Length > 2 ? line[2..] : "";
                var para = new Paragraph { Parent = doc };
                para.Style.IsBlockQuote = true;
                AddInlines(para, ParseInlines(content, TextStyle.Default));
                doc.Blocks.Add(para);
                i++;
                continue;
            }

            // Horizontal rule (skip — add nothing)
            if (HrRegex.IsMatch(line))
            {
                i++;
                continue;
            }

            // Unordered list item
            var bulletMatch = BulletRegex.Match(line);
            if (bulletMatch.Success)
            {
                var para = new Paragraph { Parent = doc };
                para.Style.ListType = ListType.Bullet;
                AddInlines(para, ParseInlines(bulletMatch.Groups[1].Value, TextStyle.Default));
                doc.Blocks.Add(para);
                i++;
                continue;
            }

            // Ordered list item
            var orderedMatch = OrderedRegex.Match(line);
            if (orderedMatch.Success)
            {
                var para = new Paragraph { Parent = doc };
                para.Style.ListType = ListType.Number;
                AddInlines(para, ParseInlines(orderedMatch.Groups[1].Value, TextStyle.Default));
                doc.Blocks.Add(para);
                i++;
                continue;
            }

            // GFM table: lines matching |...|
            if (TableCellRegex.IsMatch(line.Trim()))
            {
                int tableStart = i;
                while (i < lines.Length
                       && !string.IsNullOrWhiteSpace(lines[i])
                       && TableCellRegex.IsMatch(lines[i].Trim()))
                    i++;

                var tableLines = lines[tableStart..i];
                var table = ParseTable(tableLines, doc);
                if (table != null)
                    doc.Blocks.Add(table);
                continue;
            }

            // Normal paragraph: accumulate consecutive non-blank, non-special lines
            var paragraphLines = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && !IsSpecialLine(lines[i]))
            {
                paragraphLines.Add(lines[i]);
                i++;
            }

            if (paragraphLines.Count > 0)
            {
                var para = new Paragraph { Parent = doc };
                for (int j = 0; j < paragraphLines.Count; j++)
                {
                    if (j > 0)
                    {
                        // Hard line break when previous line has 2+ trailing spaces
                        if (paragraphLines[j - 1].EndsWith("  "))
                            para.AddInline(new LineBreak { Parent = para });
                        else
                            para.AddInline(new TextRun(" ", TextStyle.Default) { Parent = para });
                    }
                    AddInlines(para, ParseInlines(paragraphLines[j].TrimEnd(), TextStyle.Default));
                }
                doc.Blocks.Add(para);
            }
        }
    }

    private static bool IsSpecialLine(string line)
    {
        if (HeadingRegex.IsMatch(line)) return true;
        if (line.StartsWith("> ") || line == ">") return true;
        if (BulletRegex.IsMatch(line)) return true;
        if (OrderedRegex.IsMatch(line)) return true;
        if (TableCellRegex.IsMatch(line.Trim())) return true;
        if (HrRegex.IsMatch(line)) return true;
        return false;
    }

    private static void AddInlines(Paragraph para, List<Inline> inlines)
    {
        foreach (var inline in inlines)
            para.AddInline(inline);
    }

    // ──────────────────────────────────────────────────────────────────
    // GFM table parser
    // ──────────────────────────────────────────────────────────────────

    private TableBlock? ParseTable(string[] tableLines, EditorDoc doc)
    {
        if (tableLines.Length < 2) return null;
        if (!TableSepRegex.IsMatch(tableLines[1].Trim())) return null;

        var table = new TableBlock { Parent = doc };

        // Header row
        var headerCells = SplitTableRow(tableLines[0]);
        var headerRow = new TableRow { Parent = table };
        foreach (var cellContent in headerCells)
        {
            var cell = new TableCell { Parent = headerRow };
            cell.Blocks.Clear();
            var cellPara = new Paragraph();
            AddInlines(cellPara, ParseInlines(cellContent.Trim(), TextStyle.Default));
            cell.Blocks.Add(cellPara);
            headerRow.Cells.Add(cell);
        }
        table.Rows.Add(headerRow);

        // Data rows
        for (int i = 2; i < tableLines.Length; i++)
        {
            var dataCells = SplitTableRow(tableLines[i]);
            var row = new TableRow { Parent = table };
            foreach (var cellContent in dataCells)
            {
                var cell = new TableCell { Parent = row };
                cell.Blocks.Clear();
                var cellPara = new Paragraph();
                AddInlines(cellPara, ParseInlines(cellContent.Trim(), TextStyle.Default));
                cell.Blocks.Add(cellPara);
                row.Cells.Add(cell);
            }
            table.Rows.Add(row);
        }

        return table;
    }

    private static List<string> SplitTableRow(string line)
    {
        line = line.Trim();
        if (line.StartsWith('|')) line = line[1..];
        if (line.EndsWith('|')) line = line[..^1];
        return line.Split('|').ToList();
    }

    // ──────────────────────────────────────────────────────────────────
    // Inline parser
    // ──────────────────────────────────────────────────────────────────

    private List<Inline> ParseInlines(string text, TextStyle style)
    {
        var result = new List<Inline>();
        if (string.IsNullOrEmpty(text)) return result;

        int pos = 0;
        var textBuffer = new StringBuilder();

        void FlushText()
        {
            if (textBuffer.Length > 0)
            {
                result.Add(new TextRun(textBuffer.ToString(), style));
                textBuffer.Clear();
            }
        }

        while (pos < text.Length)
        {
            char c = text[pos];

            // Escape sequence: \X where X is a special char
            if (c == '\\' && pos + 1 < text.Length && "\\`*_{}[]()#+-.!|~".Contains(text[pos + 1]))
            {
                textBuffer.Append(text[pos + 1]);
                pos += 2;
                continue;
            }

            // Image: ![alt](src) — must check before link
            if (c == '!' && pos + 1 < text.Length && text[pos + 1] == '[')
            {
                if (TryParseImage(text, pos, out var img, out int imgConsumed))
                {
                    FlushText();
                    result.Add(img!);
                    pos += imgConsumed;
                    continue;
                }
            }

            // Link: [text](url) or [text](url "title")
            if (c == '[')
            {
                if (TryParseLink(text, pos, out var link, out int linkConsumed, style))
                {
                    FlushText();
                    result.Add(link!);
                    pos += linkConsumed;
                    continue;
                }
            }

            // HTML tags: <sub>, <sup>, <br>, <br/>, <br />
            if (c == '<')
            {
                // <br /> (6 chars) before <br/> (5 chars) before <br> (4 chars)
                if (MatchAt(text, pos, "<br />"))
                {
                    FlushText(); result.Add(new LineBreak()); pos += 6; continue;
                }
                if (MatchAt(text, pos, "<br/>"))
                {
                    FlushText(); result.Add(new LineBreak()); pos += 5; continue;
                }
                if (MatchAt(text, pos, "<br>"))
                {
                    FlushText(); result.Add(new LineBreak()); pos += 4; continue;
                }

                // <sub>...</sub>
                if (MatchAt(text, pos, "<sub>"))
                {
                    int closePos = IndexOfIgnoreCase(text, "</sub>", pos + 5);
                    if (closePos >= 0)
                    {
                        FlushText();
                        var inner = text.Substring(pos + 5, closePos - pos - 5);
                        result.AddRange(ParseInlines(inner, style.WithSubscript(true)));
                        pos = closePos + 6;
                        continue;
                    }
                }

                // <sup>...</sup>
                if (MatchAt(text, pos, "<sup>"))
                {
                    int closePos = IndexOfIgnoreCase(text, "</sup>", pos + 5);
                    if (closePos >= 0)
                    {
                        FlushText();
                        var inner = text.Substring(pos + 5, closePos - pos - 5);
                        result.AddRange(ParseInlines(inner, style.WithSuperscript(true)));
                        pos = closePos + 6;
                        continue;
                    }
                }
            }

            // Bold+Italic: ***text*** or ___text___
            if (pos + 2 < text.Length
                && ((c == '*' && text[pos + 1] == '*' && text[pos + 2] == '*')
                 || (c == '_' && text[pos + 1] == '_' && text[pos + 2] == '_')))
            {
                string delim = new string(c, 3);
                int closePos = text.IndexOf(delim, pos + 3, StringComparison.Ordinal);
                if (closePos >= 0)
                {
                    FlushText();
                    var inner = text.Substring(pos + 3, closePos - pos - 3);
                    result.AddRange(ParseInlines(inner, style.WithBold(true).WithItalic(true)));
                    pos = closePos + 3;
                    continue;
                }
            }

            // Bold: **text** or __text__ (not ***)
            if (pos + 1 < text.Length
                && ((c == '*' && text[pos + 1] == '*' && (pos + 2 >= text.Length || text[pos + 2] != '*'))
                 || (c == '_' && text[pos + 1] == '_' && (pos + 2 >= text.Length || text[pos + 2] != '_'))))
            {
                string delim = new string(c, 2);
                int closePos = text.IndexOf(delim, pos + 2, StringComparison.Ordinal);
                if (closePos >= 0)
                {
                    FlushText();
                    var inner = text.Substring(pos + 2, closePos - pos - 2);
                    result.AddRange(ParseInlines(inner, style.WithBold(true)));
                    pos = closePos + 2;
                    continue;
                }
            }

            // Italic: *text* or _text_ (not ** or __)
            if ((c == '*' && (pos + 1 >= text.Length || text[pos + 1] != '*'))
             || (c == '_' && (pos + 1 >= text.Length || text[pos + 1] != '_')))
            {
                int closePos = text.IndexOf(c, pos + 1);
                if (closePos >= 0)
                {
                    FlushText();
                    var inner = text.Substring(pos + 1, closePos - pos - 1);
                    result.AddRange(ParseInlines(inner, style.WithItalic(true)));
                    pos = closePos + 1;
                    continue;
                }
            }

            // Strikethrough: ~~text~~
            if (c == '~' && pos + 1 < text.Length && text[pos + 1] == '~')
            {
                int closePos = text.IndexOf("~~", pos + 2, StringComparison.Ordinal);
                if (closePos >= 0)
                {
                    FlushText();
                    var inner = text.Substring(pos + 2, closePos - pos - 2);
                    result.AddRange(ParseInlines(inner, style.WithStrikethrough(true)));
                    pos = closePos + 2;
                    continue;
                }
            }

            textBuffer.Append(c);
            pos++;
        }

        FlushText();
        return result;
    }

    // ──────────────────────────────────────────────────────────────────
    // Link and image helpers
    // ──────────────────────────────────────────────────────────────────

    private bool TryParseLink(string text, int pos, out HyperlinkInline? link,
        out int consumed, TextStyle style)
    {
        link = null;
        consumed = 0;

        // pos is at '['
        int closeBracket = FindMatchingCloseBracket(text, pos + 1);
        if (closeBracket < 0) return false;

        if (closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(') return false;

        int closeParen = FindMatchingCloseParen(text, closeBracket + 2);
        if (closeParen < 0) return false;

        var urlPart = text.Substring(closeBracket + 2, closeParen - closeBracket - 2).Trim();
        ParseUrlAndTitle(urlPart, out string url, out string? title);

        var linkText = text.Substring(pos + 1, closeBracket - pos - 1);
        var children = ParseInlines(linkText, style);

        link = new HyperlinkInline(url, title);
        foreach (var child in children)
            link.AddChild(child);

        consumed = closeParen - pos + 1;
        return true;
    }

    private static bool TryParseImage(string text, int pos, out ImageInline? img, out int consumed)
    {
        img = null;
        consumed = 0;

        // pos is at '!', text[pos+1] == '['
        int closeBracket = FindMatchingCloseBracket(text, pos + 2);
        if (closeBracket < 0) return false;

        if (closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(') return false;

        int closeParen = FindMatchingCloseParen(text, closeBracket + 2);
        if (closeParen < 0) return false;

        var altText = text.Substring(pos + 2, closeBracket - pos - 2);
        var srcPart = text.Substring(closeBracket + 2, closeParen - closeBracket - 2).Trim();

        img = new ImageInline(srcPart, altText);
        consumed = closeParen - pos + 1;
        return true;
    }

    private static void ParseUrlAndTitle(string urlPart, out string url, out string? title)
    {
        // Match: url "title" or url 'title'
        var m = Regex.Match(urlPart, @"^(.*?)\s+[""'](.*)[""']\s*$");
        if (m.Success)
        {
            url = m.Groups[1].Value.Trim();
            title = m.Groups[2].Value;
        }
        else
        {
            url = urlPart;
            title = null;
        }
    }

    /// <summary>Finds the matching ']' starting from <paramref name="start"/>, tracking nesting.</summary>
    private static int FindMatchingCloseBracket(string text, int start)
    {
        int depth = 0;
        for (int i = start; i < text.Length; i++)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']')
            {
                if (depth == 0) return i;
                depth--;
            }
        }
        return -1;
    }

    /// <summary>Finds the matching ')' starting from <paramref name="start"/>, skipping quoted titles.</summary>
    private static int FindMatchingCloseParen(string text, int start)
    {
        int depth = 0;
        bool inQuote = false;
        char quoteChar = '"';
        for (int i = start; i < text.Length; i++)
        {
            if (inQuote)
            {
                if (text[i] == quoteChar) inQuote = false;
            }
            else
            {
                if (text[i] == '"' || text[i] == '\'') { inQuote = true; quoteChar = text[i]; }
                else if (text[i] == '(') depth++;
                else if (text[i] == ')')
                {
                    if (depth == 0) return i;
                    depth--;
                }
            }
        }
        return -1;
    }

    // ──────────────────────────────────────────────────────────────────
    // String utilities
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Case-insensitive prefix match at position <paramref name="pos"/>.</summary>
    private static bool MatchAt(string text, int pos, string lowerMatch)
    {
        if (pos + lowerMatch.Length > text.Length) return false;
        for (int i = 0; i < lowerMatch.Length; i++)
        {
            if (char.ToLowerInvariant(text[pos + i]) != lowerMatch[i])
                return false;
        }
        return true;
    }

    private static int IndexOfIgnoreCase(string text, string value, int startIndex)
        => text.IndexOf(value, startIndex, StringComparison.OrdinalIgnoreCase);
}
