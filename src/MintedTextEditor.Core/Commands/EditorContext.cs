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
}
