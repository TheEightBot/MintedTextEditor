using MintedTextEditor.Core.Input;

namespace MintedTextEditor.Maui.Input;

/// <summary>
/// An invisible, platform-backed view that captures hardware keyboard input
/// and routes it to the editor's <see cref="EditorInputController"/>.
/// Add this as a transparent overlay on top of the canvas, then call
/// <see cref="View.Focus"/> to direct keyboard events here.
/// </summary>
internal sealed class KeyboardProxy : View
{
    /// <summary>Raised when the user types printable text (one or more characters).</summary>
    public event EventHandler<string>? TextInput;

    /// <summary>Raised when a navigation or editing key is pressed.</summary>
    public event EventHandler<EditorKeyEventArgs>? KeyDown;

    internal void RaiseTextInput(string text) => TextInput?.Invoke(this, text);
    internal void RaiseKeyDown(EditorKeyEventArgs args) => KeyDown?.Invoke(this, args);
}
