namespace MintedTextEditor.Core.Input;

/// <summary>
/// Flags representing currently held modifier keys.
/// </summary>
[Flags]
public enum InputModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Meta = 1 << 3
}
