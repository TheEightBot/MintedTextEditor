namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Platform-independent font definition.
/// </summary>
public class EditorFont
{
    public string Family { get; set; } = "Default";
    public float Size { get; set; } = 14f;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }

    public EditorFont() { }

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
    public EditorColor Color { get; set; } = EditorColor.Black;
    public float StrokeWidth { get; set; } = 1f;
    public PaintStyle Style { get; set; } = PaintStyle.Fill;
    public bool IsAntiAlias { get; set; } = true;
    public EditorFont? Font { get; set; }
}

/// <summary>
/// Simple rectangle structure for layout and rendering.
/// </summary>
public readonly struct EditorRect : IEquatable<EditorRect>
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public EditorRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool Contains(float px, float py)
        => px >= Left && px <= Right && py >= Top && py <= Bottom;

    public EditorRect Inflate(float dx, float dy)
        => new(X - dx, Y - dy, Width + 2 * dx, Height + 2 * dy);

    public EditorRect Offset(float dx, float dy)
        => new(X + dx, Y + dy, Width, Height);

    public static EditorRect Empty => new(0, 0, 0, 0);

    public bool Equals(EditorRect other)
        => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is EditorRect r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public static bool operator ==(EditorRect left, EditorRect right) => left.Equals(right);
    public static bool operator !=(EditorRect left, EditorRect right) => !left.Equals(right);
}

/// <summary>
/// Simple size structure.
/// </summary>
public readonly struct EditorSize : IEquatable<EditorSize>
{
    public float Width { get; }
    public float Height { get; }

    public EditorSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public static EditorSize Empty => new(0, 0);

    public bool Equals(EditorSize other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is EditorSize s && Equals(s);
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    public static bool operator ==(EditorSize left, EditorSize right) => left.Equals(right);
    public static bool operator !=(EditorSize left, EditorSize right) => !left.Equals(right);
}

/// <summary>
/// Simple point structure.
/// </summary>
public readonly struct EditorPoint : IEquatable<EditorPoint>
{
    public float X { get; }
    public float Y { get; }

    public EditorPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static EditorPoint Zero => new(0, 0);

    public bool Equals(EditorPoint other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is EditorPoint p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(EditorPoint left, EditorPoint right) => left.Equals(right);
    public static bool operator !=(EditorPoint left, EditorPoint right) => !left.Equals(right);
}

/// <summary>
/// Font metrics for precise text positioning (ascent, descent, leading).
/// Ascent is negative (above baseline), descent is positive (below baseline).
/// </summary>
public readonly struct EditorFontMetrics
{
    public float Ascent { get; }
    public float Descent { get; }
    public float Leading { get; }

    /// <summary>Total line height (Descent - Ascent + Leading).</summary>
    public float LineHeight => Descent - Ascent + Leading;

    /// <summary>Text height without leading (Descent - Ascent).</summary>
    public float TextHeight => Descent - Ascent;

    public EditorFontMetrics(float ascent, float descent, float leading = 0)
    {
        Ascent = ascent;
        Descent = descent;
        Leading = leading;
    }
}
