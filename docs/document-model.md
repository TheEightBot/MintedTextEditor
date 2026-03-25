# Document Model

MintedTextEditor uses a structured, tree-based document model. Understanding it helps you work with the API programmatically — for loading content, inspecting state, or building tooling on top of the editor.

## Overview

```
Document
└── Block[]
    ├── Paragraph
    │   └── Inline[]
    │       ├── TextRun          (styled text)
    │       ├── HyperlinkInline  (linked text)
    │       ├── ImageInline      (embedded image)
    │       └── LineBreak        (soft line break)
    └── TableBlock
        └── TableRow[]
            └── TableCell[]
                └── Paragraph[]
```

## Document

`Document` is the root container. It holds an ordered list of `Block` objects and owns the shared document state.

```csharp
var doc = editor.Document;
int blockCount = doc.Blocks.Count;
```

## Block

`Block` is the base class for block-level content. Two concrete types exist:

| Type | Description |
|---|---|
| `Paragraph` | A single paragraph of inline content |
| `TableBlock` | A table with rows and cells |

```csharp
foreach (Block block in doc.Blocks)
{
    if (block is Paragraph para)
        Console.WriteLine(para.PlainText);
    else if (block is TableBlock table)
        Console.WriteLine($"Table: {table.RowCount}×{table.ColumnCount}");
}
```

## Paragraph

`Paragraph` holds a list of `Inline` elements plus a `ParagraphStyle` that describes the block-level formatting.

```csharp
var para = (Paragraph)doc.Blocks[0];

// Block-level style
ParagraphStyle style = para.Style;
Console.WriteLine(style.Alignment);    // Left | Center | Right | Justify
Console.WriteLine(style.HeadingLevel); // 0 = body, 1–6 = heading level
Console.WriteLine(style.ListType);     // None | Bullet | Numbered

// Plain text content
string text = para.PlainText;
```

### ParagraphStyle Properties

| Property | Type | Description |
|---|---|---|
| `Alignment` | `TextAlignment` | Horizontal alignment |
| `HeadingLevel` | `int` | 0 = body text, 1–6 = H1–H6 |
| `ListType` | `ListType` | None, Bullet, or Numbered |
| `IndentLevel` | `int` | Indentation depth (0 = none) |
| `LineSpacing` | `float` | Line height multiplier |
| `SpaceBefore` | `float` | Extra space before paragraph (px) |
| `SpaceAfter` | `float` | Extra space after paragraph (px) |
| `IsBlockQuote` | `bool` | Renders as a block quote |
| `Direction` | `TextDirection` | LTR or RTL |

## Inline

`Inline` is the base class for all content within a paragraph. Four concrete types:

### TextRun

A contiguous span of text with a single `TextStyle`.

```csharp
var run = new TextRun("Hello, ", new TextStyle { IsBold = true });
Console.WriteLine(run.Text);     // "Hello, "
Console.WriteLine(run.Style.IsBold); // true
```

### HyperlinkInline

A clickable inline element that wraps a `TextRun` and carries a URL.

```csharp
var link = new HyperlinkInline("MintedTextEditor", "https://github.com/TheEightBot/MintedTextEditor");
Console.WriteLine(link.Url);   // "https://github.com/TheEightBot/MintedTextEditor"
```

### ImageInline

An embedded image, stored as raw bytes with optional display dimensions.

```csharp
var image = new ImageInline(imageBytes, width: 320, height: 240);
```

### LineBreak

A soft line break (`Shift+Enter`) within a paragraph, which wraps to a new visual line without starting a new paragraph.

```csharp
var lb = new LineBreak();
```

## TextStyle

`TextStyle` describes character-level formatting and is an **immutable value type**. All mutation returns a new instance.

```csharp
var style = new TextStyle();
var bold = style.WithBold(true);
var boldItalic = bold.WithItalic(true);
```

### TextStyle Properties

| Property | Type | Default |
|---|---|---|
| `IsBold` | `bool` | `false` |
| `IsItalic` | `bool` | `false` |
| `IsUnderline` | `bool` | `false` |
| `IsStrikethrough` | `bool` | `false` |
| `IsSubscript` | `bool` | `false` |
| `IsSuperscript` | `bool` | `false` |
| `FontFamily` | `string?` | `null` (system default) |
| `FontSize` | `float?` | `null` (theme default) |
| `ForegroundColor` | `EditorColor?` | `null` (theme default) |
| `BackgroundColor` | `EditorColor?` | `null` (none) |

## DocumentEditor

`DocumentEditor` is the high-level API for programmatic editing. It wraps a `Document` and exposes operations that correctly maintain invariants and return a `DocumentPosition` pointing to the resulting cursor location.

```csharp
var editor = new DocumentEditor(doc);

// Insert text at a position
var pos = new DocumentPosition(blockIndex: 0, inlineIndex: 0, charOffset: 0);
editor.Insert(pos, "Hello, world!");

// Delete a range of characters
editor.Delete(new TextRange(start, end));

// Apply character formatting over a range
editor.ApplyStyle(range, s => s.WithBold(true));
```

## DocumentPosition

`DocumentPosition` identifies a location within the document by block index, inline index, and character offset.

```csharp
var pos = new DocumentPosition(blockIndex: 0, inlineIndex: 0, charOffset: 5);
```

## TableBlock

`TableBlock` contains a rectangular grid of cells. Each cell contains one or more `Paragraph` blocks, allowing rich content within table cells.

See [docs/tables.md](tables.md) for the full table API.
