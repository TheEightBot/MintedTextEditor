# Markdown Interop

MintedTextEditor supports Markdown import and export via the `MintedTextEditor.Core.Markdown` namespace, including GitHub Flavored Markdown (GFM) extensions such as tables and strikethrough.

## Export to Markdown

```csharp
using MintedTextEditor.Core.Markdown;

var markdown = editor.Document.GetMarkdown();
```

### Export Options

Customize the Markdown output using `MarkdownExportOptions`:

```csharp
var options = new MarkdownExportOptions
{
    UseGfmExtensions = true, // Enable GitHub Flavored Markdown (default: true)
    LineEnding = "\r\n"      // Customize line endings (default: "\n")
};
var markdown = editor.Document.GetMarkdown(options);
```

### Example Output

Given a document with a heading and a paragraph, `GetMarkdown()` produces:

```markdown
# MintedTextEditor

A custom-drawn rich text editor for .NET MAUI.
```

With GFM enabled, a strikethrough run exports as:

```markdown
~~deleted text~~
```

## Import from Markdown

Load a new document from a Markdown string:

```csharp
using MintedTextEditor.Core.Markdown;

var document = EditorDocumentExtensions.LoadMarkdown("# Hello World\nThis is a paragraph.");
```

Append Markdown to an existing document:

```csharp
editor.Document.AppendMarkdown("## Subheading\nAnother paragraph.");
```

## Supported Syntax

### Block Elements

| Markdown syntax | Maps to |
|---|---|
| `# Heading` – `###### Heading` | `Paragraph` with `HeadingLevel` 1–6 |
| Blank-line-separated text | `Paragraph` (body text) |
| `- item` / `* item` | `Paragraph` with `ListType.Bullet` |
| `1. item` | `Paragraph` with `ListType.Numbered` |
| `> text` | `Paragraph` with `IsBlockQuote = true` |
| GFM table (`\| col \| col \|`) | `TableBlock` |

### Inline Elements

| Markdown syntax | Maps to |
|---|---|
| `**text**` / `__text__` | `IsBold = true` |
| `*text*` / `_text_` | `IsItalic = true` |
| `~~text~~` (GFM) | `IsStrikethrough = true` |
| `[label](url)` | `HyperlinkInline` |
| `![alt](url)` | `ImageInline` |

## GFM Extensions

When `UseGfmExtensions` is `true` (the default), the exporter emits GFM-compatible syntax:

- Strikethrough uses `~~text~~` instead of being omitted.
- Tables are rendered using the GFM pipe-table syntax.

Set `UseGfmExtensions = false` to target stricter CommonMark output (tables and strikethrough will be omitted or converted to plain text).
