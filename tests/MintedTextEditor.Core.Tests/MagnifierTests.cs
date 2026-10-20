using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class LongPressDetectorTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void TryFire_AfterDuration_FiresOnce()
    {
        var detector = new LongPressDetector { Duration = Ms(500) };
        detector.Begin(10, 10, Ms(0));

        Assert.False(detector.TryFire(Ms(499)));
        Assert.True(detector.TryFire(Ms(500)));
        Assert.False(detector.TryFire(Ms(600)));
    }

    [Fact]
    public void Move_WithinSlop_KeepsTracking()
    {
        var detector = new LongPressDetector { Slop = 10f };
        detector.Begin(10, 10, Ms(0));

        Assert.True(detector.Move(16, 18)); // distance 10
        Assert.True(detector.TryFire(detector.Duration));
        Assert.Equal(16f, detector.X);
        Assert.Equal(18f, detector.Y);
    }

    [Fact]
    public void Move_BeyondSlop_Cancels()
    {
        var detector = new LongPressDetector { Slop = 10f };
        detector.Begin(10, 10, Ms(0));

        Assert.False(detector.Move(10, 21));
        Assert.False(detector.TryFire(Ms(10_000)));
    }

    [Fact]
    public void Cancel_PreventsFiring()
    {
        var detector = new LongPressDetector();
        detector.Begin(0, 0, Ms(0));
        detector.Cancel();

        Assert.False(detector.IsTracking);
        Assert.False(detector.TryFire(Ms(10_000)));
    }

    [Fact]
    public void Begin_RestartsAfterCancel()
    {
        var detector = new LongPressDetector { Duration = Ms(500) };
        detector.Begin(0, 0, Ms(0));
        detector.Move(100, 100);
        detector.Begin(50, 50, Ms(1000));

        Assert.False(detector.TryFire(Ms(1400)));
        Assert.True(detector.TryFire(Ms(1500)));
    }
}

public class MagnifierTests
{
    private static readonly EditorRect Bounds = new(0, 0, 400, 800);

    private static Magnifier Create(float x, float y)
    {
        var magnifier = new Magnifier
        {
            Options = new MagnifierOptions { Width = 120, Height = 40, VerticalOffset = 50, Zoom = 2f },
        };
        magnifier.Show(x, y);
        return magnifier;
    }

    [Fact]
    public void GetLensRect_CentresAboveFocus()
    {
        var lens = Create(200, 300).GetLensRect(Bounds);

        Assert.Equal(new EditorRect(140, 230, 120, 40), lens);
    }

    [Theory]
    [InlineData(10f, 0f)]     // near the left edge
    [InlineData(395f, 280f)]  // near the right edge
    public void GetLensRect_StaysInsideHorizontally(float focusX, float expectedLeft)
    {
        var lens = Create(focusX, 300).GetLensRect(Bounds);

        Assert.Equal(expectedLeft, lens.X);
    }

    [Fact]
    public void GetLensRect_NearTop_ClampsToTop()
    {
        var lens = Create(200, 20).GetLensRect(Bounds);

        Assert.Equal(0f, lens.Y);
    }

    [Fact]
    public void GetLensRect_LargerThanBounds_ShrinksToFit()
    {
        var lens = Create(50, 15).GetLensRect(new EditorRect(0, 0, 100, 30));

        Assert.Equal(new EditorRect(0, 0, 100, 30), lens);
    }

    [Fact]
    public void ShowAndHide_ToggleVisibility()
    {
        var magnifier = Create(1, 1);
        Assert.True(magnifier.IsVisible);

        magnifier.Hide();
        Assert.False(magnifier.IsVisible);
    }

    [Fact]
    public void Presets_ApproximateNativeMagnifiers()
    {
        var android = MagnifierOptions.Android;
        Assert.Equal((100f, 48f, 1.25f), (android.Width, android.Height, android.Zoom));

        var apple = MagnifierOptions.Apple;
        Assert.Equal(apple.Height / 2f, apple.CornerRadius); // capsule

        // Presets are fresh instances, so changing one never changes the default.
        apple.Zoom = 3f;
        Assert.Equal(1.25f, MagnifierOptions.Apple.Zoom);
    }
}

public class MagnifierRendererTests
{
    private readonly MagnifierRenderer _renderer = new();
    private readonly MockDrawingContext _ctx = new();

    [Fact]
    public void Render_Hidden_DrawsNothing()
    {
        bool contentDrawn = false;
        _renderer.Render(_ctx, new Magnifier(), new EditorRect(0, 0, 400, 800), EditorColor.White, _ => contentDrawn = true);

        Assert.False(contentDrawn);
        Assert.Empty(_ctx.TransformCalls);
        Assert.Empty(_ctx.FillRectCalls);
    }

    [Fact]
    public void Render_Visible_DrawsZoomedContentInsideClippedLens()
    {
        var magnifier = new Magnifier
        {
            Options = new MagnifierOptions { Width = 120, Height = 40, CornerRadius = 20, VerticalOffset = 50, Zoom = 2f },
        };
        magnifier.Show(200, 300);

        List<string>? callsWhenContentDrawn = null;
        _renderer.Render(_ctx, magnifier, new EditorRect(0, 0, 400, 800), EditorColor.White,
            ctx => callsWhenContentDrawn = new List<string>(((MockDrawingContext)ctx).TransformCalls));

        // Lens is (140, 230, 120, 40); its centre (200, 250) shows the focus (200, 300) at 2×.
        Assert.Equal(new[]
        {
            "Save",
            "ClipRoundRect(140,230,120,40,20)",
            "Translate(200,250)",
            "Scale(2,2)",
            "Translate(-200,-300)",
        }, callsWhenContentDrawn);
        Assert.Equal("Restore", _ctx.TransformCalls[^1]);
        Assert.Contains(_ctx.FillRectCalls, c => c.Rect.Equals(new EditorRect(140, 230, 120, 40)) && c.Paint.Color.Equals(EditorColor.White));
    }

    [Fact]
    public void Render_CornerRadius_LimitedToHalfTheSmallerSide()
    {
        var magnifier = new Magnifier { Options = new MagnifierOptions { Width = 60, Height = 60, CornerRadius = 100 } };
        magnifier.Show(200, 300);

        _renderer.Render(_ctx, magnifier, new EditorRect(0, 0, 400, 800), EditorColor.White, _ => { });

        Assert.Contains(_ctx.TransformCalls, c => c.StartsWith("ClipRoundRect(") && c.EndsWith(",30)"));
    }
}
