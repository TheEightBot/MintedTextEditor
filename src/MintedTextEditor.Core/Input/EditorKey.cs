namespace MintedTextEditor.Core.Input;

/// <summary>
/// Platform-independent key codes for the editor.
/// </summary>
public enum EditorKey
{
    None = 0,

    // Arrow keys
    Left,
    Right,
    Up,
    Down,

    // Navigation
    Home,
    End,
    PageUp,
    PageDown,

    // Editing
    Backspace,
    Delete,
    Enter,
    Tab,
    Space,
    Escape,

    // Letters
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Digits
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    // Function keys
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    // Modifiers (as standalone key events)
    LeftShift,
    RightShift,
    LeftControl,
    RightControl,
    LeftAlt,
    RightAlt,
    LeftMeta,
    RightMeta
}
