# HTML Interop

MintedTextEditor supports round-trip HTML import and export, making it easy to load and persist content as HTML.

## Export to HTML

```csharp
string html = editor.ExportHtml();
```

The result is a fragment — a series of block-level HTML elements (`<p>`, `<h1>`–`<h6>`, `<ul>`, `<ol>`, `<table>`, etc.) without `<html>`, `<head>`, or `<body>` wrappers.

### Example Output

Given a document with a bold heading and a bullet list, `ExportHtml()` produces:

```html
<h1><strong>MintedTextEditor</strong></h1>
<p>A custom-drawn rich text editor for .NET MAUI.</p>
<ul>
  <li>Fully drawn via SkiaSharp</li>
  <li>No native text views</li>
  <li>MIT licensed</li>
</ul>
```

## Import from HTML

```csharp
editor.ImportHtml("<p>Hello <b>world</b>!</p>");
```

Import replaces the current document content entirely. To append HTML instead:

```csharp
editor.AppendHtml("<p>Additional content.</p>");
```

## Supported Tags

### Block Elements

| Tag | Maps to |
|---|---|
| `<p>` | `Paragraph` (body text) |
| `<h1>`–`<h6>` | `Paragraph` with `HeadingLevel` 1–6 |
| `<ul>` / `<ol>` | `Paragraph` sequence with `ListType.Bullet` / `ListType.Numbered` |
| `<li>` | Individual list item paragraph |
| `<blockquote>` | `Paragraph` with `IsBlockQuote = true` |
| `<table>` | `TableBlock` |
| `<tr>` | `TableRow` |
| `<td>` / `<th>` | `TableCell` |
| `<br>` | `LineBreak` inline |

### Inline Elements

| Tag | Maps to |
|---|---|
| `<strong>` / `<b>` | `IsBold = true` |
| `<em>` / `<i>` | `IsItalic = true` |
| `<u>` | `IsUnderline = true` |
| `<s>` / `<del>` / `<strike>` | `IsStrikethrough = true` |
| `<sub>` | `IsSubscript = true` |
| `<sup>` | `IsSuperscript = true` |
| `<span style="color:...">` | `ForegroundColor` |
| `<span style="background-color:...">` | `BackgroundColor` |
| `<span style="font-family:...">` | `FontFamily` |
| `<span style="font-size:...">` | `FontSize` |
| `<a href="...">` | `HyperlinkInline` |
| `<img src="...">` | `ImageInline` (loads from URL or data URI) |

### Style Attributes

Inline `style` attributes on `<p>`, `<div>`, and block elements are parsed for:

| CSS Property | Maps to |
|---|---|
| `text-align` | `ParagraphStyle.Alignment` |
| `padding-left` / `margin-left` | `ParagraphStyle.IndentLevel` |
| `line-height` | `ParagraphStyle.LineSpacing` |
| `margin-top` | `ParagraphStyle.SpaceBefore` |
| `margin-bottom` | `ParagraphStyle.SpaceAfter` |
| `direction: rtl` | `ParagraphStyle.Direction = RTL` |

## HTML Customization

Provide a custom `IHtmlSerializer` to control export output:

```csharp
editor.HtmlSerializer = new MyHtmlSerializer();
```

Provide a custom `IHtmlParser` to extend import handling:

```csharp
editor.HtmlParser = new MyHtmlParser();
```

## Round-trip Fidelity

The HTML parser preserves all formatting that maps to an HTML equivalent. Content that has no HTML representation (e.g., highlight color when used as a background span) is preserved via `data-` attributes on the surrounding `<span>`:

```html
<span data-minted-highlight="true" style="background-color:#FFFF00">highlighted</span>
```
