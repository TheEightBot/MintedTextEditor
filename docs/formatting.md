# Formatting

MintedTextEditor supports two levels of formatting: **character formatting** (applied to inline text runs) and **paragraph formatting** (applied to entire block-level elements).

All formatting APIs take the document and a `TextRange`. In the MAUI control, use `editor.Document` and `editor.Selection.Range`:

```csharp
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

var doc = editor.Document;
var range = editor.Selection.Range;
```

## Character Formatting

Character formatting is represented by `TextStyle`, an immutable class. All `With…` methods return a new `TextStyle` rather than mutating in place.

### Toggling via the FormattingEngine

The recommended way to apply character formatting is through `FormattingEngine` (`editor.Formatting`), which handles splitting and merging runs automatically. A toggle turns the format on unless the whole range already has it.

```csharp
var fmt = editor.Formatting;

fmt.ToggleBold(doc, range);
fmt.ToggleItalic(doc, range);
fmt.ToggleUnderline(doc, range);
fmt.ToggleStrikethrough(doc, range);
fmt.ToggleSubscript(doc, range);
fmt.ToggleSuperscript(doc, range);
fmt.ClearFormatting(doc, range);
```

With an empty range (a collapsed caret), toggles set a pending style that is applied to the next text the user types. The pending style starts from the formatting at the caret, so toggling Bold inside bold text turns bold off for the next text and keeps the other formatting.

Without a pending style, typed text inherits the formatting of the character before the caret (or the character after it at the start of a paragraph; an empty paragraph uses `TextStyle.Default`). Pressing Enter carries that formatting into the new paragraph, so typing after Enter at the end of bold text stays bold. Typing or pasting at the start or end of a hyperlink with nothing selected adds plain text next to it rather than extending the link; text that replaces a selection inside a link (typing over it, or find and replace) stays in the link. `DocumentEditor.GetInsertionStyle(doc, position)` returns that inherited style, and `FormattingEngine.GetEffectiveCaretStyle(doc, position, editor.Formatting.PendingStyle, editor.FontFormatting.PendingStyle)` returns the style the next typed text will get. The toolbar's Bold, Italic, Underline, Strikethrough, Subscript and Superscript buttons show that style when nothing is selected.

### Setting Font Properties

Font family, size and colors go through `FontFormattingEngine` (`editor.FontFormatting`):

```csharp
var font = editor.FontFormatting;

font.ApplyFontFamily(doc, range, "Georgia");
font.ApplyFontSize(doc, range, 18f);
font.ApplyTextColor(doc, range, new EditorColor(255, 0, 0));                 // red
font.ApplyHighlightColor(doc, range, new EditorColor(255, 255, 0, 64));      // semi-transparent yellow
font.RemoveHighlightColor(doc, range);
```

`EditorColor` takes `(r, g, b, a = 255)`; see [Theming](theming.md#editorcolor).

### Programmatic (DocumentEditor + UndoManager)

The engines above change the document directly and are not recorded for undo. For undo-aware programmatic formatting, push an `ApplyStyleAction`; `UndoManager.Push` executes it and records it:

```csharp
using MintedTextEditor.Core.Editing;

var action = new ApplyStyleAction(doc, range, s => s
    .WithBold(true)
    .WithFontSize(14f));

editor.UndoManager.Push(action);   // executes and records undo
```

### Querying Current Formatting

```csharp
TextStyle style = FontFormattingEngine.GetCurrentTextStyle(doc, editor.Selection.Active);
bool isBold = style.IsBold;
string family = style.FontFamily;

// Common style over a range, or null when the range is empty
TextStyle? common = FontFormattingEngine.GetTextStyleForRange(doc, range);
```

---

## Paragraph Formatting

Paragraph formatting is stored in `ParagraphStyle` on each `Paragraph` block. `ParagraphFormattingEngine` is a static class; each method applies to every paragraph the range touches.

### Setting Alignment

```csharp
ParagraphFormattingEngine.SetAlignment(doc, range, TextAlignment.Center);
ParagraphFormattingEngine.SetAlignment(doc, range, TextAlignment.Justify);
```

Available values: `Left` · `Center` · `Right` · `Justify`

### Headings

```csharp
ParagraphFormattingEngine.SetHeadingLevel(doc, range, 1);   // H1
ParagraphFormattingEngine.SetHeadingLevel(doc, range, 2);   // H2
ParagraphFormattingEngine.SetHeadingLevel(doc, range, 0);   // body text
```

### Lists

```csharp
ParagraphFormattingEngine.ToggleBulletList(doc, range);
ParagraphFormattingEngine.ToggleNumberList(doc, range);
ParagraphFormattingEngine.IncreaseIndent(doc, range);
ParagraphFormattingEngine.DecreaseIndent(doc, range);
```

### Indentation and Spacing

```csharp
ParagraphFormattingEngine.SetLineSpacing(doc, range, 1.5f);

// Other ParagraphStyle properties have no dedicated method; set them through DocumentEditor
DocumentEditor.ApplyParagraphStyle(doc, range, s =>
{
    s.IndentLevel = 2;
    s.SpaceBefore = 8f;
    s.SpaceAfter = 8f;
});
```

### Block Quote

```csharp
ParagraphFormattingEngine.SetParagraphFormat(doc, range, "Quote");   // also "Normal", "Heading1"–"Heading6"
```

### RTL Direction

```csharp
DocumentEditor.ApplyParagraphStyle(doc, range, s => s.Direction = TextDirection.RightToLeft);
```

---

## Undo / Redo Integration

Typing, deleting, pasting, and the insert commands (hyperlink, image, table, rows and columns) are recorded by the `UndoManager`. The formatting engines are not; use `ApplyStyleAction` (above) when formatting must be undoable.

```csharp
editor.Undo();   // or editor.UndoManager.Undo()
editor.Redo();
```

`editor.Undo()`/`Redo()` also move the caret to the restored position and raise `SelectionChanged` when it moves.

---

## Programmatic Style Inspection

```csharp
var doc = editor.Document;
var para = (Paragraph)doc.Blocks[0];

// Check inline runs
foreach (var inline in para.Inlines)
{
    if (inline is TextRun run)
        Console.WriteLine($"{run.Text}: bold={run.Style.IsBold}");
}

// Check paragraph style
Console.WriteLine(para.Style.Alignment);
Console.WriteLine(para.Style.HeadingLevel);
```
