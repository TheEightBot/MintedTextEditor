using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Appearance of the text magnifier (loupe) shown while dragging the caret. All sizes are
/// in logical units (points / dp). <see cref="Apple"/> and <see cref="Android"/> approximate
/// the native magnifiers; every value can be changed.
/// </summary>
public sealed class MagnifierOptions
{
    /// <summary>Lens width.</summary>
    public float Width { get; set; } = 120f;

    /// <summary>Lens height.</summary>
    public float Height { get; set; } = 44f;

    /// <summary>Corner radius; half the height gives a pill, and Width == Height with half that gives a circle.</summary>
    public float CornerRadius { get; set; } = 22f;

    /// <summary>Magnification factor.</summary>
    public float Zoom { get; set; } = 1.25f;

    /// <summary>
    /// Distance from the magnified point (the caret line's centre) up to the lens centre, so
    /// the lens sits above the finger.
    /// </summary>
    public float VerticalOffset { get; set; } = 56f;

    /// <summary>Lens outline color; transparent for none.</summary>
    public EditorColor BorderColor { get; set; } = new(128, 128, 128, 96);

    /// <summary>Lens outline width.</summary>
    public float BorderWidth { get; set; } = 1f;

    /// <summary>Drop-shadow color; transparent for none.</summary>
    public EditorColor ShadowColor { get; set; } = new(0, 0, 0, 40);

    /// <summary>How far the shadow extends below the lens.</summary>
    public float ShadowOffset { get; set; } = 3f;

    /// <summary>Approximates the iOS / iPadOS / Mac Catalyst text loupe: a capsule.</summary>
    public static MagnifierOptions Apple => new();

    /// <summary>
    /// Approximates <c>android.widget.Magnifier</c>: 100×48 dp, 1.25× zoom, centred 42 dp
    /// above the line, with an elevation shadow and no outline.
    /// </summary>
    public static MagnifierOptions Android => new()
    {
        Width = 100f,
        Height = 48f,
        CornerRadius = 24f,
        Zoom = 1.25f,
        VerticalOffset = 42f + 24f,
        BorderColor = EditorColor.Transparent,
        BorderWidth = 0f,
        ShadowColor = new EditorColor(0, 0, 0, 56),
        ShadowOffset = 4f,
    };
}

/// <summary>
/// State and geometry of the text magnifier: whether it is shown, which point of the
/// editor it magnifies, and where its lens goes. Coordinates are in the same space the
/// editor is drawn in. Drawing is done by <see cref="MagnifierRenderer"/>.
/// </summary>
public sealed class Magnifier
{
    /// <summary>Appearance of the lens.</summary>
    public MagnifierOptions Options { get; set; } = new();

    /// <summary>Whether the lens is shown.</summary>
    public bool IsVisible { get; private set; }

    /// <summary>The magnified point, drawn at the centre of the lens.</summary>
    public float FocusX { get; private set; }

    /// <inheritdoc cref="FocusX"/>
    public float FocusY { get; private set; }

    /// <summary>Shows the lens magnifying (<paramref name="focusX"/>, <paramref name="focusY"/>).</summary>
    public void Show(float focusX, float focusY)
    {
        FocusX = focusX;
        FocusY = focusY;
        IsVisible = true;
    }

    /// <summary>Hides the lens.</summary>
    public void Hide() => IsVisible = false;

    /// <summary>
    /// The lens rectangle: centred horizontally on the focus and <see cref="MagnifierOptions.VerticalOffset"/>
    /// above it, then moved inside <paramref name="bounds"/> so it is never cut off.
    /// </summary>
    public EditorRect GetLensRect(EditorRect bounds)
    {
        float width = Options.Width;
        float height = Options.Height;

        float left = FocusX - width / 2f;
        left = Math.Min(left, bounds.Right - width);
        left = Math.Max(left, bounds.Left);

        float top = FocusY - Options.VerticalOffset - height / 2f;
        top = Math.Min(top, bounds.Bottom - height);
        top = Math.Max(top, bounds.Top);

        return new EditorRect(left, top, width, height);
    }
}
