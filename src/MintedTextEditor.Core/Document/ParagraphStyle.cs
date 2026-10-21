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
    /// <summary>Line height multiplier (1.0 = single spacing).</summary>
    public float LineSpacing { get; set; } = 1.0f;
    /// <summary>Extra space above the paragraph, in pixels.</summary>
    public float SpaceBefore { get; set; }
    /// <summary>Extra space below the paragraph, in pixels.</summary>
    public float SpaceAfter { get; set; } = 8f;
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
        LineSpacing = LineSpacing,
        SpaceBefore = SpaceBefore,
        SpaceAfter = SpaceAfter,
        ListType = ListType,
        HeadingLevel = HeadingLevel,
        Direction = Direction,
        IsBlockQuote = IsBlockQuote
    };
}
