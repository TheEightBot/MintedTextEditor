namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Platform-independent font definition.
/// </summary>
public class EditorFont
{
    /// <summary>Font family name. "Default" (the default) selects the platform's default family.</summary>
    public string Family { get; set; } = "Default";
    /// <summary>Font size in drawing units. Defaults to 14.</summary>
    public float Size { get; set; } = 14f;
    /// <summary>Whether the font is bold.</summary>
    public bool IsBold { get; set; }
    /// <summary>Whether the font is italic.</summary>
    public bool IsItalic { get; set; }

    /// <summary>Creates a regular 14-unit font in the default family.</summary>
    public EditorFont() { }

    /// <summary>Creates a font with the given family, size and style.</summary>
    /// <param name="family">Font family name.</param>
    /// <param name="size">Font size in drawing units.</param>
    /// <param name="bold">Whether the font is bold.</param>
    /// <param name="italic">Whether the font is italic.</param>
    public EditorFont(string family, float size, bool bold = false, bool italic = false)
    {
        Family = family;
        Size = size;
        IsBold = bold;
        IsItalic = italic;
    }
}

/// <summary>
/// Platform-independent paint/style definition for drawing operations.
/// </summary>
public class EditorPaint
{
    /// <summary>Color used to fill, stroke or draw text. Defaults to black.</summary>
    public EditorColor Color { get; set; } = EditorColor.Black;
    /// <summary>Line width for stroked shapes and lines. Defaults to 1.</summary>
    public float StrokeWidth { get; set; } = 1f;
    /// <summary>Requested paint style. Defaults to <see cref="PaintStyle.Fill"/>. <see cref="IDrawingContext"/> methods fill or stroke according to the method called (for example <see cref="IDrawingContext.FillRect"/> versus <see cref="IDrawingContext.DrawRect"/>).</summary>
    public PaintStyle Style { get; set; } = PaintStyle.Fill;
    /// <summary>Whether edges are anti-aliased. Defaults to true.</summary>
    public bool IsAntiAlias { get; set; } = true;
    /// <summary>Font used for text operations; when null a default <see cref="EditorFont"/> is used.</summary>
    public EditorFont? Font { get; set; }
}

/// <summary>
/// Simple rectangle structure for layout and rendering.
/// </summary>
public readonly struct EditorRect : IEquatable<EditorRect>
{
    /// <summary>X coordinate of the left edge.</summary>
    public float X { get; }
    /// <summary>Y coordinate of the top edge.</summary>
    public float Y { get; }
    /// <summary>Width of the rectangle.</summary>
    public float Width { get; }
    /// <summary>Height of the rectangle.</summary>
    public float Height { get; }

    /// <summary>X coordinate of the left edge (same as <see cref="X"/>).</summary>
    public float Left => X;
    /// <summary>Y coordinate of the top edge (same as <see cref="Y"/>).</summary>
    public float Top => Y;
    /// <summary>X coordinate of the right edge (<see cref="X"/> + <see cref="Width"/>).</summary>
    public float Right => X + Width;
    /// <summary>Y coordinate of the bottom edge (<see cref="Y"/> + <see cref="Height"/>).</summary>
    public float Bottom => Y + Height;

    /// <summary>Creates a rectangle from its top-left corner and size.</summary>
    /// <param name="x">X coordinate of the left edge.</param>
    /// <param name="y">Y coordinate of the top edge.</param>
    /// <param name="width">Width of the rectangle.</param>
    /// <param name="height">Height of the rectangle.</param>
    public EditorRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Returns whether the point (<paramref name="px"/>, <paramref name="py"/>) lies inside the rectangle, edges included.</summary>
    public bool Contains(float px, float py)
        => px >= Left && px <= Right && py >= Top && py <= Bottom;

    /// <summary>Returns a copy grown by <paramref name="dx"/> on the left and right and by <paramref name="dy"/> on the top and bottom (negative values shrink it).</summary>
    public EditorRect Inflate(float dx, float dy)
        => new(X - dx, Y - dy, Width + 2 * dx, Height + 2 * dy);

    /// <summary>Returns a copy moved by (<paramref name="dx"/>, <paramref name="dy"/>) with the same size.</summary>
    public EditorRect Offset(float dx, float dy)
        => new(X + dx, Y + dy, Width, Height);

    /// <summary>A zero-sized rectangle at the origin.</summary>
    public static EditorRect Empty => new(0, 0, 0, 0);

    /// <inheritdoc/>
    public bool Equals(EditorRect other)
        => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EditorRect r && Equals(r);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    /// <summary>Returns whether two rectangles have the same position and size.</summary>
    public static bool operator ==(EditorRect left, EditorRect right) => left.Equals(right);
    /// <summary>Returns whether two rectangles differ in position or size.</summary>
    public static bool operator !=(EditorRect left, EditorRect right) => !left.Equals(right);
}

/// <summary>
/// Simple size structure.
/// </summary>
public readonly struct EditorSize : IEquatable<EditorSize>
{
    /// <summary>Width.</summary>
    public float Width { get; }
    /// <summary>Height.</summary>
    public float Height { get; }

    /// <summary>Creates a size from a width and height.</summary>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    public EditorSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>A size of zero width and height.</summary>
    public static EditorSize Empty => new(0, 0);

    /// <inheritdoc/>
    public bool Equals(EditorSize other) => Width == other.Width && Height == other.Height;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EditorSize s && Equals(s);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    /// <summary>Returns whether two sizes have the same width and height.</summary>
    public static bool operator ==(EditorSize left, EditorSize right) => left.Equals(right);
    /// <summary>Returns whether two sizes differ in width or height.</summary>
    public static bool operator !=(EditorSize left, EditorSize right) => !left.Equals(right);
}

/// <summary>
/// Simple point structure.
/// </summary>
public readonly struct EditorPoint : IEquatable<EditorPoint>
{
    /// <summary>X coordinate.</summary>
    public float X { get; }
    /// <summary>Y coordinate.</summary>
    public float Y { get; }

    /// <summary>Creates a point from its coordinates.</summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    public EditorPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>The point (0, 0).</summary>
    public static EditorPoint Zero => new(0, 0);

    /// <inheritdoc/>
    public bool Equals(EditorPoint other) => X == other.X && Y == other.Y;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EditorPoint p && Equals(p);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y);
    /// <summary>Returns whether two points have the same coordinates.</summary>
    public static bool operator ==(EditorPoint left, EditorPoint right) => left.Equals(right);
    /// <summary>Returns whether two points differ in either coordinate.</summary>
    public static bool operator !=(EditorPoint left, EditorPoint right) => !left.Equals(right);
}

/// <summary>
/// Font metrics for precise text positioning (ascent, descent, leading).
/// Ascent is negative (above baseline), descent is positive (below baseline).
/// </summary>
public readonly struct EditorFontMetrics
{
    /// <summary>Distance from the baseline to the top of the tallest glyphs (negative).</summary>
    public float Ascent { get; }
    /// <summary>Distance from the baseline to the bottom of the lowest glyphs (positive).</summary>
    public float Descent { get; }
    /// <summary>Recommended extra spacing between lines.</summary>
    public float Leading { get; }

    /// <summary>Total line height (Descent - Ascent + Leading).</summary>
    public float LineHeight => Descent - Ascent + Leading;

    /// <summary>Text height without leading (Descent - Ascent).</summary>
    public float TextHeight => Descent - Ascent;

    /// <summary>Creates font metrics.</summary>
    /// <param name="ascent">Ascent (negative, above the baseline).</param>
    /// <param name="descent">Descent (positive, below the baseline).</param>
    /// <param name="leading">Extra line spacing; defaults to 0.</param>
    public EditorFontMetrics(float ascent, float descent, float leading = 0)
    {
        Ascent = ascent;
        Descent = descent;
        Leading = leading;
    }
}
