using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Layout;

/// <summary>
/// A single visual line produced by the layout engine.
/// Contains one or more <see cref="LayoutRun"/> items positioned horizontally.
/// </summary>
public class LayoutLine
{
    /// <summary>The positioned runs that make up this visual line.</summary>
    public List<LayoutRun> Runs { get; } = new();

    /// <summary>Y-offset of this line relative to the top of its parent block.</summary>
    public float Y { get; set; }

    /// <summary>Height of this line in pixels (max ascent + max descent + leading).</summary>
    public float Height { get; set; }

    /// <summary>Baseline offset from the top of this line (distance from Y to the text baseline).</summary>
    public float Baseline { get; set; }

    /// <summary>Total width of all runs on this line.</summary>
    public float Width => Runs.Count > 0
        ? Runs[^1].X + Runs[^1].Width - Runs[0].X
        : 0;

    /// <summary>Index of the block this line belongs to.</summary>
    public int BlockIndex { get; set; }

    /// <summary>Index of this visual line within its parent block.</summary>
    public int LineIndexInBlock { get; set; }

    /// <summary>
    /// Default X position for the caret when this line has no runs.
    /// For LTR empty lines this is 0; for RTL empty lines it is the right edge of the available area.
    /// </summary>
    public float DefaultCaretX { get; set; }

    /// <summary>
    /// Returns the bounding rectangle of this line relative to its parent block.
    /// </summary>
    public EditorRect GetBounds(float blockX = 0)
        => new(blockX, Y, Width, Height);
}
