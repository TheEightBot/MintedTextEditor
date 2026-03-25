# Images & Hyperlinks

## Images

### Inserting an Image

Images can be inserted from a file path, a stream, or raw bytes.

```csharp
// From file path (uses the registered IImageProvider)
await editor.InsertImageAsync("/path/to/photo.jpg");

// From a stream
await editor.InsertImageAsync(stream, width: 320, height: 240);

// From raw bytes
editor.InsertImage(imageBytes, displayWidth: 320, displayHeight: 240);
```

### ImageInline Model

All embedded images are represented in the document as `ImageInline`:

```csharp
var image = new ImageInline(bytes, displayWidth: 320, displayHeight: 240);
```

| Property | Description |
|---|---|
| `Data` | Raw image bytes (PNG, JPEG, WebP, etc.) |
| `DisplayWidth` | Rendered width in logical pixels |
| `DisplayHeight` | Rendered height in logical pixels |
| `AltText` | Accessible alternative text |

### Image Resizing

When the user selects an image and uses the resize handle, `DisplayWidth` and `DisplayHeight` are updated. To handle this in code:

```csharp
editor.ImageResized += (sender, e) =>
{
    Console.WriteLine($"Image resized to {e.Image.DisplayWidth}×{e.Image.DisplayHeight}");
};
```

### Providing a Custom Image Loader

If images are referenced by URL in imported HTML, implement `IImageProvider` to resolve and load them:

```csharp
public class MyImageProvider : IImageProvider
{
    public async Task<byte[]?> LoadAsync(string source)
    {
        using var client = new HttpClient();
        return await client.GetByteArrayAsync(source);
    }
}
```

Register it during setup:

```csharp
builder.Services.AddSingleton<IImageProvider, MyImageProvider>();
```

---

## Hyperlinks

### Inserting a Hyperlink

```csharp
// Insert a hyperlink at the caret or around the current selection
editor.InsertHyperlink("https://github.com/TheEightBot/MintedTextEditor", "MintedTextEditor");
```

### HyperlinkInline Model

```csharp
var link = new HyperlinkInline(displayText: "MintedTextEditor", url: "https://...");
```

| Property | Description |
|---|---|
| `DisplayText` | Visible link text |
| `Url` | The target URL |
| `Style` | `TextStyle` applied to the link text |

### Editing a Hyperlink

Position the caret inside an existing hyperlink and call:

```csharp
editor.EditHyperlink("https://new-url.example.com", "New Label");
```

### Removing a Hyperlink

```csharp
editor.RemoveHyperlink();   // converts the link to plain text
```

### Handling Clicks

```csharp
editor.HyperlinkActivated += async (sender, e) =>
{
    // Validate before opening
    if (Uri.TryCreate(e.Url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
    {
        await Browser.OpenAsync(e.Url, BrowserLaunchMode.SystemPreferred);
    }
};
```

> **Security note**: Always validate hyperlink URLs before navigating. Restrict allowed schemes and validate hostnames when appropriate for your use case.

### Custom Link Styling

Links inherit the theme's `HyperlinkColor` by default. Override per-document:

```csharp
editor.Theme = editor.Theme with
{
    HyperlinkColor = new EditorColor(0xFF00AA44),
};
```
