using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Commands;

/// <summary>
/// Bundles the objects that a command needs to act on the editor state.
/// </summary>
public sealed class EditorContext
{
    public Document.Document Document { get; }
    public Selection Selection { get; }
    public UndoManager UndoManager { get; }
    public FormattingEngine FormattingEngine { get; }
    public FontFormattingEngine FontFormattingEngine { get; }
    public IClipboardProvider? Clipboard { get; }

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
