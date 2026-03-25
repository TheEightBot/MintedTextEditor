using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Theming;

/// <summary>
/// Factory that produces pre-configured <see cref="EditorStyle"/> instances.
/// </summary>
public static class EditorTheme
{
    /// <summary>Returns a pre-configured style for the given <paramref name="mode"/>.</summary>
    public static EditorStyle Create(EditorThemeMode mode) => mode switch
    {
        EditorThemeMode.Light         => CreateLight(),
        EditorThemeMode.Dark          => CreateDark(),
        EditorThemeMode.HighContrast  => CreateHighContrast(),
        _                             => CreateLight()
    };

    /// <summary>Clean white/light theme.</summary>
    public static EditorStyle CreateLight() => new()
    {
        // Surface
        Background    = EditorColor.White,
        BorderColor   = new EditorColor(204, 204, 204),
        BorderWidth   = 1f,

        // Text
        DefaultTextColor  = EditorColor.Black,
        DefaultFontFamily = "Segoe UI",
        DefaultFontSize   = 12f,

        // Caret
        CaretColor = EditorColor.Black,
        CaretWidth = 2f,

        // Selection
        SelectionHighlightColor = EditorColor.CornflowerBlue,
        SelectionTextColor      = EditorColor.White,

        // Hyperlink
        HyperlinkColor      = new EditorColor(0,   102, 204),
        HyperlinkHoverColor = new EditorColor(0,    70, 153),

        // Toolbar
        ToolbarBackground    = new EditorColor(245, 245, 245),
        ToolbarButtonColor   = new EditorColor( 60,  60,  60),
        ToolbarSeparatorColor= new EditorColor(200, 200, 200),
        ToolbarHoverColor    = new EditorColor(220, 220, 220),
        ToolbarActiveColor   = new EditorColor(190, 210, 240),

        // Context menu
        ContextMenuBackground = EditorColor.White,
        ContextMenuTextColor  = EditorColor.Black,
        ContextMenuHoverColor = new EditorColor(220, 235, 255),

        // Scrollbar
        ScrollbarTrackColor = new EditorColor(240, 240, 240),
        ScrollbarThumbColor = new EditorColor(160, 160, 160),
        ScrollbarWidth      = 6f,

        // Line numbers
        LineNumbersGutterColor = new EditorColor(248, 248, 248),
        LineNumbersTextColor   = new EditorColor(150, 150, 150),
        LineNumbersGutterWidth = 48f,

        // Focus
        FocusRingColor = new EditorColor(0, 120, 215),

        // Spacing
        LineSpacing      = 2f,
        ParagraphSpacing = 8f,

        // Padding
        Padding = EditorPadding.All(8f)
    };

    /// <summary>Dark mode theme.</summary>
    public static EditorStyle CreateDark() => new()
    {
        // Surface
        Background  = new EditorColor( 30,  30,  30),
        BorderColor = new EditorColor( 70,  70,  70),
        BorderWidth = 1f,

        // Text
        DefaultTextColor  = new EditorColor(212, 212, 212),
        DefaultFontFamily = "Segoe UI",
        DefaultFontSize   = 12f,

        // Caret
        CaretColor = new EditorColor(220, 220, 220),
        CaretWidth = 2f,

        // Selection
        SelectionHighlightColor = new EditorColor( 38,  79, 120),
        SelectionTextColor      = new EditorColor(212, 212, 212),

        // Hyperlink
        HyperlinkColor      = new EditorColor( 86, 156, 214),
        HyperlinkHoverColor = new EditorColor(156, 200, 254),

        // Toolbar
        ToolbarBackground     = new EditorColor( 37,  37,  38),
        ToolbarButtonColor    = new EditorColor(204, 204, 204),
        ToolbarSeparatorColor = new EditorColor( 68,  68,  68),
        ToolbarHoverColor     = new EditorColor( 60,  60,  60),
        ToolbarActiveColor    = new EditorColor( 50,  80, 120),

        // Context menu
        ContextMenuBackground = new EditorColor( 45,  45,  45),
        ContextMenuTextColor  = new EditorColor(212, 212, 212),
        ContextMenuHoverColor = new EditorColor( 60,  80, 110),

        // Scrollbar
        ScrollbarTrackColor = new EditorColor( 40,  40,  40),
        ScrollbarThumbColor = new EditorColor(100, 100, 100),
        ScrollbarWidth      = 6f,

        // Line numbers
        LineNumbersGutterColor = new EditorColor( 35,  35,  35),
        LineNumbersTextColor   = new EditorColor(100, 100, 100),
        LineNumbersGutterWidth = 48f,

        // Focus
        FocusRingColor = new EditorColor(0, 120, 215),

        // Spacing
        LineSpacing      = 2f,
        ParagraphSpacing = 8f,

        // Padding
        Padding = EditorPadding.All(8f)
    };

    /// <summary>High-contrast accessibility theme (black background, white text).</summary>
    public static EditorStyle CreateHighContrast() => new()
    {
        // Surface
        Background  = EditorColor.Black,
        BorderColor = EditorColor.White,
        BorderWidth = 2f,

        // Text
        DefaultTextColor  = EditorColor.White,
        DefaultFontFamily = "Segoe UI",
        DefaultFontSize   = 12f,

        // Caret
        CaretColor = EditorColor.White,
        CaretWidth = 3f,

        // Selection
        SelectionHighlightColor = EditorColor.Yellow,
        SelectionTextColor      = EditorColor.Black,

        // Hyperlink
        HyperlinkColor      = new EditorColor( 55, 218, 255),
        HyperlinkHoverColor = EditorColor.Yellow,

        // Toolbar
        ToolbarBackground     = EditorColor.Black,
        ToolbarButtonColor    = EditorColor.White,
        ToolbarSeparatorColor = EditorColor.White,
        ToolbarHoverColor     = new EditorColor( 50,  50,  50),
        ToolbarActiveColor    = new EditorColor(255, 255,   0),

        // Context menu
        ContextMenuBackground = EditorColor.Black,
        ContextMenuTextColor  = EditorColor.White,
        ContextMenuHoverColor = new EditorColor( 50,  50,  50),

        // Scrollbar
        ScrollbarTrackColor = EditorColor.Black,
        ScrollbarThumbColor = EditorColor.White,
        ScrollbarWidth      = 8f,

        // Line numbers
        LineNumbersGutterColor = EditorColor.Black,
        LineNumbersTextColor   = EditorColor.White,
        LineNumbersGutterWidth = 52f,

        // Focus
        FocusRingColor = EditorColor.Yellow,

        // Spacing
        LineSpacing      = 2f,
        ParagraphSpacing = 8f,

        // Padding
        Padding = EditorPadding.All(8f)
    };
}
