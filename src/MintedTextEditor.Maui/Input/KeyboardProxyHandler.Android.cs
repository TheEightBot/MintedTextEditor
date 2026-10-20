// Android implementation of the KeyboardProxy handler.
// Compiled only for android (see the Compile conditions in the .csproj).

using System.Text;
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

    /// <summary>Dead-key (accent) state shared by the view and its InputConnection.</summary>
    internal DeadKeyComposer DeadKeys { get; } = new(KeyCharacterMap.GetDeadChar);

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

            // Tell the IME where the caret is and what text surrounds it, in the same
            // paragraph-relative coordinates the InputConnection reports. Without this the
            // IME assumes an empty field (wrong auto-caps, mis-targeted composing regions).
            var ctx = _proxy.GetImeTextContext();
            outAttrs.InitialSelStart = ctx.SelectionStart;
            outAttrs.InitialSelEnd = ctx.SelectionEnd;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
                outAttrs.SetInitialSurroundingText(new Java.Lang.String(ctx.ParagraphText));
        }
        _inputConnection.ResetComposingState();
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
            // Any in-flight composition or dead key belongs to the old focus session.
            _inputConnection.ResetComposingState();
            DeadKeys.Reset();
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
            DeadKeys.Reset();
            _proxy.RaiseKeyDown(new EditorKeyEventArgs(editorKey, '\0', mods));
            // A hardware key moved the caret or edited text: any IME composition is stale.
            NotifyImeSelectionChanged();
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
        //    Dead keys (accents on international layouts) carry a flag bit and must be
        //    combined with the next key rather than converted directly.
        var text = DeadKeys.Process(e.UnicodeChar);
        if (text is not null)
        {
            if (text.Length > 0) _proxy.RaiseTextInput(text);
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
        Keycode.NumpadEnter => EditorKey.Enter,
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
/// InputConnection that routes IME input to the editor. Composition logic lives in the
/// platform-neutral <see cref="ImeCompositionTracker"/>; this class adapts Android's
/// <c>InputConnection</c> contract to it and answers the IME's "text around the cursor"
/// queries from the editor's real paragraph text (paragraph-relative offsets). That is
/// what Samsung Keyboard and MIUI rely on for auto-capitalisation, re-composing an
/// existing word (<c>setComposingRegion</c>) and word/emoji deletion.
/// </summary>
internal sealed class KeyboardInputConnection : BaseInputConnection
{
    private readonly Android.Views.View _view;
    private readonly KeyboardProxy _proxy;
    private readonly ImeCompositionTracker _tracker;

    public KeyboardInputConnection(Android.Views.View targetView, KeyboardProxy proxy)
        : base(targetView, false)
    {
        _view = targetView;
        _proxy = proxy;
        _tracker = new ImeCompositionTracker(new ProxyImeSink(proxy));
    }

    // ── Composition ──────────────────────────────────────────────────────────

    public override bool SetComposingText(Java.Lang.ICharSequence? text, int newCursorPosition)
    {
        var str = text?.ToString() ?? string.Empty;
        _tracker.SetComposingText(str, newCursorPosition, _proxy.GetImeTextContext());
        // Composing text with a newline splits the paragraph (the tracker ends the
        // composition); re-sync the IME to the new paragraph's offsets.
        AfterEdit(paragraphChanged: ContainsNewline(str));
        return true;
    }

    public override bool SetComposingRegion(int start, int end)
    {
        _tracker.SetComposingRegion(start, end, _proxy.GetImeTextContext());
        ReportSelection();
        return true;
    }

    public override bool FinishComposingText()
    {
        _tracker.FinishComposing();
        ReportSelection();
        return true;
    }

    public override bool CommitText(Java.Lang.ICharSequence? text, int newCursorPosition)
    {
        var str = text?.ToString() ?? string.Empty;
        _tracker.CommitText(str, newCursorPosition, _proxy.GetImeTextContext());
        // A committed newline (MIUI Enter) splits the paragraph: new offset frame.
        AfterEdit(paragraphChanged: ContainsNewline(str));
        return true;
    }

    public override bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        // Running past the paragraph edge joins paragraphs (the editor handles it); the
        // IME's paragraph-relative offsets are then stale, so restart.
        AfterEdit(_tracker.DeleteSurrounding(beforeLength, afterLength, _proxy.GetImeTextContext()));
        return true;
    }

    public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
    {
        AfterEdit(_tracker.DeleteSurroundingInCodePoints(beforeLength, afterLength, _proxy.GetImeTextContext()));
        return true;
    }

    // BaseInputConnection implements these against its own private Editable, so without
    // overrides a picked suggestion (Gboard on API 34+ uses replaceText) never reaches us.
    public override bool ReplaceText(int start, int end, Java.Lang.ICharSequence text, int newCursorPosition, TextAttribute? textAttribute)
    {
        var str = text?.ToString() ?? string.Empty;
        _tracker.Replace(start, end, str, newCursorPosition, _proxy.GetImeTextContext());
        AfterEdit(paragraphChanged: ContainsNewline(str));
        return true;
    }

    public override bool CommitCompletion(CompletionInfo? text)
    {
        var str = text?.Text?.ToString() ?? string.Empty;
        _tracker.CommitText(str, 1, _proxy.GetImeTextContext());
        AfterEdit(paragraphChanged: ContainsNewline(str));
        return true;
    }

    public override bool SetSelection(int start, int end)
    {
        // setSelection does not end a composition on Android: keep the composing range.
        _tracker.SetSelection(start, end);
        ReportSelection();
        return true;
    }

    public override bool PerformEditorAction([Android.Runtime.GeneratedEnum] ImeAction actionCode)
    {
        // Despite IME_FLAG_NO_ENTER_ACTION some OEM keyboards (MIUI) deliver Enter as an
        // editor action. Treat the "submit"-style actions as a paragraph break.
        switch (actionCode)
        {
            case ImeAction.Unspecified:
            case ImeAction.None:
            case ImeAction.Done:
            case ImeAction.Go:
            case ImeAction.Send:
                _tracker.Enter();
                RestartInput();
                return true;
            default:
                return base.PerformEditorAction(actionCode);
        }
    }

    public override bool SendKeyEvent(KeyEvent? e)
    {
        if (e?.Action == KeyEventActions.Down)
        {
            var editorKey = e.KeyCode switch
            {
                Keycode.Del         => EditorKey.Backspace,
                Keycode.ForwardDel  => EditorKey.Delete,
                Keycode.Enter       => EditorKey.Enter,
                Keycode.NumpadEnter => EditorKey.Enter,
                _ => EditorKey.None,
            };
            if (editorKey != EditorKey.None)
            {
                var before = _proxy.GetImeTextContext();
                bool atStart = before.SelectionStart == 0 && before.SelectionEnd == 0;
                bool atEnd = before.SelectionStart == before.ParagraphText.Length
                             && before.SelectionEnd == before.SelectionStart;
                // Enter, or Backspace/Delete that merges paragraphs, changes the paragraph
                // the IME's offsets refer to; restart the input session in that case.
                bool frameChanges = editorKey == EditorKey.Enter
                    || (editorKey == EditorKey.Backspace && atStart)
                    || (editorKey == EditorKey.Delete && atEnd);

                _tracker.Reset();
                _proxy.RaiseKeyDown(new EditorKeyEventArgs(editorKey));
                if (frameChanges) RestartInput();
                else ReportSelection();
                return true;
            }

            // Printable character delivered as a key event (some IMEs / hardware keys).
            var deadKeys = (_view as KeyboardCaptureAndroidView)?.DeadKeys;
            var text = deadKeys is not null
                ? deadKeys.Process(e.UnicodeChar)
                : (Rune.IsValid(e.UnicodeChar) && e.UnicodeChar != 0 ? char.ConvertFromUtf32(e.UnicodeChar) : null);
            if (text is not null)
            {
                if (text.Length > 0) _proxy.RaiseTextInput(text);
                ReportSelection();
                return true;
            }
        }
        return base.SendKeyEvent(e);
    }

    // ── Queries: answered from the editor, not BaseInputConnection's empty Editable ──

    public override Java.Lang.ICharSequence? GetTextBeforeCursorFormatted(int n, [Android.Runtime.GeneratedEnum] GetTextFlags flags)
        => new Java.Lang.String(_proxy.GetImeTextContext().TextBeforeCursor(n));

    public override Java.Lang.ICharSequence? GetTextAfterCursorFormatted(int n, [Android.Runtime.GeneratedEnum] GetTextFlags flags)
        => new Java.Lang.String(_proxy.GetImeTextContext().TextAfterCursor(n));

    public override Java.Lang.ICharSequence? GetSelectedTextFormatted([Android.Runtime.GeneratedEnum] GetTextFlags flags)
    {
        var selected = _proxy.GetImeTextContext().SelectedText;
        return selected.Length == 0 ? null : new Java.Lang.String(selected);
    }

    public override SurroundingText? GetSurroundingText(int beforeLength, int afterLength, int flags)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            return null;
        var ctx = _proxy.GetImeTextContext();
        // Clamp the counts before adding: IMEs may pass very large values (int.MaxValue).
        int start = ctx.SelectionStart - Math.Clamp(beforeLength, 0, ctx.SelectionStart);
        int end = ctx.SelectionEnd + Math.Clamp(afterLength, 0, ctx.ParagraphText.Length - ctx.SelectionEnd);
        return new SurroundingText(
            new Java.Lang.String(ctx.ParagraphText[start..end]),
            ctx.SelectionStart - start,
            ctx.SelectionEnd - start,
            start);
    }

    public override ExtractedText? GetExtractedText(ExtractedTextRequest? request, [Android.Runtime.GeneratedEnum] GetTextFlags flags)
    {
        var ctx = _proxy.GetImeTextContext();
        return new ExtractedText
        {
            Text = new Java.Lang.String(ctx.ParagraphText),
            StartOffset = 0,
            PartialStartOffset = -1,
            PartialEndOffset = -1,
            SelectionStart = ctx.SelectionStart,
            SelectionEnd = ctx.SelectionEnd,
        };
    }

    public override Android.Text.CapitalizationMode GetCursorCapsMode(
        [Android.Runtime.GeneratedEnum] Android.Text.CapitalizationMode reqModes)
    {
        var ctx = _proxy.GetImeTextContext();
        return Android.Text.TextUtils.GetCapsMode(ctx.ParagraphText, ctx.SelectionStart, reqModes);
    }

    /// <summary>
    /// Discards the locally tracked composing text without touching the editor content.
    /// Called when the editor's content or selection has changed programmatically (e.g.
    /// a toolbar formatting toggle) so the next IME event starts from a clean slate
    /// rather than trying to diff against the pre-change composition.
    /// </summary>
    public void ResetComposingState() => _tracker.Reset();

    /// <summary>
    /// Re-initializes the IME session (it re-reads EditorInfo and surrounding text).
    /// Used when the caret moves to a different paragraph, since offsets are paragraph-relative.
    /// </summary>

    private void AfterEdit(bool paragraphChanged)
    {
        if (paragraphChanged) RestartInput();
        else ReportSelection();
    }

    private static bool ContainsNewline(string text)
        => text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0;

    private void RestartInput()
    {
        _tracker.Reset();
        (_view as KeyboardCaptureAndroidView)?.NotifyImeSelectionChanged();
    }

    /// <summary>
    /// Keeps the IME's cursor model in sync after each edit (cheaper than RestartInput),
    /// so Samsung/MIUI don't compose or delete against stale offsets.
    /// </summary>
    private void ReportSelection()
    {
        if (_view.Context?.GetSystemService(Context.InputMethodService) is not InputMethodManager imm)
            return;
        var ctx = _proxy.GetImeTextContext();
        imm.UpdateSelection(_view, ctx.SelectionStart, ctx.SelectionEnd,
            _tracker.ComposingStart, _tracker.ComposingEnd);
    }

    /// <summary>
    /// Forwards tracker edits to the editor's exact-range sink. Falls back to synthesized
    /// keys if no editor is attached (keeps the proxy usable on its own).
    /// </summary>
    private sealed class ProxyImeSink(KeyboardProxy proxy) : IImeEditSink
    {
        public void InsertText(string text)
        {
            if (proxy.ImeSink is { } sink) sink.InsertText(text);
            else proxy.RaiseTextInput(text);
        }

        public void DeleteBefore(int count)
        {
            if (proxy.ImeSink is { } sink) { sink.DeleteBefore(count); return; }
            for (int i = 0; i < count; i++)
                proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Backspace));
        }

        public void DeleteAfter(int count)
        {
            if (proxy.ImeSink is { } sink) { sink.DeleteAfter(count); return; }
            for (int i = 0; i < count; i++)
                proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Delete));
        }

        public void SetSelection(int start, int end) => proxy.ImeSink?.SetSelection(start, end);

        public void Enter()
        {
            if (proxy.ImeSink is { } sink) sink.Enter();
            else proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Enter));
        }
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
