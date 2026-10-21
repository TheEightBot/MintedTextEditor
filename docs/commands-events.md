# Commands & Events

## Command System

MintedTextEditor uses a command pattern (`IEditorCommand`, namespace `MintedTextEditor.Core.Commands`) for editor operations. Toolbar buttons hold an `IEditorCommand` and execute it when tapped; `EditorCommandRegistry` keeps commands by name.

### IEditorCommand

```csharp
public interface IEditorCommand
{
    string Name { get; }
    string Description { get; }
    void Execute(EditorContext context);
    bool CanExecute(EditorContext context);
}
```

Commands that only toggle or apply inline character formatting also implement the marker interface `ICharacterFormattingCommand`, so the editor keeps the selection stable around them.

### EditorContext

`EditorContext` is passed to every command and provides access to the editor state:

| Property | Type | Description |
|---|---|---|
| `Document` | `Document` | The current document |
| `Selection` | `Selection` | Current selection (`Anchor`, `Active`, `Range`, `IsEmpty`) |
| `UndoManager` | `UndoManager` | Undo stack; `Push` executes and records an action |
| `FormattingEngine` | `FormattingEngine` | Character formatting helpers |
| `FontFormattingEngine` | `FontFormattingEngine` | Font family, size and color helpers |
| `Clipboard` | `IClipboardProvider?` | Clipboard access, if available |

### Executing a Command Programmatically

Build an `EditorContext` from the control's public state and execute a command directly:

```csharp
using MintedTextEditor.Core.Commands;

var ctx = new EditorContext(
    editor.Document,
    editor.Selection,
    editor.UndoManager,
    editor.Formatting,
    editor.FontFormatting);

var link = new InsertHyperlinkCommand { Url = "https://example.com" };
if (link.CanExecute(ctx))
    link.Execute(ctx);
```

Or look commands up by name in a registry. `EditorCommandRegistry.CreateDefault()` contains all built-in commands (names are case-insensitive):

```csharp
var registry = EditorCommandRegistry.CreateDefault();
registry.Execute("ToggleBold", ctx);   // returns false if not found or CanExecute is false
```

Built-in command names: `ToggleBold`, `ToggleItalic`, `ToggleUnderline`, `ToggleStrikethrough`, `ToggleSubscript`, `ToggleSuperscript`, `ClearFormatting`, `AlignLeft`, `AlignCenter`, `AlignRight`, `AlignJustify`, `ToggleBulletList`, `ToggleNumberList`, `IncreaseIndent`, `DecreaseIndent`, `Undo`, `Redo`, `Copy`, `Cut`, `SelectAll`, `InsertHyperlink`, `RemoveHyperlink`, `OpenHyperlink`, `InsertImage`, `RemoveImage`, `InsertTable`, `InsertRowAbove`, `InsertRowBelow`, `DeleteRow`, `InsertColumnLeft`, `InsertColumnRight`, `DeleteColumn`, `DeleteTable`, `ApplyFontFamily`, `ApplyFontSize`, `ApplyTextColor`, `ApplyHighlightColor`. Commands that need input expose it as properties (e.g. `InsertHyperlinkCommand.Url`, `InsertTableCommand.Rows`/`Columns`, `ApplyFontFamilyCommand.FontFamily`).

### Registering a Custom Command

```csharp
var registry = EditorCommandRegistry.CreateDefault();
registry.Register(new MyCustomCommand());   // replaces any command with the same Name
```

The control does not expose a registry of its own. To surface a custom command in the toolbar, put it on a `ToolbarButton`; see [Toolbar Customization](toolbar-customization.md).

---

## Keyboard Bindings

Default bindings (`Ctrl` and `Cmd` are interchangeable):

| Shortcut | Action |
|---|---|
| `Ctrl/Cmd+B` | Bold |
| `Ctrl/Cmd+I` | Italic |
| `Ctrl/Cmd+U` | Underline |
| `Ctrl/Cmd+Z` | Undo |
| `Ctrl/Cmd+Y` / `Ctrl/Cmd+Shift+Z` | Redo |
| `Ctrl/Cmd+C` | Copy |
| `Ctrl/Cmd+X` | Cut |
| `Ctrl/Cmd+V` | Paste |
| `Ctrl/Cmd+A` | Select All |
| `Ctrl/Cmd+Left/Right` | Move by word (add `Shift` to select) |
| `Ctrl/Cmd+Home/End` | Start/end of document |
| `Tab` / `Shift+Tab` | Next/previous table cell; outside a table, `Tab` inserts a tab character |

Table shortcuts are listed in [Tables](tables.md#keyboard-navigation).

### Adding Custom Key Bindings

`EditorInputController.KeyBindings` holds `EditorKeyBinding` entries that are checked before the defaults. The MAUI control does not expose its input controller, so custom bindings apply when you host `EditorInputController` yourself:

```csharp
using MintedTextEditor.Core.Input;

inputController.KeyBindings.Add(new EditorKeyBinding(
    EditorKey.S,
    InputModifiers.Control,
    (controller, document, layout, drawingContext) =>
    {
        Save(document);
        return true;   // handled
    }));
```

---

## Events

`MintedEditorView` raises the following events. Event argument types are in `MintedTextEditor.Core.Events`.

### TextChanged

Raised after any change to the document content or formatting.

```csharp
editor.TextChanged += (sender, e) =>
{
    // e.AffectedRange: the TextRange that changed
    SaveButton.IsEnabled = true;
};
```

### SelectionChanged

Raised when the caret moves or the selection range changes.

```csharp
editor.SelectionChanged += (sender, e) =>
{
    StatusLabel.Text = e.IsEmpty
        ? $"Paragraph {e.Active.BlockIndex + 1}"
        : $"Selected from {e.Anchor} to {e.Active}";
};
```

### HyperlinkClicked

Raised when the user taps/clicks a hyperlink. Afterwards, if `Cancel` is not set and Ctrl/Cmd was held, the editor opens the URL through its `HyperlinkHandler`.

```csharp
editor.HyperlinkClicked += async (sender, e) =>
{
    e.Cancel = true;
    await Browser.OpenAsync(e.Url, BrowserLaunchMode.SystemPreferred);
};
```

### IsHyperlinkSelectedChanged

Raised when the caret or selection enters or leaves a hyperlink (`e.IsSelected`, `e.Url`).

### EditorFocusChanged

Raised once per native focus change (`e.IsFocused`), including a tap into the editor and focus moving to another control. Read the current state from `IsEditorFocused`.

### ImageRequested

Raised when the toolbar's insert-image button is tapped. Set `Source` and `Handled = true` to supply the image; otherwise the editor prompts for a URL or path.

```csharp
editor.ImageRequested += (sender, e) =>
{
    e.Source = "https://example.com/photo.png";
    e.Handled = true;
};
```

### ContentLoaded

Raised at the end of `LoadHtml` and `LoadMarkdown`, after the document has been replaced and the caret moved to the start. `TextChanged` is raised first, as for any change. `AppendHtml` and `AppendMarkdown` do not raise it.

```csharp
editor.ContentLoaded += (sender, e) => SaveButton.IsEnabled = false;
```

### IsReadOnlyChanged

Raised when the `IsReadOnly` property changes value (`e.IsReadOnly`), whether set from code or a binding.

### ImageInserted / ImageRemoved

Raised once per image after an edit adds images to the document or removes them: inserting from the toolbar or a command, pasting, deleting a selection, undo and redo. `ImageInserted` carries the `ImageInline` (`e.Image`); `ImageRemoved` carries its source (`e.Source`). Images in table cells and hyperlinks count too.

- They are raised once the edit has finished (on the next turn of the UI dispatcher), after `TextChanged`.
- Images that arrive with `LoadHtml` / `LoadMarkdown` are not reported: the loaded content is the new starting point.
- Undo and redo rebuild paragraphs, so a removed image and an added image with the same source in one edit count as the same image and raise nothing. Replacing an image with one from a different source raises both events, including when the source of an existing image is changed in place with `ImageEngine.ReplaceImage`: `ImageRemoved` carries the old source and `ImageInserted` the updated image.

```csharp
editor.ImageRemoved += (sender, e) => imageStore.Release(e.Source);
```

### Formatting state events

These events report the formatting at the caret or selection, for keeping a custom toolbar or status bar in sync. Each fires only when its value actually changes: after the caret or selection moves, after an edit, undo or redo, and after formatting is chosen from the toolbar or a shortcut (including formatting chosen with nothing selected, which applies to the next typed text).

| Event | Arguments | Fires when this changes |
|---|---|---|
| `FontFamilyChanged` | `FontFamily` | Font family |
| `FontSizeChanged` | `FontSize` | Font size |
| `FontAttributesChanged` | `IsBold`, `IsItalic`, `IsSubscript`, `IsSuperscript` | Any of the four |
| `TextDecorationsChanged` | `IsUnderline`, `IsStrikethrough` | Either of the two |
| `TextColorChanged` | `Color` | Text colour |
| `HighlightTextColorChanged` | `Color` | Highlight colour (transparent when none) |
| `TextFormattingChanged` | `Format`: `"Normal"`, `"Heading1"`–`"Heading6"` or `"Quote"` | Heading level or block quote of the paragraph |
| `HorizontalTextAlignmentChanged` | `Alignment` | Paragraph alignment |
| `ListTypeChanged` | `ListType` | Paragraph list type |

How the values are worked out (`FormattingState.Compute` in Core):

- **Caret**: from the formatting chosen for the next typed text if there is any, otherwise from the text at the caret (including text inside a hyperlink).
- **Selection**: bold, italic, underline, strikethrough, subscript and superscript are true only when they apply to all the selected text, including text in table cells and hyperlinks. Font family, size and colours are the selected text's common value, or the value of its first character when they are mixed.
- **Paragraph values** come from the paragraph at the start of the selection.

`editor.CurrentFormatting` returns the latest `FormattingState`, so you can read the initial values before any event has fired.

```csharp
editor.FontAttributesChanged += (sender, e) =>
{
    BoldButton.IsChecked = e.IsBold;
    ItalicButton.IsChecked = e.IsItalic;
};
editor.TextFormattingChanged += (sender, e) => HeadingPicker.SelectedItem = e.Format;
```

Caret and selection moves, typing, shortcuts and toolbar formatting raise these events straight away. Other document changes (undo, redo, changes made through the document or engine APIs) raise them once the change has finished, on the next turn of the UI dispatcher.

### UndoStackChanged

Raised by `editor.UndoManager` when the undo stack changes — useful for updating undo/redo button states in a custom toolbar.

```csharp
editor.UndoManager.UndoStackChanged += (sender, e) =>
{
    UndoButton.IsEnabled = editor.UndoManager.CanUndo;
    RedoButton.IsEnabled = editor.UndoManager.CanRedo;
};
```
