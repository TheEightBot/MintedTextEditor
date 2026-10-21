namespace MintedTextEditor.Core.Input;

/// <summary>
/// Platform-independent key codes for the editor.
/// </summary>
public enum EditorKey
{
    /// <summary>No key, or a key the editor does not recognise.</summary>
    None = 0,

    // Arrow keys
    /// <summary>The Left Arrow key.</summary>
    Left,
    /// <summary>The Right Arrow key.</summary>
    Right,
    /// <summary>The Up Arrow key.</summary>
    Up,
    /// <summary>The Down Arrow key.</summary>
    Down,

    // Navigation
    /// <summary>The Home key.</summary>
    Home,
    /// <summary>The End key.</summary>
    End,
    /// <summary>The Page Up key.</summary>
    PageUp,
    /// <summary>The Page Down key.</summary>
    PageDown,

    // Editing
    /// <summary>The Backspace key.</summary>
    Backspace,
    /// <summary>The Delete (forward delete) key.</summary>
    Delete,
    /// <summary>The Enter/Return key.</summary>
    Enter,
    /// <summary>The Tab key.</summary>
    Tab,
    /// <summary>The Space bar.</summary>
    Space,
    /// <summary>The Escape key.</summary>
    Escape,

    // Letters
    /// <summary>The A key.</summary>
    A,
    /// <summary>The B key.</summary>
    B,
    /// <summary>The C key.</summary>
    C,
    /// <summary>The D key.</summary>
    D,
    /// <summary>The E key.</summary>
    E,
    /// <summary>The F key.</summary>
    F,
    /// <summary>The G key.</summary>
    G,
    /// <summary>The H key.</summary>
    H,
    /// <summary>The I key.</summary>
    I,
    /// <summary>The J key.</summary>
    J,
    /// <summary>The K key.</summary>
    K,
    /// <summary>The L key.</summary>
    L,
    /// <summary>The M key.</summary>
    M,
    /// <summary>The N key.</summary>
    N,
    /// <summary>The O key.</summary>
    O,
    /// <summary>The P key.</summary>
    P,
    /// <summary>The Q key.</summary>
    Q,
    /// <summary>The R key.</summary>
    R,
    /// <summary>The S key.</summary>
    S,
    /// <summary>The T key.</summary>
    T,
    /// <summary>The U key.</summary>
    U,
    /// <summary>The V key.</summary>
    V,
    /// <summary>The W key.</summary>
    W,
    /// <summary>The X key.</summary>
    X,
    /// <summary>The Y key.</summary>
    Y,
    /// <summary>The Z key.</summary>
    Z,

    // Digits
    /// <summary>The 0 key on the main keyboard row.</summary>
    D0,
    /// <summary>The 1 key on the main keyboard row.</summary>
    D1,
    /// <summary>The 2 key on the main keyboard row.</summary>
    D2,
    /// <summary>The 3 key on the main keyboard row.</summary>
    D3,
    /// <summary>The 4 key on the main keyboard row.</summary>
    D4,
    /// <summary>The 5 key on the main keyboard row.</summary>
    D5,
    /// <summary>The 6 key on the main keyboard row.</summary>
    D6,
    /// <summary>The 7 key on the main keyboard row.</summary>
    D7,
    /// <summary>The 8 key on the main keyboard row.</summary>
    D8,
    /// <summary>The 9 key on the main keyboard row.</summary>
    D9,

    // Function keys
    /// <summary>The F1 function key.</summary>
    F1,
    /// <summary>The F2 function key.</summary>
    F2,
    /// <summary>The F3 function key.</summary>
    F3,
    /// <summary>The F4 function key.</summary>
    F4,
    /// <summary>The F5 function key.</summary>
    F5,
    /// <summary>The F6 function key.</summary>
    F6,
    /// <summary>The F7 function key.</summary>
    F7,
    /// <summary>The F8 function key.</summary>
    F8,
    /// <summary>The F9 function key.</summary>
    F9,
    /// <summary>The F10 function key.</summary>
    F10,
    /// <summary>The F11 function key.</summary>
    F11,
    /// <summary>The F12 function key.</summary>
    F12,

    // Modifiers (as standalone key events)
    /// <summary>The left Shift key.</summary>
    LeftShift,
    /// <summary>The right Shift key.</summary>
    RightShift,
    /// <summary>The left Control key.</summary>
    LeftControl,
    /// <summary>The right Control key.</summary>
    RightControl,
    /// <summary>The left Alt (Option) key.</summary>
    LeftAlt,
    /// <summary>The right Alt (Option) key.</summary>
    RightAlt,
    /// <summary>The left Meta key (Command on Apple platforms, Windows key on Windows).</summary>
    LeftMeta,
    /// <summary>The right Meta key (Command on Apple platforms, Windows key on Windows).</summary>
    RightMeta
}
