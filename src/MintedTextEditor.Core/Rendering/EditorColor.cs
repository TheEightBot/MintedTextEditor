namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Platform-independent color representation using RGBA values.
/// </summary>
public readonly struct EditorColor : IEquatable<EditorColor>
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }

    public EditorColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public EditorColor WithAlpha(byte alpha) => new(R, G, B, alpha);

    public static EditorColor FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);
    public static EditorColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    // Common colors
    public static EditorColor Transparent => new(0, 0, 0, 0);
    public static EditorColor Black => new(0, 0, 0);
    public static EditorColor White => new(255, 255, 255);
    public static EditorColor LightGray => new(211, 211, 211);
    public static EditorColor Gray => new(128, 128, 128);
    public static EditorColor DarkGray => new(64, 64, 64);
    public static EditorColor Red => new(255, 0, 0);
    public static EditorColor Blue => new(0, 0, 255);
    public static EditorColor Green => new(0, 128, 0);
    public static EditorColor CornflowerBlue => new(100, 149, 237);
    public static EditorColor Yellow => new(255, 255, 0);

    public bool Equals(EditorColor other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is EditorColor c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(EditorColor left, EditorColor right) => left.Equals(right);
    public static bool operator !=(EditorColor left, EditorColor right) => !left.Equals(right);
    public override string ToString() => $"EditorColor({R}, {G}, {B}, {A})";

    /// <summary>Pack ARGB into a single uint for use as a cache key.</summary>
    public uint ToUint32() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;
}
