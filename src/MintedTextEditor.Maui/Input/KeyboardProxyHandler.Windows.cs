// Windows (WinUI) implementation of the KeyboardProxy handler.
// Compiled only for windows (see the Compile conditions in the .csproj).

using Microsoft.Maui.Handlers;
using MintedTextEditor.Core.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace MintedTextEditor.Maui.Input;

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
        GotFocus  += (_, _) => _proxy.RaiseNativeFocusChanged(true);
        LostFocus += (_, _) => _proxy.RaiseNativeFocusChanged(false);
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
internal sealed partial class KeyboardProxyHandler : ViewHandler<KeyboardProxy, KeyboardCaptureWindowsView>
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
