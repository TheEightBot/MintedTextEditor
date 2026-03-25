using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Represents the blinking text cursor in the editor.
/// Tracks the current document position, preferred X for vertical navigation, and blink state.
/// </summary>
public class Caret
{
    /// <summary>Current position in the document.</summary>
    public DocumentPosition Position { get; set; } = new DocumentPosition(0, 0, 0);

    /// <summary>
    /// Preferred X coordinate (in pixels) for vertical caret movement.
    /// Set when the caret moves horizontally; preserved across up/down moves.
    /// A value of -1 means "not yet computed."
    /// </summary>
    public float PreferredX { get; set; } = -1f;

    /// <summary>Whether the caret is currently visible (used for blink animation).</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Duration of one blink phase (on or off) in milliseconds.</summary>
    public int BlinkIntervalMs { get; set; } = 530;

    private long _lastBlinkToggle;
    private bool _blinkEnabled = true;

    /// <summary>
    /// Resets the blink timer, making the caret immediately visible.
    /// Call this on any user input.
    /// </summary>
    public void ResetBlink()
    {
        IsVisible = true;
        _lastBlinkToggle = Environment.TickCount64;
    }

    /// <summary>
    /// Updates the blink state based on elapsed time. Call once per frame.
    /// </summary>
    public void UpdateBlink()
    {
        if (!_blinkEnabled) return;

        long now = Environment.TickCount64;
        if (now - _lastBlinkToggle >= BlinkIntervalMs)
        {
            IsVisible = !IsVisible;
            _lastBlinkToggle = now;
        }
    }

    /// <summary>Enables or disables caret blinking.</summary>
    public bool BlinkEnabled
    {
        get => _blinkEnabled;
        set
        {
            _blinkEnabled = value;
            if (!value) IsVisible = true;
        }
    }

    /// <summary>
    /// Moves the caret to a new position and invalidates the preferred X.
    /// </summary>
    public void MoveTo(DocumentPosition position)
    {
        Position = position;
        PreferredX = -1f;
        ResetBlink();
    }

    /// <summary>
    /// Moves the caret to a new position, preserving the preferred X for vertical navigation.
    /// </summary>
    public void MoveToPreservingX(DocumentPosition position)
    {
        Position = position;
        ResetBlink();
    }
}
