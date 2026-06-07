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

    /// <summary>
    /// Raised when the editor wants the platform soft keyboard shown.
    /// The platform handler subscribes to this and brings up the IME.
    /// </summary>
    internal event EventHandler? ShowKeyboardRequested;

    /// <summary>
    /// Raised when the editor wants the platform soft keyboard hidden.
    /// </summary>
    internal event EventHandler? HideKeyboardRequested;

    /// <summary>
    /// Raised when the editor's selection or document content has changed programmatically
    /// (e.g. after a toolbar formatting toggle) so the platform IME can be updated.
    /// On Android the handler calls <c>InputMethodManager.UpdateSelection</c> and resets
    /// the composing state to prevent the IME from operating on a stale model (which
    /// would cause spurious character deletions).  No-op on platforms whose handler does
    /// not subscribe.
    /// </summary>
    internal event EventHandler? SelectionChangedNotified;

    internal void RaiseTextInput(string text) => TextInput?.Invoke(this, text);
    internal void RaiseKeyDown(EditorKeyEventArgs args) => KeyDown?.Invoke(this, args);

    /// <summary>
    /// Requests that the platform soft keyboard be shown. Unlike <see cref="View.Focus"/>,
    /// this also re-shows the keyboard when the proxy already holds focus (e.g. the user
    /// dismissed the keyboard with the back button and then tapped the editor again).
    /// No-op on platforms whose handler does not subscribe.
    /// </summary>
    internal void ShowKeyboard() => ShowKeyboardRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Requests that the platform soft keyboard be hidden.</summary>
    internal void HideKeyboard() => HideKeyboardRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Notifies the platform that the editor's selection or content has changed, so the
    /// IME can update its cursor/composing model.  No-op when no handler is subscribed.
    /// </summary>
    internal void NotifySelectionChanged() => SelectionChangedNotified?.Invoke(this, EventArgs.Empty);
}
