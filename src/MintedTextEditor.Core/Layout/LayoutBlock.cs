using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Layout;

/// <summary>
/// Layout result for a single <see cref="Document.Block"/>.
/// Contains the visual lines produced by laying out the block's content.
/// </summary>
public class LayoutBlock
{
    /// <summary>The visual lines in this block.</summary>
    public List<LayoutLine> Lines { get; } = new();

    /// <summary>Total height of this block including all lines and paragraph spacing.</summary>
    public float TotalHeight { get; set; }

    /// <summary>Y-offset of this block relative to the top of the document.</summary>
    public float Y { get; set; }

    /// <summary>Index of the source block in the document.</summary>
    public int BlockIndex { get; set; }

    /// <summary>Paragraph style for this block. Used by the renderer for decorations (bullets, numbers, block quotes).</summary>
    public ParagraphStyle? ParagraphStyle { get; set; }

    /// <summary>For numbered list items, the 1-based sequential number within the current list run. Zero for non-numbered blocks.</summary>
    public int ListNumber { get; set; }
}
