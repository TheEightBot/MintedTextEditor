# Accessibility

MintedTextEditor draws all of its content, including the toolbar, on a single SkiaSharp canvas. This page describes what is supported today and what is not.

## Screen Reader Support

Screen readers (TalkBack, VoiceOver, Narrator) see the editor as **one labelled element**. They cannot yet read the document text, the caret position or the selection, and the toolbar's buttons are not individually reachable.

### What works

`MintedEditorView` sets MAUI's `SemanticProperties` on itself:

| Property | Value |
|---|---|
| `SemanticProperties.Description` | `EditorStrings.EditorAccessibilityLabel` ("Rich text editor"), then `EditorStrings.EditorReadOnlyAccessibilityState` ("read only") when `IsReadOnly` is true, then `PlaceholderText` while the document is empty. Parts are joined with ", ", e.g. "Rich text editor, read only" or "Rich text editor, Start typing…". |
| `SemanticProperties.Hint` | `EditorStrings.EditorAccessibilityHint` ("Double-tap to edit text"); no hint while read-only. |

- The values are updated when `IsReadOnly` or `PlaceholderText` changes and when the document becomes empty or non-empty.
- When `IsReadOnly` changes, the new description is announced with `SemanticScreenReader.Announce`.
- The SkiaSharp canvas and the hidden keyboard-input view are excluded from the accessibility tree (`AutomationProperties.IsInAccessibleTree = false`), so the editor shows up as a single element instead of unlabelled child views.
- Strings come from `EditorStrings.Current` (see [Localization](#localization)), read again on each of the updates above and when the view gets its handler. Assign a custom `EditorStrings` before the editor is created or shown.

The text is built by `MintedTextEditor.Core.Accessibility.EditorAccessibility.ForEditor(isReadOnly, isEmpty, placeholderText, strings)`, which returns an `AccessibilityProperties` (`Label`, `Hint`). You can call it to describe a custom host the same way.

### Your own labels

If you set `SemanticProperties.Description` or `SemanticProperties.Hint` on the editor yourself, the editor keeps your value and stops updating that property:

```csharp
SemanticProperties.SetDescription(editor, "Message body");
SemanticProperties.SetHint(editor, "Double-tap to write your message");
```

### What does not work yet

- **Document content**: the text, caret, selection, formatting, links, images and tables are drawn on a canvas and are not exposed. A screen reader announces only the description above, not what has been typed.
- **Toolbar**: the toolbar is drawn on the same canvas, so its buttons, dropdowns and colour pickers are not separate accessible elements and cannot be reached or activated with a screen reader. `EditorStrings.ToolbarAccessibilityLabel` and `ContextMenuAccessibilityLabel` are not applied. To make formatting accessible, hide the built-in toolbar (`ShowToolbar = false`) and build your own from native MAUI buttons that call the editor's commands (see [Commands & Events](commands-events.md)); keyboard shortcuts also work (below).
- **Activation**: what a double-tap (or Narrator's default action) does on the editor element is left to each platform's default handling of the view; it has not been verified on every platform that this focuses the editor and opens the keyboard.
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

The built-in toolbar labels and the insert prompts in `MintedEditorView` are currently fixed English text and do not read `EditorStrings`; use it for your own UI and custom toolbars (see the sample app's Localization page).

## Platform-Specific Notes

### Android
- The soft keyboard appears when the editor is tapped or focused. Gboard and other IMEs (Samsung, MIUI) are supported, including composing text, suggestions, auto-capitalisation and whole-emoji delete.
- Hardware keyboards are supported, including dead keys (accents) on international layouts.

### iOS / Mac Catalyst
- Keyboard input goes through a hidden `UIKeyInput` view; navigation keys are handled with `UIKeyCommand`s.

### Windows
- Keyboard input goes through a hidden WinUI `TextBox`.
