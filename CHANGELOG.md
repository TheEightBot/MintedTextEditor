# Changelog

All notable changes to MintedTextEditor will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [1.0.0] — 2026-03-25

### Added
- **Document model**: `Document`, `Block`, `Paragraph`, `TableBlock`, `Inline`, `TextRun`, `HyperlinkInline`, `ImageInline`, `LineBreak`
- **Character formatting**: bold, italic, underline, strikethrough, subscript, superscript, font family, font size, foreground color, background color
- **Paragraph formatting**: alignment (left, center, right, justify), heading levels (H1–H6), bullet lists, numbered lists, indent/outdent, line spacing, space before/after, blockquote, RTL direction
- **Tables**: insert/delete rows and columns, column width control, cell merge/split, header cells
- **Images**: insert from bytes/stream/file, display size, alt text, resize handle
- **Hyperlinks**: insert, edit, remove; `HyperlinkActivated` event; URL validation at click time
- **HTML import/export**: round-trip HTML via `ImportHtml`/`ExportHtml` with full inline style and attribute support
- **Undo/redo**: unlimited history, operation merging for consecutive character input
- **Clipboard**: cut, copy, paste with plain text and rich (internal format) support
- **Find/Replace**: case-sensitive and case-insensitive search, replace one/all
- **Spell-check integration point**: `ISpellCheckProvider` interface, red squiggle rendering
- **Embedded icon packs**: Lucide, Heroicons, and Material Symbols icon sets embedded as assembly resources in `MintedTextEditor.Core` — no app-bundle asset setup required. Icons are rasterized at the correct display resolution via Svg.Skia.
- **Theme-aware icon tinting**: icons are stored as black-ink SVGs and recolored at draw time using `SKColorFilter` (SrcIn blend mode) to match the current theme foreground color; dark-mode toolbars are fully supported.
- **Custom toolbar**: `ToolbarDefinition`, `ToolbarGroup`, built-in and custom `ToolbarItemKind` values
- **Theming**: built-in Light, Dark, and High Contrast themes; full custom `EditorTheme`
- **Command system**: `IEditorCommand`, `CommandRegistry`, keyboard binding customization
- **Accessibility**: screen reader support (TalkBack, VoiceOver, Narrator), keyboard-only navigation, high contrast, RTL, localization via `IEditorStringProvider`
- **Platforms**: Android (API 21+), iOS (15.0+), macOS/Mac Catalyst (15.0+), Windows (10 build 1803+)
- **Rendering**: fully custom-drawn via SkiaSharp — no native `TextView`/`UITextView`/`WebView` wrapping
- **NuGet packages**: `MintedTextEditor.Core`, `MintedTextEditor.SkiaSharp`, `MintedTextEditor.Maui`
- **CI/CD**: GitHub Actions pipeline for build, test, and NuGet publish
- **Documentation**: Getting Started, Document Model, Formatting, Toolbar Customization, Theming, Commands & Events, HTML Interop, Images & Hyperlinks, Tables, Accessibility, Architecture

---

[Unreleased]: https://github.com/TheEightBot/MintedTextEditor/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/TheEightBot/MintedTextEditor/releases/tag/v1.0.0
