using System.Globalization;
using System.Text;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Rendering;
using EditorDoc = MintedTextEditor.Core.Document.Document;

namespace MintedTextEditor.Core.Html;

/// <summary>
/// Parses an HTML string into a <see cref="Document"/> model.
/// No external dependencies — uses a hand-written tokenizer.
/// </summary>
public class HtmlImporter
{
    /// <summary>Parses <paramref name="html"/> and returns a new <see cref="Document"/>.</summary>
    public EditorDoc Import(string html)
    {
        var tokens = Tokenize(html);
        return new Builder(tokens).Build();
    }

    // ──────────────────────────────────────────────────────────────────
    // Tokenizer
    // ──────────────────────────────────────────────────────────────────

    private enum TokenType { Text, OpenTag, CloseTag, SelfClose }

    private sealed class HtmlToken
    {
        public TokenType Type { get; }
        public string Name { get; }
        public IReadOnlyDictionary<string, string> Attributes { get; }
        public string Text { get; }

        public HtmlToken(TokenType type, string name,
            IReadOnlyDictionary<string, string> attributes, string text)
        {
            Type = type;
            Name = name;
            Attributes = attributes;
            Text = text;
        }
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyAttrs =
        new Dictionary<string, string>();

    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    { "br", "img", "hr", "input", "meta", "link", "area", "base", "col", "embed", "param", "source", "track", "wbr" };

    private static List<HtmlToken> Tokenize(string html)
    {
        var tokens = new List<HtmlToken>();
        int pos = 0;
        int len = html.Length;

        while (pos < len)
        {
            if (html[pos] != '<')
            {
                // Text node
                int end = html.IndexOf('<', pos);
                if (end < 0) end = len;
                var raw = html.Substring(pos, end - pos);
                // Normalize whitespace: collapse runs of whitespace to a single space
                var text = NormalizeWhitespace(DecodeEntities(raw));
                if (text.Length > 0)
                    tokens.Add(new HtmlToken(TokenType.Text, "", EmptyAttrs, text));
                pos = end;
                continue;
            }

            // Comment: <!-- ... -->
            if (pos + 3 < len && html[pos + 1] == '!' && html[pos + 2] == '-' && html[pos + 3] == '-')
            {
                int end = html.IndexOf("-->", pos + 4, StringComparison.Ordinal);
                pos = end < 0 ? len : end + 3;
                continue;
            }

            // DOCTYPE / CDATA / other <!
            if (pos + 1 < len && html[pos + 1] == '!')
            {
                int end = html.IndexOf('>', pos + 2);
                pos = end < 0 ? len : end + 1;
                continue;
            }

            // Close tag: </name>
            if (pos + 1 < len && html[pos + 1] == '/')
            {
                int end = html.IndexOf('>', pos + 2);
                if (end < 0) { pos++; continue; }
                var name = html.Substring(pos + 2, end - pos - 2).Trim().ToLowerInvariant();
                if (name.Length > 0)
                    tokens.Add(new HtmlToken(TokenType.CloseTag, name, EmptyAttrs, ""));
                pos = end + 1;
                continue;
            }

            // Open or self-closing tag: <name attrs /> or <name attrs>
            {
                int end = FindTagEnd(html, pos + 1);
                if (end < 0) { pos++; continue; }

                var inner = html.Substring(pos + 1, end - pos - 1);
                bool selfClose = inner.Length > 0 && inner[inner.Length - 1] == '/';
                if (selfClose) inner = inner.TrimEnd('/', ' ');

                var (name, attrs) = ParseTagContent(inner);
                if (name.Length > 0)
                {
                    bool isVoid = selfClose || VoidElements.Contains(name);
                    tokens.Add(new HtmlToken(
                        isVoid ? TokenType.SelfClose : TokenType.OpenTag,
                        name, attrs, ""));
                }
                pos = end + 1;
            }
        }

        return tokens;
    }

    /// <summary>Scan past a tag's closing &gt;, respecting quoted attribute values.</summary>
    private static int FindTagEnd(string html, int start)
    {
        int i = start;
        int len = html.Length;
        while (i < len)
        {
            char c = html[i];
            if (c == '>') return i;
            if (c == '"') { i++; while (i < len && html[i] != '"') i++; }
            else if (c == '\'') { i++; while (i < len && html[i] != '\'') i++; }
            i++;
        }
        return -1;
    }

    private static (string name, Dictionary<string, string> attrs) ParseTagContent(string content)
    {
        content = content.Trim();
        if (content.Length == 0) return ("", new Dictionary<string, string>());

        // Extract tag name
        int i = 0;
        while (i < content.Length && !char.IsWhiteSpace(content[i])) i++;
        var name = content[..i].ToLowerInvariant();
        content = content[i..].TrimStart();

        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (content.Length > 0)
        {
            // Attribute name (runs until '=' or whitespace)
            i = 0;
            while (i < content.Length && content[i] != '=' && !char.IsWhiteSpace(content[i])) i++;
            if (i == 0) { content = content[1..]; continue; }

            var attrName = content[..i].ToLowerInvariant();
            content = content[i..].TrimStart();

            if (content.Length == 0 || content[0] != '=')
            {
                attrs[attrName] = ""; // boolean attribute
                continue;
            }

            content = content[1..].TrimStart(); // skip '='

            string attrValue;
            if (content.Length > 0 && (content[0] == '"' || content[0] == '\''))
            {
                char q = content[0];
                content = content[1..];
                int end = content.IndexOf(q);
                if (end < 0) end = content.Length;
                attrValue = DecodeEntities(content[..end]);
                content = end < content.Length ? content[(end + 1)..].TrimStart() : "";
            }
            else
            {
                // Unquoted attribute
                i = 0;
                while (i < content.Length && !char.IsWhiteSpace(content[i])) i++;
                attrValue = content[..i];
                content = content[i..].TrimStart();
            }

            attrs[attrName] = attrValue;
        }

        return (name, attrs);
    }

    private static string DecodeEntities(string text)
    {
        if (text.IndexOf('&') < 0) return text;
        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] != '&') { sb.Append(text[i++]); continue; }
            int semi = text.IndexOf(';', i + 1);
            if (semi <= i) { sb.Append(text[i++]); continue; }

            var entity = text.Substring(i + 1, semi - i - 1);
            string? decoded = entity switch
            {
                "amp" => "&",
                "lt" => "<",
                "gt" => ">",
                "quot" => "\"",
                "apos" => "'",
                "nbsp" => "\u00a0",
                _ when entity.StartsWith('#') => DecodeNumericEntity(entity[1..]),
                _ => null
            };

            if (decoded != null) { sb.Append(decoded); i = semi + 1; }
            else { sb.Append(text[i++]); }
        }
        return sb.ToString();
    }

    private static string? DecodeNumericEntity(string code)
    {
        if (code.Length == 0) return null;
        try
        {
            int cp = (code[0] is 'x' or 'X')
                ? Convert.ToInt32(code[1..], 16)
                : int.Parse(code, CultureInfo.InvariantCulture);
            return char.ConvertFromUtf32(cp);
        }
        catch { return null; }
    }

    private static string NormalizeWhitespace(string text)
    {
        // Collapse runs of whitespace (including newlines/tabs) to a single space
        var sb = new StringBuilder(text.Length);
        bool inSpace = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!inSpace) { sb.Append(' '); inSpace = true; }
            }
            else
            {
                sb.Append(c);
                inSpace = false;
            }
        }
        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────────
    // Document Builder
    // ──────────────────────────────────────────────────────────────────

    private static readonly HashSet<string> SkipContentTags = new(StringComparer.OrdinalIgnoreCase)
    { "head", "style", "script" };

    private sealed class Builder
    {
        private readonly List<HtmlToken> _tokens;

        // ── Mutable build state ──
        private readonly EditorDoc _doc;
        private List<Block> _currentBlocks;
        private Paragraph? _currentPara;

        private readonly Stack<TextStyle> _styleStack = new();
        private readonly Stack<ListType> _listTypeStack = new();
        private readonly Stack<List<Block>> _blocksStack = new();
        private readonly Stack<HyperlinkInline> _anchorStack = new();

        // Current table-building state
        private TableBlock? _currentTable;
        private TableRow? _currentRow;

        private int _skipDepth;
        private int _blockquoteDepth;

        public Builder(List<HtmlToken> tokens)
        {
            _tokens = tokens;
            _doc = new EditorDoc();
            _doc.Blocks.Clear();
            _currentBlocks = _doc.Blocks;
            _styleStack.Push(TextStyle.Default);
            _listTypeStack.Push(ListType.None);
        }

        public EditorDoc Build()
        {
            foreach (var token in _tokens)
                ProcessToken(token);

            if (_doc.Blocks.Count == 0)
                _doc.Blocks.Add(new Paragraph());

            return _doc;
        }

        private TextStyle CurrentStyle() => _styleStack.Peek();

        private void EnsureParagraph()
        {
            if (_currentPara != null) return;
            _currentPara = new Paragraph();
            if (_blockquoteDepth > 0) _currentPara.Style.IsBlockQuote = true;
            _currentBlocks.Add(_currentPara);
        }

        private void FlushParagraph() => _currentPara = null;

        private void AddInline(Inline inline)
        {
            EnsureParagraph();
            if (_anchorStack.Count > 0)
                _anchorStack.Peek().AddChild(inline);
            else
                _currentPara!.AddInline(inline);
        }

        private void PushStyle(TextStyle style) => _styleStack.Push(style);

        private void PopStyle()
        {
            if (_styleStack.Count > 1) _styleStack.Pop();
        }

        private void ProcessToken(HtmlToken token)
        {
            if (token.Type == TokenType.Text)
            {
                if (_skipDepth > 0) return;
                var text = token.Text;
                if (string.IsNullOrEmpty(text) || text == " " && _currentPara == null) return;
                AddInline(new TextRun(text, CurrentStyle()));
                return;
            }

            if (token.Type == TokenType.OpenTag || token.Type == TokenType.SelfClose)
            {
                ProcessOpenOrSelfClose(token);
                return;
            }

            if (token.Type == TokenType.CloseTag)
            {
                ProcessCloseTag(token);
            }
        }

        private void ProcessOpenOrSelfClose(HtmlToken token)
        {
            var tag = token.Name;
            var attrs = token.Attributes;
            bool isSelf = token.Type == TokenType.SelfClose;

            if (SkipContentTags.Contains(tag))
            {
                if (!isSelf) _skipDepth++;
                return;
            }

            if (_skipDepth > 0) return;

            switch (tag)
            {
                // ── Ignored structural tags ────────────────────────────
                case "html":
                case "body":
                    return;

                // ── Block-level paragraph containers ──────────────────
                case "p":
                case "div":
                case "section":
                case "article":
                case "header":
                case "footer":
                case "main":
                case "aside":
                case "nav":
                case "pre":
                {
                    if (!isSelf) FlushParagraph();
                    var para = NewBlockParagraph(attrs);
                    _currentPara = para;
                    _currentBlocks.Add(para);
                    return;
                }

                // ── Headings ─────────────────────────────────────────
                case "h1": case "h2": case "h3":
                case "h4": case "h5": case "h6":
                {
                    FlushParagraph();
                    var para = NewBlockParagraph(attrs);
                    para.Style.HeadingLevel = tag[1] - '0';
                    _currentPara = para;
                    _currentBlocks.Add(para);
                    return;
                }

                // ── Blockquote ────────────────────────────────────────
                case "blockquote":
                    FlushParagraph();
                    _blockquoteDepth++;
                    return;

                // ── Lists ─────────────────────────────────────────────
                case "ul":
                    _listTypeStack.Push(ListType.Bullet);
                    return;
                case "ol":
                    _listTypeStack.Push(ListType.Number);
                    return;
                case "li":
                {
                    FlushParagraph();
                    var para = new Paragraph();
                    para.Style.ListType = _listTypeStack.Peek();
                    if (_blockquoteDepth > 0) para.Style.IsBlockQuote = true;
                    ApplyCssToParaStyle(para.Style, attrs);
                    _currentPara = para;
                    _currentBlocks.Add(para);
                    return;
                }

                // ── Table ─────────────────────────────────────────────
                case "table":
                {
                    FlushParagraph();
                    _currentTable = new TableBlock(0, 0);
                    _currentBlocks.Add(_currentTable);
                    return;
                }
                case "tr":
                {
                    if (_currentTable != null)
                    {
                        _currentRow = new TableRow { Parent = _currentTable };
                        _currentTable.Rows.Add(_currentRow);
                    }
                    return;
                }
                case "td":
                case "th":
                {
                    if (_currentRow != null)
                    {
                        var cell = new TableCell();
                        cell.Parent = _currentRow;
                        cell.Blocks.Clear();
                        _currentRow.Cells.Add(cell);
                        _blocksStack.Push(_currentBlocks);
                        _currentBlocks = cell.Blocks;
                        FlushParagraph();
                    }
                    return;
                }

                // ── Inline formatting ──────────────────────────────────
                case "strong":
                case "b":
                    PushStyle(CurrentStyle().WithBold(true));
                    return;
                case "em":
                case "i":
                    PushStyle(CurrentStyle().WithItalic(true));
                    return;
                case "u":
                    PushStyle(CurrentStyle().WithUnderline(true));
                    return;
                case "s":
                case "del":
                case "strike":
                    PushStyle(CurrentStyle().WithStrikethrough(true));
                    return;
                case "sub":
                    PushStyle(CurrentStyle().WithSubscript(true));
                    return;
                case "sup":
                    PushStyle(CurrentStyle().WithSuperscript(true));
                    return;
                case "span":
                {
                    var ns = CurrentStyle();
                    if (attrs.TryGetValue("style", out var css))
                        ns = ApplyCssToTextStyle(ns, css);
                    PushStyle(ns);
                    return;
                }

                // ── Hyperlink ─────────────────────────────────────────
                case "a":
                {
                    if (isSelf) return;
                    attrs.TryGetValue("href", out var href);
                    attrs.TryGetValue("title", out var title);
                    _anchorStack.Push(new HyperlinkInline(href ?? "", title));
                    return;
                }

                // ── Image (void element) ───────────────────────────────
                case "img":
                {
                    attrs.TryGetValue("src", out var src);
                    attrs.TryGetValue("alt", out var alt);
                    float w = 0, h = 0;
                    if (attrs.TryGetValue("width", out var ws))
                        float.TryParse(ws, NumberStyles.Float, CultureInfo.InvariantCulture, out w);
                    if (attrs.TryGetValue("height", out var hs))
                        float.TryParse(hs, NumberStyles.Float, CultureInfo.InvariantCulture, out h);
                    AddInline(new ImageInline(src ?? "", alt ?? "", w, h));
                    return;
                }

                // ── Line break (void element) ─────────────────────────
                case "br":
                    AddInline(new LineBreak());
                    return;

                case "hr":
                    FlushParagraph();
                    return;

                // ── Unknown tags: skip the tag but preserve child content
                default:
                    return;
            }
        }

        private void ProcessCloseTag(HtmlToken token)
        {
            var tag = token.Name;

            if (SkipContentTags.Contains(tag))
            {
                if (_skipDepth > 0) _skipDepth--;
                return;
            }

            if (_skipDepth > 0) return;

            switch (tag)
            {
                case "p":
                case "div":
                case "section":
                case "article":
                case "header":
                case "footer":
                case "main":
                case "aside":
                case "nav":
                case "pre":
                case "h1": case "h2": case "h3":
                case "h4": case "h5": case "h6":
                case "li":
                    FlushParagraph();
                    return;

                case "blockquote":
                    FlushParagraph();
                    if (_blockquoteDepth > 0) _blockquoteDepth--;
                    return;

                case "ul":
                case "ol":
                    if (_listTypeStack.Count > 1) _listTypeStack.Pop();
                    return;

                case "table":
                    _currentTable = null;
                    _currentRow = null;
                    return;

                case "tr":
                    _currentRow = null;
                    return;

                case "td":
                case "th":
                    FlushParagraph();
                    if (_blocksStack.Count > 0)
                        _currentBlocks = _blocksStack.Pop();
                    return;

                case "strong":
                case "b":
                case "em":
                case "i":
                case "u":
                case "s":
                case "del":
                case "strike":
                case "sub":
                case "sup":
                case "span":
                    PopStyle();
                    return;

                case "a":
                {
                    if (_anchorStack.Count > 0)
                    {
                        var anchor = _anchorStack.Pop();
                        EnsureParagraph();
                        if (_anchorStack.Count > 0)
                            _anchorStack.Peek().AddChild(anchor);
                        else
                            _currentPara!.AddInline(anchor);
                    }
                    return;
                }

                default:
                    return; // Unknown close tag — ignore
            }
        }

        private Paragraph NewBlockParagraph(IReadOnlyDictionary<string, string> attrs)
        {
            var para = new Paragraph();
            if (_blockquoteDepth > 0) para.Style.IsBlockQuote = true;
            ApplyCssToParaStyle(para.Style, attrs);
            return para;
        }

        private static void ApplyCssToParaStyle(ParagraphStyle style,
            IReadOnlyDictionary<string, string> attrs)
        {
            if (attrs.TryGetValue("style", out var css))
            {
                foreach (var decl in css.Split(';'))
                {
                    var parts = decl.Split(':', 2);
                    if (parts.Length != 2) continue;
                    var prop = parts[0].Trim().ToLowerInvariant();
                    var val = parts[1].Trim().ToLowerInvariant();
                    switch (prop)
                    {
                        case "text-align":
                            style.Alignment = val switch
                            {
                                "center" => TextAlignment.Center,
                                "right" => TextAlignment.Right,
                                "justify" => TextAlignment.Justify,
                                _ => TextAlignment.Left
                            };
                            break;
                        case "direction":
                            style.Direction = val == "rtl"
                                ? TextDirection.RightToLeft
                                : TextDirection.LeftToRight;
                            break;
                    }
                }
            }

            if (attrs.TryGetValue("dir", out var dir))
            {
                style.Direction = dir.Equals("rtl", StringComparison.OrdinalIgnoreCase)
                    ? TextDirection.RightToLeft
                    : TextDirection.LeftToRight;
            }
        }

        private static TextStyle ApplyCssToTextStyle(TextStyle style, string css)
        {
            foreach (var decl in css.Split(';'))
            {
                var parts = decl.Split(':', 2);
                if (parts.Length != 2) continue;
                var prop = parts[0].Trim().ToLowerInvariant();
                var val = parts[1].Trim();
                style = prop switch
                {
                    "font-weight" => style.WithBold(
                        val is "bold" or "700" or "800" or "900"),
                    "font-style" => style.WithItalic(
                        val.Equals("italic", StringComparison.OrdinalIgnoreCase)),
                    "text-decoration" => ApplyTextDecoration(style, val),
                    "font-family" => style.WithFontFamily(ParseFontFamily(val)),
                    "font-size" => TryParseFontSize(val, out var fs)
                        ? style.WithFontSize(fs) : style,
                    "color" => TryParseColor(val, out var tc)
                        ? style.WithTextColor(tc) : style,
                    "background-color" => TryParseColor(val, out var bg)
                        ? style.WithHighlightColor(bg) : style,
                    _ => style
                };
            }
            return style;
        }

        private static TextStyle ApplyTextDecoration(TextStyle style, string value)
        {
            var lower = value.ToLowerInvariant();
            if (lower.Contains("underline")) style = style.WithUnderline(true);
            if (lower.Contains("line-through")) style = style.WithStrikethrough(true);
            return style;
        }

        private static string ParseFontFamily(string value)
        {
            var first = value.Split(',')[0].Trim();
            return first.Trim('"', '\'');
        }

        private static bool TryParseFontSize(string value, out float size)
        {
            var v = value.ToLowerInvariant().Trim();
            if (v.EndsWith("pt"))
                return float.TryParse(v[..^2], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out size);
            if (v.EndsWith("px"))
            {
                if (float.TryParse(v[..^2], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float px))
                {
                    size = px * 0.75f;
                    return true;
                }
            }
            size = 0;
            return false;
        }

        private static bool TryParseColor(string value, out EditorColor color)
        {
            var v = value.Trim().ToLowerInvariant();

            color = v switch
            {
                "black" => EditorColor.Black,
                "white" => EditorColor.White,
                "red" => EditorColor.Red,
                "blue" => EditorColor.Blue,
                "green" => EditorColor.Green,
                "gray" or "grey" => EditorColor.Gray,
                "lightgray" or "lightgrey" or "silver" => EditorColor.LightGray,
                "darkgray" or "darkgrey" => EditorColor.DarkGray,
                "yellow" => EditorColor.Yellow,
                "transparent" => EditorColor.Transparent,
                _ => default
            };

            // Named color matched
            if (v is "black" or "white" or "red" or "blue" or "green"
                or "gray" or "grey" or "lightgray" or "lightgrey"
                or "silver" or "darkgray" or "darkgrey" or "yellow" or "transparent")
                return true;

            // Hex: #RGB or #RRGGBB
            if (v.StartsWith('#'))
            {
                var hex = v[1..];
                if (hex.Length == 3)
                    hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
                if (hex.Length == 6)
                {
                    try
                    {
                        byte r = Convert.ToByte(hex[..2], 16);
                        byte g = Convert.ToByte(hex[2..4], 16);
                        byte b = Convert.ToByte(hex[4..6], 16);
                        color = EditorColor.FromRgb(r, g, b);
                        return true;
                    }
                    catch { }
                }
            }

            // rgb(r, g, b)
            if (v.StartsWith("rgb(") && v.EndsWith(")"))
            {
                var parts = v[4..^1].Split(',');
                if (parts.Length >= 3
                    && byte.TryParse(parts[0].Trim(), out byte r)
                    && byte.TryParse(parts[1].Trim(), out byte g)
                    && byte.TryParse(parts[2].Trim(), out byte b))
                {
                    color = EditorColor.FromRgb(r, g, b);
                    return true;
                }
            }

            return false;
        }
    }
}
