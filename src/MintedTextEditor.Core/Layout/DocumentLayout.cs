namespace MintedTextEditor.Core.Layout;

/// <summary>
/// Complete layout result for an entire document.
/// Produced by <see cref="TextLayoutEngine"/> and consumed by the renderer and hit-testing.
/// </summary>
public class DocumentLayout
{
    /// <summary>Layout results for each block in the document.</summary>
    public List<LayoutBlock> Blocks { get; } = new();

    /// <summary>Total height of the entire laid-out document.</summary>
    public float TotalHeight { get; set; }

    /// <summary>Viewport width used during layout.</summary>
    public float ViewportWidth { get; set; }
}
