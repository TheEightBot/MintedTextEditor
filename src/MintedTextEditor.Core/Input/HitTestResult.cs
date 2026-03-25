using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Input;

/// <summary>
/// Result of a hit test mapping pixel coordinates to a document position.
/// </summary>
public class HitTestResult
{
    /// <summary>The document position corresponding to the hit location.</summary>
    public DocumentPosition Position { get; }

    /// <summary>The layout line that was hit (null if document is empty).</summary>
    public LayoutLine? Line { get; }

    /// <summary>The layout run that was hit (null if line is empty).</summary>
    public LayoutRun? Run { get; }

    /// <summary>True if the hit was at or beyond the end of the line.</summary>
    public bool IsAtLineEnd { get; }

    /// <summary>True if the hit was below the last block in the document.</summary>
    public bool IsAfterLastBlock { get; }

    public HitTestResult(
        DocumentPosition position,
        LayoutLine? line = null,
        LayoutRun? run = null,
        bool isAtLineEnd = false,
        bool isAfterLastBlock = false)
    {
        Position = position;
        Line = line;
        Run = run;
        IsAtLineEnd = isAtLineEnd;
        IsAfterLastBlock = isAfterLastBlock;
    }
}
