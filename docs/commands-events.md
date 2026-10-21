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

Raised when the caret moves or the selection range changes, including when `Undo()`/`Redo()` move the caret and after `LoadHtml`/`LoadMarkdown` reset it.

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

### UndoStackChanged

Raised by `editor.UndoManager` when the undo stack changes — useful for updating undo/redo button states in a custom toolbar.

```csharp
editor.UndoManager.UndoStackChanged += (sender, e) =>
{
    UndoButton.IsEnabled = editor.UndoManager.CanUndo;
    RedoButton.IsEnabled = editor.UndoManager.CanRedo;
};
```
