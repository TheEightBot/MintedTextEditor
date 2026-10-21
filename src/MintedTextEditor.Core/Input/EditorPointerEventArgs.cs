namespace MintedTextEditor.Core.Input;

/// <summary>
/// Platform-independent pointer event data.
/// </summary>
public class EditorPointerEventArgs
{
    /// <summary>X coordinate in document space (relative to the editor surface).</summary>
    public float X { get; }

    /// <summary>Y coordinate in document space (relative to the editor surface).</summary>
    public float Y { get; }

    /// <summary>The type of pointer action (pressed, released, moved, etc.).</summary>
    public InputAction Action { get; }

    /// <summary>Mouse button index (0 = primary, 1 = middle, 2 = secondary).</summary>
    public int Button { get; }

    /// <summary>Active modifier keys at the time of the event.</summary>
    public InputModifiers Modifiers { get; }

    /// <summary>Number of sequential clicks (1 = single, 2 = double, 3 = triple).</summary>
    public int ClickCount { get; }

    /// <summary>Timestamp in milliseconds.</summary>
    public long Timestamp { get; }

    /// <summary>Creates pointer event data.</summary>
    /// <param name="x">X coordinate relative to the editor surface.</param>
    /// <param name="y">Y coordinate relative to the editor surface.</param>
    /// <param name="action">The type of pointer action.</param>
    /// <param name="button">Mouse button index (0 = primary, 1 = middle, 2 = secondary).</param>
    /// <param name="modifiers">Active modifier keys.</param>
    /// <param name="clickCount">Number of sequential clicks.</param>
    /// <param name="timestamp">Timestamp in milliseconds.</param>
    public EditorPointerEventArgs(
        float x, float y, InputAction action,
        int button = 0, InputModifiers modifiers = InputModifiers.None,
        int clickCount = 1, long timestamp = 0)
    {
        X = x;
        Y = y;
        Action = action;
        Button = button;
        Modifiers = modifiers;
        ClickCount = clickCount;
        Timestamp = timestamp;
    }
}
