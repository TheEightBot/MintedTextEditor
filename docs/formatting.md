# Formatting

MintedTextEditor supports two levels of formatting: **character formatting** (applied to inline text runs) and **paragraph formatting** (applied to entire block-level elements).

## Character Formatting

Character formatting is represented by `TextStyle`, an immutable value type. All "with" methods return a new `TextStyle` rather than mutating in place.

### Toggling via the FormattingEngine

The recommended way to apply character formatting is through `FormattingEngine`, which handles splitting and merging runs automatically.

```csharp
// Get the engine from the editor
var fmt = editor.FormattingEngine;

// Toggle bold across the current selection
fmt.ToggleBold();
fmt.ToggleItalic();
fmt.ToggleUnderline();
fmt.ToggleStrikethrough();
fmt.ToggleSubscript();
fmt.ToggleSuperscript();
```

### Setting Font Properties

```csharp
fmt.SetFontFamily("Georgia");
fmt.SetFontSize(18f);
fmt.SetForegroundColor(new EditorColor(0xFFFF0000));   // red
fmt.SetBackgroundColor(new EditorColor(0x40FFFF00));  // semi-transparent yellow highlight
```

### Programmatic (DocumentEditor + UndoManager)

For undo-aware programmatic formatting:

```csharp
var action = new ApplyStyleAction(doc, selectedRange, s => s
    .WithBold(true)
    .WithFontSize(14f));

undoManager.Push(action);   // executes and records undo
```

### Querying Current Formatting

```csharp
TextStyle style = fmt.GetStyleAtCaret();
bool isBold = style.IsBold;
string? family = style.FontFamily;
```

---

## Paragraph Formatting

Paragraph formatting is stored in `ParagraphStyle` on each `Paragraph` block.

### Setting Alignment

```csharp
var paragraphFmt = editor.ParagraphFormattingEngine;

paragraphFmt.SetAlignment(TextAlignment.Center);
paragraphFmt.SetAlignment(TextAlignment.Justify);
```

Available values: `Left` · `Center` · `Right` · `Justify`

### Headings

```csharp
paragraphFmt.SetHeadingLevel(1);   // H1
paragraphFmt.SetHeadingLevel(2);   // H2
paragraphFmt.SetHeadingLevel(0);   // body text
```

### Lists

```csharp
paragraphFmt.ToggleBulletList();
paragraphFmt.ToggleNumberedList();
paragraphFmt.Indent();
paragraphFmt.Outdent();
```

### Indentation and Spacing

```csharp
paragraphFmt.SetIndentLevel(2);
paragraphFmt.SetLineSpacing(1.5f);
paragraphFmt.SetSpaceBefore(8f);
paragraphFmt.SetSpaceAfter(8f);
```

### Block Quote

```csharp
paragraphFmt.ToggleBlockQuote();
```

### RTL Direction

```csharp
paragraphFmt.SetDirection(TextDirection.RightToLeft);
```

---

## Undo / Redo Integration

All formatting operations performed through `FormattingEngine` and `ParagraphFormattingEngine` are automatically pushed to the `UndoManager`.

```csharp
editor.UndoManager.Undo();
editor.UndoManager.Redo();
```

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
