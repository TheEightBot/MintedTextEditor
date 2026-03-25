# Commands & Events

## Command System

MintedTextEditor uses a command pattern (`IEditorCommand`) for all editor operations. Every toolbar button, menu item, and keyboard shortcut dispatches a command through the `CommandRegistry`.

### IEditorCommand

```csharp
public interface IEditorCommand
{
    string Id { get; }
    string Label { get; }
    bool CanExecute(EditorContext context);
    DocumentPosition Execute(EditorContext context);
}
```

### EditorContext

`EditorContext` is passed to every command and provides access to the full editor state:

| Property | Type | Description |
|---|---|---|
| `Document` | `Document` | The current document |
| `CaretPosition` | `DocumentPosition` | Current caret location |
| `Selection` | `TextRange?` | Current selection, or `null` |
| `UndoManager` | `UndoManager` | Push-able undo stack |
| `FormattingEngine` | `FormattingEngine` | Character formatting helpers |
| `ParagraphFormattingEngine` | `ParagraphFormattingEngine` | Block formatting helpers |
| `Clipboard` | `IClipboardProvider` | Read/write clipboard access |

### Executing a Command Programmatically

```csharp
editor.CommandRegistry.Execute("Bold");
editor.CommandRegistry.Execute("InsertHyperlink", new HyperlinkCommandArgs("https://example.com", "Example"));
```

### Registering a Custom Command

```csharp
editor.CommandRegistry.Register(new MyCustomCommand());
```

See [Toolbar Customization](toolbar-customization.md) for surfacing custom commands in the toolbar.

---

## Keyboard Bindings

Default bindings:

| Shortcut | Command |
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
| `Ctrl/Cmd+K` | Insert/Edit Hyperlink |
| `Tab` | Indent (in list) |
| `Shift+Tab` | Outdent (in list) |

### Adding Custom Key Bindings

```csharp
editor.KeyBindings.Add(new KeyBinding(Key.S, ModifierKeys.Control, "Save"));
```

---

## Events

### DocumentChanged

Raised after any mutation to the document content.

```csharp
editor.DocumentChanged += (sender, e) =>
{
    // e.ChangeKind: Insert | Delete | FormatApplied | StructureChanged
    SaveButton.IsEnabled = true;
};
```

### SelectionChanged

Raised when the caret moves or the selection range changes.

```csharp
editor.SelectionChanged += (sender, e) =>
{
    StatusLabel.Text = e.Selection is { } sel
        ? $"Selected: {sel.Length} chars"
        : $"Line {e.CaretPosition.BlockIndex + 1}";
};
```

### HyperlinkActivated

Raised when the user taps/clicks a hyperlink.

```csharp
editor.HyperlinkActivated += async (sender, e) =>
{
    await Browser.OpenAsync(e.Url, BrowserLaunchMode.SystemPreferred);
};
```

### ImageTapped

Raised when the user taps/clicks on an embedded image.

```csharp
editor.ImageTapped += (sender, e) =>
{
    // Show re-size / replace dialog, etc.
};
```

### UndoStackChanged

Raised when the undo stack changes — useful for updating undo/redo button states in a custom toolbar.

```csharp
editor.UndoStackChanged += (sender, e) =>
{
    UndoButton.IsEnabled = e.CanUndo;
    RedoButton.IsEnabled = e.CanRedo;
};
```
