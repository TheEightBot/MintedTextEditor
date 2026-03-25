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

    /// <summary>Optional background colour for this cell.</summary>
    public EditorColor? Background { get; set; }

    /// <summary>Per-side border widths (in pixels). null means inherit from <see cref="TableStyle"/>.</summary>
    public EditorBorder? Border { get; set; }

    /// <summary>The parent row that owns this cell.</summary>
    public TableRow? Parent { get; internal set; }

    /// <summary>Whether this cell has been absorbed into a merged span and should be skipped during layout.</summary>
    public bool IsMerged { get; internal set; }

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
    public float Top { get; set; }
    public float Right { get; set; }
    public float Bottom { get; set; }
    public float Left { get; set; }

    public EditorBorder(float uniform = 1f)
        => Top = Right = Bottom = Left = uniform;

    public EditorBorder(float top, float right, float bottom, float left)
        => (Top, Right, Bottom, Left) = (top, right, bottom, left);
}
