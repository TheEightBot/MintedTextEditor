namespace MintedTextEditor.Core.Input;

/// <summary>
/// Flags representing currently held modifier keys.
/// </summary>
[Flags]
public enum InputModifiers
{
    /// <summary>No modifier keys are held.</summary>
    None = 0,
    /// <summary>The Shift key.</summary>
    Shift = 1 << 0,
    /// <summary>The Control (Ctrl) key.</summary>
    Control = 1 << 1,
    /// <summary>The Alt (Option on macOS) key.</summary>
    Alt = 1 << 2,
    /// <summary>The Meta key (Command on Apple platforms, Windows key on Windows).</summary>
    Meta = 1 << 3
}
