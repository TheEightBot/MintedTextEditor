namespace MintedTextEditor.Core.Input;

/// <summary>
/// Describes the type of pointer/input action.
/// </summary>
public enum InputAction
{
    /// <summary>A pointer button or touch was pressed down.</summary>
    Pressed,
    /// <summary>A pointer button or touch was released.</summary>
    Released,
    /// <summary>The pointer moved; extends a drag selection while a press is active.</summary>
    Moved,
    /// <summary>A scroll (wheel or pan) action.</summary>
    Scroll,
    /// <summary>A double tap gesture.</summary>
    DoubleTap,
    /// <summary>A long press gesture.</summary>
    LongPress
}
