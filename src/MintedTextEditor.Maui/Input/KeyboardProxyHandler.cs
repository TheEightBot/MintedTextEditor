// Platform-specific keyboard capture handler for KeyboardProxy.
// Each #if block contains the complete platform view + ViewHandler implementation.

using Microsoft.Maui.Handlers;
using MintedTextEditor.Core.Input;

#if IOS || MACCATALYST
using Foundation;
using ObjCRuntime;
using UIKit;
#elif ANDROID
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.AppCompat.Widget;
#elif WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
#endif

namespace MintedTextEditor.Maui.Input;

// ─── iOS / Mac Catalyst ─────────────────────────────────────────────────────────
#if IOS || MACCATALYST

/// <summary>
/// A transparent UIView subclass that implements UIKeyInput so it can serve as
/// the first responder for keyboard events. Navigation keys are captured via
/// UIKeyCommand entries; printable text goes through InsertText; backspace
/// fires DeleteBackward.
/// </summary>
internal sealed class KeyboardCaptureUIView : UIView, IUIKeyInput
{
    private readonly KeyboardProxy _proxy;

    // Pre-built navigation key commands (arrow keys × plain,shift,cmd variants)
    private readonly UIKeyCommand[] _keyCommands;

    public KeyboardCaptureUIView(KeyboardProxy proxy) : base(CoreGraphics.CGRect.Empty)
    {
        _proxy = proxy;
        BackgroundColor = UIColor.Clear;
        Opaque = false;
        _keyCommands = BuildKeyCommands();
    }

    // ── UIResponder ──────────────────────────────────────────────────────────────

    public override bool CanBecomeFirstResponder => true;
    public override UIKeyCommand[] KeyCommands => _keyCommands;

    // ── UIKeyInput protocol ──────────────────────────────────────────────────────

    /// Whether the receiver has any text.  We always report false so the system
    /// doesn't offer autocorrect / autocomplete on this "input field".
    public bool HasText => false;

    /// Called for all printable characters (and \\r for Enter, \\t for Tab).
    public void InsertText(string text)
    {
        if (text == "\r" || text == "\n")
        {
            _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Enter));
        }
        else if (text == "\t")
        {
            _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Tab));
        }
        else if (!string.IsNullOrEmpty(text))
        {
            _proxy.RaiseTextInput(text);
        }
    }

    /// Called for the Backspace key.
    public void DeleteBackward()
        => _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Backspace));

    // ── Key command selectors (one per intent) ───────────────────────────────────

    [Export("keyNavLeft:")]
    public void KeyNavLeft(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(MakeNav(EditorKey.Left, cmd.ModifierFlags));

    [Export("keyNavRight:")]
    public void KeyNavRight(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(MakeNav(EditorKey.Right, cmd.ModifierFlags));

    [Export("keyNavUp:")]
    public void KeyNavUp(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(MakeNav(EditorKey.Up, cmd.ModifierFlags));

    [Export("keyNavDown:")]
    public void KeyNavDown(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(MakeNav(EditorKey.Down, cmd.ModifierFlags));

    [Export("keyNavHome:")]
    public void KeyNavHome(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(MakeNav(EditorKey.Home, cmd.ModifierFlags));

    [Export("keyNavEnd:")]
    public void KeyNavEnd(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(MakeNav(EditorKey.End, cmd.ModifierFlags));

    [Export("keyNavPageUp:")]
    public void KeyNavPageUp(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(MakeNav(EditorKey.PageUp, cmd.ModifierFlags));

    [Export("keyNavPageDown:")]
    public void KeyNavPageDown(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(MakeNav(EditorKey.PageDown, cmd.ModifierFlags));

    [Export("keyNavDeleteForward:")]
    public void KeyNavDeleteForward(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(MakeNav(EditorKey.Delete, cmd.ModifierFlags));

    [Export("keyNavEscape:")]
    public void KeyNavEscape(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(MakeNav(EditorKey.Escape, cmd.ModifierFlags));

    // ── Editing shortcut selectors ───────────────────────────────────────────────

    [Export("keyEditUndo:")]
    public void KeyEditUndo(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Z, 'z', InputModifiers.Meta));

    [Export("keyEditRedo:")]
    public void KeyEditRedo(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.Z, 'z', InputModifiers.Meta | InputModifiers.Shift));

    [Export("keyEditSelectAll:")]
    public void KeyEditSelectAll(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.A, 'a', InputModifiers.Meta));

    [Export("keyEditBold:")]
    public void KeyEditBold(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.B, 'b', InputModifiers.Meta));

    [Export("keyEditItalic:")]
    public void KeyEditItalic(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.I, 'i', InputModifiers.Meta));

    [Export("keyEditUnderline:")]
    public void KeyEditUnderline(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.U, 'u', InputModifiers.Meta));

    [Export("keyEditCopy:")]
    public void KeyEditCopy(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.C, 'c', InputModifiers.Meta));

    [Export("keyEditCut:")]
    public void KeyEditCut(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.X, 'x', InputModifiers.Meta));

    [Export("keyEditPaste:")]
    public void KeyEditPaste(UIKeyCommand cmd)
        => _proxy.RaiseKeyDown(new EditorKeyEventArgs(EditorKey.V, 'v', InputModifiers.Meta));

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static EditorKeyEventArgs MakeNav(EditorKey key, UIKeyModifierFlags flags)
    {
        var mods = InputModifiers.None;
        if ((flags & UIKeyModifierFlags.Shift) != 0)   mods |= InputModifiers.Shift;
        if ((flags & UIKeyModifierFlags.Command) != 0) mods |= InputModifiers.Meta;
        if ((flags & UIKeyModifierFlags.Control) != 0) mods |= InputModifiers.Control;
        if ((flags & UIKeyModifierFlags.Alternate) != 0) mods |= InputModifiers.Alt;
        return new EditorKeyEventArgs(key, '\0', mods);
    }

    private UIKeyCommand[] BuildKeyCommands()
    {
        UIKeyModifierFlags none  = (UIKeyModifierFlags)0;
        UIKeyModifierFlags shift = UIKeyModifierFlags.Shift;
        UIKeyModifierFlags cmd   = UIKeyModifierFlags.Command;
        UIKeyModifierFlags cs    = UIKeyModifierFlags.Command | UIKeyModifierFlags.Shift;

        Selector navLeft      = new("keyNavLeft:");
        Selector navRight     = new("keyNavRight:");
        Selector navUp        = new("keyNavUp:");
        Selector navDown      = new("keyNavDown:");
        Selector navHome      = new("keyNavHome:");
        Selector navEnd       = new("keyNavEnd:");
        Selector navPageUp    = new("keyNavPageUp:");
        Selector navPageDown  = new("keyNavPageDown:");
        Selector navDelFwd    = new("keyNavDeleteForward:");
        Selector navEscape    = new("keyNavEscape:");
        Selector editUndo     = new("keyEditUndo:");
        Selector editRedo     = new("keyEditRedo:");
        Selector editSelAll   = new("keyEditSelectAll:");
        Selector editBold     = new("keyEditBold:");
        Selector editItalic   = new("keyEditItalic:");
        Selector editUnderline = new("keyEditUnderline:");
        Selector editCopy     = new("keyEditCopy:");
        Selector editCut      = new("keyEditCut:");
        Selector editPaste    = new("keyEditPaste:");

        // Arrow keys with ←/→/↑/↓ selector constants
        NSString left  = UIKeyCommand.LeftArrow;
        NSString right = UIKeyCommand.RightArrow;
        NSString up    = UIKeyCommand.UpArrow;
        NSString down  = UIKeyCommand.DownArrow;

        // Home / End / PageUp / PageDown added in iOS 13.4 / Mac Catalyst 13.4
        NSString? home   = null;
        NSString? end    = null;
        NSString? pgUp   = null;
        NSString? pgDown = null;
        try { home   = UIKeyCommand.Home;     } catch { }
        try { end    = UIKeyCommand.End;      } catch { }
        try { pgUp   = UIKeyCommand.PageUp;   } catch { }
        try { pgDown = UIKeyCommand.PageDown; } catch { }

        // Escape constant (UIKeyInputEscape)
        NSString? esc = null;
        try { esc = UIKeyCommand.Escape; } catch { }

        // Forward delete: Fn+Delete sends DEL (0x7f) on extended keyboards
        var forwardDelete = new NSString("\u007f");

        var list = new System.Collections.Generic.List<UIKeyCommand>
        {
            // Arrow keys — plain navigation
            Kc(left,  none,  navLeft),
            Kc(right, none,  navRight),
            Kc(up,    none,  navUp),
            Kc(down,  none,  navDown),
            // Arrow keys + Shift — extend selection
            Kc(left,  shift, navLeft),
            Kc(right, shift, navRight),
            Kc(up,    shift, navUp),
            Kc(down,  shift, navDown),
            // Cmd+Left/Right → Home/End of line
            Kc(left,  cmd,   navHome),
            Kc(right, cmd,   navEnd),
            // Cmd+Left/Right + Shift → extend to Home/End of line
            Kc(left,  cs,    navHome),
            Kc(right, cs,    navEnd),
            // Cmd+Up/Down → document start/end
            Kc(up,    cmd,   navHome),
            Kc(down,  cmd,   navEnd),
            // Cmd+Up/Down + Shift → extend to document start/end
            Kc(up,    cs,    navHome),
            Kc(down,  cs,    navEnd),
            // Forward delete
            Kc(forwardDelete, none, navDelFwd),
            // Editing shortcuts
            Kc(new NSString("z"), cmd, editUndo),
            Kc(new NSString("z"), cs,  editRedo),
            Kc(new NSString("a"), cmd, editSelAll),
            Kc(new NSString("b"), cmd, editBold),
            Kc(new NSString("i"), cmd, editItalic),
            Kc(new NSString("u"), cmd, editUnderline),
            Kc(new NSString("c"), cmd, editCopy),
            Kc(new NSString("x"), cmd, editCut),
            Kc(new NSString("v"), cmd, editPaste),
        };

        if (home   != null) { list.Add(Kc(home,   none,  navHome));   list.Add(Kc(home,   shift, navHome));   }
        if (end    != null) { list.Add(Kc(end,    none,  navEnd));    list.Add(Kc(end,    shift, navEnd));    }
        if (pgUp   != null) { list.Add(Kc(pgUp,   none,  navPageUp)); list.Add(Kc(pgUp,   shift, navPageUp)); }
        if (pgDown != null) { list.Add(Kc(pgDown, none,  navPageDown)); list.Add(Kc(pgDown, shift, navPageDown)); }
        if (esc    != null) { list.Add(Kc(esc, none, navEscape)); }

        return list.ToArray();
    }

    private static UIKeyCommand Kc(NSString input, UIKeyModifierFlags mods, Selector selector)
        => UIKeyCommand.Create(input, mods, selector);
}

/// <summary>MAUI handler that backs <see cref="KeyboardProxy"/> on iOS / Mac Catalyst.</summary>
internal sealed class KeyboardProxyHandler : ViewHandler<KeyboardProxy, KeyboardCaptureUIView>
{
    public KeyboardProxyHandler() : base(ViewHandler.ViewMapper) { }

    protected override KeyboardCaptureUIView CreatePlatformView()
        => new KeyboardCaptureUIView(VirtualView);

    protected override void ConnectHandler(KeyboardCaptureUIView platformView)
    {
        base.ConnectHandler(platformView);
    }

    protected override void DisconnectHandler(KeyboardCaptureUIView platformView)
    {
        base.DisconnectHandler(platformView);
    }
}

// ─── Android ─────────────────────────────────────────────────────────────────────
#elif ANDROID

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
            outAttrs.ImeOptions = ImeFlags.NoEnterAction;
        }
        return _inputConnection;
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        if (e == null) return base.OnKeyDown(keyCode, e);
        var editorKey = MapKeyCode(keyCode);
        if (editorKey == EditorKey.None) return base.OnKeyDown(keyCode, e);
        var mods = MapMetaState(e.MetaState);
        _proxy.RaiseKeyDown(new EditorKeyEventArgs(editorKey, '\0', mods));
        return true;
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

/// <summary>Minimal InputConnection that routes IME text commits and deletes to the proxy.</summary>
internal sealed class KeyboardInputConnection : BaseInputConnection
{
    private readonly KeyboardProxy _proxy;

    public KeyboardInputConnection(Android.Views.View targetView, KeyboardProxy proxy)
        : base(targetView, false)
    {
        _proxy = proxy;
    }

    public override bool CommitText(Java.Lang.ICharSequence? text, int newCursorPosition)
    {
        var str = text?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(str))
            _proxy.RaiseTextInput(str);
        return true;
    }

    public override bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
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
                _proxy.RaiseKeyDown(new EditorKeyEventArgs(editorKey));
                return true;
            }
        }
        return base.SendKeyEvent(e);
    }
}

/// <summary>MAUI handler that backs <see cref="KeyboardProxy"/> on Android.</summary>
internal sealed class KeyboardProxyHandler : ViewHandler<KeyboardProxy, KeyboardCaptureAndroidView>
{
    public KeyboardProxyHandler() : base(ViewHandler.ViewMapper) { }

    protected override KeyboardCaptureAndroidView CreatePlatformView()
        => new KeyboardCaptureAndroidView(Context!, VirtualView);

    protected override void ConnectHandler(KeyboardCaptureAndroidView platformView)
    {
        base.ConnectHandler(platformView);
    }

    protected override void DisconnectHandler(KeyboardCaptureAndroidView platformView)
    {
        base.DisconnectHandler(platformView);
    }
}

// ─── Windows ─────────────────────────────────────────────────────────────────────
#elif WINDOWS

/// <summary>
/// A transparent WinUI TextBox that captures keyboard input on Windows.
/// Its KeyDown event handles navigation/editing keys; TextChanged handles
/// printable character input.
/// </summary>
internal sealed class KeyboardCaptureWindowsView : Microsoft.UI.Xaml.Controls.TextBox
{
    private readonly KeyboardProxy _proxy;
    private bool _suppressTextChanged;

    public KeyboardCaptureWindowsView(KeyboardProxy proxy)
    {
        _proxy = proxy;
        Opacity = 0;
        Width = 1;
        Height = 1;
        IsSpellCheckEnabled = false;
        IsTextPredictionEnabled = false;
        AcceptsReturn = true;
        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap;

        KeyDown += OnKeyDown;
        TextChanged += OnTextChanged;
    }

    private void OnKeyDown(object sender,
        Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var vk      = e.Key;
        var mods    = BuildModifiers();
        var ctrl    = mods.HasFlag(InputModifiers.Control) || mods.HasFlag(InputModifiers.Meta);

        // Map Windows VirtualKey to EditorKey
        var editorKey = vk switch
        {
            Windows.System.VirtualKey.Left      => EditorKey.Left,
            Windows.System.VirtualKey.Right     => EditorKey.Right,
            Windows.System.VirtualKey.Up        => EditorKey.Up,
            Windows.System.VirtualKey.Down      => EditorKey.Down,
            Windows.System.VirtualKey.Home      => EditorKey.Home,
            Windows.System.VirtualKey.End       => EditorKey.End,
            Windows.System.VirtualKey.PageUp    => EditorKey.PageUp,
            Windows.System.VirtualKey.PageDown  => EditorKey.PageDown,
            Windows.System.VirtualKey.Back      => EditorKey.Backspace,
            Windows.System.VirtualKey.Delete    => EditorKey.Delete,
            Windows.System.VirtualKey.Enter     => EditorKey.Enter,
            Windows.System.VirtualKey.Tab       => EditorKey.Tab,
            Windows.System.VirtualKey.Escape    => EditorKey.Escape,
            Windows.System.VirtualKey.A when ctrl => EditorKey.A,
            Windows.System.VirtualKey.B when ctrl => EditorKey.B,
            Windows.System.VirtualKey.C when ctrl => EditorKey.C,
            Windows.System.VirtualKey.I when ctrl => EditorKey.I,
            Windows.System.VirtualKey.U when ctrl => EditorKey.U,
            Windows.System.VirtualKey.V when ctrl => EditorKey.V,
            Windows.System.VirtualKey.X when ctrl => EditorKey.X,
            Windows.System.VirtualKey.Y when ctrl => EditorKey.Y,
            Windows.System.VirtualKey.Z when ctrl => EditorKey.Z,
            _ => EditorKey.None,
        };

        if (editorKey == EditorKey.None) return;

        // Suppress the TextBox handling so it doesn't alter its own text
        e.Handled = true;
        _proxy.RaiseKeyDown(new EditorKeyEventArgs(editorKey, '\0', mods));

        // Keep the TextBox empty so TextChanged only fires for real typed characters
        _suppressTextChanged = true;
        Text = string.Empty;
        _suppressTextChanged = false;
    }

    private void OnTextChanged(object sender,
        Microsoft.UI.Xaml.Controls.TextChangedEventArgs e)
    {
        if (_suppressTextChanged) return;
        var text = Text;
        if (!string.IsNullOrEmpty(text))
        {
            _proxy.RaiseTextInput(text);
            _suppressTextChanged = true;
            Text = string.Empty;
            _suppressTextChanged = false;
        }
    }

    private static InputModifiers BuildModifiers()
    {
        var state = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread;
        var mods = InputModifiers.None;
        if (IsKeyDown(state, Windows.System.VirtualKey.Shift))   mods |= InputModifiers.Shift;
        if (IsKeyDown(state, Windows.System.VirtualKey.Control)) mods |= InputModifiers.Control;
        if (IsKeyDown(state, Windows.System.VirtualKey.Menu))    mods |= InputModifiers.Alt;
        if (IsKeyDown(state, Windows.System.VirtualKey.LeftWindows)
         || IsKeyDown(state, Windows.System.VirtualKey.RightWindows)) mods |= InputModifiers.Meta;
        return mods;
    }

    private static bool IsKeyDown(
        Func<Windows.System.VirtualKey, Windows.UI.Core.CoreVirtualKeyStates> getState,
        Windows.System.VirtualKey key)
        => (getState(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
}

/// <summary>MAUI handler that backs <see cref="KeyboardProxy"/> on Windows.</summary>
internal sealed class KeyboardProxyHandler : ViewHandler<KeyboardProxy, KeyboardCaptureWindowsView>
{
    public KeyboardProxyHandler() : base(ViewHandler.ViewMapper) { }

    protected override KeyboardCaptureWindowsView CreatePlatformView()
        => new KeyboardCaptureWindowsView(VirtualView);

    protected override void ConnectHandler(KeyboardCaptureWindowsView platformView)
    {
        base.ConnectHandler(platformView);
    }

    protected override void DisconnectHandler(KeyboardCaptureWindowsView platformView)
    {
        base.DisconnectHandler(platformView);
    }
}

#endif
