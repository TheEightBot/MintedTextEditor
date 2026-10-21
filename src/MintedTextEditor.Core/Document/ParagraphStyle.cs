using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Document;

/// <summary>
/// Specifies the type of list a paragraph belongs to.
/// </summary>
public enum ListType
{
    /// <summary>Not part of a list.</summary>
    None,
    /// <summary>Bulleted (unordered) list item.</summary>
    Bullet,
    /// <summary>Numbered (ordered) list item.</summary>
    Number
}

/// <summary>
/// Specifies text direction.
/// </summary>
public enum TextDirection
{
    /// <summary>Text flows from left to right.</summary>
    LeftToRight,
    /// <summary>Text flows from right to left.</summary>
    RightToLeft
}

/// <summary>
/// Style properties for a paragraph block.
/// </summary>
public class ParagraphStyle
{
    /// <summary>Shared instance with default paragraph settings.</summary>
    public static ParagraphStyle Default { get; } = new();

    /// <summary>Horizontal alignment of the paragraph's lines.</summary>
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    /// <summary>Number of indentation levels applied to the paragraph.</summary>
    public int IndentLevel { get; set; }
    // Null until set, so layout can tell an explicit value from the default and fall back
    // to the theme's spacing (see TextLayoutEngine.DefaultLineSpacing / DefaultParagraphSpacing).
    private float? _lineSpacing;
    private float? _spaceAfter;

    /// <summary>
    /// Line height multiplier (1.0 = single spacing). Defaults to 1.0. Setting it, even to 1.0,
    /// makes the paragraph ignore the theme's extra line spacing.
    /// </summary>
    public float LineSpacing
    {
        get => _lineSpacing ?? 1.0f;
        set => _lineSpacing = value;
    }

    /// <summary>
    /// <c>true</c> when <see cref="LineSpacing"/> has been set explicitly; <c>false</c> when the
    /// layout uses the theme's line spacing for this paragraph.
    /// </summary>
    public bool IsLineSpacingSet => _lineSpacing.HasValue;

    /// <summary>Extra space above the paragraph, in pixels.</summary>
    public float SpaceBefore { get; set; }

    /// <summary>
    /// Extra space below the paragraph, in pixels. Defaults to 8. While it has not been set,
    /// the layout uses the theme's paragraph spacing instead (8 in the built-in themes).
    /// </summary>
    public float SpaceAfter
    {
        get => _spaceAfter ?? 8f;
        set => _spaceAfter = value;
    }

    /// <summary>
    /// <c>true</c> when <see cref="SpaceAfter"/> has been set explicitly; <c>false</c> when the
    /// layout uses the theme's paragraph spacing for this paragraph.
    /// </summary>
    public bool IsSpaceAfterSet => _spaceAfter.HasValue;

    /// <summary>The list type of the paragraph.</summary>
    public ListType ListType { get; set; } = ListType.None;

    /// <summary>Heading level: 0 = normal text, 1–6 = heading levels.</summary>
    public int HeadingLevel { get; set; }

    /// <summary>Text direction of the paragraph.</summary>
    public TextDirection Direction { get; set; } = TextDirection.LeftToRight;

    /// <summary>When true, this paragraph is rendered as a block quote (left border + tinted background).</summary>
    public bool IsBlockQuote { get; set; }

    /// <summary>Creates a copy of this style.</summary>
    public ParagraphStyle Clone() => new()
    {
        Alignment = Alignment,
        IndentLevel = IndentLevel,
        _lineSpacing = _lineSpacing,
        SpaceBefore = SpaceBefore,
        _spaceAfter = _spaceAfter,
        ListType = ListType,
        HeadingLevel = HeadingLevel,
        Direction = Direction,
        IsBlockQuote = IsBlockQuote
    };
}
