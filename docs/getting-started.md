# Getting Started with MintedTextEditor

## Prerequisites

- .NET 10 SDK
- .NET MAUI workload (`dotnet workload install maui`)
- A .NET MAUI project targeting Android, iOS, macOS (Mac Catalyst), or Windows

## Installation

Install the MAUI package, which transitively includes `MintedTextEditor.Core` and `MintedTextEditor.SkiaSharp`:

```bash
dotnet add package MintedTextEditor.Maui
```

If you only need the rendering layer without MAUI:

```bash
dotnet add package MintedTextEditor.SkiaSharp
```

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

`UseMintedTextEditor()` registers:
- `IClipboardProvider` (system clipboard integration)
- `IHyperlinkProvider` (opens URLs via the platform browser)
- `IImageProvider` (loads images from file paths and streams)
- The SkiaSharp drawing engine

## Add the Editor to a Page

### XAML

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:minted="clr-namespace:MintedTextEditor.Maui;assembly=MintedTextEditor.Maui"
    x:Class="MyApp.MainPage">

    <minted:RichTextEditor
        x:Name="Editor"
        VerticalOptions="FillAndExpand"
        HorizontalOptions="FillAndExpand" />

</ContentPage>
```

### C#

```csharp
var editor = new RichTextEditor
{
    VerticalOptions = LayoutOptions.Fill,
    HorizontalOptions = LayoutOptions.Fill,
};

Content = editor;
```

## Import and Export HTML

```csharp
// Load content into the editor
editor.ImportHtml("<p>Hello <b>world</b>!</p>");

// Export the current document as HTML
string html = editor.ExportHtml();
```

## Responding to Changes

```csharp
editor.DocumentChanged += (sender, e) =>
{
    // Document was mutated — save, enable "Save" button, etc.
};

editor.SelectionChanged += (sender, e) =>
{
    // Update toolbar state, show cursor position, etc.
};
```

## Next Steps

- [Document Model](document-model.md) — understand how content is structured
- [Formatting](formatting.md) — apply character and paragraph styles programmatically
- [Toolbar Customization](toolbar-customization.md) — build a custom toolbar
- [Theming](theming.md) — switch themes or create a custom one
