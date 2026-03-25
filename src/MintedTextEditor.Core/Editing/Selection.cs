using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Tracks the current editor selection: a fixed anchor and a moving active end.
/// When anchor equals active the selection is empty (caret-only).
/// </summary>
public class Selection
{
    /// <summary>Fixed end of the selection — where the selection started.</summary>
    public DocumentPosition Anchor { get; private set; } = new DocumentPosition(0, 0, 0);

    /// <summary>Moving end of the selection — follows the caret.</summary>
    public DocumentPosition Active { get; private set; } = new DocumentPosition(0, 0, 0);

    /// <summary>Whether the selection is empty (no text selected).</summary>
    public bool IsEmpty => Anchor == Active;

    /// <summary>The normalized text range (Start &lt;= End).</summary>
    public TextRange Range => new TextRange(Anchor, Active);

    /// <summary>Collapses the selection to a single point, clearing any range.</summary>
    public void CollapseTo(DocumentPosition position)
    {
        Anchor = position;
        Active = position;
    }

    /// <summary>Extends the active end to <paramref name="position"/>, keeping Anchor fixed.</summary>
    public void ExtendTo(DocumentPosition position)
    {
        Active = position;
    }

    /// <summary>Sets selection to an explicit anchor/active pair.</summary>
    public void Set(DocumentPosition anchor, DocumentPosition active)
    {
        Anchor = anchor;
        Active = active;
    }

    /// <summary>Selects the entire content of <paramref name="document"/>.</summary>
    public void SelectAll(Document.Document document)
    {
        Anchor = new DocumentPosition(0, 0, 0);

        if (document.Blocks.Count == 0)
        {
            Active = Anchor;
            return;
        }

        int lastBlock = document.Blocks.Count - 1;
        var para = document.Blocks[lastBlock] as Paragraph;
        if (para is null || para.Inlines.Count == 0)
        {
            Active = new DocumentPosition(lastBlock, 0, 0);
            return;
        }

        int lastInline = para.Inlines.Count - 1;
        Active = new DocumentPosition(lastBlock, lastInline, para.Inlines[lastInline].Length);
    }
}
