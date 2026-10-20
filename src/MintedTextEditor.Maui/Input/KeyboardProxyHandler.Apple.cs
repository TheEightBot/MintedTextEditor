// iOS / Mac Catalyst implementation of the KeyboardProxy handler.
// Compiled only for ios/maccatalyst (see the Compile conditions in the .csproj).

using Microsoft.Maui.Handlers;
using MintedTextEditor.Core.Input;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace MintedTextEditor.Maui.Input;

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

    // Report first-responder changes so the editor can show/hide its caret. Resign is
    // also called by UIKit when another control (e.g. a sibling Entry) takes focus.
    public override bool BecomeFirstResponder()
    {
        bool became = base.BecomeFirstResponder();
        if (became) _proxy.RaiseNativeFocusChanged(true);
        return became;
    }

    public override bool ResignFirstResponder()
    {
        bool resigned = base.ResignFirstResponder();
        if (resigned) _proxy.RaiseNativeFocusChanged(false);
        return resigned;
    }

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
internal sealed partial class KeyboardProxyHandler : ViewHandler<KeyboardProxy, KeyboardCaptureUIView>
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
