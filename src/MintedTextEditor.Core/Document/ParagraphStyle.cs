using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Document;

/// <summary>
/// Specifies the type of list a paragraph belongs to.
/// </summary>
public enum ListType
{
    None,
    Bullet,
    Number
}

/// <summary>
/// Specifies text direction.
/// </summary>
public enum TextDirection
{
    LeftToRight,
    RightToLeft
}

/// <summary>
/// Style properties for a paragraph block.
/// </summary>
public class ParagraphStyle
{
    public static ParagraphStyle Default { get; } = new();

    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public int IndentLevel { get; set; }
    public float LineSpacing { get; set; } = 1.0f;
    public float SpaceBefore { get; set; }
    public float SpaceAfter { get; set; } = 8f;
    public ListType ListType { get; set; } = ListType.None;

    /// <summary>Heading level: 0 = normal text, 1–6 = heading levels.</summary>
    public int HeadingLevel { get; set; }

    public TextDirection Direction { get; set; } = TextDirection.LeftToRight;

    /// <summary>When true, this paragraph is rendered as a block quote (left border + tinted background).</summary>
    public bool IsBlockQuote { get; set; }

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
