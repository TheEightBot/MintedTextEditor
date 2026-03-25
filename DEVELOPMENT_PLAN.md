# MintedTextEditor — Development Plan

A best-in-class, fully custom-drawn Rich Text Editor for .NET MAUI (and future .NET platforms).
All rendering is performed on a SkiaSharp canvas — **zero native UI controls**.
Architecture mirrors [KumikoUI](https://github.com/TheEightBot/KumikoUI): platform-independent core → swappable drawing backend → platform host.

> **Workflow:** Each phase is implemented, reviewed, and committed to git before proceeding to the next.

---

## Table of Contents

- [Phase 0 — Repository & Solution Scaffolding](#phase-0--repository--solution-scaffolding)
- [Phase 1 — Core Abstractions & Document Model](#phase-1--core-abstractions--document-model)
- [Phase 2 — Text Layout Engine & Basic Rendering](#phase-2--text-layout-engine--basic-rendering)
- [Phase 3 — Caret, Cursor & Text Navigation](#phase-3--caret-cursor--text-navigation)
- [Phase 4 — Text Selection](#phase-4--text-selection)
- [Phase 5 — Text Input & Basic Editing](#phase-5--text-input--basic-editing)
- [Phase 6 — Clipboard Operations](#phase-6--clipboard-operations)
- [Phase 7 — Undo / Redo](#phase-7--undo--redo)
- [Phase 8 — Character Formatting](#phase-8--character-formatting)
- [Phase 9 — Paragraph Formatting](#phase-9--paragraph-formatting)
- [Phase 10 — Font Manipulation](#phase-10--font-manipulation)
- [Phase 11 — Hyperlinks](#phase-11--hyperlinks)
- [Phase 12 — Image Support](#phase-12--image-support)
- [Phase 13 — Table Support](#phase-13--table-support)
- [Phase 14 — Command System & Events](#phase-14--command-system--events)
- [Phase 15 — Toolbar](#phase-15--toolbar)
- [Phase 16 — Context Menu](#phase-16--context-menu)
- [Phase 17 — HTML Import / Export](#phase-17--html-import--export)
- [Phase 18 — Theming & Styling](#phase-18--theming--styling)
- [Phase 19 — Accessibility, Localization & RTL](#phase-19--accessibility-localization--rtl)
- [Phase 20 — Testing Infrastructure](#phase-20--testing-infrastructure)
- [Phase 21 — CI / CD Pipeline](#phase-21--ci--cd-pipeline)
- [Phase 22 — Documentation & README](#phase-22--documentation--readme)
- [Phase 23 — Sample Application](#phase-23--sample-application)
- [Phase 24 — Performance & Polish](#phase-24--performance--polish)
- [Phase 25 — UX Parity Hardening (Post-Review)](#phase-25--ux-parity-hardening-post-review)
- [Phase 26 — Toolbar & Selection Quality Fixes (Post-Review II)](#phase-26--toolbar--selection-quality-fixes-post-review-ii)

---

## Phase 0 — Repository & Solution Scaffolding

Set up the repository, solution file, project structure, and build infrastructure mirroring the KumikoUI pattern.

### Solution Structure
- [x] Create `MintedTextEditor.sln` with solution folders: `src`, `tests`, `samples`, `[ Solution ]`, `Build`
- [x] Create `Directory.Build.props` with shared NuGet metadata (Authors, License, SourceLink, PackageIcon, README)
- [x] Create `.gitignore` (Visual Studio / .NET / macOS / JetBrains)
- [x] Create `LICENSE` (MIT)
- [x] Create placeholder `README.md`
- [x] Create `images/` folder with logo placeholder

### Projects
- [x] `src/MintedTextEditor.Core/MintedTextEditor.Core.csproj` — `net10.0`, platform-independent core library
- [x] `src/MintedTextEditor.SkiaSharp/MintedTextEditor.SkiaSharp.csproj` — `net10.0`, SkiaSharp drawing implementation, references Core
- [x] `src/MintedTextEditor.Maui/MintedTextEditor.Maui.csproj` — multi-targeted MAUI host (`net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, `net10.0-windows10.0.19041.0`), references Core + SkiaSharp
- [x] `tests/MintedTextEditor.Core.Tests/MintedTextEditor.Core.Tests.csproj` — `net10.0`, xUnit + coverlet
- [x] `samples/SampleApp.Maui/SampleApp.Maui.csproj` — MAUI sample application

### Initial Files
- [x] Create empty marker classes / namespaces so all projects compile
- [x] Verify `dotnet build` succeeds for the entire solution

### Git
- [x] `git init`, initial commit with scaffolding

---

## Phase 1 — Core Abstractions & Document Model

Define the platform-independent rendering abstractions and the rich-text document model that all higher layers build upon.

### Drawing Abstractions (`MintedTextEditor.Core/Rendering/`)
- [x] `IDrawingContext` interface — mirrors KumikoUI pattern: `DrawRect`, `FillRect`, `DrawRoundRect`, `FillRoundRect`, `DrawLine`, `DrawText`, `DrawTextInRect`, `MeasureText`, `GetFontMetrics`, `ClipRect`, `Save`, `Restore`, `Translate`, `DrawImage`
- [x] Extended text drawing: `DrawTextRun` (draw a run of styled text at a position), `MeasureTextRun`
- [x] `ITextShaper` interface — for advanced text shaping (ligatures, complex scripts); simple pass-through default implementation
- [x] Primitives: `EditorRect`, `EditorSize`, `EditorPoint`, `EditorColor`, `EditorPaint`, `EditorFont`, `EditorFontMetrics`
- [x] Enums: `TextAlignment`, `VerticalAlignment`, `PaintStyle`
- [x] `PaintCache` — pooled/cached paint objects to avoid per-frame allocations

### Document Model (`MintedTextEditor.Core/Document/`)
- [x] `Document` — root container; holds a list of `Block` elements
- [x] `Block` (abstract) — base for block-level elements (paragraph, heading, list item, table, horizontal rule)
- [x] `Paragraph : Block` — holds an `InlineCollection` (list of `Inline` elements)
- [x] `Inline` (abstract) — base for inline content
- [x] `TextRun : Inline` — a contiguous span of text with a single `TextStyle`
- [x] `ImageInline : Inline` — an inline image with source, alt text, width, height
- [x] `HyperlinkInline : Inline` — wraps child inlines with a URL and optional title
- [x] `LineBreak : Inline` — explicit line break within a paragraph
- [x] `TextStyle` — immutable value type: font family, font size, bold, italic, underline, strikethrough, subscript, superscript, text color, highlight color, baseline offset
- [x] `ParagraphStyle` — alignment (left/center/right/justify), indent level, line spacing, space before/after, list type (none/bullet/number), heading level (0=none, 1-6), text direction (LTR/RTL)
- [x] `DocumentPosition` — (blockIndex, inlineIndex, offset) for cursor addressing
- [x] `TextRange` — (start: DocumentPosition, end: DocumentPosition) for selections
- [x] Document change notifications: `IDocumentChangeListener` / `DocumentChanged` event

### Document Manipulation (`MintedTextEditor.Core/Document/`)
- [x] `DocumentEditor` — stateless helper methods for safe document mutations:
  - `InsertText(Document, DocumentPosition, string, TextStyle) → DocumentPosition`
  - `DeleteRange(Document, TextRange) → DocumentPosition`
  - `SplitBlock(Document, DocumentPosition) → DocumentPosition`
  - `MergeBlocks(Document, int blockIndex) → DocumentPosition`
  - `ApplyTextStyle(Document, TextRange, Action<TextStyle>) → void`
  - `ApplyParagraphStyle(Document, TextRange, Action<ParagraphStyle>) → void`

### Tests
- [x] `Document` creation and block manipulation tests
- [x] `TextRun` splitting and merging tests
- [x] `DocumentEditor.InsertText` / `DeleteRange` round-trip tests
- [x] `DocumentPosition` comparison and navigation tests
- [x] `TextStyle` immutability and equality tests

### Git
- [x] Commit: "Phase 1 — Core abstractions & document model"

---

## Phase 2 — Text Layout Engine & Basic Rendering

Build the layout engine that converts the document model into positioned visual lines, and render them using the drawing abstractions.

### Text Layout (`MintedTextEditor.Core/Layout/`)
- [x] `LayoutLine` — a single visual line: list of `LayoutRun` items, y-offset, line height, baseline
- [x] `LayoutRun` — a positioned text segment: text, x-offset, width, associated `TextRun` reference, style
- [x] `LayoutBlock` — layout result for one `Block`: list of `LayoutLine`, total height, block index
- [x] `DocumentLayout` — full layout result: list of `LayoutBlock`, total document height, viewport width
- [x] `TextLayoutEngine` — performs word-wrapping and line-breaking:
  - Input: `Document` + viewport width + `IDrawingContext` (for measuring)
  - Output: `DocumentLayout`
  - Supports: word-wrap, character-wrap fallback for long words, respects paragraph indent levels
- [x] `LayoutCache` — caches layout results per block; invalidates selectively on edits

### SkiaSharp Implementation (`MintedTextEditor.SkiaSharp/`)
- [x] `SkiaDrawingContext : IDrawingContext, IDisposable` — maps all drawing calls to `SKCanvas`, with per-frame paint/font caching (mirrors KumikoUI's `SkiaDrawingContext`)
- [x] `DrawTextRun` / `MeasureTextRun` implementation using `SKFont` and `SKPaint`

### Document Renderer (`MintedTextEditor.Core/Rendering/`)
- [x] `DocumentRenderer` — traverses `DocumentLayout` and draws each run via `IDrawingContext`
  - Draws backgrounds (highlight colors)
  - Draws text runs with appropriate styles
  - Draws block decorations (list bullets/numbers, heading emphasis)
  - Handles vertical scrolling offset
  - Clips to viewport bounds

### Tests
- [x] Layout engine: single-paragraph word-wrap tests
- [x] Layout engine: multi-paragraph layout tests
- [x] Layout engine: inline style boundary alignment tests
- [x] Layout cache invalidation tests

### Git
- [x] Commit: "Phase 2 — Text layout engine & basic rendering"

---

## Phase 3 — Caret, Cursor & Text Navigation

Implement the blinking caret, hit-testing from pixel coordinates to document positions, and keyboard/pointer navigation.

### Hit Testing (`MintedTextEditor.Core/Input/`)
- [x] `HitTestResult` — (DocumentPosition, LayoutLine, LayoutRun, isAtLineEnd, isAfterLastBlock)
- [x] `DocumentHitTester` — given (x, y) and `DocumentLayout`, returns `HitTestResult`
  - Character-level hit testing within runs
  - Snap-to-nearest-character logic
  - Line-level hit testing for click-on-margin behavior

### Caret (`MintedTextEditor.Core/Editing/`)
- [x] `Caret` — current `DocumentPosition`, preferred X for vertical navigation, blink state
- [x] `CaretRenderer` — draws the caret line at the correct pixel position using `DocumentLayout`
- [x] Caret blink timer integration (on/off cycle, reset on input)

### Navigation (`MintedTextEditor.Core/Editing/`)
- [x] Arrow keys: left/right (character), up/down (visual line), maintaining preferred X
- [x] Word navigation: Ctrl/Cmd + Left/Right (jump by word boundary)
- [x] Line navigation: Home/End (line start/end)
- [x] Document navigation: Ctrl+Home / Ctrl+End
- [x] Page navigation: PageUp / PageDown (viewport-height steps)
- [x] Pointer click: single-click positions caret via hit testing
- [ ] Ensure caret remains visible: auto-scroll viewport when caret moves beyond visible area

### Input Abstractions (`MintedTextEditor.Core/Input/`)
- [x] `EditorPointerEventArgs` — (x, y, action, button, modifiers, clickCount, timestamp)
- [x] `EditorKeyEventArgs` — (key, character, modifiers, isKeyDown)
- [x] `EditorKey` enum — arrows, Home, End, PageUp, PageDown, Tab, Enter, Escape, Backspace, Delete, A-Z, etc.
- [x] `InputAction` enum — Pressed, Released, Moved, Scroll, DoubleTap, LongPress
- [x] `InputModifiers` [Flags] — None, Shift, Control, Alt, Meta
- [x] `EditorInputController` — central input dispatcher (mirrors KumikoUI's `GridInputController`)

### Tests
- [x] Hit testing: click within a word returns correct position
- [x] Hit testing: click in margin snaps to line start/end
- [x] Navigation: arrow keys in single-line and multi-line scenarios
- [x] Navigation: word-jump boundaries
- [x] Navigation: Ctrl+Home / Ctrl+End

### Git
- [x] Commit: "Phase 3 — Caret, cursor & text navigation"

---

## Phase 4 — Text Selection

Extend the caret into a selection range with visual highlighting and keyboard/pointer selection gestures.

### Selection Model (`MintedTextEditor.Core/Editing/`)
- [x] `Selection` — anchor position + active position (caret) forming a `TextRange`; supports zero-length (caret-only) and ranged selection
- [x] `SelectionRenderer` — draws selection highlight rectangles across multiple lines
- [x] Selection normalization: ordered start/end regardless of anchor vs. active direction

### Selection Gestures
- [x] Shift + Arrow keys: extend selection character/line/word at a time
- [x] Shift + Home/End: extend selection to line boundaries
- [x] Shift + Ctrl+Home/End: extend to document start/end
- [x] Ctrl/Cmd + A: select all
- [x] Mouse/touch drag: click-and-drag to select a range
- [x] Double-click/tap: select word
- [x] Triple-click/tap: select paragraph/block
- [x] Shift + click: extend selection from anchor to click position

### Selection Utilities
- [x] `GetSelectedText(Document, TextRange) → string` — extracts plain text from selection
- [ ] `GetSelectedDocument(Document, TextRange) → Document` — extracts a sub-document (for rich copy)

### Tests
- [x] Shift+arrow extends selection
- [x] Double-click selects word
- [x] Triple-click selects paragraph
- [x] Select-all returns entire document text
- [x] Drag selection across multiple blocks

### Git
- [x] Commit: "Phase 4 — Text selection"

---

## Phase 5 — Text Input & Basic Editing

Handle keyboard text entry, backspace, delete, Enter (paragraph split), and typed-over selections.

### Text Entry (`MintedTextEditor.Core/Editing/`)
- [x] Character input: insert typed character at caret position
- [x] If selection is active, delete selection first, then insert
- [x] Backspace: delete character before caret (or delete selection)
- [x] Delete: delete character after caret (or delete selection)
- [x] Enter: split current block at caret position (`DocumentEditor.SplitBlock`)
- [x] Backspace at start of block: merge with previous block (`DocumentEditor.MergeBlocks`)
- [x] Delete at end of block: merge with next block
- [x] Tab key behavior (configurable: insert tab character, increase indent, or move focus)

### Input Method / Keyboard Proxy (`MintedTextEditor.Maui/`)
- [x] Hidden `KeyboardProxy` overlay for keyboard capture — transparent 1×1 `View` focused on touch, sits above the `SKCanvasView` in a `Grid`
- [x] Platform-specific keyboard configuration (Android `BaseInputConnection` IME, iOS/Mac Catalyst `UIKeyInput` + `UIKeyCommand` for nav keys)
- [x] Translate platform text events into `EditorKeyEventArgs` / character input → `EditorInputController`

### Tests
- [x] Insert character at various positions
- [x] Backspace and delete at boundaries
- [x] Enter splits paragraph correctly
- [x] Type-over-selection replaces selected text
- [x] Backspace at block boundary merges blocks

### Git
- [x] Commit: "Phase 5 — Text input & basic editing"

---

## Phase 6 — Clipboard Operations

Implement cut, copy, and paste with both plain text and rich text support.

### Clipboard (`MintedTextEditor.Core/Editing/`)
- [x] `IClipboardProvider` interface — `SetTextAsync(string)`, `GetTextAsync()`
- [x] `ClipboardOperations` — static helpers: `CopyAsync`, `CutAsync`, `PasteAsync` using `IClipboardProvider`
- [x] Keyboard shortcuts: Ctrl/Cmd + C (copy), Ctrl/Cmd + X (cut), Ctrl/Cmd + V (paste)
- [ ] Rich paste: if clipboard contains rich content (HTML/RTF), parse and insert styled content
- [x] Plain paste: insert plain text with current caret style
- [ ] Paste with style matching: Ctrl/Cmd + Shift + V (paste as plain text)

### MAUI Clipboard Provider (`MintedTextEditor.Maui/`)
- [x] `MauiClipboardProvider : IClipboardProvider` — wraps `Clipboard.Default` from MAUI Essentials

### Tests
- [x] Copy selected text, verify clipboard content
- [x] Cut removes selection and places on clipboard
- [x] Paste inserts at caret
- [x] Paste replaces selection

### Git
- [x] Commit: "Phase 6 — Clipboard operations"

---

## Phase 7 — Undo / Redo

Implement a robust undo/redo stack for all document mutations.

### Undo System (`MintedTextEditor.Core/Editing/`)
- [x] `IUndoableAction` interface — `Execute()`, `Undo()`, `Redo()`, `Description` (string), `MergeWith(IUndoableAction) → bool`
- [x] Concrete actions: `InsertTextAction`, `DeleteRangeAction`, `SplitBlockAction`, `MergeBlocksAction`, `ApplyStyleAction`, `CompositeAction`
- [x] `UndoManager` — manages undo/redo stacks with configurable max depth
  - `Push(IUndoableAction)` — adds action, clears redo stack
  - `Undo()` — pops from undo, pushes to redo, restores state
  - `Redo()` — pops from redo, pushes to undo, re-applies
  - `CanUndo` / `CanRedo` properties
  - `UndoStackChanged` event
- [x] Action merging: consecutive character inserts merge into a single action (with timeout)
- [x] `DocumentEditor` integration: all mutations route through undo system
- [x] Keyboard shortcuts: Ctrl/Cmd + Z (undo), Ctrl/Cmd + Y / Ctrl/Cmd + Shift + Z (redo)

### Tests
- [x] Insert text, undo, verify original state
- [x] Undo + redo round-trip
- [x] Action merging: rapid typing groups into single undo step
- [x] Max stack depth eviction
- [x] Style changes are undoable

### Git
- [x] Commit: "Phase 7 — Undo / redo"

---

## Phase 8 — Character Formatting

Apply inline text styles: bold, italic, underline, strikethrough, subscript, superscript.

### Formatting API (`MintedTextEditor.Core/Formatting/`)
- [x] `FormattingEngine` — applies/removes/toggles character formats over a `TextRange`:
  - `ToggleBold(Document, TextRange)`
  - `ToggleItalic(Document, TextRange)`
  - `ToggleUnderline(Document, TextRange)`
  - `ToggleStrikethrough(Document, TextRange)`
  - `ToggleSubscript(Document, TextRange)`
  - `ToggleSuperscript(Document, TextRange)`
  - `ClearFormatting(Document, TextRange)` — reset to default text style
- [x] Toggle logic: if entire range is already bold → remove bold; otherwise → apply bold
- [x] When no selection: set "pending style" so next typed character inherits the toggled format
- [x] Keyboard shortcuts: Ctrl/Cmd + B (bold), Ctrl/Cmd + I (italic), Ctrl/Cmd + U (underline)

### Rendering Support
- [x] `DocumentRenderer` draws underline decoration (line below text baseline)
- [x] `DocumentRenderer` draws strikethrough decoration (line through text center)
- [x] `DocumentRenderer` handles superscript/subscript (reduced font size + baseline offset)
- [ ] `SkiaDrawingContext` supports bold/italic font resolution via `SKTypeface`

### Tests
- [x] Toggle bold on a range: verify `TextStyle.IsBold` on affected runs
- [x] Toggle on partially-formatted range: splits runs correctly
- [x] Clear formatting resets all styles
- [x] Pending style applies to next typed character
- [x] Underline and strikethrough render at correct positions

### Git
- [x] Commit: "Phase 8 — Character formatting"

---

## Phase 9 — Paragraph Formatting

Block-level formatting: alignment, lists, indentation, headings.

### Paragraph Formatting (`MintedTextEditor.Core/Formatting/`)
- [x] `ParagraphFormattingEngine`:
  - `SetAlignment(Document, TextRange, TextAlignment)` — Left, Center, Right, Justify
  - `ToggleBulletList(Document, TextRange)` — toggle unordered list
  - `ToggleNumberList(Document, TextRange)` — toggle ordered list
  - `IncreaseIndent(Document, TextRange)` — increase indent level
  - `DecreaseIndent(Document, TextRange)` — decrease indent level
  - `SetHeadingLevel(Document, TextRange, int level)` — 0 = normal, 1-6 = heading
  - `SetParagraphFormat(Document, TextRange, string format)` — "Normal", "Heading1"–"Heading6", "Quote"
  - `SetLineSpacing(Document, TextRange, float spacing)` — 1.0, 1.5, 2.0, etc.

### Rendering Support
- [x] Bullet rendering: draw bullet glyph (•) at indent position before paragraph
- [x] Number rendering: draw sequential number at indent position before paragraph
- [x] Indent rendering: apply left margin based on indent level
- [x] Heading rendering: apply heading-level font sizes and weights
- [x] Justify alignment: distribute extra space between words on each line (except last line)
- [x] Block quote rendering: left border + background tint

### Tests
- [x] Set alignment on paragraph, verify rendering positions
- [x] Toggle bullet list on/off
- [x] Nested indent levels
- [x] Heading levels apply correct font sizes
- [x] Number list sequential numbering across multiple paragraphs

### Git
- [x] Commit: "Phase 9 — Paragraph formatting"

---

## Phase 10 — Font Manipulation

Font family, font size, text color, and highlight/background color.

### Font API (`MintedTextEditor.Core/Formatting/`)
- [x] `FontFormattingEngine`:
  - `ApplyFontFamily(Document, TextRange, string family)`
  - `ApplyFontSize(Document, TextRange, float size)`
  - `ApplyTextColor(Document, TextRange, EditorColor color)`
  - `ApplyHighlightColor(Document, TextRange, EditorColor color)`
  - `RemoveHighlightColor(Document, TextRange)`
- [x] Query current format at caret: `GetCurrentTextStyle(Document, DocumentPosition) → TextStyle`
- [x] Query format of selection: `GetTextStyleForRange(Document, TextRange) → TextStyle?` (returns null for mixed values)

### Rendering Support
- [x] `SkiaDrawingContext` resolves font families to `SKTypeface` with fallback chain
- [x] Draw highlight backgrounds per-run before text
- [x] Text color per-run

### Tests
- [x] Apply font family to range splits runs
- [x] Apply font size to range
- [x] Apply text color
- [x] Apply highlight color and verify background rendering
- [x] Mixed-format query returns null

### Git
- [x] Commit: "Phase 10 — Font manipulation"

---

## Phase 11 — Hyperlinks

Insert, edit, remove, detect, and open hyperlinks.

### Hyperlink Support (`MintedTextEditor.Core/Document/`, `MintedTextEditor.Core/Formatting/`)
- [x] `HyperlinkInline` in document model (from Phase 1) — URL, title, child inlines
- [x] `HyperlinkEngine`:
  - `InsertHyperlink(Document, TextRange, string url, string? title)` — wraps selected text (or inserts new text) as a hyperlink
  - `EditHyperlink(Document, HyperlinkInline, string newUrl, string? newTitle)`
  - `RemoveHyperlink(Document, TextRange)` — unwraps hyperlink, keeps text
  - `GetHyperlinkAtPosition(Document, DocumentPosition) → HyperlinkInline?`
- [x] Auto-detect URLs while typing (optional, configurable)
- [x] Open hyperlink action: `IHyperlinkHandler` interface for platform-specific URL opening

### Rendering Support
- [x] Hyperlink text rendered with underline + accent color (configurable)
- [ ] Hover/pointer-over cursor change indication
- [x] Ctrl/Cmd + click to open hyperlink

### MAUI Integration
- [x] `MauiHyperlinkHandler : IHyperlinkHandler` — uses `Launcher.Default.OpenAsync(uri)`

### Events
- [x] `HyperlinkClicked` event with URL and cancel support
- [x] `IsHyperlinkSelectedChanged` event

### Tests
- [x] Insert hyperlink wraps text
- [x] Remove hyperlink preserves text
- [x] Edit hyperlink URL
- [x] Auto-detect URL on space after typing "https://..."
- [x] Hit-test returns hyperlink at position

### Git
- [x] Commit: "Phase 11 — Hyperlinks"

---

## Phase 12 — Image Support

Insert, display, resize, and manage inline images.

### Image Model (`MintedTextEditor.Core/Document/`)
- [x] `ImageInline` (from Phase 1) — source (byte[] or stream reference), alt text, width, height, aspect ratio lock
- [x] `IImageProvider` interface — `LoadImageAsync(source) → object` (returns platform-specific image handle)

### Image Operations (`MintedTextEditor.Core/Formatting/`)
- [x] `ImageEngine`:
  - `InsertImage(Document, DocumentPosition, ImageSource) → ImageInline`
  - `RemoveImage(Document, ImageInline)`
  - `ResizeImage(Document, ImageInline, float width, float height)`
  - `ReplaceImage(Document, ImageInline, ImageSource)`

### Rendering Support
- [x] `DocumentRenderer` draws `ImageInline` using `IDrawingContext.DrawImage`
- [x] Image selection: click on image selects it; draw selection handles (8 resize grips)
- [x] Drag resize handles to resize (maintain aspect ratio by default, free-resize with Shift)
- [x] Image placeholder while loading

### MAUI Integration
- [x] `MauiImageProvider : IImageProvider` — loads from file path, stream, or gallery picker
- [x] `ImageRequested` event (mirrors Syncfusion) for custom image source dialogs

### SkiaSharp Integration
- [x] Load images as `SKImage` / `SKBitmap` for rendering
- [x] Scale/clip images to fit layout bounds

### Tests
- [x] Insert image at position
- [x] Resize image maintains aspect ratio
- [x] Remove image from document
- [x] Layout accounts for image dimensions in line height

### Git
- [x] Commit: "Phase 12 — Image support"

---

## Phase 13 — Table Support

Insert and edit tables with rows, columns, cell merging, and styling.

### Table Model (`MintedTextEditor.Core/Document/`)
- [x] `TableBlock : Block` — rows × columns grid of `TableCell`
- [x] `TableCell` — contains a mini-`Document` (list of `Block`), column span, row span, background color, borders
- [x] `TableRow` — list of `TableCell`, row height
- [x] `TableStyle` — border style, cell padding, header row style

### Table Operations (`MintedTextEditor.Core/Formatting/`)
- [x] `TableEngine`:
  - `InsertTable(Document, DocumentPosition, int rows, int cols) → TableBlock`
  - `InsertRow(TableBlock, int afterRowIndex)`
  - `InsertColumn(TableBlock, int afterColIndex)`
  - `DeleteRow(TableBlock, int rowIndex)`
  - `DeleteColumn(TableBlock, int colIndex)`
  - `MergeCells(TableBlock, range)`
  - `SplitCell(TableBlock, cell)`
  - `SetCellBackground(TableCell, EditorColor)`

### Rendering Support
- [x] Table layout engine: column widths (auto, fixed, percentage), row heights
- [x] Cell content layout: each cell lays out its own document
- [x] Draw table borders, cell backgrounds
- [x] Tab / Shift+Tab key navigates between cells
- [x] Caret navigation within and between cells
- [x] User-resizable table columns via drag handles (persisted column widths)
- [x] User-resizable table rows via drag handles (persisted row heights)
- [x] Tapping below a terminal table moves caret to a writable paragraph below the table

### Table UX Parity (Research-Informed)
- [x] Align keyboard behavior with common editors (ProseMirror/Tiptap, CKEditor 5, Lexical):
  - Tab/Shift+Tab moves cell-to-cell
  - Structural table edit commands are exposed from keyboard when focus is inside a cell
  - Users can move the caret out of a table without using pointer/context menu
- [x] Add table escape behavior:
  - From boundary cells, keyboard navigation can move before/after the table
  - If no adjacent block exists, create an empty paragraph to avoid caret trapping
- [x] Add keyboard shortcuts for structural operations while in a table:
  - Insert row above/below
  - Insert column left/right
  - Delete current row/column
  - Delete current table

### Tests
- [x] Insert table with specified dimensions
- [x] Add/remove rows and columns
- [x] Cell merging across columns
- [x] Tab navigation between cells
- [x] Shift+Tab reverse navigation between cells
- [x] Keyboard escape from last cell to block after table
- [x] Keyboard structural shortcuts (insert/delete row/column, delete table)
- [x] Nested content in cells

### Git
- [x] Commit: "Phase 13 — Table support"

---

## Phase 14 — Command System & Events

Formalize all editor actions as commands (ICommand) and expose a rich event system.

### Command System (`MintedTextEditor.Core/Commands/`)
- [x] `IEditorCommand` — `Execute(EditorContext)`, `CanExecute(EditorContext)`, `Name`, `Description`
- [x] `EditorContext` — bundles Document, Selection, UndoManager, FormattingEngine, etc.
- [x] Built-in commands (one per action):
  - `ToggleBoldCommand`, `ToggleItalicCommand`, `ToggleUnderlineCommand`, `ToggleStrikethroughCommand`
  - `ToggleSubscriptCommand`, `ToggleSuperscriptCommand`
  - `AlignLeftCommand`, `AlignCenterCommand`, `AlignRightCommand`, `AlignJustifyCommand`
  - `ToggleBulletListCommand`, `ToggleNumberListCommand`
  - `IncreaseIndentCommand`, `DecreaseIndentCommand`
  - `UndoCommand`, `RedoCommand`
  - `CopyCommand`, `CutCommand`, `PasteCommand`
  - `SelectAllCommand`, `ClearFormattingCommand`
  - `InsertHyperlinkCommand`, `RemoveHyperlinkCommand`, `OpenHyperlinkCommand`
  - `InsertImageCommand`, `RemoveImageCommand`
  - `InsertTableCommand`
  - `ApplyFontFamilyCommand`, `ApplyFontSizeCommand`
  - `ApplyTextColorCommand`, `ApplyHighlightColorCommand`
  - `SetHeadingLevelCommand`, `SetParagraphFormatCommand`
- [x] `EditorCommandRegistry` — register, look up, and invoke commands by name
- [x] Key binding system: map keyboard shortcuts to commands (configurable)

### Events (`MintedTextEditor.Core/Events/`)
- [x] `SelectionChanged` — fires when caret/selection moves
- [x] `TextChanged` — fires on any document content change
- [x] `FontFamilyChanged` — fires when font family at caret changes
- [x] `FontSizeChanged` — fires when font size at caret changes  
- [x] `FontAttributesChanged` — fires when bold/italic state at caret changes
- [x] `TextDecorationsChanged` — fires when underline/strikethrough at caret changes
- [x] `TextFormattingChanged` — aggregate event for any formatting change
- [x] `HorizontalTextAlignmentChanged` — fires when paragraph alignment changes
- [x] `ListTypeChanged` — fires when list type at caret changes
- [x] `TextColorChanged`, `HighlightTextColorChanged`
- [x] `HyperlinkClicked`, `IsHyperlinkSelectedChanged`
- [x] `IsReadOnlyChanged`
- [x] `ContentLoaded` — fires after HTML/content import completes
- [x] `ImageInserted`, `ImageRemoved`

### Tests
- [x] Commands execute and update document
- [x] CanExecute returns false when inappropriate (e.g., copy with no selection)
- [x] Events fire on corresponding actions
- [x] Custom commands can be registered

### Git
- [x] Commit: "Phase 14 — Command system & events"

---

## Phase 15 — Toolbar

A fully custom-drawn toolbar supporting auto-generated and user-customizable button layouts.

### Toolbar Model (`MintedTextEditor.Core/Toolbar/`)
- [x] `ToolbarItem` (abstract) — base for toolbar elements
- [x] `ToolbarButton : ToolbarItem` — icon, label, associated `IEditorCommand`, toggle state
- [x] `ToolbarSeparator : ToolbarItem` — visual divider
- [x] `ToolbarDropdown : ToolbarItem` — dropdown picker (font family, font size, heading level, color picker)
- [x] `ToolbarColorPicker : ToolbarItem` — color swatch grid for text color / highlight color
- [x] `ToolbarGroup` — logical grouping of items
- [x] `ToolbarDefinition` — configurable list of `ToolbarItem`/`ToolbarGroup` items with layout mode:
  - [x] `Wrap` — items wrap to multiple rows on desktop
  - [x] `Scroll` — horizontal scroll on mobile
  - [x] `Overflow` — overflow items collapse into a "more" menu

### Default Toolbar Layout
- [x] Auto-generated default toolbar matching Telerik/Syncfusion feature set:
  - Group: Undo, Redo
  - Separator
  - Group: Font Family dropdown, Font Size dropdown
  - Separator
  - Group: Bold, Italic, Underline, Strikethrough
  - Separator
  - Group: Text Color, Highlight Color
  - Separator
  - Group: Align Left, Align Center, Align Right, Align Justify
  - Separator
  - Group: Bullet List, Number List, Decrease Indent, Increase Indent
  - Separator
  - Group: Subscript, Superscript
  - Separator
  - Group: Insert Hyperlink, Insert Image, Insert Table
  - Separator
  - Group: Edit actions dropdown (Copy/Cut/Paste/Select All)
  - Separator
  - Group: Object actions dropdown (Open/Remove Link, Remove Image)
  - Separator
  - Group: Table actions dropdown (insert/delete row/column, delete table)
  - Separator
  - Group: Heading dropdown, Clear Formatting

### Toolbar Rendering (`MintedTextEditor.Core/Toolbar/`)
- [x] `ToolbarRenderer` — fully custom-drawn toolbar using `IDrawingContext`
- [x] Button hit testing and press/hover states
- [x] Toggle state visual (depressed/highlighted for active bold, italic, etc.)
- [x] Dropdown rendering: popup overlay with selectable items
- [x] Color picker rendering: grid of color swatches
- [x] Icon rendering normalized to avoid clipping/cutoff across button sizes
- [x] Toolbar responds to selection changes (updates toggle states, current font display)
- [x] `ShowToolbar` property — show/hide toolbar
- [x] `ToolbarItems` collection — customizable set of items

### Canvas-Integrated Toolbar (`MintedTextEditor.Maui/`)
The toolbar is rendered entirely inside the `SKCanvasView` paint surface — no native XAML controls.

- [x] `ToolbarRenderer` wired into `MintedEditorView.OnPaintSurface` — drawn above the document viewport
- [x] `ShowToolbar` bindable property on `MintedEditorView` (default `true`) — controls toolbar visibility
- [x] `ToolbarDefinition` bindable property on `MintedEditorView` — swaps active `ToolbarRenderer` instance
- [x] Touch routing in `OnCanvasTouch` — Y < `toolbarH` → `HitTest` → `IEditorCommand.Execute(EditorContext)` → `InvalidateCanvas()`
- [x] Document viewport offset by `toolbarH` so document content never overlaps toolbar
- [x] Theme colours forwarded each frame: `ToolbarBackground`, `ToolbarButtonColor`, `ToolbarActiveColor`, `ToolbarSeparatorColor`
- [x] Async icon loading via `FileSystem.OpenAppPackageFileAsync` → `SKBitmap.Decode` → `_iconCache`
- [x] `IconResolver` delegate set on `_toolbarRenderer` so icons render as bitmaps; unknown keys fall back to text labels
- [x] Icon key → bundle filename mapping for all 20 default toolbar icons (handles `number-list` → `tb_ordered_list.png`, `clear-formatting` → `tb_clear_format.png`)
- [x] XAML sample (`MainPage.xaml`) uses only `<minted:MintedEditorView ShowToolbar="True"/>` — no native toolbar overlay

### Tests
- [x] Default toolbar generates all expected items
- [x] Button click executes associated command
- [x] Toggle state reflects current selection format
- [x] Dropdown selection applies formatting
- [x] Custom toolbar definition renders correctly

### Git
- [x] Commit: "Phase 15 — Toolbar"
- [x] Commit: "Canvas-integrated toolbar with async icon loading"

---

## Phase 16 — Context Menu

Right-click / long-press context menu with standard and extensible items.

### Context Menu (`MintedTextEditor.Core/ContextMenu/`)
- [x] `ContextMenuItem` — label, icon, associated `IEditorCommand`, enabled state, separator-after flag
- [x] `ContextMenuDefinition` — ordered list of `ContextMenuItem`
- [x] Default context menu:
  - Cut, Copy, Paste
  - Separator
  - Select All
  - Separator
  - Insert Hyperlink / Edit Hyperlink / Remove Hyperlink (context-dependent)
  - Open Hyperlink (if hyperlink selected)
  - Separator
  - Insert Image
  - Insert Table (if applicable)
- [x] Extensibility: `ContextMenuItemsRequested` event allows adding/removing items before display
- [x] `ContextMenuRenderer` — fully custom-drawn popup using `IDrawingContext`
  - Rounded rectangle background with shadow
  - Hover highlight on items
  - Keyboard navigation (arrow keys, Enter to select, Escape to dismiss)

### Trigger
- [x] Right-click (secondary button) on desktop
- [x] Long-press on mobile
- [x] Positioned at pointer location, clamped to viewport bounds

### Tests
- [x] Context menu shows correct items for context (hyperlink vs. no hyperlink)
- [x] Menu item click executes command
- [x] Menu dismisses on click outside or Escape
- [x] Custom items added via event

### Git
- [x] Commit: "Phase 16 — Context menu"

---

## Phase 17 — HTML Import / Export

Load content from HTML strings/streams and export document to HTML.

### HTML Parser (`MintedTextEditor.Core/Html/`)
- [x] `HtmlImporter` — parses HTML string/stream into `Document` model
  - Supported tags: `<p>`, `<br>`, `<strong>`/`<b>`, `<em>`/`<i>`, `<u>`, `<s>`/`<del>`/`<strike>`, `<sub>`, `<sup>`, `<span>` (with inline styles), `<a>`, `<img>`, `<h1>`–`<h6>`, `<ul>`, `<ol>`, `<li>`, `<blockquote>`, `<table>`, `<tr>`, `<td>`, `<th>`, `<div>`
  - Parse inline CSS: `font-family`, `font-size`, `color`, `background-color`, `text-align`, `text-decoration`, `font-weight`, `font-style`
  - Graceful handling of unsupported tags (preserve content, ignore formatting)
  - Uses a lightweight/simple HTML parser — no heavy dependency (consider custom or `HtmlAgilityPack`)

### HTML Serializer (`MintedTextEditor.Core/Html/`)
- [x] `HtmlExporter` — serializes `Document` to clean, semantic HTML string
  - Generates minimal inline styles (only non-default attributes)
  - Produces well-formed HTML5
  - Options: include/exclude `<html>`/`<body>` wrapper, CSS class mode vs. inline style mode

### Public API
- [x] `LoadHtml(string html)` — replace document content from HTML
- [x] `LoadHtml(Stream stream)` — replace document content from HTML stream
- [x] `GetHtml() → string` — export current document as HTML
- [x] `GetHtml(TextRange) → string` — export selection as HTML

### Tests
- [x] Round-trip: `Document` → HTML → `Document` preserves content and styles
- [x] Parse complex HTML with nested formatting
- [x] Export generates valid HTML5
- [x] Unsupported tags don't crash parser
- [x] Inline CSS is correctly mapped to `TextStyle`

### Git
- [x] Commit: "Phase 17 — HTML import / export"

---

## Phase 18 — Theming & Styling

Comprehensive theming system with built-in themes and full customization.

### Theme System (`MintedTextEditor.Core/Theming/`)
- [x] `EditorThemeMode` enum — `Light`, `Dark`, `HighContrast`
- [x] `EditorStyle` — comprehensive style class covering all visual properties:
  - Editor background, border color, border width
  - Default text color, font family, font size
  - Caret color, caret width
  - Selection highlight color, selection text color
  - Hyperlink color, hyperlink hover color
  - Toolbar background, toolbar button color, toolbar separator color, toolbar hover color, toolbar active/toggle color
  - Context menu background, context menu text color, context menu hover color
  - Scrollbar track/thumb colors
  - Focus ring color
  - Line spacing, paragraph spacing
  - Padding (top, right, bottom, left)
- [x] `EditorTheme` factory — `Create(EditorThemeMode)` returns preconfigured `EditorStyle`
  - `CreateLight()` — clean white theme
  - `CreateDark()` — dark mode theme
  - `CreateHighContrast()` — high-contrast accessibility theme
- [x] Runtime theme switching: changing theme triggers full re-render
- [x] Custom theme support: users supply their own `EditorStyle`
- [x] System theme follow mode in MAUI view (`UseSystemTheme`) with automatic light/dark selection
- [x] Theme-aware default text rendering so dark mode no longer renders default text as black
- [x] Theme-aware hyperlink color application in layout/render pipeline

### Typography UX
- [x] Font-family/font-size changes at collapsed caret apply to subsequently typed text (pending style behavior)

### Tests
- [x] All three built-in themes create valid styles
- [x] Custom style overrides are respected
- [x] Theme switch triggers re-layout and re-render

### Git
- [x] Commit: "Phase 18 — Theming & styling"

---

## Phase 19 — Accessibility, Localization & RTL

Make the editor accessible, localizable, and support right-to-left text.

### Accessibility
- [x] Semantic properties on the MAUI view (`SemanticProperties.Description`, `AutomationProperties`)
- [x] Screen-reader announcements for formatting changes
- [x] High-contrast theme support (from Phase 18)
- [x] Keyboard-only navigation for all features (toolbar, context menu, editor)
- [x] Focus indicator styling
- [x] ARIA-equivalent descriptions for toolbar buttons

### Localization
- [x] Resource files for all UI strings (toolbar tooltips, context menu labels, dialog prompts)
- [x] `IStringLocalizer` integration or simple resource-based approach
- [x] Default language: English
- [x] Support for plugging in additional languages

### RTL Support
- [x] `TextDirection` on `ParagraphStyle` (LTR / RTL / Auto)
- [x] RTL text layout: right-aligned by default, mirrored indent
- [x] RTL caret behavior: caret on right side, moves right-to-left
- [x] Mixed LTR/RTL content within a single paragraph (bidirectional text)
- [x] Toolbar mirroring in RTL mode

### Tests
- [x] Semantic properties are set on the MAUI view
- [x] Localized strings load correctly
- [x] RTL paragraph renders text right-to-left
- [x] Bidirectional text scenario

### Git
- [x] Commit: "Phase 19 — Accessibility, localization & RTL"

---

## Phase 20 — Testing Infrastructure

Comprehensive test coverage across all layers.

### Unit Tests (`tests/MintedTextEditor.Core.Tests/`)
- [x] Document model creation, mutation, and validation
- [x] Text layout engine: word-wrap, multi-paragraph, inline styles
- [x] Caret navigation: all directions, word/line/document boundaries
- [x] Selection: keyboard and pointer gestures, multi-line, word/paragraph select
- [x] Text input: insert, delete, backspace, Enter, Tab, type-over-selection
- [x] Clipboard: copy, cut, paste (mock clipboard)
- [x] Undo/redo: all operation types, merging, stack limits
- [x] Character formatting: all toggle operations, pending style
- [x] Paragraph formatting: alignment, lists, indent, headings
- [x] Font formatting: family, size, color, highlight
- [x] Hyperlinks: CRUD, auto-detect, hit-test
- [x] Images: insert, resize, remove, layout impact
- [x] Tables: CRUD rows/columns, cell merging, Tab navigation
- [x] Commands: execute, canExecute, registry
- [x] HTML import: parse various HTML structures
- [x] HTML export: serialize and validate output
- [x] Theming: all built-in themes, custom overrides
- [x] Hit-testing: various viewport positions

### Integration Tests
- [x] Full editing flow: type text → format → undo → redo → export HTML → re-import → compare
- [x] Large document: performance baseline for layout/render with 10,000+ paragraphs

### Test Helpers
- [x] `MockDrawingContext : IDrawingContext` — records draw calls for assertion
- [x] `MockClipboardProvider : IClipboardProvider` — in-memory clipboard
- [x] `TestDocumentBuilder` — fluent API for creating test documents

### Git
- [x] Commit: "Phase 20 — Testing infrastructure"

---

## Phase 21 — CI / CD Pipeline

GitHub Actions workflows for build, test, and NuGet package publishing.

### CI Workflow (`.github/workflows/ci.yml`)
- [x] Trigger: push/PR to `main` and `develop` branches (ignore `**.md`, `docs/**`)
- [x] Concurrency: cancel in-progress runs for same ref
- [x] **Job 1 — Build & Test (Core + SkiaSharp)** on `ubuntu-latest`:
  - Setup .NET 10
  - Restore, build, test (`MintedTextEditor.Core.Tests`)
  - Upload test results artifact (TRX)
  - Pack dry-run to validate packaging
- [x] **Job 2 — Build (MAUI)** on `windows-latest` (requires Windows for Windows TFM):
  - Setup .NET 10 + MAUI workloads (cached)
  - Restore, build `MintedTextEditor.Maui`
  - Pack dry-run to validate packaging

### Publish Workflow (`.github/workflows/publish.yml`)
- [x] Trigger: push tag `v*` or `workflow_dispatch` with version input
- [x] Resolve version from tag or input
- [x] **Job 1 — Pack Core & SkiaSharp** on `ubuntu-latest`:
  - Build + pack with source-link, symbol packages
  - Upload `.nupkg` and `.snupkg` artifacts
- [x] **Job 2 — Pack MAUI** on `windows-latest`:
  - Build + pack with source-link, symbol packages
  - Upload `.nupkg` and `.snupkg` artifacts
- [x] **Job 3 — Publish to NuGet.org**:
  - Download all package artifacts
  - `dotnet nuget push` to NuGet.org using `NUGET_API_KEY` secret
- [x] **Job 4 — Create GitHub Release**:
  - Generate release notes
  - Attach `.nupkg` files to release

### Git
- [x] Commit: "Phase 21 — CI/CD pipeline"

---

## Phase 22 — Documentation & README

Comprehensive documentation for users and contributors.

### README.md
- [x] Project overview and tagline
- [x] Feature highlights with screenshots/GIFs
- [x] Architecture diagram (Core → SkiaSharp → MAUI layers)
- [x] Quick start guide:
  - NuGet install commands
  - `builder.UseMintedTextEditor()` in `MauiProgram.cs`
  - XAML and C# usage examples
- [x] Feature comparison table vs. Telerik / Syncfusion
- [x] Platform support matrix (iOS, Android, Mac Catalyst, Windows)
- [x] Configuration reference (toolbar customization, theming, events)
- [x] Contributing guide link
- [x] License badge, NuGet badge, CI badge

### API Documentation (`docs/`)
- [x] `docs/getting-started.md` — installation, basic setup, first editor
- [x] `docs/document-model.md` — explains Block, Inline, TextRun, etc.
- [x] `docs/formatting.md` — character and paragraph formatting guide
- [x] `docs/toolbar-customization.md` — custom toolbar layouts
- [x] `docs/theming.md` — built-in themes, custom themes
- [x] `docs/commands-events.md` — command system, event reference
- [x] `docs/html-interop.md` — HTML import/export
- [x] `docs/images-hyperlinks.md` — image and hyperlink handling
- [x] `docs/tables.md` — table creation and editing
- [x] `docs/accessibility.md` — accessibility features and guidelines
- [x] `docs/architecture.md` — in-depth architecture overview for contributors

### Other Files
- [x] `CONTRIBUTING.md` — contribution guidelines, code style, PR process
- [x] `CODE_OF_CONDUCT.md` — standard code of conduct
- [x] `CHANGELOG.md` — version history (initially empty template)

### Git
- [x] Commit: "Phase 22 — Documentation & README"

---

## Phase 23 — Sample Application

A .NET MAUI sample app demonstrating all editor features.

### Sample App (`samples/SampleApp.Maui/`)
- [x] Main page: full-featured editor with default toolbar
- [x] Demo pages:
  - Basic text editing
  - Rich formatting showcase (all character + paragraph styles)
  - Custom toolbar layout
  - HTML import/export (textarea for HTML source)
  - Load/save document
  - Theme switcher (Light / Dark / High Contrast)
  - Hyperlink and image demo
  - Table editing demo
  - Read-only mode demo
  - Event monitor (displays fired events in a log panel)
  - Custom command demo
  - Localization demo
- [x] Navigation: `Shell` or `TabbedPage` with demo list
- [x] Supports all MAUI platforms (iOS, Android, Mac Catalyst, Windows)

### Git
- [x] Commit: "Phase 23 — Sample application"

---

## Phase 24 — Performance & Polish

Final optimization pass and quality improvements.

### Performance
- [ ] Profile rendering: ensure <16ms frame time for typical documents
- [x] Virtualized rendering: only render visible blocks/lines (skip off-screen content)
- [x] Layout caching: only re-layout changed blocks, not entire document
- [ ] Paint object pooling: ensure zero per-frame allocations for paints/fonts
- [ ] Large document support: test with 50,000+ paragraph documents
- [x] Image downsampling for display — `IImageProvider` interface with `DownsampleAsync` + `IsAvailable`
- [ ] Lazy layout: defer layout of off-screen content

### Scrolling
- [x] Smooth scrolling with inertia — `InertialScroller` (5-sample window, friction=0.92, 60fps tick)
- [ ] Mouse wheel / trackpad scroll
- [x] Touch-based panning with velocity tracking — integrated in `MintedEditorView.OnCanvasTouch`
- [x] Scrollbar rendering: vertical scrollbar (track + thumb) — `DocumentRenderer.RenderScrollbar`; optional via `ShowScrollbar` bindable property
- [ ] Scroll-to-position API

### Polish
- [x] Word-wrap toggle — `TextLayoutEngine.WordWrap`; `IsWordWrap` bindable property on `MintedEditorView`
- [x] Read-only mode — `IsReadOnly` property disables all editing (implemented in prior phase)
- [ ] Placeholder text when document is empty
- [x] Line numbers (optional gutter) — `DocumentRenderer.RenderLineNumbersGutter`; `ShowLineNumbers` bindable property
- [x] Find & Replace — `FindReplaceEngine` with `Find`, `FindNext`, `FindPrevious`, `FindAll`, `Replace`, `ReplaceAll`; `FindOptions` (MatchCase, WholeWord, WrapAround)
- [x] Spell-check integration point — `ISpellCheckProvider` interface
- [x] Print support integration point — `IPrintProvider` interface
- [x] Focus management — `EditorFocusChanged` event; `Focus()`/`Unfocus()` overrides on `MintedEditorView`

### Git
- [x] Commit: "Phase 24 — Performance & polish"

---

## Phase 25 — UX Parity Hardening (Post-Review)

Targeted fixes identified during end-to-end QA against Telerik/Syncfusion/CKEditor/Tiptap interaction patterns.

### Table Editing Parity
- [x] Resize columns by drag handle with persisted per-column widths
- [x] Resize rows by drag handle with persisted per-row heights
- [x] Insert/delete row and column from toolbar-accessible controls
- [x] Delete table from toolbar-accessible controls
- [x] Ensure users can place caret and type below a terminal table via tap

### Toolbar Parity & Usability
- [x] Expose all core editing/insert/table/object operations through toolbar controls
- [x] Add context-aware action dropdowns (Edit/Object/Table) to reduce toolbar clutter
- [x] Improve icon fit and control sizing to prevent clipped/cutoff glyph rendering

### Theme & Font Behavior
- [x] Add configurable system-theme following behavior for MAUI host
- [x] Fix dark-mode readability by honoring theme default text color for default-styled runs
- [x] Fix collapsed-caret font switching so subsequent typing uses selected font settings

### Validation
- [x] Focused core test suites: table/input/toolbar/font/theme/layout
- [x] MAUI iOS simulator build validation after UX parity changes

### Git
- [ ] Commit: "Phase 25 — UX parity hardening"

---

## Phase 26 — Toolbar & Selection Quality Fixes (Post-Review II)

Targeted fixes identified during end-to-end QA on mobile (iOS/Android) and desktop targets.

### Toolbar Icon Rendering
- [ ] Fix blurry/low-resolution toolbar icon rendering — use high-quality `SKPaint` filter when drawing `SKBitmap` icons in `SkiaDrawingContext.DrawImage`
- [ ] Increase SVG rasterization target size from `max(64, 36×scale)` to `max(96, ButtonSize×2×scale)` so icons are always sharp at HiDPI density
- [ ] Increase bitmap icon draw area from 56 % of button size to 72 % so icons are more legible

### Toolbar Icon Clipping / Proportional Layout
- [ ] Replace all hardcoded pixel offsets in vector icon helpers (`DrawVectorIcon`) with proportional fractions of the current `rect` so icons render correctly at every `ButtonSize`
- [ ] Add a `Save`/`ClipRect`/`Restore` guard around each vector-icon drawing call to guarantee icons never draw outside their allocated button rectangle
- [ ] Fix undo/redo arc representation to use proportional curves rather than plain lines

### Toolbar Overflow — configurable max rows with ellipsis overflow button
- [ ] Add `MaxRows` property to `ToolbarDefinition` (0 = unlimited) and parallel `MaxRows` property on `ToolbarRenderer`
- [ ] When `LayoutMode == Wrap && MaxRows > 0`, items that would start on a row beyond `MaxRows` are collected as overflow items instead of being drawn
- [ ] Reserve a fixed `OverflowButtonWidth` slot at the trailing edge of the last allowed row and render a `…` button there
- [ ] Extend `HitTest` to return the synthetic overflow button; tapping it opens a drop-down panel listing all overflow items
- [ ] Overflow panel items fire their normal commands just like regular toolbar items
- [ ] Expose `ToolbarMaxRows` bindable property on `MintedEditorView` (default 0 = unlimited)

### Text Selection Stability After Formatting
- [ ] Add `DocumentEditor.NormalizePosition(Document doc, DocumentPosition pos)` — converts a `DocumentPosition` to a canonical `(blockIndex, inlineIndex, offset)` by walking inline lengths so stale inline indices after run-splitting are corrected
- [ ] Call `NormalizePosition` on `Selection.Anchor` and `Selection.Active` inside `MintedEditorView.ExecuteToolbarCommand` after every formatting command that touches the document
- [ ] Guard `SelectionRenderer.Render` with an inline-length bounds check so an out-of-range offset never throws and visually shows the nearest valid position instead

### Tests
- [ ] Toolbar overflow: `ToolbarRendererTests` — verify items beyond `MaxRows` are excluded and the overflow button rect is populated
- [ ] Selection normalization: add cases to `SelectionTests` verifying anchor/active remain correct after bold/italic/underline toggles that split runs

### Git
- [ ] Commit: "Phase 26 — Toolbar & selection quality fixes"

---

## Architecture Summary

```
┌──────────────────────────────────────────────────────────────┐
│                    MintedTextEditor.Maui                      │
│  ┌────────────────┐  ┌──────────────┐  ┌──────────────────┐ │
│  │ EditorView      │  │ Keyboard     │  │ MAUI Providers   │ │
│  │ (SKCanvasView)  │  │ Proxy        │  │ (Clipboard,      │ │
│  │                 │  │ (hidden      │  │  Hyperlink,      │ │
│  │                 │  │  Entry)      │  │  Image)          │ │
│  └───────┬─────────┘  └──────┬───────┘  └────────┬─────────┘ │
│          │ Touch/Pointer      │ Key events        │            │
└──────────┼────────────────────┼───────────────────┼────────────┘
           │                    │                   │
           ▼                    ▼                   │
┌──────────────────────────────────────────────────────────────┐
│                 MintedTextEditor.SkiaSharp                    │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ SkiaDrawingContext : IDrawingContext                     │  │
│  │ (SKCanvas, cached SKPaint/SKFont/SKTypeface per frame) │  │
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
           │ implements
           ▼
┌──────────────────────────────────────────────────────────────┐
│                   MintedTextEditor.Core                       │
│                                                              │
│  Rendering/          Document/         Editing/              │
│  ├─ IDrawingContext   ├─ Document       ├─ Caret             │
│  ├─ EditorPaint       ├─ Block          ├─ Selection         │
│  ├─ EditorFont        ├─ Paragraph      ├─ DocumentEditor    │
│  ├─ EditorRect        ├─ TextRun        ├─ UndoManager       │
│  ├─ EditorColor       ├─ ImageInline    ├─ EditSession       │
│  └─ PaintCache        ├─ HyperlinkInl.  └─ IClipboardProv.  │
│                       ├─ TableBlock                          │
│  Layout/              ├─ TextStyle     Formatting/           │
│  ├─ TextLayoutEngine  ├─ ParaStyle     ├─ FormattingEngine   │
│  ├─ LayoutLine        └─ DocPosition   ├─ ParagraphFmtEng.  │
│  ├─ LayoutRun                          ├─ FontFmtEngine     │
│  ├─ LayoutBlock       Input/           ├─ HyperlinkEngine   │
│  ├─ DocumentLayout    ├─ EditorInput   ├─ ImageEngine       │
│  └─ LayoutCache       ├─ HitTester    └─ TableEngine       │
│                       └─ InputEvents                        │
│  Commands/            Toolbar/         Html/                 │
│  ├─ IEditorCommand    ├─ ToolbarItem   ├─ HtmlImporter      │
│  ├─ CommandRegistry   ├─ ToolbarDef.   └─ HtmlExporter      │
│  └─ Built-in cmds    └─ ToolbarRndr                        │
│                                                              │
│  Theming/             ContextMenu/     Events/               │
│  ├─ EditorTheme       ├─ ContextMenu   └─ (all editor       │
│  └─ EditorStyle       └─ CtxMenuRndr      events)           │
└──────────────────────────────────────────────────────────────┘
```

---

## Feature Parity Reference

| Feature | Telerik | Syncfusion | MintedTextEditor |
|---|---|---|---|
| Bold / Italic / Underline | ✅ | ✅ | Phase 8 |
| Strikethrough | ✅ | ✅ | Phase 8 |
| Subscript / Superscript | ✅ | ✅ | Phase 8 |
| Font Family | ✅ | ✅ | Phase 10 |
| Font Size | ✅ | ✅ | Phase 10 |
| Text Color | ✅ | ✅ | Phase 10 |
| Highlight Color | ✅ | ✅ | Phase 10 |
| Text Alignment (L/C/R/J) | ✅ | ✅ | Phase 9 |
| Bullet List | ✅ | ✅ | Phase 9 |
| Numbered List | ✅ | ✅ | Phase 9 |
| Indent / Outdent | ✅ | ✅ | Phase 9 |
| Headings (H1–H6) | ❌ | ✅ | Phase 9 |
| Hyperlink CRUD | ✅ | ✅ | Phase 11 |
| Open Hyperlink | ✅ | ✅ | Phase 11 |
| Image Insert / Edit | ✅ | ✅ | Phase 12 |
| Image Resize | ✅ | ❌ | Phase 12 |
| Tables | ❌ | ❌ | Phase 13 |
| Undo / Redo | ✅ | ✅ | Phase 7 |
| Copy / Cut / Paste | ✅ | ✅ | Phase 6 |
| Select All | ✅ | ✅ | Phase 4 |
| Clear Formatting | ✅ | ❌ | Phase 8 |
| Toolbar (auto-generated) | ✅ | ✅ | Phase 15 |
| Toolbar (custom layout) | ✅ | ✅ | Phase 15 |
| Context Menu | ✅ | ❌ | Phase 16 |
| Commands (ICommand) | ✅ | ❌ | Phase 14 |
| Rich Event System | ✅ | ✅ | Phase 14 |
| HTML Import / Export | ✅ | ❌ | Phase 17 |
| Theming (Light/Dark/HC) | ✅ | ✅ | Phase 18 |
| RTL Support | ❌ | ✅ | Phase 19 |
| Localization | ❌ | ✅ | Phase 19 |
| Accessibility | ✅ | ✅ | Phase 19 |
| Read-Only Mode | ✅ | ❌ | Phase 24 |
| Placeholder Text | ❌ | ❌ | Phase 24 |
| Find & Replace | ❌ | ❌ | Phase 24 |
| Spell Check Integration | ❌ | ❌ | Phase 24 |
| Word Wrap Toggle | ❌ | ✅ | Phase 24 |
| Cross-platform Core | ❌ | ❌ | ✅ (by design) |
| Swappable Drawing Backend | ❌ | ❌ | ✅ (by design) |
| No Native UI Controls | ❌ | ❌ | ✅ (by design) |

---

## Key Design Decisions

1. **Document Model**: Tree-based (`Document` → `Block` → `Inline` → `TextRun`) rather than flat-buffer. Enables clean structural operations (split/merge paragraphs, table cells as sub-documents).

2. **Undo System**: Action-based (command pattern) rather than state-snapshot. Lower memory usage, supports fine-grained merging of character inserts.

3. **Layout Engine**: Block-level caching — only re-layout blocks that changed. Each block's layout is independent, enabling efficient partial updates.

4. **Rendering Pipeline**: `Document` → `TextLayoutEngine` → `DocumentLayout` → `DocumentRenderer` → `IDrawingContext`. Clean one-way data flow.

5. **Input Architecture**: Platform events → `EditorInputController` → command dispatch. Mirrors KumikoUI's `GridInputController` pattern for clean separation.

6. **Toolbar**: Fully custom-drawn on the same canvas (not native widgets). Consistent look across platforms, themeable, extensible.

7. **HTML as Interchange Format**: HTML is the primary import/export format (matching Telerik). Rich clipboard also uses HTML.

8. **Extensibility Points**: `IDrawingContext` (rendering backend), `IClipboardProvider` (clipboard), `IHyperlinkHandler` (link opening), `IImageProvider` (image loading), `ISpellCheckProvider` (spell-check), `IEditorCommand` (custom commands), events for all operations.
