# Changelog

All notable changes to MintedTextEditor will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- **Context menu**: `MintedEditorView` now shows the built-in context menu, drawn on the canvas in the theme's context-menu colors. It opens on a right-click and, on touch screens, on a long press on the selection or a long press released without dragging (long press then drag still moves the caret with the magnifier). Tap or click an item, or use Up/Down/Enter; Escape, a press outside, scrolling outside it or focus loss close it. The menu is as wide as its longest label and stays inside the editor; a menu taller than the editor scrolls (finger drag, mouse wheel or arrow keys). Cut/Copy/Paste use the keyboard-shortcut clipboard path, and Insert/Edit Hyperlink, Insert Image and Insert Table prompt like the toolbar (editing a link can be undone; an `InsertTableCommand` added by the host runs without prompting). New `IsContextMenuEnabled` property (default `true`) and `ContextMenuItemsRequested` event to customize the items. Item labels and prompts come from `EditorStrings.Current` (new `EditHyperlinkDialogTitle` and `EditHyperlinkDialogSave` strings for the edit-link prompt); an open menu closes when `EditorStrings.Current` is replaced.
- `ContextMenuRenderer` color properties (`BackgroundColor`, `BorderColor`, `SeparatorColor`, `TextColor`, `DisabledTextColor`, `HoverColor`), `ApplyStyle(EditorStyle)` and `Bounds`; an `Open` overload that sizes the popup to its labels; `FontSize` and `TrailingPadding`; and `ScrollOffset`, `MaxScrollOffset` and `ScrollBy` for menus taller than the viewport, which `Open` now shrinks to fit (and `HitTest` and arrow-key navigation follow the scroll).
- `EditHyperlinkAction`, an undoable change to a hyperlink's URL and title.
- **Basic screen-reader support**: `MintedEditorView` now sets `SemanticProperties.Description` (editor label, "read only" state, and the placeholder while empty) and `SemanticProperties.Hint` from `EditorStrings`, keeps them up to date as `IsReadOnly`, `PlaceholderText` and the document change, announces read-only changes (also when the app supplies its own description), and hides its internal canvas from the accessibility tree. On Android and iOS the keyboard-input view is hidden too and a screen-reader double-tap focuses the editor through the canvas; on Windows the keyboard-input `TextBox` stays accessible with the editor's description and hint so Narrator can focus it. Values the app sets itself are left alone. Document content and toolbar buttons are still not exposed to screen readers.
- `EditorAccessibility.ForEditor` (with an optional `label` replacing the default label) and `EditorAccessibility.IsEmpty` in `MintedTextEditor.Core.Accessibility`, which compute that description and hint, and `EditorStrings.EditorReadOnlyAccessibilityState` ("read only").
- **Editor events that were defined but never raised**: `MintedEditorView` now raises `ContentLoaded` (after `LoadHtml`/`LoadMarkdown`), `IsReadOnlyChanged`, `ImageInserted`/`ImageRemoved` (after edits, undo and redo), and formatting-state events for the caret or selection: `FontFamilyChanged`, `FontSizeChanged`, `FontAttributesChanged`, `TextDecorationsChanged`, `TextColorChanged`, `HighlightTextColorChanged`, `TextFormattingChanged`, `HorizontalTextAlignmentChanged` and `ListTypeChanged`. Formatting events fire only when the value actually changes; the latest values are in the new `CurrentFormatting` property. See [Commands & Events](docs/commands-events.md#events).
- Core: `FormattingState`, `FormattingStateTracker`, `DocumentImageTracker` and `TrackedImage` (in `MintedTextEditor.Core.Events`), which work out when those events fire.
- `MintedEditorView.ShowFocusRing` (default `false`): a thin ring in the theme's `FocusRingColor` around the editor while it has keyboard focus.
- `DocumentRenderer.SelectionTextColor` / `SelectionRects`, `DocumentRenderer.HoveredHyperlink` / `HyperlinkHoverColor`, `SelectionRenderer.GetSelectionRects`, `DocumentHitTester.HitTestHyperlink`, `ToolbarRenderer.HoverColor` / `HoveredItem` / `UpdateHover` / `ClearHover`, `TextLayoutEngine.DefaultLineSpacing` / `DefaultParagraphSpacing`, and `ParagraphStyle.IsLineSpacingSet` / `IsSpaceAfterSet`.
- `EditorStrings.CurrentChanged` (raised when `EditorStrings.Current` is replaced) and `EditorStrings.SubscribeCurrentChangedWeak` (listens without keeping the listener alive), `ToolbarItem.LocalizedLabel` for labels that follow `EditorStrings`, a `ContextMenuFactory.CreateDefault(ctx, strings)` overload, and new strings for the remaining built-in text: `Subscript`, `Superscript`, `HeadingLevel`, `EditMenu`, `ObjectMenu`, `TableMenu`, `DefaultFontFamily`, `RemoveImage`, `InsertTableEllipsis`, `HyperlinkDialogMessage`, `HyperlinkDialogInsert`, and the `ImageDialog*` and `TableDialog*` prompt strings.
- **Find and replace on `MintedEditorView`**: `FindNext`, `FindPrevious`, `Replace` and `ReplaceAll` select and scroll to matches (wrapping by default, with match-case and whole-word options), and a `FindReplaceCompleted` event reports the match count and current match. Each `Replace`/`ReplaceAll` call is a single undo step and keeps the match's formatting. The logic lives in the new Core class `EditorFindReplace`. There is no built-in find UI.
- **Table styling is rendered**: `TableStyle.BorderWidth`, `BorderColor`, `CellPadding`, `HasHeaderRow` and `HeaderBackground`, plus `TableCell.Background` and `TableCell.Border`, now affect layout and drawing (borders collapse: the wider side of a shared edge is drawn, and wide outer borders are drawn inside the table's bounds). `TableLayoutBlock` exposes `BorderWidth`, `BorderColor`, `CellInfo`, `GetCellBounds`, `GetOwningCell`, `GetColumnX` and `GetRowY`.
- **Merged table cells are laid out**: a cell with `ColumnSpan`/`RowSpan` occupies the combined area of the slots it covers, a tall spanning cell grows the last row it covers, and taps in that area hit it. Covered cells are not drawn, `Tab`/`Shift+Tab` skip them, the caret (from `Ctrl+End`, arrows, `Tab` or a tap) always lands in the spanning cell so typing edits the visible cell, and border drags ignore grid lines hidden inside a merged cell. `TableBlock.GetSpanAnchor` returns the cell occupying a slot; `TableLayoutBlock.FindColumnResizeBoundary` / `FindRowResizeBoundary` find the grid line a pointer can drag.

### Changed
- `ContextMenuRenderer` arrow-key navigation skips disabled items, Enter never returns a disabled item and `HitTest` does not focus one, and the default colors follow the light theme (pale-blue highlight with normal text instead of cornflower blue with white text).
- `ContextMenuFactory.CreateDefault` shows Edit/Remove/Open Hyperlink when the caret is on a link, not only when a selection starts on one, matching where the hyperlink commands act.
- `ISpellCheckProvider` (`MTE001`), `IPrintProvider` (`MTE002`) and `ITextShaper` / `ShapedTextResult` / `DefaultTextShaper` (`MTE003`) are now marked `[Experimental]`: the editor does not use them yet, so code that references them now reports the diagnostic and must suppress it. See [Experimental APIs](docs/architecture.md#experimental-apis).
- The package version is set once in `Directory.Build.props` instead of `1.0.0` in each project, so local and CI pack runs produce the current version rather than 1.0.0.
- **Theme properties now take effect**: `SelectionTextColor` recolors selected text, `HyperlinkHoverColor` recolors the link under the mouse pointer, `ToolbarHoverColor` fills the hovered toolbar button or drop-down, `FocusRingColor` draws the focus ring, and `LineSpacing` / `ParagraphSpacing` set the spacing of paragraphs that don't set their own `ParagraphStyle.LineSpacing` / `SpaceAfter`. Hover needs a platform that reports mouse movement (Windows).
- The built-in themes' `LineSpacing` is now 0 (was 2, unused), and `EditorStyle.ParagraphSpacing` defaults to 8, so existing documents lay out exactly as before.
- Default English labels of built-in toolbar and context menu items now match the `EditorStrings` defaults (sentence case, e.g. "Font family", "Select all", "Numbered list", "Edit hyperlink…"), and the heading dropdown lists "Normal text" and "Heading 1"–"Heading 6" instead of "Normal" and "H1"–"H6". Code that finds built-in items by `Label` should match on `Icon` instead.
- `TableStyle.BorderColor` defaults to grey (160,160,160), the colour tables have always been drawn with, instead of black, so existing tables look the same now that the property is used.

### Removed
- `SkiaSharpEngine`, a placeholder class whose only member, `IsAvailable`, always returned `true`.

### Fixed
- **Typing at a bare caret**: typed text now inherits the formatting of the character before the caret (or after it at the start of a paragraph) instead of falling back to the default style, so typing after a bold word stays bold. A pending style picked from the toolbar or a shortcut still wins, and now starts from the caret's formatting, so toggling Bold inside bold text turns it off. Typing or pasting at the start or end of a hyperlink with nothing selected no longer extends the link; text that replaces part of a link (typing over a selection, find and replace) stays in it. Pressing Enter carries the caret's formatting into the new paragraph, so typing after Enter at the end of bold text stays bold. New helpers: `DocumentEditor.GetInsertionStyle` and `FormattingEngine.GetEffectiveCaretStyle`.
- **Toolbar state at a bare caret**: the Bold, Italic, Underline, Strikethrough, Subscript and Superscript buttons now show the formatting the next typed text will get (the pending style, or the formatting at the caret) when nothing is selected, instead of always showing inactive.
- **`SelectionChanged` after undo, redo and loading**: `MintedEditorView.Undo()` and `Redo()` raise `SelectionChanged` when they move the caret, and `LoadHtml`/`LoadMarkdown` raise it after resetting the caret.
- **Undo for character formatting**: bold, italic, underline, strikethrough, subscript, superscript and clear formatting (toolbar, Ctrl+B/I/U and commands), the font family and size dropdowns, and the text and highlight colour pickers now record one undo step each. Undo restores the previous runs with their exact styles and redo reapplies the change. Toggling with a collapsed caret still only sets the pending style and is not recorded. `ApplyStyleAction` gains a constructor that wraps any formatting operation, and its undo no longer resets the paragraph style or restores the wrong paragraph when a table lies inside the range.
- **Removing a hyperlink**: `HyperlinkEngine.RemoveHyperlink` keeps the link text's formatting instead of turning it into one plain run, and `RemoveHyperlinkCommand` (toolbar and context menu) is now undoable through the new `RemoveHyperlinkAction`.
- **Editing in and around tables**: character formatting (toolbar, shortcuts, commands, `DocumentEditor.ApplyTextStyle` and the formatting engines) now applies to selected text in table cells, including selections spanning several cells (cells hidden by a merge are skipped, here and in copy and the toolbar state), and the toolbar toggles and undo work there; previously nothing changed but an undo step was recorded. Inserting a hyperlink in a table cell no longer throws, and is undoable. `DocumentEditor.GetSelectedText` (copy, cut) returns the text selected in table cells instead of an empty string, and now includes the text of hyperlinks (only the selected part of a partly selected link) and a newline for each line break, which it used to drop everywhere; so cutting linked text copies it, and linking already-linked text keeps that text in place. Undoing the deletion of a selection that runs across a table between two paragraphs (which deletes the table) now restores the table and both paragraphs, and undoing a deletion inside a cell restores the cell's text. `DocumentEditor.InsertText` at the start of an image now inserts the text before the image instead of after it. See [Tables](docs/tables.md#editing-cell-content).
- **Undo restoring copies**: undoing a delete, paragraph merge, formatting change or link removal puts back the original runs, images, links and line breaks instead of copies, so undoing an earlier image or link insertion afterwards still removes it. Line breaks no longer come back as `"\n"` text, and images and line breaks inside links are no longer dropped.
- **`InsertHyperlinkCommand.DisplayText`** is now used: when set, the link is inserted with that text at the caret, or replaces the selected text. Undoing a hyperlink insertion now restores the original text with its formatting (previously it came back unformatted), and redoing a link made from a selection no longer throws. A link inserted with the caret right after an image or another link now goes after it rather than before. Undoing a deletion or a link insertion keeps soft line breaks, images and links (with all their content) as the same objects instead of converting them to text or copies, so earlier undo steps such as an image insertion still work, and undoing one that spanned paragraphs refreshes the layout of every paragraph after them.
- Changing the theme's `HyperlinkColor` now recolors links already laid out, instead of only links in paragraphs edited afterwards.
- When the document gets shorter (e.g. smaller theme spacing), the editor no longer stays scrolled past its end.
- **Localization**: `EditorStrings` is now used by the editor. Built-in toolbar labels, the `MintedEditorView` toolbar menus (font "Default", heading levels, Edit/Object/Table menu options), the default context menu and the insert hyperlink/image/table prompts read `EditorStrings.Current`, and replacing it at runtime updates an editor already on screen. Toolbar dropdown lists now widen to fit their longest option.
- **Hit-testing long lines that need font fallback** (performance): `SkiaDrawingContext` keeps the font-fallback segmentation (and segment offsets) of its most recently measured runs in a small bounded cache, instead of re-segmenting the whole run for every caret query, and finds a caret's segment by binary search, so lines that switch font hundreds of times stay fast too. Tapping in a long unwrapped line containing an emoji no longer takes quadratic time (40 taps on a 300×`a` + 😀 + 300×`b` line: ~1050 ms → ~11 ms).
- `FindReplaceEngine` no longer returns bogus matches at the start of a table block when a table's text contains the query; only top-level paragraphs are searched.
- **`ToolbarMaxRows` lost on toolbar rebuild**: assigning a new `ToolbarDefinition`, changing `ToolbarItems` or clearing them no longer drops the row limit set with `MintedEditorView.ToolbarMaxRows`, which previously showed every row again. Once set, `ToolbarMaxRows` now takes precedence over `ToolbarDefinition.MaxRows` (which still applies while `ToolbarMaxRows` is unset; setting or clearing it redraws the toolbar even when the number is unchanged) and is no longer written into the app's definition object. New `ToolbarRenderer.MaxRowsOverride` and `EffectiveMaxRows` expose the same behaviour in Core.
- **HTML round-trip of colors and table spans**: the HTML importer now reads `rgba(...)` colors (alpha as 0–1 or a percentage), `#rgba`/`#rrggbbaa`, space-separated `rgb(r g b / a)` and percentage channels, so text and highlight colors with transparency survive export and re-import. `colspan`/`rowspan` are read into `TableCell.ColumnSpan`/`RowSpan` with merged placeholder cells, matching `TableEngine.MergeCells`, and rows are padded to the widest row, and a table nested in a cell no longer ends the outer table. The exporter now writes the `rgba` alpha with a `.` decimal separator in every culture.
- **Undo for paragraph formatting**: the alignment, list, indent, heading and paragraph-format (`Normal`/`Heading1`–`6`/`Quote`) commands, and the toolbar's heading dropdown, are now recorded on the `UndoManager` as one step per command, even across a multi-paragraph selection. Undo restores each paragraph's previous style and redo reapplies it. The new `ApplyParagraphStyleAction` makes other paragraph style changes undoable. The `ParagraphFormattingEngine` documentation no longer claims its methods are undoable; they still change the document directly.

---

## [0.3.1] — 2026-10-21

### Added
- XML documentation for every public type and member in `MintedTextEditor.Core`, `MintedTextEditor.SkiaSharp` and `MintedTextEditor.Maui`, so IntelliSense describes the whole API and the build is free of documentation warnings.
- A CI check that fails a pull request changing `src/` without an entry under `[Unreleased]` in this file (add the `skip-changelog` label when no entry is needed).

### Changed
- README, CONTRIBUTING and every page under `docs/` checked against the current API and corrected: many code samples used type and member names that don't exist (e.g. `RichTextEditor`, `ImportHtml`, `EditorTheme.Dark`, `ToolbarItemKind`) and are now rewritten to compile. Added the 0.2–0.3 features (caret magnifier, focus events, Markdown, text shaping, emoji handling), and stated plainly what is not supported yet (screen readers, some theme and table properties).
- This changelog now covers every release, including 0.1.1, and the initial release is correctly listed as 0.1.0.

### Fixed
- **Images from a custom `ImageProvider`**: a bitmap returned by `ImageProvider` is no longer replaced (and leaked) by decoding the image name as an app-package asset, or lost when that asset decode fails.
- **Wrapping very long unbroken text**: character wrapping finds grapheme boundaries once per text fragment and searches each line from its own position, measuring only text near the line end. A 20,000-character token with no spaces now lays out in linear rather than quadratic time. Lines still always take at least one whole grapheme.
- **Hit-testing long unwrapped lines**: caret offsets within a shaped run are found by binary search over its glyph clusters instead of a scan from the start, so tapping in a line thousands of characters long no longer takes quadratic time.
- **Shaping cache for long runs**: shaped runs longer than 256 characters are kept in a small least-recently-used cache, bounded by total glyph count, instead of a single slot. A line split into several long segments (e.g. text either side of an emoji, with word wrap off) no longer reshapes them in turn on every caret query.

---

## [0.3.0] — 2026-10-20

### Added
- **Caret drag and magnifier (#2)**: on touch screens, press and hold to pick up the caret and drag it. A magnifier (loupe) above the finger shows the caret's line, zoomed. It looks like the native magnifier by default (`MagnifierOptions.Apple` / `MagnifierOptions.Android`), and you can change it with `MintedEditorView.MagnifierOptions` (size, corner radius, zoom, offset, border, shadow) or turn it off with `IsMagnifierEnabled`.
- `IDrawingContext.Scale`, `IDrawingContext.ClipRoundRect` and `IDrawingContext.MeasureCaretOffset` (default implementations provided, so existing implementations keep compiling). The Skia context measures caret offsets within the whole shaped run, so the caret and hit-testing match kerned text.

### Fixed
- **Emoji ZWJ sequences (#9)**: 👨‍👩‍👧, 👩‍💻 and other joined emoji draw as one glyph instead of separate people; flags draw as one flag.
- **Grapheme clusters (#9)**: whole ZWJ sequences stay together when truncating with an ellipsis, wrapping long unbroken text and hit-testing a tap, so an emoji is never cut in half.
- **Emoji presentation (#9)**: `❤️` and other VS16 sequences use the color emoji font even when the text font has a monochrome glyph, and emoji such as 😀 no longer fall back to a monochrome font (e.g. DejaVu Sans on Linux).
- **Bitmap updates (#9)**: changes to a mutable `SKBitmap` after it was first drawn now show. Only immutable bitmaps are cached; icons and images the editor loads are marked immutable so they stay cached.

### Changed
- Text in the Skia renderer is shaped with HarfBuzz (new dependency: `HarfBuzzSharp`), adding kerning, ligatures and complex-script shaping (each script in a run is shaped with its own rules; glyphs stay in logical order, matching caret placement). Line widths may change slightly. Set `SkiaDrawingContext.ShapeAllText = false` at startup to shape only emoji and fallback-font text.

---

## [0.2.0] — 2026-10-20

### Added
- **Markdown import/export**: `MarkdownImporter` and `MarkdownExporter` (with `MarkdownExportOptions` for GitHub-flavoured tables and strikethrough, and line endings), `Document.GetMarkdown()` / `AppendMarkdown()` / `MarkdownDocumentExtensions.LoadMarkdown()`, and `GetMarkdown()` / `LoadMarkdown()` / `AppendMarkdown()` on `MintedEditorView`.

### Fixed
- **Android input (#5)**: soft keyboard now appears on tap/focus; hardware-keyboard printable keys and Ctrl shortcuts are handled; IME composing text (`setComposingText`) is applied as you type; the Return key (committed as `"\n"`) splits the paragraph instead of inserting a literal newline.
- Formatting toggles (e.g. superscript) no longer drift the selection, lose text, or leave the Android IME composing against stale state.
- Character formatting no longer leaks past the selection to the end of the paragraph.
- Emoji render via Skia font fallback; variation-selector sequences (e.g. 🛠️) no longer draw a trailing box.
- **Focus / caret (#6)**: the caret is hidden and stops blinking when focus moves to another control (e.g. an `Entry`), on all platforms. The blink timer runs only while the editor is focused, so an idle editor no longer repaints 10× per second.
- **Android IMEs other than Gboard (#6)**: the input connection now reports the real text around the caret to the keyboard (correct auto-capitalisation and word boundaries), adopts `setComposingRegion` so Samsung/MIUI no longer duplicate words, handles `deleteSurroundingTextInCodePoints` (whole-emoji delete), `performEditorAction` and numpad Enter, and never deletes the selection on `deleteSurroundingText`.
- **Android suggestions**: picking a Gboard suggestion (`replaceText`, API 34+) now replaces the word; previously it was silently dropped.
- Backspace, Delete and Left/Right arrow step over a whole grapheme cluster (emoji, skin-tone and ZWJ sequences, flags, combining marks) instead of splitting it.
- Backspace at the start of a paragraph (and Delete at the end) left the caret at the end of the joined paragraph instead of at the join point.
- Bold and italic text rendered as regular since the SkiaSharp 4 update: the default family ("Default") was resolved without applying the requested style. Styles are now matched within the platform default family, with synthetic bold/italic when a family lacks the face.
- Crash when measuring or hit-testing text that splits an emoji (lone surrogate) in the Skia renderer; such text now also measures correctly instead of as zero width.
- Crash on Android hardware keyboards with dead keys (accents on international layouts); accents now combine with the next key.
- Formatting a selection that starts and ends partway through runs formatted only part of it.
- A formatting toggle with no selection no longer restructures runs (which could leave stale layout and jump the caret).
- Undo/Redo after formatting changes no longer uses stale positions (undo deleted nothing; redo duplicated text).

### Changed
- `EditorFocusChanged` now fires on real native focus changes (tap into the editor, focus moving to another control), exactly once per change. Previously it fired only from `Focus()`/`Unfocus()`. New read-only `IsEditorFocused`; `Unfocus()` now actually removes native focus. The editor starts unfocused (no caret until focused).
- SkiaSharp / SkiaSharp.Views.Maui.Controls 3.119.2 → 4.153.1 and Svg.Skia 3.2.1 → 5.2.3 for current MAUI 10 support.
- Sample app builds on the current MAUI SDK (10.0.110): icon/splash SVGs no longer use `<text>`, which crashes Resizetizer 10.0.110.

---

## [0.1.1] — 2026-03-26

### Changed
- CI and publish workflows no longer cache NuGet packages and install the MAUI workload in one step. No library changes.

---

## [0.1.0] — 2026-03-25

Initial release.

### Added
- **Document model**: `Document`, `Block`, `Paragraph`, `TableBlock`, `Inline`, `TextRun`, `HyperlinkInline`, `ImageInline`, `LineBreak`
- **Character formatting**: bold, italic, underline, strikethrough, subscript, superscript, font family, font size, foreground color, background color
- **Paragraph formatting**: alignment (left, center, right, justify), heading levels (H1–H6), bullet lists, numbered lists, indent/outdent, line spacing, space before/after, blockquote, RTL direction
- **Tables**: insert/delete rows and columns, column width control, cell merge/split, header cells
- **Images**: insert from bytes/stream/file, display size, alt text, resize handle
- **Hyperlinks**: insert, edit, remove; `HyperlinkClicked` event and `IHyperlinkHandler`; URL validation at click time
- **HTML import/export**: round-trip HTML via `HtmlImporter`/`HtmlExporter` (`LoadHtml`/`GetHtml`) with inline style and attribute support
- **Undo/redo**: unlimited history, operation merging for consecutive character input
- **Clipboard**: cut, copy, paste with plain text and rich (internal format) support
- **Find/Replace**: case-sensitive and case-insensitive search, replace one/all
- **Spell-check integration point**: `ISpellCheckProvider` interface (not yet used by the control)
- **Embedded icon packs**: Lucide, Heroicons, and Material Symbols icon sets embedded as assembly resources in `MintedTextEditor.Core` — no app-bundle asset setup required. Icons are rasterized at the correct display resolution via Svg.Skia.
- **Theme-aware icon tinting**: icons are stored as black-ink SVGs and recolored at draw time using `SKColorFilter` (SrcIn blend mode) to match the current theme foreground color; dark-mode toolbars are fully supported.
- **Custom toolbar**: `ToolbarDefinition`, `ToolbarGroup` and `ToolbarItem` types, built-in and custom items, `ToolbarLayoutMode`
- **Theming**: built-in Light, Dark, and High Contrast themes (`EditorTheme`); custom themes via `EditorStyle`
- **Command system**: `IEditorCommand`, `EditorCommandRegistry`, keyboard binding customization
- **Accessibility and localization**: keyboard-only navigation, a high-contrast theme, right-to-left paragraph direction, and `EditorStrings` for localized text (not yet read by the toolbar or menus)
- **Platforms**: Android (API 21+), iOS (15.0+), macOS/Mac Catalyst (15.0+), Windows (10 version 1809+, when built on Windows)
- **Rendering**: fully custom-drawn via SkiaSharp — no native `TextView`/`UITextView`/`WebView` wrapping
- **NuGet packages**: `MintedTextEditor.Core`, `MintedTextEditor.SkiaSharp`, `MintedTextEditor.Maui`
- **CI/CD**: GitHub Actions pipeline for build, test, and NuGet publish
- **Documentation**: Getting Started, Document Model, Formatting, Toolbar Customization, Theming, Commands & Events, HTML Interop, Images & Hyperlinks, Tables, Accessibility, Architecture

---

[Unreleased]: https://github.com/TheEightBot/MintedTextEditor/compare/v0.3.1...HEAD
[0.3.1]: https://github.com/TheEightBot/MintedTextEditor/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/TheEightBot/MintedTextEditor/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/TheEightBot/MintedTextEditor/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/TheEightBot/MintedTextEditor/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/TheEightBot/MintedTextEditor/releases/tag/v0.1.0
