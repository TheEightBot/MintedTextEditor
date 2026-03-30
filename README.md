<p align="center">
  <img src="images/logo.png" alt="MintedTextEditor logo" width="120" />
</p>

# MintedTextEditor

[![CI](https://github.com/TheEightBot/MintedTextEditor/actions/workflows/ci.yml/badge.svg)](https://github.com/TheEightBot/MintedTextEditor/actions/workflows/ci.yml)
[![NuGet (Core)](https://img.shields.io/nuget/v/MintedTextEditor.Core.svg?label=NuGet%20Core)](https://www.nuget.org/packages/MintedTextEditor.Core)
[![NuGet (MAUI)](https://img.shields.io/nuget/v/MintedTextEditor.Maui.svg?label=NuGet%20MAUI)](https://www.nuget.org/packages/MintedTextEditor.Maui)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Rich text editing for .NET MAUI, rendered entirely on a SkiaSharp canvas.

MintedTextEditor is organized into three packages:
- `MintedTextEditor.Core`: document model, editing engine, layout, formatting, commands, events, HTML interop.
- `MintedTextEditor.SkiaSharp`: drawing backend implementing `IDrawingContext`.
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
- Toolbar and command architecture with customizable item sets.
- Three built-in toolbar icon packs (Lucide, Heroicons, Material Symbols) — all embedded in the `MintedTextEditor.Core` package, no app-bundle asset setup required.
- Theme-aware toolbar icons: automatically recolored to the current theme's foreground at draw time, including full dark-mode support.
- HTML and Markdown import/export pipelines, including support for GitHub Flavored Markdown (GFM).
- Theming support (light, dark, high contrast, and custom styles).
- Accessibility and localization support including RTL scenarios.

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
```

## Platform Support

| Platform | Minimum Version |
|---|---|
| Android | API 21 (Android 5.0) |
| iOS | 15.0 |
| macOS (Mac Catalyst) | 15.0 |
| Windows | 10.0.17763 |

## Quick Start

### 1) Install package

```bash
dotnet add package MintedTextEditor.Maui
```

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
using MintedTextEditor.Core.Markdown;

// Load a document from Markdown
var document = EditorDocumentExtensions.LoadMarkdown("# Hello World\nThis is a paragraph.");

// Export the current document to a Markdown string
var markdown = editor.Document.GetMarkdown();
```

For full import/export options, including GitHub Flavored Markdown (GFM) support, see [docs/markdown-interop.md](docs/markdown-interop.md).

## Sample App

The repository includes a runnable MAUI sample with focused pages for:
- basic editing
- formatting
- load/save, HTML, and Markdown
- images and hyperlinks
- tables
- localization
- theming and toolbar icon packs

Project: [samples/SampleApp.Maui](samples/SampleApp.Maui)

## Configuration

### Theming

```csharp
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
Editor.ToolbarIconPack = ToolbarIconPack.Lucide;          // default
Editor.ToolbarIconPack = ToolbarIconPack.Heroicons;
Editor.ToolbarIconPack = ToolbarIconPack.MaterialSymbols;
```

All icons are rendered with black ink and recolored at draw time using `SKColorFilter` (SrcIn) to match the current theme color. Switching between light and dark themes updates icon colors automatically without reloading assets.

### Markdown Interop

#### Import Markdown
You can load Markdown content into the editor using the `LoadMarkdown` or `AppendMarkdown` methods:
```csharp
using MintedTextEditor.Core.Markdown;

// Load a new document from Markdown
var document = EditorDocumentExtensions.LoadMarkdown("# Hello World\nThis is a paragraph.");

// Append Markdown to an existing document
editor.Document.AppendMarkdown("## Subheading\nAnother paragraph.");
```

#### Export Markdown
Export the current document to a Markdown string:
```csharp
using MintedTextEditor.Core.Markdown;

var markdown = editor.Document.GetMarkdown();
```

#### Export Options
Customize the Markdown export behavior using the `MarkdownExportOptions` class:
```csharp
var options = new MarkdownExportOptions
{
    UseGfmExtensions = true, // Enable GitHub Flavored Markdown (default: true)
    LineEnding = "\r\n"      // Customize line endings (default: "\n")
};
var markdown = editor.Document.GetMarkdown(options);
```

### Events

```csharp
Editor.TextChanged += (_, e) => { /* document changed */ };
Editor.SelectionChanged += (_, e) => { /* selection changed */ };
Editor.HyperlinkClicked += (_, e) => { /* inspect/cancel open */ };
```

For deeper API details, see [docs/getting-started.md](docs/getting-started.md), [docs/commands-events.md](docs/commands-events.md), and [docs/toolbar-customization.md](docs/toolbar-customization.md).

## Documentation

| Document | Description |
|---|---|
| [Getting Started](docs/getting-started.md) | Installation, setup, first editor |
| [Document Model](docs/document-model.md) | Blocks, inlines, and text runs |
| [Formatting](docs/formatting.md) | Character and paragraph formatting |
| [Toolbar Customization](docs/toolbar-customization.md) | Toolbar groups and items |
| [Theming](docs/theming.md) | Built-in and custom themes |
| [Commands & Events](docs/commands-events.md) | Command and event reference |
| [HTML Interop](docs/html-interop.md) | HTML import/export behavior |
| [Markdown Interop](docs/markdown-interop.md) | Markdown import/export behavior |
| [Images & Hyperlinks](docs/images-hyperlinks.md) | Inline media and links |
| [Tables](docs/tables.md) | Table creation and editing |
| [Accessibility](docs/accessibility.md) | Accessibility guidance |
| [Architecture](docs/architecture.md) | Internal architecture overview |

## Build and Test

```bash
git clone https://github.com/TheEightBot/MintedTextEditor.git
cd MintedTextEditor

# Build solution
dotnet build

# Run core tests
dotnet test tests/MintedTextEditor.Core.Tests/

# Build MAUI project (after installing MAUI workload)
dotnet workload install maui
dotnet build src/MintedTextEditor.Maui/

# Build sample app
dotnet build samples/SampleApp.Maui/
```

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request.

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).

## License

[MIT](LICENSE)
