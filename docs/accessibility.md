# Accessibility

MintedTextEditor draws all of its content, including the toolbar, on a single SkiaSharp canvas. This page describes what is supported today and what is not.

## Screen Reader Support

Screen readers (TalkBack, VoiceOver, Narrator) see the editor as **one labelled element** (on Windows, its hidden input field; see [Activation](#activation)). They cannot yet read the document text, the caret position or the selection, and the toolbar's buttons are not individually reachable.

### What works

`MintedEditorView` sets MAUI's `SemanticProperties` on itself:

| Property | Value |
|---|---|
| `SemanticProperties.Description` | `EditorStrings.EditorAccessibilityLabel` ("Rich text editor"), then `EditorStrings.EditorReadOnlyAccessibilityState` ("read only") when `IsReadOnly` is true, then `PlaceholderText` while the document is empty. Parts are joined with ", ", e.g. "Rich text editor, read only" or "Rich text editor, Start typing…". |
| `SemanticProperties.Hint` | `EditorStrings.EditorAccessibilityHint` ("Double-tap to edit text"); no hint while read-only. |

- The values are updated when `IsReadOnly` or `PlaceholderText` changes and when the document becomes empty or non-empty.
- When `IsReadOnly` changes, the new state is announced with `SemanticScreenReader.Announce`: the new description, e.g. "Rich text editor, read only" or "Rich text editor".
- The SkiaSharp canvas is excluded from the accessibility tree (`AutomationProperties.IsInAccessibleTree = false`) so it does not show up as an unlabelled child. What happens to the hidden keyboard-input view depends on the platform (see [Activation](#activation)).
- Strings come from `EditorStrings.Current` (see [Localization](#localization)), read again on each of the updates above and when the view gets its handler. Assign a custom `EditorStrings` before the editor is created or shown.

The text is built by `MintedTextEditor.Core.Accessibility.EditorAccessibility.ForEditor(isReadOnly, isEmpty, placeholderText, strings, label)`, which returns an `AccessibilityProperties` (`Label`, `Hint`). You can call it to describe a custom host the same way.

### Your own labels

If you set `SemanticProperties.Description` or `SemanticProperties.Hint` on the editor yourself, the editor keeps your value and stops updating that property:

```csharp
SemanticProperties.SetDescription(editor, "Message body");
SemanticProperties.SetHint(editor, "Double-tap to write your message");
```

Read-only changes are still announced with your description: your description followed by `EditorReadOnlyAccessibilityState` (and the placeholder while the document is empty), e.g. "Message body, read only", and just "Message body" when it becomes editable again. Your description itself is not changed, so if you want the read-only state to be part of it, include it yourself.

### Activation

The editor does not add a gesture recognizer or a custom accessibility action. Activating it with a screen reader works as follows:

| Platform | Accessible element | What activating it does |
|---|---|---|
| Android (TalkBack) | `MintedEditorView` (the hidden keyboard-input view is excluded) | The view has no click action, so a double-tap makes Android send a tap to the centre of the element. That tap reaches the canvas like a finger tap: it places the caret at the point under the centre of the editor, focuses the editor and shows the soft keyboard. |
| iOS / Mac Catalyst (VoiceOver) | `MintedEditorView` (the hidden keyboard-input view is excluded) | The view does not override `accessibilityActivate`, so VoiceOver taps its activation point (the centre). Same result as on Android. |
| Windows (Narrator) | The hidden keyboard-input `TextBox`, which stays in the accessibility tree and gets the same description and hint (yours, if you set them on the editor) | Narrator's default action on the edit field, Tab, or a click gives it keyboard focus, which focuses the editor. `MintedEditorView` itself also carries the description. |

When the editor is read-only, touches are ignored and no hint is set; on Windows the input field can still take focus but rejects edits. This relies on the platforms' default screen-reader behaviour and has not been tested on devices for every platform and screen-reader version.

### What does not work yet

- **Document content**: the text, caret, selection, formatting, links, images and tables are drawn on a canvas and are not exposed. A screen reader announces only the description above, not what has been typed.
- **Toolbar**: the toolbar is drawn on the same canvas, so its buttons, dropdowns and colour pickers are not separate accessible elements and cannot be reached or activated with a screen reader. `EditorStrings.ToolbarAccessibilityLabel` and `ContextMenuAccessibilityLabel` are not applied. To make formatting accessible, hide the built-in toolbar (`ShowToolbar = false`) and build your own from native MAUI buttons that call the editor's commands (see [Commands & Events](commands-events.md)); keyboard shortcuts also work (below).
- **Activation position**: on Android and iOS, activating the editor places the caret at the centre of the editor, not where it was before (see [Activation](#activation)).
- `AccessibilityProperties.IsHidden` and `LiveRegion` are not used by the control.

Exposing the content properly needs a platform accessibility-node tree (an Android `AccessibilityNodeProvider`, iOS `UIAccessibilityElement`s / `UITextInput`, WinUI automation peers with a text pattern). That is planned follow-up work.

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
| `MintedEditorView` insert prompts | `HyperlinkDialog*`, `ImageDialog*`, `TableDialog*`, and `EditHyperlinkDialogTitle` / `EditHyperlinkDialogSave` for the context menu's edit-link prompt | Each time a prompt is shown |

Setting `EditorStrings.Current` raises the static `EditorStrings.CurrentChanged` event; `MintedEditorView` listens to it while loaded and redraws, so switching language at runtime updates an editor that is already on screen (an open context menu is closed; it shows the new labels when opened again). The event is static, so a subscriber stays alive until it unsubscribes; `EditorStrings.SubscribeCurrentChangedWeak(target, static t => ...)` listens without keeping `target` alive and returns an `IDisposable` that unsubscribes.

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
