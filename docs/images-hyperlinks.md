# Images & Hyperlinks

## Images

### Inserting an Image

Images are referenced by a source string: an `http(s)://` URL, an absolute file path or `file://` URI, or the name of an app-package asset (a file in `Resources/Raw`). SVG sources are rasterized.

The toolbar's insert-image button raises `ImageRequested`; supply a source there, or the editor prompts the user for one:

```csharp
editor.ImageRequested += (sender, e) =>
{
    e.Source = "/path/to/photo.jpg";
    e.Handled = true;
};
```

To insert from code, use the undoable `InsertImageCommand` (namespace `MintedTextEditor.Core.Commands`) at the current selection:

```csharp
var ctx = new EditorContext(editor.Document, editor.Selection, editor.UndoManager,
    editor.Formatting, editor.FontFormatting);

new InsertImageCommand { Source = "https://example.com/photo.png", AltText = "A photo" }.Execute(ctx);
```

Or insert directly into the model with `ImageEngine` (not recorded for undo):

```csharp
using MintedTextEditor.Core.Formatting;

ImageEngine.InsertImage(editor.Document, editor.Selection.Active, "photo.png",
    altText: "A photo", width: 320, height: 240);
```

### ImageInline Model

All embedded images are represented in the document as `ImageInline`:

```csharp
var image = new ImageInline("photo.png", altText: "A photo", width: 320, height: 240);
```

| Property | Description |
|---|---|
| `Source` | URL, file path or asset name |
| `AltText` | Accessible alternative text |
| `Width` | Display width in logical pixels (0 = natural size) |
| `Height` | Display height in logical pixels (0 = natural size) |

### Image Resizing

Tap an image to select it, then drag one of its corner or edge handles to resize it; `Width` and `Height` are updated and `TextChanged` is raised. From code:

```csharp
ImageEngine.ResizeImage(editor.Document, image, width: 200, height: 150, maintainAspectRatio: true);
ImageEngine.ReplaceImage(editor.Document, image, "other.png");
ImageEngine.RemoveImage(editor.Document, image);
```

### Providing a Custom Image Loader

`MintedEditorView.ImageProvider` (type `MintedTextEditor.Core.Formatting.IImageProvider`) loads document images. The default, `MauiImageProvider`, handles app-package assets, file paths and HTTP(S) URLs, including SVG. Replace it to load from your own sources:

```csharp
using MintedTextEditor.Core.Formatting;
using SkiaSharp;

public class MyImageProvider : IImageProvider
{
    public async Task<object> LoadImageAsync(string source)
    {
        byte[] bytes = await MyStore.ReadAsync(source);
        return SKBitmap.Decode(bytes);   // the MAUI renderer uses SKBitmap results
    }
}

editor.ImageProvider = new MyImageProvider();
```

How the control uses it:

- Each source is loaded once, in the background, the first time it is drawn; a placeholder is shown until it arrives. The result is cached per source string.
- If the provider throws or returns something other than an `SKBitmap`, the editor falls back to its built-in loaders (app-package asset, then file path, then HTTP(S)).
- A bitmap returned by your provider is drawn as it is, so if you keep changing a mutable `SKBitmap` later, the changes show. Call `SetImmutable()` on bitmaps that never change so the renderer can cache them.

---

## Hyperlinks

### Inserting a Hyperlink

The toolbar's hyperlink button prompts for a URL. From code, use the undoable `InsertHyperlinkCommand`; selected text becomes the link text, otherwise the URL is inserted as the text:

```csharp
// ctx: an EditorContext built as shown under "Inserting an Image"
new InsertHyperlinkCommand { Url = "https://github.com/TheEightBot/MintedTextEditor" }.Execute(ctx);
```

`HyperlinkEngine.InsertHyperlink(doc, range, url, title)` does the same directly on the model, without undo.

### HyperlinkInline Model

```csharp
var link = new HyperlinkInline("https://...", title: "Optional tooltip");
link.AddChild(new TextRun("MintedTextEditor"));
```

| Property | Description |
|---|---|
| `Url` | The target URL |
| `Title` | Optional title (exported as the HTML `title` attribute) |
| `Children` | Inlines that make up the visible link text |

Links are drawn in the theme's `HyperlinkColor`.

### Editing a Hyperlink

```csharp
var link = HyperlinkEngine.GetHyperlinkAtPosition(editor.Document, editor.Selection.Active);
if (link is not null)
    HyperlinkEngine.EditHyperlink(editor.Document, link, "https://new-url.example.com");
```

### Removing a Hyperlink

The link's text stays in place with its character formatting (bold, colours, …). From code, use the undoable `RemoveHyperlinkCommand`, which acts on the link at the caret, or call the engine directly, without undo:

```csharp
new RemoveHyperlinkCommand().Execute(ctx);                                    // undoable
HyperlinkEngine.RemoveHyperlink(editor.Document, editor.Selection.Active);   // not recorded for undo
```

The toolbar's Object menu also has Open Link and Remove Link.

### Handling Clicks

Tapping a link raises `HyperlinkClicked`. The editor then opens the URL itself only if Ctrl/Cmd was held and the event was not cancelled, using `editor.HyperlinkHandler` (default `MauiHyperlinkHandler`, which uses the platform launcher). To open links on a plain tap, or to validate them first, handle the event:

```csharp
editor.HyperlinkClicked += async (sender, e) =>
{
    e.Cancel = true;   // don't let the editor open it

    // Validate before opening
    if (Uri.TryCreate(e.Url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
    {
        await Browser.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }
};
```

You can also replace `editor.HyperlinkHandler` with your own `IHyperlinkHandler` (`void OpenUrl(string url)`).

> **Security note**: Always validate hyperlink URLs before navigating. Restrict allowed schemes and validate hostnames when appropriate for your use case.

### Custom Link Styling

Links use the theme's `HyperlinkColor`. `EditorStyle` is a record, so override it with a `with` expression:

```csharp
editor.Theme = EditorTheme.CreateLight() with
{
    HyperlinkColor = new EditorColor(0, 170, 68),
};
```
