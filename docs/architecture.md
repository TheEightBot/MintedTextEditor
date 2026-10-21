# Architecture

MintedTextEditor is structured in three layers, each with a clear responsibility boundary. Lower layers have no dependency on higher layers.

```
┌──────────────────────────────────────────────────────────────────┐
│                      MintedTextEditor.Maui                        │
│                                                                    │
│  MintedEditorView (ContentView)                                    │
│  ├─ SKCanvasView host (paint, touch, caret drag + magnifier)       │
│  ├─ KeyboardProxy + KeyboardProxyHandler (hidden native input     │
│  │    view; one partial file per platform)                         │
│  ├─ Providers: MauiClipboardProvider, MauiHyperlinkHandler,        │
│  │    MauiImageProvider                                            │
│  ├─ EmbeddedIconLoader (toolbar SVG → SKBitmap)                    │
│  └─ MintedTextEditorHostingExtensions (UseMintedTextEditor),       │
│     XmlnsDefinitions                                               │
├──────────────────────────────────────────────────────────────────┤
│                   MintedTextEditor.SkiaSharp                      │
│                                                                    │
│  SkiaDrawingContext : IDrawingContext                              │
│  ├─ SKPaint / SKFont / SKTypeface caches (per frame)               │
│  ├─ Font fallback (emoji font, other scripts)                     │
│  └─ Image drawing (SKBitmap; SKImage cache for immutable bitmaps)  │
│  HarfBuzzTextShaper (internal; HarfBuzz shaping per script run)    │
├──────────────────────────────────────────────────────────────────┤
│                    MintedTextEditor.Core                           │
│                                                                    │
│  Document                                                          │
│  ├─ Document, Block, Paragraph, TableBlock/Row/Cell               │
│  ├─ Inline, TextRun, HyperlinkInline, ImageInline, LineBreak      │
│  ├─ TextStyle (immutable), ParagraphStyle (mutable)               │
│  ├─ DocumentPosition, TextRange                                    │
│  └─ DocumentEditor (structural mutations)                          │
│                                                                    │
│  Editing                                                           │
│  ├─ Caret, Selection (+ renderers)                                 │
│  ├─ UndoManager, IUndoableAction (+ actions)                        │
│  ├─ ClipboardOperations, IClipboardProvider                        │
│  ├─ Magnifier, MagnifierOptions                                    │
│  └─ FindReplaceEngine, ISpellCheckProvider, IPrintProvider         │
│                                                                    │
│  Formatting                                                        │
│  ├─ FormattingEngine, FontFormattingEngine                         │
│  ├─ ParagraphFormattingEngine                                      │
│  └─ HyperlinkEngine, ImageEngine, TableEngine                      │
│                                                                    │
│  Layout                                                            │
│  ├─ TextLayoutEngine (line breaking, alignment, tables)           │
│  ├─ LayoutCache (per-block invalidation)                           │
│  └─ DocumentLayout / LayoutBlock / LayoutLine / LayoutRun          │
│                                                                    │
│  Input                                                             │
│  ├─ EditorInputController (keys, text, pointer, clipboard, IME)    │
│  ├─ DocumentHitTester (pixel → DocumentPosition)                   │
│  ├─ ImeCompositionTracker, IImeEditSink, ImeTextContext            │
│  └─ DeadKeyComposer, LongPressDetector, InertialScroller           │
│                                                                    │
│  Commands                                                          │
│  ├─ IEditorCommand / EditorCommandRegistry / EditorContext         │
│  └─ Edit, Formatting, Font, Paragraph, Hyperlink, Image, Table     │
│     commands                                                       │
│                                                                    │
│  HTML I/O: HtmlImporter, HtmlExporter                              │
│  Markdown I/O: MarkdownImporter, MarkdownExporter                  │
│                                                                    │
│  Rendering                                                         │
│  ├─ IDrawingContext (platform-independent drawing API)            │
│  ├─ DocumentRenderer (layout → IDrawingContext calls)              │
│  └─ MagnifierRenderer                                              │
│                                                                    │
│  Toolbar, ContextMenu, Theming, Localization, Accessibility,       │
│  Events                                                            │
└──────────────────────────────────────────────────────────────────┘
```

## Layer Details

### MintedTextEditor.Core

The core layer is **fully platform-independent** — it targets `net10.0` only and has no dependency on MAUI, SkiaSharp, or any platform SDK. This makes it testable with a mock `IDrawingContext` in a plain xUnit project (`tests/MintedTextEditor.Core.Tests`). The toolbar icon SVGs (Lucide, Heroicons, Material Symbols) are embedded resources in this assembly.

**Document Model**: `TextStyle` is an immutable class whose `With…` methods return new instances; `ParagraphStyle` is a mutable class. Undo works by recording actions that know how to reverse themselves, not by snapshotting styles.

**TextLayoutEngine** converts a `Document` into a `DocumentLayout` of `LayoutBlock`s (or `TableLayoutBlock`s), each with `LayoutLine`s made of positioned `LayoutRun`s. Wrapping never splits a grapheme cluster (emoji ZWJ sequences, flags, combining marks), and very long unbroken text wraps in linear time. `LayoutCache` keeps the layout per block and re-lays out only the blocks a change touches. `DocumentHitTester` maps a point to a `DocumentPosition`.

**UndoManager** keeps undo/redo stacks of `IUndoableAction` entries (default depth 100). `Push` executes an action and records it; consecutive typed text with the same style is merged into one step.

**EditorInputController** turns key, text and pointer input into edits on the document. `ImeCompositionTracker` models an input method's composing region and turns each IME update into minimal edits on an `IImeEditSink`; `DeadKeyComposer` combines accent keys from hardware keyboards.

**EditorCommandRegistry** is a case-insensitive, name-keyed collection of `IEditorCommand`. All state lives in the `EditorContext` passed at execution time; commands that need input (URL, table size, font) carry it as properties.

**IDrawingContext** is the drawing API that the renderers call. `Scale`, `ClipRoundRect`, `MeasureCaretOffset` and `DrawTintedImage` have default implementations, so an implementation written against an earlier version keeps compiling: by default `Scale` does nothing, `ClipRoundRect` clips to the rectangle, `MeasureCaretOffset` measures the text before the index, and `DrawTintedImage` draws untinted.

### MintedTextEditor.SkiaSharp

This layer provides the `IDrawingContext` implementation backed by SkiaSharp. It targets `net10.0` and depends on `SkiaSharp` 4.153.1 and `HarfBuzzSharp` 14.2.1.301.

**SkiaDrawingContext** wraps an `SKCanvas` and translates drawing calls (text, rects, lines, images, clips, transforms) into `SKCanvas` calls. It caches `SKPaint`, `SKFont` and `SKTypeface` objects for its lifetime (the MAUI view creates one per frame) and resolves a fallback typeface for characters the run's font cannot draw; emoji (including VS16 sequences such as ❤️) always use the color emoji font. `MeasureCaretOffset` measures within the whole shaped run, so caret placement and hit-testing match kerned text. Only immutable `SKBitmap`s are cached as `SKImage`s, so changes to a mutable bitmap show on the next frame.

**Text shaping**: text is shaped with HarfBuzz (`HarfBuzzTextShaper`), giving kerning, ligatures and complex-script shaping. Each script in a run is shaped with its own rules, and glyphs stay in logical order to match caret placement. Shaped runs are cached; runs longer than 256 characters go into a small least-recently-used cache. Set `SkiaDrawingContext.ShapeAllText = false` once at startup, before any layout, to shape only emoji and fallback-font text and draw the rest with Skia's faster unshaped text.

`SkiaSharpEngine` is a placeholder (`IsAvailable`) and is not part of the rendering path.

### MintedTextEditor.Maui

The MAUI layer hosts the core and the Skia renderer in a MAUI control. It targets `net10.0-android`, `net10.0-ios` and `net10.0-maccatalyst`, plus `net10.0-windows10.0.19041.0` when built on Windows.

**MintedEditorView** is the public `ContentView`. It owns the `Document`, `UndoManager`, formatting engines, `EditorInputController`, `LayoutCache` and renderers. It exposes bindable properties (`Theme`, `ThemeMode`, `UseSystemTheme`, `IsReadOnly`, `PlaceholderText`, `IsWordWrap`, `ShowLineNumbers`, `ShowScrollbar`, `ShowToolbar`, `ToolbarDefinition`, `ToolbarItems`, `ToolbarIconPack`, `ToolbarMaxRows`, `IsMagnifierEnabled`, `MagnifierOptions`) and events (`TextChanged`, `SelectionChanged`, `HyperlinkClicked`, `IsHyperlinkSelectedChanged`, `EditorFocusChanged`, `ImageRequested`).

On touch screens a long press picks up the caret (`LongPressDetector`) and dragging moves it; `MagnifierRenderer` draws the document again, zoomed, inside the lens.

**KeyboardProxy** is a hidden 1×1 view whose `KeyboardProxyHandler` creates a native input view: a `View` with an `InputConnection` on Android (`KeyboardProxyHandler.Android.cs`), a `UIKeyInput` view on iOS and Mac Catalyst (`KeyboardProxyHandler.Apple.cs`), and a `TextBox` on Windows (`KeyboardProxyHandler.Windows.cs`). The csproj compiles each file only for its platforms. The proxy raises key, text and IME events that the view forwards to `EditorInputController`, and reports native focus changes, which drive `IsEditorFocused` and `EditorFocusChanged`.

## Data Flow

```
User input
  ├─► KeyboardProxy (native key / text / IME events)
  │     └─► EditorInputController.HandleKeyDown / HandleTextInput / IME edits
  ├─► SKCanvasView touch
  │     └─► hit-test, selection, caret drag, image/table resize, link taps
  └─► Toolbar tap
        └─► IEditorCommand.Execute(EditorContext)

Edits
  └─► UndoManager.Push(action) / DocumentEditor / formatting engines
        └─► Document.NotifyChanged(…)
              ├─► LayoutCache.OnDocumentChanged(…)  (invalidate blocks)
              ├─► MintedEditorView.TextChanged
              └─► SKCanvasView.InvalidateSurface()
                    └─► PaintSurface
                          └─► LayoutCache.GetLayout(…)
                                └─► DocumentRenderer / ToolbarRenderer / MagnifierRenderer
                                      └─► SkiaDrawingContext → SKCanvas
```

## Extension Points

| Extension | Interface / API | Purpose |
|---|---|---|
| Custom drawing backend | `IDrawingContext` | Replace SkiaSharp with another renderer |
| Custom image loading | `MintedEditorView.ImageProvider` (`Formatting.IImageProvider`) | Resolve images from custom sources |
| Custom link opening | `MintedEditorView.HyperlinkHandler` (`IHyperlinkHandler`) | Control how URLs open |
| Custom clipboard | `IClipboardProvider` | Integrate with a custom clipboard (core `EditorInputController`) |
| HTML / Markdown output | `HtmlExportOptions`, `MarkdownExportOptions` | Configure export |
| Custom toolbar | `ToolbarDefinition`, `ToolbarItems` | Choose and arrange toolbar items |
| Custom string localization | `EditorStrings` | Replace UI strings |
| Custom command | `IEditorCommand` | Add new operations (e.g. as toolbar buttons) |
| Text shaping mode | `SkiaDrawingContext.ShapeAllText` | Trade shaping quality for speed |
