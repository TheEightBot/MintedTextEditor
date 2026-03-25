namespace MintedTextEditor.Core.Input;

/// <summary>
/// Physics-based inertial scroller.
///
/// Usage:
/// 1. Call <see cref="BeginPan"/> when a touch/pointer drag starts.
/// 2. Call <see cref="Pan"/> on each move event to accumulate velocity.
/// 3. Call <see cref="EndPan"/> when the gesture is released to start the deceleration phase.
/// 4. On each animation tick (e.g. from a 60 Hz timer) call <see cref="Tick"/> and apply the
///    returned delta value to the scroll offset.
/// 5. Call <see cref="Stop"/> to cancel inertia (e.g. when the user taps again).
/// </summary>
public sealed class InertialScroller
{
    // ─── Configuration ────────────────────────────────────────────────────────

    /// <summary>Friction coefficient applied each tick (0–1). Higher = faster stop.</summary>
    public float Friction { get; set; } = 0.92f;

    /// <summary>Minimum velocity (units/tick) below which inertia stops.</summary>
    public float VelocityThreshold { get; set; } = 0.5f;

    /// <summary>Maximum velocity cap (units/tick), prevents fling-to-infinity.</summary>
    public float MaxVelocity { get; set; } = 80f;

    // ─── State ────────────────────────────────────────────────────────────────

    /// <summary>Whether inertia is actively decelerating.</summary>
    public bool IsScrolling => Math.Abs(_velocity) > VelocityThreshold;

    /// <summary>Current inertial velocity in content units per animation tick.</summary>
    public float Velocity => _velocity;

    private float _velocity;
    private float _lastY;
    private long _lastTimestamp;   // Ticks (DateTime.UtcNow.Ticks)

    // Sliding window for velocity averaging.
    private const int WindowSize = 5;
    private readonly float[] _deltaBuffer = new float[WindowSize];
    private int _bufferHead;
    private int _bufferCount;

    // ─── Gesture lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Call at the start of a touch/pointer drag.
    /// </summary>
    public void BeginPan(float y)
    {
        _velocity    = 0f;
        _lastY       = y;
        _lastTimestamp = DateTime.UtcNow.Ticks;
        _bufferHead  = 0;
        _bufferCount = 0;
    }

    /// <summary>
    /// Call on each move event.  Returns the raw scroll delta to apply immediately.
    /// </summary>
    public float Pan(float y)
    {
        long now    = DateTime.UtcNow.Ticks;
        long dtTicks = now - _lastTimestamp;

        float delta = _lastY - y;   // positive = scroll down
        _lastY       = y;
        _lastTimestamp = now;

        // Store delta in circular buffer for velocity averaging.
        float perTick = dtTicks > 0
            ? delta / (dtTicks / (float)TimeSpan.TicksPerMillisecond / (1000f / 60f))
            : delta;

        _deltaBuffer[_bufferHead % WindowSize] = perTick;
        _bufferHead++;
        _bufferCount = Math.Min(_bufferCount + 1, WindowSize);

        return delta;
    }

    /// <summary>
    /// Call when the touch/pointer is released.  Latches the current velocity and starts inertia.
    /// </summary>
    public void EndPan()
    {
        if (_bufferCount == 0) return;

        float sum = 0f;
        for (int i = 0; i < _bufferCount; i++)
            sum += _deltaBuffer[i];

        _velocity = Math.Clamp(sum / _bufferCount, -MaxVelocity, MaxVelocity);
    }

    // ─── Animation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call once per animation frame while <see cref="IsScrolling"/> is <c>true</c>.
    /// Returns the scroll delta to apply this tick.
    /// </summary>
    public float Tick()
    {
        if (!IsScrolling)
        {
            _velocity = 0f;
            return 0f;
        }

        float delta = _velocity;
        _velocity *= Friction;

        if (Math.Abs(_velocity) < VelocityThreshold)
            _velocity = 0f;

        return delta;
    }

    /// <summary>Immediately halts all inertia (call when the user taps the surface).</summary>
    public void Stop() => _velocity = 0f;

    /// <summary>
    /// Convenience: clamp a proposed scroll offset to [0, maxScroll].
    /// Returns the clamped value and zeroes velocity if a boundary was hit.
    /// </summary>
    public float ClampScroll(float proposedOffset, float maxScroll)
    {
        if (proposedOffset < 0f)
        {
            _velocity = 0f;
            return 0f;
        }
        if (proposedOffset > maxScroll)
        {
            _velocity = 0f;
            return maxScroll;
        }
        return proposedOffset;
    }
}
