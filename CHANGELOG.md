# Changelog

All notable changes to MintedTextEditor will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Changed
- The package version is set once in `Directory.Build.props` instead of `1.0.0` in each project, so local and CI pack runs produce the current version rather than 1.0.0.

### Fixed
- **Hit-testing long lines that need font fallback** (performance): `SkiaDrawingContext` keeps the font-fallback segmentation (and segment offsets) of its most recently measured runs in a small bounded cache, instead of re-segmenting the whole run for every caret query. Tapping in a long unwrapped line containing an emoji no longer takes quadratic time (40 taps on a 300×`a` + 😀 + 300×`b` line: ~1050 ms → ~11 ms).

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
