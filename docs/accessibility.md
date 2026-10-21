# Accessibility

MintedTextEditor draws all of its content, including the toolbar, on a single SkiaSharp canvas. This page describes what is supported today and what is not.

## Screen Reader Support

The editor does not yet expose its content to screen readers (TalkBack, VoiceOver, Narrator): the text, caret, selection and toolbar buttons are pixels on a canvas, and the native accessibility tree contains only the canvas view and a hidden keyboard-input view.

### Customize Accessible Labels

`MintedEditorView` is a regular MAUI view, so you can describe it as a whole with MAUI's semantic properties:

```csharp
SemanticProperties.SetDescription(editor, "Main document editor");
SemanticProperties.SetHint(editor, "Double-tap to edit text");
```

`MintedTextEditor.Core.Accessibility.AccessibilityProperties` and the accessibility strings on `EditorStrings` (`EditorAccessibilityLabel`, `EditorAccessibilityHint`, `ToolbarAccessibilityLabel`, `ContextMenuAccessibilityLabel`) are building blocks for this; the control does not apply them yet.

## Keyboard-Only Operation

With a hardware keyboard, text can be edited without a pointer:

| Category | Notes |
|---|---|
| Caret navigation | Arrow keys, Home, End, Page Up/Down |
| Selection | Shift + any navigation key; Ctrl/Cmd+A selects all |
| Word navigation | Ctrl/Cmd + Left/Right |
| Select word | Ctrl/Cmd + Shift + Left/Right |
| Start/end of line | Home / End |
| Start/end of document | Ctrl/Cmd + Home / Ctrl/Cmd + End |
| Editing | Standard keys for insert, delete, backspace; Backspace/Delete remove a whole emoji or other grapheme cluster |
| Formatting | Ctrl/Cmd + B/I/U |
| Tables | Tab / Shift+Tab between cells; see [Tables](tables.md#keyboard-navigation) |

The toolbar has no keyboard focus; its actions are available as shortcuts above or through your own UI (see [Commands & Events](commands-events.md)).

## High Contrast Theme

MintedTextEditor ships with a built-in high contrast theme:

```csharp
editor.Theme = EditorTheme.CreateHighContrast();
// or
editor.ThemeMode = EditorThemeMode.HighContrast;
```

The high contrast theme:
- White text on black background
- Yellow selection highlight
- White caret, 3 units wide

It is not selected automatically; switch to it when the platform reports high contrast.

## RTL Text Support

Right-to-left direction is set per paragraph:

```csharp
// Set on the paragraphs in the selection
DocumentEditor.ApplyParagraphStyle(editor.Document, editor.Selection.Range,
    s => s.Direction = TextDirection.RightToLeft);
```

HTML `dir="rtl"` and `direction: rtl` are imported as well. In an RTL paragraph, indentation is taken from the right and `Left` alignment means the start of the line, so text is right-aligned. Text is shaped with HarfBuzz, which applies each script's shaping rules (for example Arabic joining), but glyphs are kept in logical order to match caret placement: bidirectional reordering is not implemented.

## Localization

UI strings are provided by `EditorStrings` (namespace `MintedTextEditor.Core.Localization`). Subclass it, override the strings you need, and set `EditorStrings.Current`:

```csharp
public class SpanishEditorStrings : EditorStrings
{
    public override string Bold => "Negrita";
    public override string Italic => "Cursiva";
}

EditorStrings.Current = new SpanishEditorStrings();
```

The editor reads `EditorStrings.Current` for its own text:

| Where | Strings | When it is read |
|---|---|---|
| Built-in toolbars (`ToolbarDefinition.CreateDefault()`, `CreateMinimal()`, `CreateFormattingOnly()`, `CreateInsertOnly()`) | Item labels: `Bold`, `FontFamily`, `InsertLink`, `HeadingLevel`, `EditMenu`, … | Every time `ToolbarItem.Label` is read, so on every draw |
| `MintedEditorView` toolbar menus | Font family "Default" entry (`DefaultFontFamily`), heading levels (`NormalText`, `Heading1`–`Heading6`), the Edit, Object and Table menu options (`Copy`, `RemoveImage`, `InsertTableEllipsis`, `DeleteRow`, …) | When the toolbar is set up, and again whenever `EditorStrings.Current` changes |
| `ContextMenuFactory.CreateDefault(ctx)` | Item labels: `Cut`, `SelectAll`, `EditLink`, `InsertRowAbove`, … | Each time the menu is created; `CreateDefault(ctx, strings)` takes the strings explicitly |
| `MintedEditorView` insert prompts | `HyperlinkDialog*`, `ImageDialog*`, `TableDialog*` | Each time a prompt is shown |

Setting `EditorStrings.Current` raises the static `EditorStrings.CurrentChanged` event; `MintedEditorView` listens to it and redraws, so switching language at runtime updates an editor that is already on screen.

Toolbar labels are drawn only when an item has no icon (or for a dropdown with nothing selected), so for icon buttons they mostly serve as names. A built-in item resolves its label through `ToolbarItem.LocalizedLabel` (for example `s => s.Bold`). Assigning `Label` clears `LocalizedLabel` and fixes the text; set `LocalizedLabel` on your own items to make them follow `EditorStrings` too:

```csharp
var button = new ToolbarButton { LocalizedLabel = s => s.ClearFormat, Command = new ClearFormattingCommand() };
```

Because the default English labels come from `EditorStrings`, find built-in toolbar items by `Icon` (for example `"bold"`) rather than by `Label` when the app may be localized.

`HyperlinkDialogUrlLabel`, `HyperlinkDialogTextLabel`, `HyperlinkDialogOk` and `HyperlinkDialogInvalidUrl` are for a custom hyperlink dialog; the built-in prompt does not use them. The accessibility strings are not applied yet (see [Customize Accessible Labels](#customize-accessible-labels)). The sample app's Localization page switches between English, Spanish and French.

## Platform-Specific Notes

### Android
- The soft keyboard appears when the editor is tapped or focused. Gboard and other IMEs (Samsung, MIUI) are supported, including composing text, suggestions, auto-capitalisation and whole-emoji delete.
- Hardware keyboards are supported, including dead keys (accents) on international layouts.

### iOS / Mac Catalyst
- Keyboard input goes through a hidden `UIKeyInput` view; navigation keys are handled with `UIKeyCommand`s.

### Windows
- Keyboard input goes through a hidden WinUI `TextBox`.
