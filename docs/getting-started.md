# Getting Started with MintedTextEditor

## Prerequisites

- .NET 10 SDK
- .NET MAUI workload (`dotnet workload install maui`)
- A .NET MAUI 10 project targeting Android (API 21+), iOS (15.0+), Mac Catalyst (15.0+), or Windows (10.0.17763+)

## Installation

Install the MAUI package, which transitively includes `MintedTextEditor.Core` and `MintedTextEditor.SkiaSharp`:

```bash
dotnet add package MintedTextEditor.Maui
```

If you only need the rendering layer without MAUI:

```bash
dotnet add package MintedTextEditor.SkiaSharp
```

This references SkiaSharp 4.153.1 and HarfBuzzSharp 14.2.1.301. On desktop Linux, also add the native assets (`SkiaSharp.NativeAssets.Linux` and `HarfBuzzSharp.NativeAssets.Linux`, same versions), as the SkiaSharp test project does.

If you only need the platform-independent document model (e.g., for a server-side export pipeline):

```bash
dotnet add package MintedTextEditor.Core
```

## Register Services

In `MauiProgram.cs`, call `UseMintedTextEditor()` on the `MauiAppBuilder`:

```csharp
using MintedTextEditor.Maui;

var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseMintedTextEditor();

return builder.Build();
```

`UseMintedTextEditor()` calls `UseSkiaSharp()` and registers the handler for the hidden `KeyboardProxy` view that receives keyboard and IME input. It registers no DI services: each `MintedEditorView` creates its own MAUI clipboard provider, and exposes `HyperlinkHandler` (default `MauiHyperlinkHandler`, opens URLs with the platform launcher) and `ImageProvider` (default `MauiImageProvider`) as properties you can replace.

## Add the Editor to a Page

### XAML

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:minted="https://schemas.mintedtexteditor.com/2026"
    x:Class="MyApp.MainPage">

    <minted:MintedEditorView
        x:Name="Editor"
        ShowToolbar="True"
        PlaceholderText="Start typing..." />

</ContentPage>
```

The `https://schemas.mintedtexteditor.com/2026` namespace maps to `MintedTextEditor.Maui`; `clr-namespace:MintedTextEditor.Maui;assembly=MintedTextEditor.Maui` also works.

### C#

```csharp
using MintedTextEditor.Maui;

var editor = new MintedEditorView
{
    VerticalOptions = LayoutOptions.Fill,
    HorizontalOptions = LayoutOptions.Fill,
};

Content = editor;
```

## Common Properties

| Property | Default | Description |
|---|---|---|
| `ShowToolbar` | `true` | Show the built-in toolbar |
| `IsReadOnly` | `false` | Disable editing |
| `PlaceholderText` | `"Start typing…"` | Shown when the document is empty |
| `IsWordWrap` | `true` | Wrap lines at the view width |
| `ShowLineNumbers` | `false` | Show a line-number gutter |
| `ShowScrollbar` | `true` | Show the vertical scrollbar |
| `ShowFocusRing` | `true` | Draw a ring in the theme's `FocusRingColor` around the editor while it has focus |
| `IsMagnifierEnabled` | `true` | Show the magnifier while dragging the caret |

See [Theming](theming.md) and [Toolbar Customization](toolbar-customization.md) for the rest.

## Import and Export HTML and Markdown

```csharp
// Load content into the editor (replaces the document)
editor.LoadHtml("<p>Hello <b>world</b>!</p>");
editor.LoadMarkdown("Hello **world**!");

// Export the current document
string html = editor.GetHtml();
string markdown = editor.GetMarkdown();
```

## Responding to Changes

```csharp
editor.TextChanged += (sender, e) =>
{
    // Document was mutated — save, enable "Save" button, etc.
};

editor.SelectionChanged += (sender, e) =>
{
    // Update toolbar state, show cursor position, etc.
};
```

## Focus

The editor starts unfocused, with no caret. It takes focus when the user taps into it or when you call `Focus()`, which also shows the soft keyboard. `Unfocus()` removes native focus and hides the keyboard.

```csharp
editor.EditorFocusChanged += (sender, e) =>
{
    Console.WriteLine(e.IsFocused ? "focused" : "unfocused");
};

bool hasFocus = editor.IsEditorFocused;
```

`EditorFocusChanged` is raised once per real native focus change, including focus moving to another control such as an `Entry`. The caret blinks only while the editor is focused.

## Caret Drag and Magnifier

On touch screens, press and hold to pick up the caret, then drag to move it. While dragging, a magnifier (loupe) shows the line under your finger. By default it looks like the platform's native magnifier (`MagnifierOptions.Android` on Android, `MagnifierOptions.Apple` elsewhere), and you can change its look or turn it off:

```csharp
using MintedTextEditor.Core.Editing;

editor.IsMagnifierEnabled = true;          // default
editor.MagnifierOptions = new MagnifierOptions
{
    Width = 80, Height = 80, CornerRadius = 40,  // a round loupe
    Zoom = 1.5f,
};
// Or start from a preset: MagnifierOptions.Apple / MagnifierOptions.Android
```

`MagnifierOptions` also has `VerticalOffset` (distance above the finger), `BorderColor`, `BorderWidth`, `ShadowColor` and `ShadowOffset`. Sizes are in logical units.

## Next Steps

- [Document Model](document-model.md) — understand how content is structured
- [Formatting](formatting.md) — apply character and paragraph styles programmatically
- [Toolbar Customization](toolbar-customization.md) — build a custom toolbar
- [Theming](theming.md) — switch themes or create a custom one
- [HTML Interop](html-interop.md) and [Markdown Interop](markdown-interop.md) — load and save content
