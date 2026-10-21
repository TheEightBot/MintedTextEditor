# Markdown Interop

MintedTextEditor supports Markdown import and export via the `MintedTextEditor.Core.Markdown` namespace, including GitHub Flavored Markdown (GFM) extensions such as tables and strikethrough. The parser and writer are built in; there is no external Markdown dependency.

## Export to Markdown

```csharp
string markdown = editor.GetMarkdown();
```

The same export is available on any `Document`:

```csharp
using MintedTextEditor.Core.Markdown;

string markdown = document.GetMarkdown();
```

### Export Options

Customize the Markdown output using `MarkdownExportOptions`:

```csharp
var options = new MarkdownExportOptions
{
    UseGfmExtensions = true, // Enable GitHub Flavored Markdown (default: true)
    LineEnding = "\r\n"      // Customize line endings (default: "\n")
};
var markdown = editor.GetMarkdown(options);
```

### Example Output

Given a document with a heading and a paragraph, `GetMarkdown()` produces:

```markdown
# MintedTextEditor

A custom\-drawn rich text editor for \.NET MAUI\.
```

Markdown punctuation in text (such as `*`, `_`, `#`, `-`, `.` and `|`) is escaped with a backslash so it reads back as plain text.

With GFM enabled, a strikethrough run exports as:

```markdown
~~deleted text~~
```

## Import from Markdown

Replace the editor's content:

```csharp
editor.LoadMarkdown("# Hello World\nThis is a paragraph.");
```

Append Markdown to the end of the current document:

```csharp
editor.AppendMarkdown("## Subheading\nAnother paragraph.");
```

Without the control, create a new `Document` from a string or a `Stream`:

```csharp
using MintedTextEditor.Core.Markdown;

var document = MarkdownDocumentExtensions.LoadMarkdown("# Hello World\nThis is a paragraph.");
document.AppendMarkdown("## Subheading\nAnother paragraph.");
```

`MarkdownImporter` and `MarkdownExporter` can also be used directly.

## Supported Syntax

### Block Elements

| Markdown syntax | Maps to |
|---|---|
| `# Heading` – `###### Heading` | `Paragraph` with `HeadingLevel` 1–6 |
| Blank-line-separated text | `Paragraph` (body text); single line breaks become spaces, a line ending in two spaces becomes a `LineBreak` |
| `- item` / `* item` / `+ item` | `Paragraph` with `ListType.Bullet` |
| `1. item` | `Paragraph` with `ListType.Number` |
| `> text` | `Paragraph` with `IsBlockQuote = true` |
| GFM table (`\| col \| col \|` with a `\| --- \|` separator row) | `TableBlock` |
| `---` / `***` / `___` | Ignored |

Nested lists, fenced code blocks and setext headings are not recognised.

### Inline Elements

| Markdown syntax | Maps to |
|---|---|
| `**text**` / `__text__` | `IsBold = true` |
| `*text*` / `_text_` | `IsItalic = true` |
| `***text***` / `___text___` | Bold and italic |
| `~~text~~` (GFM) | `IsStrikethrough = true` |
| `<sub>text</sub>` / `<sup>text</sup>` | `IsSubscript` / `IsSuperscript` |
| `<br>` | `LineBreak` |
| `[label](url "title")` | `HyperlinkInline` |
| `![alt](url)` | `ImageInline` |

Underline, font, color and paragraph alignment have no Markdown form and are dropped on export.

## GFM Extensions

When `UseGfmExtensions` is `true` (the default), the exporter emits GFM-compatible syntax:

- Strikethrough uses `~~text~~`.
- Tables are rendered using the GFM pipe-table syntax; the first row becomes the header row.

Set `UseGfmExtensions = false` to target stricter CommonMark output: strikethrough runs are written as plain text and tables are omitted.
