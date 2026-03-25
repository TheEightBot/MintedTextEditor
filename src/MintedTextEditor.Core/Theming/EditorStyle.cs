using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Theming;

/// <summary>
/// Comprehensive visual style specification for the editor.
/// All color and spacing properties are set through instance initialization.
/// </summary>
public sealed record EditorStyle
{
    // ── Editor surface ──────────────────────────────────────────────────

    /// <summary>Editor content area background color.</summary>
    public EditorColor Background { get; init; }

    /// <summary>Border color drawn around the editor control.</summary>
    public EditorColor BorderColor { get; init; }

    /// <summary>Border width in logical pixels.</summary>
    public float BorderWidth { get; init; }

    // ── Default text ────────────────────────────────────────────────────

    /// <summary>Default text foreground color when no explicit color is applied.</summary>
    public EditorColor DefaultTextColor { get; init; }

    /// <summary>Default font family name.</summary>
    public string DefaultFontFamily { get; init; } = "Segoe UI";

    /// <summary>Default font size in points.</summary>
    public float DefaultFontSize { get; init; }

    // ── Caret ───────────────────────────────────────────────────────────

    /// <summary>Caret (text insertion point) color.</summary>
    public EditorColor CaretColor { get; init; }

    /// <summary>Caret width in logical pixels.</summary>
    public float CaretWidth { get; init; }

    // ── Selection ───────────────────────────────────────────────────────

    /// <summary>Background highlight color of selected text.</summary>
    public EditorColor SelectionHighlightColor { get; init; }

    /// <summary>Foreground color of selected text, or <c>Transparent</c> to keep original.</summary>
    public EditorColor SelectionTextColor { get; init; }

    // ── Hyperlinks ──────────────────────────────────────────────────────

    /// <summary>Default hyperlink text color.</summary>
    public EditorColor HyperlinkColor { get; init; }

    /// <summary>Hyperlink color when hovered.</summary>
    public EditorColor HyperlinkHoverColor { get; init; }

    // ── Toolbar ─────────────────────────────────────────────────────────

    /// <summary>Toolbar background color.</summary>
    public EditorColor ToolbarBackground { get; init; }

    /// <summary>Toolbar button icon/text color.</summary>
    public EditorColor ToolbarButtonColor { get; init; }

    /// <summary>Toolbar separator line color.</summary>
    public EditorColor ToolbarSeparatorColor { get; init; }

    /// <summary>Toolbar button background when hovered.</summary>
    public EditorColor ToolbarHoverColor { get; init; }

    /// <summary>Toolbar button background when active or toggled on.</summary>
    public EditorColor ToolbarActiveColor { get; init; }

    // ── Context menu ────────────────────────────────────────────────────

    /// <summary>Context menu background color.</summary>
    public EditorColor ContextMenuBackground { get; init; }

    /// <summary>Context menu item text color.</summary>
    public EditorColor ContextMenuTextColor { get; init; }

    /// <summary>Context menu item background when hovered.</summary>
    public EditorColor ContextMenuHoverColor { get; init; }

    // ── Scrollbar ───────────────────────────────────────────────────────

    /// <summary>Scrollbar track (trough) background color.</summary>
    public EditorColor ScrollbarTrackColor { get; init; }

    /// <summary>Scrollbar thumb color.</summary>
    public EditorColor ScrollbarThumbColor { get; init; }
    /// <summary>Width of the scrollbar in logical pixels.</summary>
    public float ScrollbarWidth { get; init; } = 6f;

    // ── Line numbers gutter ─────────────────────────────────────────────

    /// <summary>Background color of the line-number gutter.</summary>
    public EditorColor LineNumbersGutterColor { get; init; }

    /// <summary>Text color used for line numbers.</summary>
    public EditorColor LineNumbersTextColor { get; init; }

    /// <summary>Width of the line-number gutter in logical pixels.</summary>
    public float LineNumbersGutterWidth { get; init; } = 48f;
    // ── Focus ───────────────────────────────────────────────────────────

    /// <summary>Focus ring outline color drawn around the focused editor control.</summary>
    public EditorColor FocusRingColor { get; init; }

    // ── Spacing ─────────────────────────────────────────────────────────

    /// <summary>Extra space between lines within a paragraph, in logical pixels.</summary>
    public float LineSpacing { get; init; }

    /// <summary>Space below each paragraph in logical pixels.</summary>
    public float ParagraphSpacing { get; init; }

    // ── Padding ─────────────────────────────────────────────────────────

    /// <summary>Inner padding between the editor border and the content area.</summary>
    public EditorPadding Padding { get; init; }
}

/// <summary>
/// Inner padding on all four sides of the editor content area.
/// </summary>
public readonly record struct EditorPadding(float Top, float Right, float Bottom, float Left)
{
    /// <summary>Uniform padding on all sides.</summary>
    public static EditorPadding All(float value) => new(value, value, value, value);
}
