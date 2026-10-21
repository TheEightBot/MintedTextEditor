namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Platform-independent color representation using RGBA values.
/// </summary>
public readonly struct EditorColor : IEquatable<EditorColor>
{
    /// <summary>Red component (0-255).</summary>
    public byte R { get; }
    /// <summary>Green component (0-255).</summary>
    public byte G { get; }
    /// <summary>Blue component (0-255).</summary>
    public byte B { get; }
    /// <summary>Alpha component (0 = fully transparent, 255 = fully opaque).</summary>
    public byte A { get; }

    /// <summary>Creates a color from red, green, blue and optional alpha components.</summary>
    /// <param name="r">Red component.</param>
    /// <param name="g">Green component.</param>
    /// <param name="b">Blue component.</param>
    /// <param name="a">Alpha component; defaults to 255 (opaque).</param>
    public EditorColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Returns a copy of this color with the alpha component replaced by <paramref name="alpha"/>.</summary>
    public EditorColor WithAlpha(byte alpha) => new(R, G, B, alpha);

    /// <summary>Creates a color from alpha, red, green and blue components.</summary>
    public static EditorColor FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);
    /// <summary>Creates an opaque color from red, green and blue components.</summary>
    public static EditorColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    // Common colors
    /// <summary>Fully transparent black (0, 0, 0, 0).</summary>
    public static EditorColor Transparent => new(0, 0, 0, 0);
    /// <summary>Opaque black (0, 0, 0).</summary>
    public static EditorColor Black => new(0, 0, 0);
    /// <summary>Opaque white (255, 255, 255).</summary>
    public static EditorColor White => new(255, 255, 255);
    /// <summary>Opaque light gray (211, 211, 211).</summary>
    public static EditorColor LightGray => new(211, 211, 211);
    /// <summary>Opaque gray (128, 128, 128).</summary>
    public static EditorColor Gray => new(128, 128, 128);
    /// <summary>Opaque dark gray (64, 64, 64).</summary>
    public static EditorColor DarkGray => new(64, 64, 64);
    /// <summary>Opaque red (255, 0, 0).</summary>
    public static EditorColor Red => new(255, 0, 0);
    /// <summary>Opaque blue (0, 0, 255).</summary>
    public static EditorColor Blue => new(0, 0, 255);
    /// <summary>Opaque green (0, 128, 0).</summary>
    public static EditorColor Green => new(0, 128, 0);
    /// <summary>Opaque cornflower blue (100, 149, 237).</summary>
    public static EditorColor CornflowerBlue => new(100, 149, 237);
    /// <summary>Opaque yellow (255, 255, 0).</summary>
    public static EditorColor Yellow => new(255, 255, 0);

    /// <inheritdoc/>
    public bool Equals(EditorColor other) => R == other.R && G == other.G && B == other.B && A == other.A;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EditorColor c && Equals(c);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    /// <summary>Returns whether two colors have identical RGBA components.</summary>
    public static bool operator ==(EditorColor left, EditorColor right) => left.Equals(right);
    /// <summary>Returns whether two colors differ in any RGBA component.</summary>
    public static bool operator !=(EditorColor left, EditorColor right) => !left.Equals(right);
    /// <inheritdoc/>
    public override string ToString() => $"EditorColor({R}, {G}, {B}, {A})";

    /// <summary>Pack ARGB into a single uint for use as a cache key.</summary>
    public uint ToUint32() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;
}
