namespace MintedTextEditor.Core.Input;

/// <summary>
/// Platform-independent keyboard event data.
/// </summary>
public class EditorKeyEventArgs
{
    /// <summary>The key that was pressed or released.</summary>
    public EditorKey Key { get; }

    /// <summary>The character produced by this key event ('\0' if none).</summary>
    public char Character { get; }

    /// <summary>Active modifier keys at the time of the event.</summary>
    public InputModifiers Modifiers { get; }

    /// <summary>True if this is a key-down event; false for key-up.</summary>
    public bool IsKeyDown { get; }

    public EditorKeyEventArgs(EditorKey key, char character = '\0', InputModifiers modifiers = InputModifiers.None, bool isKeyDown = true)
    {
        Key = key;
        Character = character;
        Modifiers = modifiers;
        IsKeyDown = isKeyDown;
    }

    /// <summary>True if the Control (or Meta on macOS) modifier is active.</summary>
    public bool HasControlOrMeta => (Modifiers & (InputModifiers.Control | InputModifiers.Meta)) != 0;
}
