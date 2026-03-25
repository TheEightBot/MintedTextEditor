using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Input;

/// <summary>
/// Represents a configurable keyboard shortcut handled by <see cref="EditorInputController"/>.
/// </summary>
public sealed class EditorKeyBinding
{
    public EditorKeyBinding(
        EditorKey key,
        InputModifiers modifiers,
        Func<EditorInputController, Document.Document, DocumentLayout, IDrawingContext, bool> handler)
    {
        Key = key;
        Modifiers = modifiers;
        Handler = handler;
    }

    /// <summary>The key that triggers this binding.</summary>
    public EditorKey Key { get; }

    /// <summary>Required modifiers (Shift/Control/Alt/Meta).</summary>
    public InputModifiers Modifiers { get; }

    /// <summary>The action executed when the binding is matched.</summary>
    public Func<EditorInputController, Document.Document, DocumentLayout, IDrawingContext, bool> Handler { get; }

    internal bool Matches(EditorKeyEventArgs e)
        => e.Key == Key && Normalize(e.Modifiers) == Normalize(Modifiers);

    private static InputModifiers Normalize(InputModifiers modifiers)
        => modifiers & (InputModifiers.Shift | InputModifiers.Control | InputModifiers.Alt | InputModifiers.Meta);
}
