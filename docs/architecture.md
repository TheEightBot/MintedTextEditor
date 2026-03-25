# Architecture

MintedTextEditor is structured in three layers, each with a clear responsibility boundary. Lower layers have no dependency on higher layers.

```
┌──────────────────────────────────────────────────────────────────┐
│                      MintedTextEditor.Maui                        │
│                                                                    │
│  RichTextEditor (MAUI View)                                        │
│  ├─ SKCanvasView host                                              │
│  ├─ KeyboardProxy (platform-specific key event bridge)            │
│  ├─ MAUI DI registration (UseMintedTextEditor extension)          │
│  ├─ Clipboard, Hyperlink, Image providers (MAUI implementations)  │
│  └─ MintedTextEditorHostingExtensions                             │
├──────────────────────────────────────────────────────────────────┤
│                   MintedTextEditor.SkiaSharp                      │
│                                                                    │
│  SkiaSharpEngine : IDrawingEngine                                  │
│  SkiaDrawingContext : IDrawingContext                               │
│  ├─ SKPaint / SKFont pool (per-frame cache)                       │
│  ├─ Text shaping (SKTextBlob + HarfBuzz via SkiaSharp)            │
│  └─ Image decoding (SKBitmap)                                     │
├──────────────────────────────────────────────────────────────────┤
│                    MintedTextEditor.Core                           │
│                                                                    │
│  Document Model                                                    │
│  ├─ Document, Block, Paragraph, TableBlock                        │
│  ├─ Inline, TextRun, HyperlinkInline, ImageInline, LineBreak      │
│  ├─ TextStyle (immutable, value-type character formatting)         │
│  └─ ParagraphStyle (immutable, block-level formatting)            │
│                                                                    │
│  Editing                                                           │
│  ├─ DocumentEditor (structural mutations)                          │
│  ├─ UndoManager (command stack with merge support)                │
│  └─ SelectionManager (caret + range selection)                    │
│                                                                    │
│  Formatting                                                        │
│  ├─ FormattingEngine (character formatting, run splitting/merging) │
│  └─ ParagraphFormattingEngine (block-level formatting)            │
│                                                                    │
│  Layout                                                            │
│  ├─ TextLayoutEngine (line breaking, run shaping, box model)       │
│  ├─ LayoutCache (invalidation + incremental re-layout)            │
│  └─ DocumentPosition / HitTest (pixel ↔ DocumentPosition)        │
│                                                                    │
│  Commands                                                          │
│  ├─ IEditorCommand / CommandRegistry                              │
│  ├─ EditCommands (insert, delete, clipboard)                       │
│  ├─ FormattingCommands (bold, italic, font …)                      │
│  ├─ ParagraphCommands (align, list, indent …)                      │
│  └─ HyperlinkCommands (insert, edit, remove)                      │
│                                                                    │
│  HTML I/O                                                          │
│  ├─ HtmlSerializer (Document → HTML string)                       │
│  └─ HtmlParser (HTML string → Document)                           │
│                                                                    │
│  Rendering                                                         │
│  ├─ IDrawingContext (platform-independent drawing API)            │
│  └─ DocumentRenderer (walk layout → IDrawingContext calls)         │
└──────────────────────────────────────────────────────────────────┘
```

## Layer Details

### MintedTextEditor.Core

The core layer is **fully platform-independent** — it targets `net10.0` only and has no dependency on MAUI, SkiaSharp, or any platform SDK. This makes it testable with a mock `IDrawingContext` in a plain xUnit project.

**Document Model** is immutable-style: `TextStyle` and `ParagraphStyle` are record-like structs that return new instances from "with" methods, making undo simple (store the preceding state).

**TextLayoutEngine** converts a `Document` into a flat list of `LayoutLine` records. Each `LayoutLine` contains a list of `LayoutRun` (a positioned, shaped run of glyphs or an image placeholder). Hit testing maps a point to a `DocumentPosition`; the inverse maps a `DocumentPosition` to a pixel rectangle.

**UndoManager** implements a push/pop stack of `IDocumentOperation` entries. Operations that affect the same caret position within a short debounce window are merged into a single undo step.

**CommandRegistry** is a string-keyed dictionary of `IEditorCommand`. Each command is stateless; all mutable state lives in `EditorContext` which is passed at execution time.

### MintedTextEditor.SkiaSharp

This layer provides the `IDrawingContext` implementation backed by SkiaSharp. It targets `net10.0` and has a dependency on `SkiaSharp`.

**SkiaDrawingContext** wraps an `SKCanvas` and translates drawing calls (draw text run, draw rect, draw image, clip) into `SKCanvas` API calls. It maintains a pool of `SKPaint` and `SKFont` objects keyed by `TextStyle` to avoid per-frame GC pressure.

**SkiaSharpEngine** manages the SkiaSharp lifecycle — surface creation, invalidation, and frame rendering. It is given a `DocumentRenderer` and a `LayoutCache`; on each frame it calls `DocumentRenderer.Render(drawingContext)`.

### MintedTextEditor.Maui

The MAUI layer wraps `SkiaSharpEngine` in a `SKCanvasView` and bridges platform input events.

**RichTextEditor** is the public `View` type. It exposes bindable properties (`Theme`, `IsReadOnly`, `ShowToolbar`, `ToolbarDefinition`) and events (`DocumentChanged`, `SelectionChanged`, `HyperlinkActivated`, `ImageTapped`, `UndoStackChanged`).

**KeyboardProxy** receives `KeyboardAccelerator` (Windows) / `UIKeyCommand` (iOS/macOS) / `KeyEvent` (Android) and translates them into `CommandRegistry.Execute` calls on the core.

## Data Flow

```
User input
  └─► KeyboardProxy / PointerHandler
        └─► CommandRegistry.Execute(command, context)
              └─► IEditorCommand.Execute(context)
                    ├─► DocumentEditor (structural changes)
                    └─► UndoManager.Push(operation)
                          └─► LayoutCache.Invalidate(…)
                                └─► SkiaSharpEngine.Invalidate()
                                      └─► SKCanvasView.InvalidateSurface()
                                            └─► SkiaSharpEngine.OnFrame()
                                                  └─► DocumentRenderer.Render(context)
                                                        └─► SkiaDrawingContext → SKCanvas
```

## Extension Points

| Extension | Interface | Purpose |
|---|---|---|
| Custom drawing backend | `IDrawingContext` | Replace SkiaSharp with another renderer |
| Custom image loading | `IImageProvider` | Resolve images from custom sources |
| Custom clipboard | `IClipboardProvider` | Integrate with a custom clipboard |
| Custom HTML serializer | `IHtmlSerializer` | Custom HTML output |
| Custom HTML parser | `IHtmlParser` | Custom HTML import |
| Custom toolbar renderer | `IToolbarRenderer` | Fully custom toolbar drawing |
| Custom string localization | `IEditorStringProvider` | Localize all editor UI strings |
| Custom command | `IEditorCommand` | Add new operations to the editor |
