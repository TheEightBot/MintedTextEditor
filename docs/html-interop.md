# HTML Interop

MintedTextEditor supports round-trip HTML import and export, making it easy to load and persist content as HTML.

## Export to HTML

```csharp
string html = editor.GetHtml();
```

The result is a fragment — a series of block-level HTML elements (`<p>`, `<h1>`–`<h6>`, `<ul>`, `<ol>`, `<blockquote>`, `<table>`) without `<html>`, `<head>`, or `<body>` wrappers, written on a single line.

### Example Output

Given a document with a bold heading, a paragraph and a bullet list, `GetHtml()` produces (line breaks added here for readability):

```html
<h1><strong>MintedTextEditor</strong></h1>
<p>A custom-drawn rich text editor for .NET MAUI.</p>
<ul><li>Fully drawn via SkiaSharp</li><li>No native text views</li><li>MIT licensed</li></ul>
```

### Working with a Document directly

The same functionality is available on any `Document` through extension methods in `MintedTextEditor.Core.Html`:

```csharp
using MintedTextEditor.Core.Html;

string html = document.GetHtml();
string full = document.GetHtml(new HtmlExportOptions { IncludeDocumentWrapper = true }); // adds <!DOCTYPE html><html><body>
string part = document.GetHtml(editor.Selection.Range);    // only the selected paragraphs/text
var loaded = HtmlDocumentExtensions.LoadHtml("<p>Hi</p>");      // new Document; also accepts a Stream
document.AppendHtml("<p>More</p>");
```

`HtmlExporter` and `HtmlImporter` can also be used directly (`new HtmlExporter(options).Export(doc)`, `new HtmlImporter().Import(html)`).

## Import from HTML

```csharp
editor.LoadHtml("<p>Hello <b>world</b>!</p>");
```

Import replaces the current document content entirely. To append HTML instead:

```csharp
editor.AppendHtml("<p>Additional content.</p>");
```

## Supported Tags

### Block Elements

| Tag | Maps to |
|---|---|
| `<p>`, `<div>`, `<section>`, `<article>`, `<header>`, `<footer>`, `<main>`, `<aside>`, `<nav>`, `<pre>` | `Paragraph` (body text) |
| `<h1>`–`<h6>` | `Paragraph` with `HeadingLevel` 1–6 |
| `<ul>` / `<ol>` | `Paragraph` sequence with `ListType.Bullet` / `ListType.Number` |
| `<li>` | Individual list item paragraph |
| `<blockquote>` | `Paragraph` with `IsBlockQuote = true` |
| `<table>` | `TableBlock` |
| `<tr>` | `TableRow` |
| `<td>` / `<th>` | `TableCell` (exported as `<td>`) |
| `<br>` | `LineBreak` inline |

`<hr>` ends the current paragraph and is otherwise dropped. `<head>`, `<style>` and `<script>` are skipped with their content. Other unknown tags are ignored but their text is kept.

### Inline Elements

| Tag | Maps to |
|---|---|
| `<strong>` / `<b>` | `IsBold = true` |
| `<em>` / `<i>` | `IsItalic = true` |
| `<u>` | `IsUnderline = true` |
| `<s>` / `<del>` / `<strike>` | `IsStrikethrough = true` |
| `<sub>` | `IsSubscript = true` |
| `<sup>` | `IsSuperscript = true` |
| `<span style="color:...">` | `TextColor` |
| `<span style="background-color:...">` | `HighlightColor` |
| `<span style="font-family:...">` | `FontFamily` (first family in the list) |
| `<span style="font-size:...">` | `FontSize` (`pt`, or `px` × 0.75) |
| `<span style="font-weight:bold">` / `font-style:italic` / `text-decoration:underline line-through` | Bold / italic / underline / strikethrough |
| `<a href="..." title="...">` | `HyperlinkInline` |
| `<img src="..." alt="..." width="..." height="...">` | `ImageInline` (the source is stored as-is and loaded when drawn; see [Images & Hyperlinks](images-hyperlinks.md)) |

Colors may be `#rgb`, `#rrggbb`, `rgb(...)`, or a few named colors.

### Style Attributes

Inline `style` attributes on paragraph, heading and list-item elements are parsed for:

| CSS Property / attribute | Maps to |
|---|---|
| `text-align` | `ParagraphStyle.Alignment` |
| `direction: rtl` or `dir="rtl"` | `ParagraphStyle.Direction = TextDirection.RightToLeft` |

Other paragraph properties (indent level, line spacing, space before/after) are not read from or written to HTML.

## Round-trip Fidelity

Exported HTML imports back to the same structure and character formatting. Font family, font size, text color and highlight are written as a `<span style="...">`, and only when they differ from the defaults:

```html
<span style="background-color:#FFFF00">highlighted</span>
```

Paragraph alignment and direction round-trip through `style`. Not preserved: indent level, line spacing and paragraph spacing; table header rows; cell spans (`colspan`/`rowspan` are exported but not read back); and colors with transparency, which are exported as `rgba(...)` but not parsed on import.
