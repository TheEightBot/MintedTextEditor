using MintedTextEditor.Core.Input;

namespace MintedTextEditor.Maui.Input;

/// <summary>
/// An invisible, platform-backed view that captures hardware keyboard input
/// and routes it to the editor's <see cref="EditorInputController"/>.
/// Add this as a transparent overlay on top of the canvas, then call
/// <c>VisualElement.Focus()</c> to direct keyboard events here.
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

    /// <summary>
    /// Raised when the platform capture view gains (<see langword="true"/>) or loses
    /// (<see langword="false"/>) native keyboard focus — including focus moving to a
    /// sibling native control, which MAUI's own Focused/Unfocused events don't reliably
    /// report for this internal view. Drives the editor's caret and focus state.
    /// </summary>
    internal event EventHandler<bool>? NativeFocusChanged;

    internal void RaiseNativeFocusChanged(bool isFocused) => NativeFocusChanged?.Invoke(this, isFocused);

    /// <summary>
    /// Exact-range edit target for IME composition (set by the editor). Platforms with a
    /// composing input model (Android) route composition through this instead of
    /// synthesizing Backspace keys, so edits never over-delete.
    /// </summary>
    internal IImeEditSink? ImeSink { get; set; }

    /// <summary>
    /// Supplies the caret paragraph's text and selection so the IME can answer
    /// "text around the cursor" queries truthfully (set by the editor).
    /// </summary>
    internal Func<ImeTextContext>? ImeContextProvider { get; set; }

    internal ImeTextContext GetImeTextContext() => ImeContextProvider?.Invoke() ?? ImeTextContext.Empty;

    internal void RaiseTextInput(string text) => TextInput?.Invoke(this, text);
    internal void RaiseKeyDown(EditorKeyEventArgs args) => KeyDown?.Invoke(this, args);

    /// <summary>
    /// Requests that the platform soft keyboard be shown. Unlike <c>VisualElement.Focus()</c>,
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
