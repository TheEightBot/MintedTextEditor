# Theming

MintedTextEditor ships with three built-in themes and supports fully custom themes.

## Built-in Themes

| Theme | Constant | Description |
|---|---|---|
| Light | `EditorTheme.Light` | Light background, dark text (default) |
| Dark | `EditorTheme.Dark` | Dark background, light text |
| High Contrast | `EditorTheme.HighContrast` | Maximum contrast for accessibility |

### Switching Themes

```csharp
editor.Theme = EditorTheme.Dark;
```

In XAML:

```xml
<minted:RichTextEditor Theme="{x:Static minted:EditorTheme.Dark}" />
```

## Custom Themes

Create a custom `EditorTheme` by setting individual color properties:

```csharp
editor.Theme = new EditorTheme
{
    // Canvas
    Background           = new EditorColor(0xFF1E1E1E),

    // Text
    DefaultForeground    = new EditorColor(0xFFD4D4D4),
    HeadingForeground    = new EditorColor(0xFFFFFFFF),

    // Selection
    SelectionBackground  = new EditorColor(0x664080D0),
    SelectionForeground  = new EditorColor(0xFFFFFFFF),

    // Caret
    CaretColor           = new EditorColor(0xFFD4D4D4),

    // Toolbar
    ToolbarBackground    = new EditorColor(0xFF252526),
    ToolbarForeground    = new EditorColor(0xFFCCCCCC),
    ToolbarButtonHover   = new EditorColor(0xFF2D2D30),
    ToolbarButtonActive  = new EditorColor(0xFF094771),
    ToolbarSeparator     = new EditorColor(0xFF3F3F46),

    // Links
    HyperlinkColor       = new EditorColor(0xFF4EC9B0),

    // Block quote
    BlockQuoteBorder     = new EditorColor(0xFF569CD6),
    BlockQuoteBackground = new EditorColor(0xFF1A2433),
};
```

## EditorColor

`EditorColor` wraps a packed `uint` ARGB value (`0xAARRGGBB`):

```csharp
// From packed ARGB
var color = new EditorColor(0xFF3399FF);

// From components
var color2 = new EditorColor(alpha: 255, red: 51, green: 153, blue: 255);

// Fully transparent
var transparent = EditorColor.Transparent;
```

## Responding to System Theme Changes

Subscribe to MAUI's `Application.RequestedThemeChanged` event and update the editor theme accordingly:

```csharp
Application.Current!.RequestedThemeChanged += (s, e) =>
{
    editor.Theme = e.RequestedTheme == AppTheme.Dark
        ? EditorTheme.Dark
        : EditorTheme.Light;
};
```

## Theme Properties Reference

| Property | Description |
|---|---|
| `Background` | Canvas background |
| `DefaultForeground` | Default text color |
| `HeadingForeground` | Heading text color |
| `SelectionBackground` | Text selection fill |
| `SelectionForeground` | Text color within selection |
| `CaretColor` | Cursor caret color |
| `ToolbarBackground` | Toolbar fill |
| `ToolbarForeground` | Toolbar icon/label color |
| `ToolbarButtonHover` | Toolbar button hover fill |
| `ToolbarButtonActive` | Toolbar button pressed/active fill |
| `ToolbarSeparator` | Toolbar group separator |
| `HyperlinkColor` | Link text color |
| `BlockQuoteBorder` | Left border of block quotes |
| `BlockQuoteBackground` | Background of block quotes |
