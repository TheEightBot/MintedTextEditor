using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Commands;

/// <summary>
/// Bundles the objects that a command needs to act on the editor state.
/// </summary>
public sealed class EditorContext
{
    /// <summary>The document being edited.</summary>
    public Document.Document Document { get; }
    /// <summary>The current selection and caret within <see cref="Document"/>.</summary>
    public Selection Selection { get; }
    /// <summary>Undo/redo history for the document.</summary>
    public UndoManager UndoManager { get; }
    /// <summary>Engine used for character formatting such as bold and italic.</summary>
    public FormattingEngine FormattingEngine { get; }
    /// <summary>Engine used for font family, size, and colour formatting.</summary>
    public FontFormattingEngine FontFormattingEngine { get; }
    /// <summary>Clipboard provider, or null when no clipboard is available.</summary>
    public IClipboardProvider? Clipboard { get; }

    /// <summary>Creates a context from the given editor components.</summary>
    /// <param name="document">The document being edited.</param>
    /// <param name="selection">The current selection.</param>
    /// <param name="undoManager">Undo/redo history for the document.</param>
    /// <param name="formattingEngine">Character formatting engine.</param>
    /// <param name="fontFormattingEngine">Font formatting engine.</param>
    /// <param name="clipboard">Optional clipboard provider.</param>
    public EditorContext(
        Document.Document document,
        Selection selection,
        UndoManager undoManager,
        FormattingEngine formattingEngine,
        FontFormattingEngine fontFormattingEngine,
        IClipboardProvider? clipboard = null)
    {
        Document = document;
        Selection = selection;
        UndoManager = undoManager;
        FormattingEngine = formattingEngine;
        FontFormattingEngine = fontFormattingEngine;
        Clipboard = clipboard;
    }

    /// <summary>
    /// Applies a character-formatting operation to the current selection as one undo step,
    /// keeping the selection on the same text afterwards.
    /// </summary>
    /// <remarks>
    /// When the selection is empty, <paramref name="apply"/> runs directly and nothing is
    /// recorded: the formatting engines only update their pending style in that case.
    /// Otherwise an <see cref="ApplyStyleAction"/> is pushed to <see cref="UndoManager"/>.
    /// </remarks>
    /// <param name="description">Description of the undo step, e.g. "Bold".</param>
    /// <param name="apply">
    /// Formats the range it is given, e.g. <c>r =&gt; ctx.FormattingEngine.ToggleBold(ctx.Document, r)</c>.
    /// </param>
    public void ApplyCharacterFormatting(string description, Action<TextRange> apply)
    {
        var (anchor, active) = DocumentEditor.ApplyCharacterFormatting(
            Document, Selection.Anchor, Selection.Active,
            range =>
            {
                if (range.IsEmpty)
                    apply(range);
                else
                    UndoManager.Push(new ApplyStyleAction(Document, range, apply, description));
            });
        Selection.Set(anchor, active);
    }
}
