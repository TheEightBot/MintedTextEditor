# Theming

MintedTextEditor ships with three built-in themes and supports fully custom themes. A theme is an `EditorStyle` (a record in `MintedTextEditor.Core.Theming`); `EditorTheme` creates the built-in ones.

## Built-in Themes

| Theme | Factory | `EditorThemeMode` | Description |
|---|---|---|---|
| Light | `EditorTheme.CreateLight()` | `Light` | Light background, dark text (default) |
| Dark | `EditorTheme.CreateDark()` | `Dark` | Dark background, light text |
| High Contrast | `EditorTheme.CreateHighContrast()` | `HighContrast` | White text on black, yellow selection |

### Switching Themes

```csharp
using MintedTextEditor.Core.Theming;

editor.Theme = EditorTheme.CreateDark();
// or
editor.ThemeMode = EditorThemeMode.Dark;
```

In XAML:

```xml
<minted:MintedEditorView ThemeMode="Dark" />
```

## Custom Themes

`EditorStyle` is a record, so start from a built-in theme and override what you need with a `with` expression:

```csharp
using MintedTextEditor.Core.Rendering;

editor.Theme = EditorTheme.CreateDark() with
{
    // Canvas
    Background              = new EditorColor(30, 30, 30),

    // Text
    DefaultTextColor        = new EditorColor(212, 212, 212),

    // Selection
    SelectionHighlightColor = new EditorColor(64, 128, 208, 102),

    // Caret
    CaretColor              = new EditorColor(212, 212, 212),

    // Toolbar
    ToolbarBackground       = new EditorColor(37, 37, 38),
    ToolbarButtonColor      = new EditorColor(204, 204, 204),
    ToolbarActiveColor      = new EditorColor(9, 71, 113),
    ToolbarSeparatorColor   = new EditorColor(63, 63, 70),

    // Links
    HyperlinkColor          = new EditorColor(78, 201, 176),
};
```

Setting `Theme` turns `UseSystemTheme` off.

## EditorColor

`EditorColor` (namespace `MintedTextEditor.Core.Rendering`) is a struct of byte components:

```csharp
// From components (alpha defaults to 255)
var color = new EditorColor(51, 153, 255);
var translucent = new EditorColor(51, 153, 255, 128);

// Factory methods
var color2 = EditorColor.FromArgb(255, 51, 153, 255);
var color3 = EditorColor.FromRgb(51, 153, 255);

// Named colors, e.g. fully transparent
var transparent = EditorColor.Transparent;

uint argb = color.ToUint32(); // 0xAARRGGBB
```

## Responding to System Theme Changes

Set `UseSystemTheme = true` and the editor uses the built-in light or dark theme to match the app's current theme (`Application.Current.RequestedTheme`):

```csharp
editor.UseSystemTheme = true;
```

To follow the system with your own themes instead, subscribe to MAUI's `Application.RequestedThemeChanged` event:

```csharp
Application.Current!.RequestedThemeChanged += (s, e) =>
{
    editor.Theme = e.RequestedTheme == AppTheme.Dark
        ? myDarkTheme
        : myLightTheme;
};
```

## Theme Properties Reference

Properties the MAUI control currently uses:

| Property | Description |
|---|---|
| `Background` | Canvas background |
| `DefaultTextColor` | Default text color |
| `DefaultFontFamily` / `DefaultFontSize` / `Padding` | Font and position of the placeholder text |
| `SelectionHighlightColor` | Text selection fill |
| `CaretColor` / `CaretWidth` | Caret color and width |
| `HyperlinkColor` | Link text color |
| `ToolbarBackground` | Toolbar fill |
| `ToolbarButtonColor` | Toolbar icon/label color (icons are tinted with it) |
| `ToolbarActiveColor` | Toolbar button active/toggled fill |
| `ToolbarSeparatorColor` | Toolbar group separator and disabled items |
| `ScrollbarTrackColor` / `ScrollbarThumbColor` / `ScrollbarWidth` | Scrollbar |
| `LineNumbersGutterColor` / `LineNumbersTextColor` / `LineNumbersGutterWidth` | Line-number gutter (`ShowLineNumbers`) |

`EditorStyle` also defines `BorderColor`, `BorderWidth`, `SelectionTextColor`, `HyperlinkHoverColor`, `ToolbarHoverColor`, `ContextMenuBackground`, `ContextMenuTextColor`, `ContextMenuHoverColor`, `FocusRingColor`, `LineSpacing` and `ParagraphSpacing`, which the built-in themes set but the control does not draw with yet.
