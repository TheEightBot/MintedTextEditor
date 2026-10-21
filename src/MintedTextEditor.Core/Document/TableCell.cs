using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Document;

/// <summary>
/// A single cell within a <see cref="TableRow"/>. Contains a list of <see cref="Block"/> elements
/// (much like a mini-document) and carries cell-level formatting.
/// </summary>
public class TableCell
{
    /// <summary>The block content of this cell (paragraphs, nested tables, etc.).</summary>
    public List<Block> Blocks { get; } = new();

    /// <summary>Number of columns this cell spans (1 = normal, >1 = merged).</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>Number of rows this cell spans (1 = normal, >1 = merged).</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>Optional background colour for this cell, filled behind its content (and over its whole span).</summary>
    public EditorColor? Background { get; set; }

    /// <summary>
    /// Per-side border widths (in pixels), drawn in <see cref="TableStyle.BorderColor"/>. null uses
    /// <see cref="TableStyle.BorderWidth"/> on every side. Where two cells share an edge, the wider border is drawn.
    /// </summary>
    public EditorBorder? Border { get; set; }

    /// <summary>The parent row that owns this cell.</summary>
    public TableRow? Parent { get; internal set; }

    /// <summary>
    /// Whether this cell has been absorbed into a merged span (set by <c>TableEngine.MergeCells</c>).
    /// Layout skips any slot covered by another cell's <see cref="RowSpan"/> / <see cref="ColumnSpan"/>.
    /// </summary>
    public bool IsMerged { get; internal set; }

    /// <summary>Creates a cell containing a single empty paragraph.</summary>
    public TableCell()
    {
        Blocks.Add(new Paragraph());
    }

    /// <summary>Gets the plain text content of this cell.</summary>
    public string GetText() => string.Join("\n", Blocks.Select(b => b.GetText()));
}

/// <summary>Per-side border specification (widths in pixels).</summary>
public sealed class EditorBorder
{
    /// <summary>Top border width.</summary>
    public float Top { get; set; }
    /// <summary>Right border width.</summary>
    public float Right { get; set; }
    /// <summary>Bottom border width.</summary>
    public float Bottom { get; set; }
    /// <summary>Left border width.</summary>
    public float Left { get; set; }

    /// <summary>Creates a border with the same width on all sides.</summary>
    public EditorBorder(float uniform = 1f)
        => Top = Right = Bottom = Left = uniform;

    /// <summary>Creates a border with individual widths per side.</summary>
    public EditorBorder(float top, float right, float bottom, float left)
        => (Top, Right, Bottom, Left) = (top, right, bottom, left);
}
