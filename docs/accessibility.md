# Accessibility

MintedTextEditor is designed to be usable with assistive technologies across all supported platforms.

## Screen Reader Support

The editor exposes semantic accessibility information through the host platform's accessibility APIs:

- The editor canvas announces its role as a "text editor" to screen readers.
- The current text content and caret position are reported via `AccessibilityValue` and assistive technology notifications.
- Selection changes trigger accessibility announcements.
- Toolbar buttons have accessible labels and hints.

### Customize Accessible Labels

```csharp
editor.AccessibilityHint = "Main document editor";
```

Toolbar buttons use their `Label` property (set on `IEditorCommand.Label`) as the accessible name.

## Keyboard-Only Operation

The editor is fully operable without a pointer:

| Category | Notes |
|---|---|
| Caret navigation | Arrow keys, Home, End, Page Up/Down |
| Selection | Shift + any navigation key |
| Word navigation | Ctrl/Cmd + Left/Right |
| Select word | Ctrl/Cmd + Shift + Left/Right |
| Start/end of line | Home / End |
| Start/end of document | Ctrl/Cmd + Home / Ctrl/Cmd + End |
| Editing | Standard keys for insert, delete, backspace |
| Formatting | Ctrl/Cmd + B/I/U, etc. |
| Toolbar focus | F6 to move focus to toolbar; Enter to activate |

## High Contrast Theme

MintedTextEditor ships with a built-in high contrast theme:

```csharp
editor.Theme = EditorTheme.HighContrast;
```

The high contrast theme:
- White text on black background
- Yellow selection highlight
- White caret
- All interactive elements have a visible focus ring

## RTL Text Support

Right-to-left text is supported at the paragraph level:

```csharp
// Set via ParagraphFormattingEngine
editor.ParagraphFormattingEngine.SetDirection(TextDirection.RightToLeft);

// Or set directly on a paragraph
para.Style = para.Style with { Direction = TextDirection.RightToLeft };
```

The editor automatically mirrors paragraph layout and selection rendering for RTL paragraphs. Bidirectional (BIDI) text within a single paragraph is supported via the underlying SkiaSharp text shaping engine.

## Localization

MintedTextEditor's toolbar labels and dialog strings are loaded from a resource dictionary and can be replaced with localized versions:

```csharp
editor.StringProvider = new MyLocalizedStringProvider();
```

Implement `IEditorStringProvider`:

```csharp
public class MyLocalizedStringProvider : IEditorStringProvider
{
    public string GetString(string key)
        => Resources.ResourceManager.GetString(key) ?? key;
}
```

## Platform-Specific Notes

### Android
- TalkBack is supported via `AccessibilityNodeInfo` on the editor canvas view.
- Users can swipe to navigate by character and word.

### iOS / macOS
- VoiceOver is supported via `UIAccessibility` (iOS) and macOS Accessibility APIs.
- The editor responds to VoiceOver focus and reads paragraph content.

### Windows
- Narrator is supported via `AutomationPeer` on the backing `SKXamlCanvas`.
- High contrast detection via `AccessibilitySettings.HighContrast` automatically switches the theme.
