<p align="center">
  <img src="https://raw.githubusercontent.com/TheEightBot/MintedTextEditor/main/images/logo.png" alt="MintedTextEditor logo" width="120" />
</p>

# MintedTextEditor

[![CI](https://github.com/TheEightBot/MintedTextEditor/actions/workflows/ci.yml/badge.svg)](https://github.com/TheEightBot/MintedTextEditor/actions/workflows/ci.yml)
[![NuGet (Core)](https://img.shields.io/nuget/v/MintedTextEditor.Core.svg?label=NuGet%20Core)](https://www.nuget.org/packages/MintedTextEditor.Core)
[![NuGet (MAUI)](https://img.shields.io/nuget/v/MintedTextEditor.Maui.svg?label=NuGet%20MAUI)](https://www.nuget.org/packages/MintedTextEditor.Maui)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/TheEightBot/MintedTextEditor/blob/main/LICENSE)

Rich text editing for .NET MAUI, rendered entirely on a SkiaSharp canvas.

MintedTextEditor is organized into three packages:
- `MintedTextEditor.Core`: document model, editing engine, layout, formatting, commands, events, HTML and Markdown interop.
- `MintedTextEditor.SkiaSharp`: drawing backend implementing `IDrawingContext`, with HarfBuzz text shaping.
- `MintedTextEditor.Maui`: MAUI control (`MintedEditorView`) and platform integration.

## Table of Contents

- [Highlights](#highlights)
- [Package Layout](#package-layout)
- [Platform Support](#platform-support)
- [Quick Start](#quick-start)
- [Sample App](#sample-app)
- [Configuration](#configuration)
- [Documentation](#documentation)
- [Build and Test](#build-and-test)
- [Contributing](#contributing)
- [Code of Conduct](#code-of-conduct)
- [License](#license)

## Highlights

- Full-canvas rendering using SkiaSharp (no native text editor controls).
- Character formatting: bold, italic, underline, strikethrough, subscript, superscript, font family/size, text color, highlight.
- Paragraph formatting: alignment, indentation, line spacing, headings, lists, block quote.
- Inline content: hyperlinks, images, tables.
- Editing primitives: caret navigation, selection, clipboard, undo/redo.
- Touch caret drag with a native-style magnifier (loupe), configurable or switchable off.
- Text shaped with HarfBuzz (kerning, ligatures, complex scripts); emoji, ZWJ sequences and flags draw as one glyph and are never split by the caret, Backspace, wrapping or hit-testing.
- Android soft-keyboard and IME support (composition, suggestions, dead keys on hardware keyboards).
- Toolbar and command architecture with customizable item sets.
- Three built-in toolbar icon packs (Lucide, Heroicons, Material Symbols) — all embedded in the `MintedTextEditor.Core` package, no app-bundle asset setup required.
- Theme-aware toolbar icons: automatically recolored to the current theme's foreground at draw time, including full dark-mode support.
- HTML and Markdown import/export pipelines, including support for GitHub Flavored Markdown (GFM).
- Theming support (light, dark, high contrast, and custom styles).
- Right-to-left paragraphs and replaceable UI strings (`EditorStrings`).

## Package Layout

```
src/
  MintedTextEditor.Core/
  MintedTextEditor.SkiaSharp/
  MintedTextEditor.Maui/
samples/
  SampleApp.Maui/
tests/
  MintedTextEditor.Core.Tests/
  MintedTextEditor.SkiaSharp.Tests/
```

## Platform Support

| Platform | Minimum Version |
|---|---|
| Android | API 21 (Android 5.0) |
| iOS | 15.0 |
| macOS (Mac Catalyst) | 15.0 |
| Windows | 10.0.17763 (the Windows target is only built on a Windows machine) |

## Quick Start

### 1) Install package

```bash
dotnet add package MintedTextEditor.Maui
```

This brings in `MintedTextEditor.Core`, `MintedTextEditor.SkiaSharp`, SkiaSharp 4.153.1 (`SkiaSharp.Views.Maui.Controls`), HarfBuzzSharp and Svg.Skia 5.2.3. The packages target .NET 10 / MAUI 10.

### 2) Register services

In `MauiProgram.cs`:

```csharp
using MintedTextEditor.Maui;

var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseMintedTextEditor();
```

### 3) Add control in XAML

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

### 4) Load or export HTML

```csharp
Editor.LoadHtml("<p>Hello <strong>world</strong></p>");
string html = Editor.GetHtml();
```

### 5) Load or export Markdown

```csharp
Editor.LoadMarkdown("# Hello World\nThis is a paragraph.");
string markdown = Editor.GetMarkdown();
```

For full import/export options, including GitHub Flavored Markdown (GFM) support, see [docs/markdown-interop.md](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/markdown-interop.md).

## Sample App

The repository includes a runnable MAUI sample with focused pages for:
- basic editing and read-only mode
- formatting
- load/save, HTML, and Markdown
- images and hyperlinks
- tables
- custom commands and a dynamic toolbar
- event monitoring
- localization
- theming and toolbar icon packs

Project: [samples/SampleApp.Maui](https://github.com/TheEightBot/MintedTextEditor/tree/main/samples/SampleApp.Maui)

## Configuration

### Theming

```csharp
using MintedTextEditor.Core.Theming;

Editor.Theme = EditorTheme.CreateDark();
Editor.UseSystemTheme = false;
```

### Toolbar icon packs

Three icon packs are included and embedded in `MintedTextEditor.Core` — no extra files are needed in the app project.

| Pack | Style |
|---|---|
| `ToolbarIconPack.Lucide` | Outlined, 2 px stroke (default) |
| `ToolbarIconPack.Heroicons` | Outlined, 1.5 px stroke |
| `ToolbarIconPack.MaterialSymbols` | Filled, Material Design paths |

```csharp
using MintedTextEditor.Core.Toolbar;

Editor.ToolbarIconPack = ToolbarIconPack.Lucide;          // default
Editor.ToolbarIconPack = ToolbarIconPack.Heroicons;
Editor.ToolbarIconPack = ToolbarIconPack.MaterialSymbols;
```

All icons are rendered with black ink and recolored at draw time using `SKColorFilter` (SrcIn) to match the current theme color. Switching between light and dark themes updates icon colors automatically without reloading assets.

### Markdown Interop

```csharp
using MintedTextEditor.Core.Markdown;

Editor.AppendMarkdown("## Subheading\nAnother paragraph.");

var markdown = Editor.GetMarkdown(new MarkdownExportOptions
{
    UseGfmExtensions = true, // GitHub Flavored Markdown: tables, ~~strikethrough~~ (default: true)
    LineEnding = "\r\n"      // default: "\n"
});
```

### Caret drag and magnifier

On touch screens, press and hold to pick up the caret and drag it; a magnifier shows the line under your finger.

```csharp
using MintedTextEditor.Core.Editing;

Editor.IsMagnifierEnabled = false;                      // turn it off
Editor.MagnifierOptions = MagnifierOptions.Android;     // or customise size, zoom, border, shadow
```

### Focus

```csharp
Editor.Focus();                      // focus the editor and show the soft keyboard
bool focused = Editor.IsEditorFocused;
Editor.EditorFocusChanged += (_, e) => { /* e.IsFocused */ };
```

`EditorFocusChanged` fires once per real native focus change, including a tap into the editor or focus moving to another control. The editor starts unfocused.

### Events

```csharp
Editor.TextChanged += (_, e) => { /* document changed */ };
Editor.SelectionChanged += (_, e) => { /* selection changed */ };
Editor.HyperlinkClicked += (_, e) => { /* e.Url; set e.Cancel to stop the default open */ };
```

For deeper API details, see [docs/getting-started.md](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/getting-started.md), [docs/commands-events.md](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/commands-events.md), and [docs/toolbar-customization.md](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/toolbar-customization.md).

## Documentation

| Document | Description |
|---|---|
| [Getting Started](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/getting-started.md) | Installation, setup, first editor |
| [Document Model](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/document-model.md) | Blocks, inlines, and text runs |
| [Formatting](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/formatting.md) | Character and paragraph formatting |
| [Toolbar Customization](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/toolbar-customization.md) | Toolbar groups and items |
| [Theming](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/theming.md) | Built-in and custom themes |
| [Commands & Events](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/commands-events.md) | Command and event reference |
| [HTML Interop](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/html-interop.md) | HTML import/export behavior |
| [Markdown Interop](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/markdown-interop.md) | Markdown import/export behavior |
| [Images & Hyperlinks](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/images-hyperlinks.md) | Inline media and links |
| [Tables](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/tables.md) | Table creation and editing |
| [Accessibility](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/accessibility.md) | Accessibility guidance |
| [Architecture](https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/architecture.md) | Internal architecture overview |

## Build and Test

```bash
git clone https://github.com/TheEightBot/MintedTextEditor.git
cd MintedTextEditor

# Build and test the platform-independent projects (any OS)
dotnet build src/MintedTextEditor.SkiaSharp/
dotnet test tests/MintedTextEditor.Core.Tests/
dotnet test tests/MintedTextEditor.SkiaSharp.Tests/

# Build MAUI project and sample app (needs the MAUI workload; iOS/Mac Catalyst need macOS)
dotnet workload install maui
dotnet build src/MintedTextEditor.Maui/
dotnet build samples/SampleApp.Maui/
```

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](https://github.com/TheEightBot/MintedTextEditor/blob/main/CONTRIBUTING.md) before opening a pull request.

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](https://github.com/TheEightBot/MintedTextEditor/blob/main/CODE_OF_CONDUCT.md).

## License

[MIT](https://github.com/TheEightBot/MintedTextEditor/blob/main/LICENSE)
