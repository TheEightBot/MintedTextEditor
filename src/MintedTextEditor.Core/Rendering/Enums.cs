namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Specifies how a shape or path is painted.
/// </summary>
public enum PaintStyle
{
    /// <summary>Fill the interior of the shape.</summary>
    Fill,
    /// <summary>Draw only the outline of the shape.</summary>
    Stroke,
    /// <summary>Fill the interior and draw the outline.</summary>
    FillAndStroke
}

/// <summary>
/// Horizontal text alignment options.
/// </summary>
public enum TextAlignment
{
    /// <summary>Align to the left edge.</summary>
    Left,
    /// <summary>Center horizontally.</summary>
    Center,
    /// <summary>Align to the right edge.</summary>
    Right,
    /// <summary>Spread words so each line except the last of a paragraph fills the available width.</summary>
    Justify
}

/// <summary>
/// Vertical text alignment options.
/// </summary>
public enum VerticalAlignment
{
    /// <summary>Align to the top edge.</summary>
    Top,
    /// <summary>Center vertically.</summary>
    Center,
    /// <summary>Align to the bottom edge.</summary>
    Bottom
}
