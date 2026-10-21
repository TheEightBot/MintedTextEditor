# Document Model

MintedTextEditor uses a structured, tree-based document model. Understanding it helps you work with the API programmatically — for loading content, inspecting state, or building tooling on top of the editor.

## Overview

```
Document
└── Block[]
    ├── Paragraph
    │   └── Inline[]
    │       ├── TextRun          (styled text)
    │       ├── HyperlinkInline  (linked text; holds child inlines)
    │       ├── ImageInline      (embedded image)
    │       └── LineBreak        (soft line break)
    └── TableBlock
        └── TableRow[]
            └── TableCell[]
                └── Block[]  (paragraphs)
```

All model types live in the `MintedTextEditor.Core.Document` namespace.

## Document

`Document` is the root container. It holds an ordered list of `Block` objects (`Blocks`) and raises `Changed` after every mutation made through the editing APIs. A new `Document` contains one empty paragraph.

```csharp
var doc = editor.Document;
int blockCount = doc.BlockCount;
string plain = doc.GetText();   // blocks joined with "\n"
```

If you change the model directly (for example `para.Style.Alignment = ...`), call `doc.NotifyChanged(...)` so the layout is refreshed; the `DocumentEditor` and formatting engine methods do this for you.

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
        Console.WriteLine(para.GetText());
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
Console.WriteLine(style.ListType);     // None | Bullet | Number

// Plain text content
string text = para.GetText();
```

### ParagraphStyle Properties

| Property | Type | Description |
|---|---|---|
| `Alignment` | `TextAlignment` | Horizontal alignment |
| `HeadingLevel` | `int` | 0 = body text, 1–6 = H1–H6 |
| `ListType` | `ListType` | `None`, `Bullet`, or `Number` |
| `IndentLevel` | `int` | Indentation depth (0 = none) |
| `LineSpacing` | `float` | Line height multiplier (default 1). Once set, the theme's `LineSpacing` no longer applies to the paragraph |
| `SpaceBefore` | `float` | Extra space before paragraph (logical units) |
| `SpaceAfter` | `float` | Extra space after paragraph (default 8). Until set, the theme's `ParagraphSpacing` is used instead |
| `IsBlockQuote` | `bool` | Renders as a block quote |
| `Direction` | `TextDirection` | `LeftToRight` or `RightToLeft` |

`ParagraphStyle` is a mutable class; `Clone()` returns a copy.

## Inline

`Inline` is the base class for all content within a paragraph. Each inline has a `Length` (in UTF-16 code units; images and line breaks count as 1) and `GetText()`. Four concrete types:

### TextRun

A contiguous span of text with a single `TextStyle`.

```csharp
var run = new TextRun("Hello, ", new TextStyle(isBold: true));
Console.WriteLine(run.Text);     // "Hello, "
Console.WriteLine(run.Style.IsBold); // true
```

### HyperlinkInline

A clickable inline element that carries a URL (and optional `Title`) and holds its display content as child inlines.

```csharp
var link = new HyperlinkInline("https://github.com/TheEightBot/MintedTextEditor");
link.AddChild(new TextRun("MintedTextEditor"));
Console.WriteLine(link.Url);       // "https://github.com/TheEightBot/MintedTextEditor"
Console.WriteLine(link.GetText()); // "MintedTextEditor"
```

### ImageInline

An embedded image, referenced by a source string (URL, file path or app-package asset name) with alt text and optional display dimensions (0 = natural size).

```csharp
var image = new ImageInline("photo.png", altText: "A photo", width: 320, height: 240);
```

See [Images & Hyperlinks](images-hyperlinks.md) for how sources are loaded.

### LineBreak

A soft line break (`Shift+Enter`) within a paragraph, which wraps to a new visual line without starting a new paragraph.

```csharp
var lb = new LineBreak();
```

## TextStyle

`TextStyle` describes character-level formatting and is an **immutable class**. The `With…` methods (`WithBold`, `WithItalic`, `WithUnderline`, `WithStrikethrough`, `WithSubscript`, `WithSuperscript`, `WithFontFamily`, `WithFontSize`, `WithTextColor`, `WithHighlightColor`) return a new instance; styles compare by value.

```csharp
var style = TextStyle.Default;
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
| `FontFamily` | `string` | `"Default"` (platform default family) |
| `FontSize` | `float` | `14` |
| `TextColor` | `EditorColor` | `EditorColor.Black` (drawn in the theme's text color) |
| `HighlightColor` | `EditorColor` | `EditorColor.Transparent` (none) |
| `BaselineOffset` | `float` | `0` |

Subscript and superscript are mutually exclusive: `WithSubscript(true)` clears superscript and vice versa.

## DocumentEditor

`DocumentEditor` is a static class of low-level editing operations. They maintain the model's invariants, raise `Document.Changed`, and return a `DocumentPosition` for the resulting caret location. They are **not** recorded for undo; to make an edit undoable, push the matching action (`InsertTextAction`, `DeleteRangeAction`, `ApplyStyleAction`, … in `MintedTextEditor.Core.Editing`) to the `UndoManager`, which executes it.

```csharp
// Insert text at a position
var start = new DocumentPosition(blockIndex: 0, inlineIndex: 0, offset: 0);
var end = DocumentEditor.InsertText(doc, start, "Hello, world!");
var range = new TextRange(start, end);

// Apply character or paragraph formatting over a range
DocumentEditor.ApplyTextStyle(doc, range, s => s.WithBold(true));
DocumentEditor.ApplyParagraphStyle(doc, range, s => s.Alignment = TextAlignment.Center);

// Delete a range
DocumentEditor.DeleteRange(doc, range);

// Undoable equivalent
editor.UndoManager.Push(new InsertTextAction(doc, start, "Hello"));
```

Other members: `SplitBlock`, `MergeBlocks`, `GetSelectedText`, `NormalizePosition`.

## DocumentPosition

`DocumentPosition` is a struct that identifies a location within the document by block index, inline index, and offset within that inline. Positions inside a table cell also carry `CellRow` and `CellCol` (`IsInTableCell` is then `true`). `TextRange` pairs two positions and always stores them in order (`Start <= End`).

```csharp
var pos = new DocumentPosition(blockIndex: 0, inlineIndex: 0, offset: 5);
var selection = editor.Selection.Range;   // TextRange
```

## TableBlock

`TableBlock` contains a grid of rows (`Rows`) and cells (`TableRow.Cells`). Each `TableCell` holds a list of blocks (`Blocks`), normally paragraphs, so cells can contain formatted text.

See [Tables](tables.md) for the full table API.
