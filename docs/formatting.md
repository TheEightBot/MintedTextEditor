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

With an empty range (a collapsed caret), toggles set a pending style that is applied to the next text the user types.

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

The engines above change the document directly and are not recorded for undo. The toolbar, the keyboard shortcuts and the character-formatting commands (`ToggleBoldCommand` … `ClearFormattingCommand`, `ApplyFontFamilyCommand`, `ApplyFontSizeCommand`, `ApplyTextColorCommand`, `ApplyHighlightColorCommand`) are: each records one undo step. From code, run one of those commands with an `EditorContext` (see [Commands & Events](commands-events.md)), or push an `ApplyStyleAction`; `UndoManager.Push` executes it and records it:

```csharp
using MintedTextEditor.Core.Editing;

var action = new ApplyStyleAction(doc, range, s => s
    .WithBold(true)
    .WithFontSize(14f));

editor.UndoManager.Push(action);   // executes and records undo

// Or wrap an engine call, e.g. a toggle that decides on/off from the current text
editor.UndoManager.Push(new ApplyStyleAction(doc, range,
    r => editor.Formatting.ToggleBold(doc, r), "Bold"));
```

Undo restores the runs exactly as they were, styles included. Push an `ApplyStyleAction` only for a non-empty range; with a collapsed caret, call the engine directly to set the pending style.

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

The toolbar applies paragraph formatting through the paragraph commands (namespace `MintedTextEditor.Core.Commands`): `AlignLeftCommand`, `AlignCenterCommand`, `AlignRightCommand`, `AlignJustifyCommand`, `ToggleBulletListCommand`, `ToggleNumberListCommand`, `IncreaseIndentCommand`, `DecreaseIndentCommand`, `SetHeadingLevelCommand` and `SetParagraphFormatCommand`. These are recorded for undo: each run is one undo step covering every paragraph in the selection. The `ParagraphFormattingEngine` methods below change the document directly and are **not** recorded for undo.

```csharp
using MintedTextEditor.Core.Commands;

var ctx = new EditorContext(editor.Document, editor.Selection, editor.UndoManager,
    editor.Formatting, editor.FontFormatting);
new AlignCenterCommand().Execute(ctx);        // undoable
new SetHeadingLevelCommand(2).Execute(ctx);   // undoable
```

To make an engine call (or any other paragraph style change) undoable, wrap it in an `ApplyParagraphStyleAction`. It snapshots the style of each paragraph in the range before and after, so undo restores the old styles and redo reapplies the new ones:

```csharp
using MintedTextEditor.Core.Editing;

editor.UndoManager.Push(new ApplyParagraphStyleAction(doc, range,
    (d, r) => ParagraphFormattingEngine.SetLineSpacing(d, r, 1.5f)));
```

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

Typing, deleting, pasting, the insert commands (hyperlink, image, table, rows and columns), removing a hyperlink, the paragraph commands (alignment, lists, indent, headings, paragraph format), and character formatting from the toolbar (buttons, font family and size dropdowns, text and highlight colour pickers), keyboard shortcuts and commands are recorded by the `UndoManager`, one step per action. Toggling a format with a collapsed caret only sets the pending style and is not recorded. The formatting engines (`FormattingEngine`, `FontFormattingEngine`, `ParagraphFormattingEngine`) are not recorded; push an `ApplyStyleAction` or `ApplyParagraphStyleAction` (above) when formatting from code must be undoable.

```csharp
editor.Undo();   // or editor.UndoManager.Undo()
editor.Redo();
```

`editor.Undo()`/`Redo()` also move the caret to the restored position.

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
