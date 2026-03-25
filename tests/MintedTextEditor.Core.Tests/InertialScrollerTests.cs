using MintedTextEditor.Core.Input;

namespace MintedTextEditor.Core.Tests;

public class InertialScrollerTests
{
    // ── Basic velocity accumulation ───────────────────────────────

    [Fact]
    public void AfterPan_IsScrolling_WhenVelocityAboveThreshold()
    {
        var scroller = new InertialScroller();
        scroller.BeginPan(0f);
        scroller.Pan(30f);  // 30 logical-px pan
        scroller.EndPan();

        Assert.True(scroller.IsScrolling);
    }

    [Fact]
    public void AfterStop_IsNotScrolling()
    {
        var scroller = new InertialScroller();
        scroller.BeginPan(0f);
        scroller.Pan(50f);
        scroller.EndPan();

        scroller.Stop();

        Assert.False(scroller.IsScrolling);
    }

    [Fact]
    public void Tick_ReturnsDeltaAndDecelerates()
    {
        var scroller = new InertialScroller();
        scroller.BeginPan(0f);
        scroller.Pan(60f);
        scroller.EndPan();

        float first  = scroller.Tick();
        float second = scroller.Tick();

        Assert.True(Math.Abs(first) > Math.Abs(second),
            "Velocity should decrease after each tick (friction).");
    }

    [Fact]
    public void Tick_EventuallyStops()
    {
        var scroller = new InertialScroller();
        scroller.BeginPan(0f);
        scroller.Pan(40f);
        scroller.EndPan();

        int ticks = 0;
        while (scroller.IsScrolling && ticks < 1000)
        {
            scroller.Tick();
            ticks++;
        }

        Assert.False(scroller.IsScrolling, "Scroller should stop within 1000 ticks.");
    }

    // ── ClampScroll ───────────────────────────────────────────────

    [Fact]
    public void ClampScroll_BelowZero_ClampsToZero()
    {
        var scroller = new InertialScroller();
        float result = scroller.ClampScroll(-10f, 500f);
        Assert.Equal(0f, result);
    }

    [Fact]
    public void ClampScroll_AboveMax_ClampsToMax()
    {
        var scroller = new InertialScroller();
        float result = scroller.ClampScroll(600f, 500f);
        Assert.Equal(500f, result);
    }

    [Fact]
    public void ClampScroll_InsideBounds_ReturnsUnchanged()
    {
        var scroller = new InertialScroller();
        float result = scroller.ClampScroll(250f, 500f);
        Assert.Equal(250f, result);
    }

    // ── Pan delta ─────────────────────────────────────────────────

    [Fact]
    public void Pan_ReturnsDeltaFromPreviousPosition()
    {
        var scroller = new InertialScroller();
        scroller.BeginPan(0f);

        float delta1 = scroller.Pan(10f);
        float delta2 = scroller.Pan(25f);

        // delta = _lastY - y, so moving y from 0→10 gives -10, from 10→25 gives -15.
        Assert.Equal(-10f, delta1);
        Assert.Equal(-15f, delta2);
    }

    [Fact]
    public void BeginPan_StopsPreviousInertia()
    {
        var scroller = new InertialScroller();
        scroller.BeginPan(0f);
        scroller.Pan(80f);
        scroller.EndPan();
        Assert.True(scroller.IsScrolling);

        // Starting a new pan should stop inertia
        scroller.BeginPan(100f);
        // After beginning a new pan, velocity is reset
        scroller.EndPan();
        Assert.False(scroller.IsScrolling);
    }
}
