namespace MintedTextEditor.Core.Input;

/// <summary>
/// Detects a long press: a touch held for <see cref="Duration"/> without moving more than
/// <see cref="Slop"/> from where it went down. Time is passed in by the caller, so the
/// detector is platform-independent and testable; the host drives it from a timer.
/// <para/>
/// Usage: call <see cref="Begin"/> on touch down, <see cref="Move"/> on every move,
/// <see cref="Cancel"/> on release, and <see cref="TryFire"/> from a timer once
/// <see cref="Duration"/> has elapsed.
/// </summary>
public sealed class LongPressDetector
{
    /// <summary>How long the touch must be held. Matches the iOS and Android defaults.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>How far (in logical units) the touch may drift before it no longer counts as a press.</summary>
    public float Slop { get; set; } = 10f;

    /// <summary><c>true</c> between <see cref="Begin"/> and the press firing or being cancelled.</summary>
    public bool IsTracking { get; private set; }

    /// <summary>The latest touch position reported to <see cref="Begin"/> or <see cref="Move"/>.</summary>
    public float X { get; private set; }

    /// <inheritdoc cref="X"/>
    public float Y { get; private set; }

    private float _startX;
    private float _startY;
    private TimeSpan _startTime;

    /// <summary>Starts tracking a touch that went down at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public void Begin(float x, float y, TimeSpan timestamp)
    {
        _startX = X = x;
        _startY = Y = y;
        _startTime = timestamp;
        IsTracking = true;
    }

    /// <summary>
    /// Reports a touch move. Moving further than <see cref="Slop"/> from the start cancels
    /// the press. Returns <c>true</c> while the press is still being tracked.
    /// </summary>
    public bool Move(float x, float y)
    {
        X = x;
        Y = y;
        if (IsTracking)
        {
            float dx = x - _startX;
            float dy = y - _startY;
            if (dx * dx + dy * dy > Slop * Slop)
                IsTracking = false;
        }

        return IsTracking;
    }

    /// <summary>Stops tracking (touch released or cancelled).</summary>
    public void Cancel() => IsTracking = false;

    /// <summary>
    /// Returns <c>true</c> exactly once, when the press is still being tracked and has been
    /// held for at least <see cref="Duration"/> at <paramref name="now"/>.
    /// </summary>
    public bool TryFire(TimeSpan now)
    {
        if (!IsTracking || now - _startTime < Duration)
            return false;

        IsTracking = false;
        return true;
    }
}
