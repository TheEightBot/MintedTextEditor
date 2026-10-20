// Android implementation of the KeyboardProxy handler.
// Compiled only for android (see the Compile conditions in the .csproj).

using Microsoft.Maui.Handlers;
using MintedTextEditor.Core.Input;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.AppCompat.Widget;

namespace MintedTextEditor.Maui.Input;

/// <summary>
/// A zero-size, transparent Android View subclass that can receive IME text input
/// and hardware key events on behalf of the editor.
/// </summary>
internal sealed class KeyboardCaptureAndroidView : Android.Views.View
{
    private readonly KeyboardProxy _proxy;
    private readonly KeyboardInputConnection _inputConnection;

    public KeyboardCaptureAndroidView(Context context, KeyboardProxy proxy) : base(context)
    {
        _proxy = proxy;
        _inputConnection = new KeyboardInputConnection(this, proxy);
        Focusable = true;
        FocusableInTouchMode = true;
    }

    public override bool OnCheckIsTextEditor() => true;

    public override IInputConnection? OnCreateInputConnection(EditorInfo? outAttrs)
    {
        if (outAttrs != null)
        {
            outAttrs.InputType = Android.Text.InputTypes.ClassText
                               | Android.Text.InputTypes.TextFlagMultiLine
                               | Android.Text.InputTypes.TextFlagNoSuggestions;
            // NoEnterAction → Enter inserts a newline instead of firing an IME action.
            // NoFullscreen → keep the IME from replacing our canvas with a fullscreen
            // extract editor in landscape (which would never reflect typed text).
            outAttrs.ImeOptions = ImeFlags.NoEnterAction | ImeFlags.NoFullscreen;
        }
        return _inputConnection;
    }

    /// <summary>
    /// Requests focus (if needed) and shows the soft keyboard. The actual
    /// <c>ShowSoftInput</c> call is posted so it runs after the view is attached
    /// and focused, which is required for the IME to appear reliably.
    /// </summary>
    public void ShowKeyboard()
    {
        if (!IsFocused)
            RequestFocus();

        Post(() =>
        {
            if (Context?.GetSystemService(Context.InputMethodService) is InputMethodManager imm)
                imm.ShowSoftInput(this, ShowFlags.Implicit);
        });
    }

    /// <summary>Hides the soft keyboard.</summary>
    public void HideKeyboard()
    {
        if (Context?.GetSystemService(Context.InputMethodService) is InputMethodManager imm)
            imm.HideSoftInputFromWindow(WindowToken, HideSoftInputFlags.None);
    }

    /// <summary>
    /// Notifies the IME that the editor's selection or content has changed programmatically
    /// (e.g. after a toolbar formatting toggle).  This prevents the IME from issuing
    /// <c>DeleteSurroundingText</c> or composing-region updates against a stale model,
    /// which would otherwise cause spurious character deletions on Android.
    ///
    /// Strategy: reset the composing state in our <see cref="KeyboardInputConnection"/>
    /// and call <c>InputMethodManager.UpdateSelection</c> so the IME re-queries the
    /// cursor position; fall back to <c>RestartInput</c> (which tears down and rebuilds
    /// the <see cref="IInputConnection"/>) when UpdateSelection is not sufficient.
    /// </summary>
    public void NotifyImeSelectionChanged()
    {
        // 1. Reset the local composing mirror so the next ReplaceComposingRegion starts fresh.
        _inputConnection.ResetComposingState();

        // 2. Tell the IME the selection is now a zero-width caret at offset 0 from its
        //    perspective.  We don't have a way to map our absolute position into the flat
        //    text offsets the IME expects without also maintaining a full text buffer here,
        //    so we use RestartInput which causes the IME to call OnCreateInputConnection
        //    again and re-initialize from our EditorInfo — the safest option.
        Post(() =>
        {
            if (Context?.GetSystemService(Context.InputMethodService) is InputMethodManager imm
                && IsFocused)
            {
                imm.RestartInput(this);
            }
        });
    }

    // Show/hide the IME automatically as the view gains or loses focus. This covers
    // the first focus (tap → MAUI Focus() → RequestFocus → here) as well as focus
    // changes triggered by the platform itself.
    protected override void OnFocusChanged(
        bool gainFocus,
        [Android.Runtime.GeneratedEnum] FocusSearchDirection direction,
        Android.Graphics.Rect? previouslyFocusedRect)
    {
        base.OnFocusChanged(gainFocus, direction, previouslyFocusedRect);
        if (gainFocus)
        {
            ShowKeyboard();
        }
        else
        {
            // Any in-flight composition belongs to the old focus session.
            _inputConnection.ResetComposingState();
            HideKeyboard();
        }
        _proxy.RaiseNativeFocusChanged(gainFocus);
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        if (e == null) return base.OnKeyDown(keyCode, e);

        var mods = MapMetaState(e.MetaState);

        // 1) Navigation / editing keys (arrows, Home/End, Backspace, Enter, …).
        var editorKey = MapKeyCode(keyCode);
        if (editorKey != EditorKey.None)
        {
            _proxy.RaiseKeyDown(new EditorKeyEventArgs(editorKey, '\0', mods));
            return true;
        }

        // 2) Ctrl/Cmd keyboard shortcuts from a hardware keyboard (Ctrl+B, Ctrl+Z, …).
        //    Route these as key-downs with modifiers; never insert them as text.
        bool hasCtrlOrMeta = (mods & (InputModifiers.Control | InputModifiers.Meta)) != 0;
        if (hasCtrlOrMeta)
        {
            var shortcutKey = MapShortcutKeyCode(keyCode);
            if (shortcutKey != EditorKey.None)
            {
                _proxy.RaiseKeyDown(new EditorKeyEventArgs(shortcutKey, '\0', mods));
                return true;
            }
            return base.OnKeyDown(keyCode, e);
        }

        // 3) Printable character from a hardware keyboard. Without this, only the
        //    soft keyboard's InputConnection could produce text — so on devices with
        //    a (hardware/host) keyboard, typed characters were silently dropped while
        //    Backspace still worked through path (1).
        int unicode = e.UnicodeChar;
        if (unicode != 0)
        {
            _proxy.RaiseTextInput(char.ConvertFromUtf32(unicode));
            return true;
        }

        return base.OnKeyDown(keyCode, e);
    }

    private static EditorKey MapKeyCode(Keycode kc) => kc switch
    {
        Keycode.DpadLeft   => EditorKey.Left,
        Keycode.DpadRight  => EditorKey.Right,
        Keycode.DpadUp     => EditorKey.Up,
        Keycode.DpadDown   => EditorKey.Down,
        Keycode.MoveHome   => EditorKey.Home,
        Keycode.MoveEnd    => EditorKey.End,
        Keycode.PageUp     => EditorKey.PageUp,
        Keycode.PageDown   => EditorKey.PageDown,
        Keycode.Del        => EditorKey.Backspace,
        Keycode.ForwardDel => EditorKey.Delete,
        Keycode.Enter      => EditorKey.Enter,
        Keycode.Tab        => EditorKey.Tab,
        Keycode.Escape     => EditorKey.Escape,
        _ => EditorKey.None,
    };

    // Letter keys that the core controller interprets as Ctrl/Cmd shortcuts.
    private static EditorKey MapShortcutKeyCode(Keycode kc) => kc switch
    {
        Keycode.A => EditorKey.A,
        Keycode.B => EditorKey.B,
        Keycode.C => EditorKey.C,
        Keycode.I => EditorKey.I,
        Keycode.U => EditorKey.U,
        Keycode.V => EditorKey.V,
        Keycode.X => EditorKey.X,
        Keycode.Y => EditorKey.Y,
        Keycode.Z => EditorKey.Z,
        _ => EditorKey.None,
    };

    private static InputModifiers MapMetaState(MetaKeyStates meta)
    {
        var mods = InputModifiers.None;
        if ((meta & MetaKeyStates.ShiftOn)   != 0) mods |= InputModifiers.Shift;
        if ((meta & MetaKeyStates.CtrlOn)    != 0) mods |= InputModifiers.Control;
        if ((meta & MetaKeyStates.AltOn)     != 0) mods |= InputModifiers.Alt;
        if ((meta & MetaKeyStates.MetaOn)    != 0) mods |= InputModifiers.Meta;
        return mods;
    }
}

/// <summary>
/// InputConnection that routes IME input to the proxy. It supports both committed
/// text (<see cref="CommitText"/>) and, crucially, in-progress composing text
/// (<see cref="SetComposingText"/>) which is how most modern soft keyboards (e.g.
/// Gboard) deliver characters as you type. The currently-composed string is tracked
/// so it can be replaced incrementally without churning the whole word on each
/// keystroke.
/// </summary>
internal sealed class KeyboardInputConnection : BaseInputConnection
{
    private readonly KeyboardProxy _proxy;

    // The text currently held in the IME's composing region and already mirrored
    // into the editor. Empty when no composition is in progress.
    private string _composingText = string.Empty;

    public KeyboardInputConnection(Android.Views.View targetView, KeyboardProxy proxy)
        : base(targetView, false)
    {
        _proxy = proxy;
    }

    public override bool SetComposingText(Java.Lang.ICharSequence? text, int newCursorPosition)
    {
        ReplaceComposingRegion(text?.ToString() ?? string.Empty);
        return true;
    }

    public override bool FinishComposingText()
    {
        // Keep whatever is already in the editor; just stop tracking it.
        _composingText = string.Empty;
        return true;
    }

    public override bool CommitText(Java.Lang.ICharSequence? text, int newCursorPosition)
    {
        var str = text?.ToString() ?? string.Empty;

        // An empty commit finalizes the current composition without changing text.
        if (str.Length == 0)
        {
            _composingText = string.Empty;
            return true;
        }

        // Replace any active composing region with the committed text, then finalize.
        ReplaceComposingRegion(str);
        _composingText = string.Empty;
        return true;
    }

    public override bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        // A surrounding delete operates outside our tracked composition.
        _composingText = string.Empty;
        for (int i = 0; i < beforeLength; i++)
            _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Backspace));
        for (int i = 0; i < afterLength; i++)
            _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Delete));
        return true;
    }

    public override bool SendKeyEvent(KeyEvent? e)
    {
        if (e?.Action == KeyEventActions.Down)
        {
            var editorKey = e.KeyCode switch
            {
                Keycode.Del        => EditorKey.Backspace,
                Keycode.ForwardDel => EditorKey.Delete,
                Keycode.Enter      => EditorKey.Enter,
                _ => EditorKey.None,
            };
            if (editorKey != EditorKey.None)
            {
                _composingText = string.Empty;
                _proxy.RaiseKeyDown(new EditorKeyEventArgs(editorKey));
                return true;
            }

            // Printable character delivered as a key event (some IMEs / hardware keys).
            int unicode = e.UnicodeChar;
            if (unicode != 0)
            {
                _proxy.RaiseTextInput(char.ConvertFromUtf32(unicode));
                return true;
            }
        }
        return base.SendKeyEvent(e);
    }

    /// <summary>
    /// Discards the locally tracked composing text without touching the editor content.
    /// Called when the editor's content or selection has changed programmatically (e.g.
    /// a toolbar formatting toggle) so the next IME event starts from a clean slate
    /// rather than trying to diff against the pre-change composition.
    /// </summary>
    public void ResetComposingState()
    {
        _composingText = string.Empty;
    }

    /// <summary>
    /// Updates the editor so the composing region reflects <paramref name="newText"/>.
    /// Diffs against the previously composed text so the common cases (appending or
    /// deleting trailing characters) touch only the changed suffix instead of
    /// re-inserting the entire word on every keystroke.
    /// </summary>
    private void ReplaceComposingRegion(string newText)
    {
        string old = _composingText;
        if (string.Equals(newText, old, StringComparison.Ordinal))
        {
            _composingText = newText;
            return;
        }

        if (newText.StartsWith(old, StringComparison.Ordinal))
        {
            // Pure append (typing forward) — insert only the new suffix.
            _proxy.RaiseTextInput(newText.Substring(old.Length));
        }
        else if (old.StartsWith(newText, StringComparison.Ordinal))
        {
            // Pure removal (backspacing within the composition) — delete the difference.
            for (int i = 0; i < old.Length - newText.Length; i++)
                _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Backspace));
        }
        else
        {
            // General case (e.g. autocorrect) — replace the whole composing region.
            for (int i = 0; i < old.Length; i++)
                _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Backspace));
            if (newText.Length > 0)
                _proxy.RaiseTextInput(newText);
        }

        _composingText = newText;
    }
}

/// <summary>MAUI handler that backs <see cref="KeyboardProxy"/> on Android.</summary>
internal sealed partial class KeyboardProxyHandler : ViewHandler<KeyboardProxy, KeyboardCaptureAndroidView>
{
    public KeyboardProxyHandler() : base(ViewHandler.ViewMapper) { }

    protected override KeyboardCaptureAndroidView CreatePlatformView()
        => new KeyboardCaptureAndroidView(Context!, VirtualView);

    protected override void ConnectHandler(KeyboardCaptureAndroidView platformView)
    {
        base.ConnectHandler(platformView);
        VirtualView.ShowKeyboardRequested    += OnShowKeyboardRequested;
        VirtualView.HideKeyboardRequested    += OnHideKeyboardRequested;
        VirtualView.SelectionChangedNotified += OnSelectionChangedNotified;
    }

    protected override void DisconnectHandler(KeyboardCaptureAndroidView platformView)
    {
        VirtualView.ShowKeyboardRequested    -= OnShowKeyboardRequested;
        VirtualView.HideKeyboardRequested    -= OnHideKeyboardRequested;
        VirtualView.SelectionChangedNotified -= OnSelectionChangedNotified;
        base.DisconnectHandler(platformView);
    }

    private void OnShowKeyboardRequested(object? sender, EventArgs e)
        => PlatformView?.ShowKeyboard();

    private void OnHideKeyboardRequested(object? sender, EventArgs e)
        => PlatformView?.HideKeyboard();

    private void OnSelectionChangedNotified(object? sender, EventArgs e)
        => PlatformView?.NotifyImeSelectionChanged();
}
