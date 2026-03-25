namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Interface for advanced text shaping (ligatures, complex scripts, bidirectional text).
/// Implementations can use HarfBuzz or platform-specific shapers.
/// </summary>
public interface ITextShaper
{
    /// <summary>
    /// Shapes the given text with the specified font, returning positioned glyphs
    /// or the input text unchanged for simple implementations.
    /// </summary>
    ShapedTextResult Shape(string text, EditorFont font);
}

/// <summary>
/// Result of a text shaping operation.
/// </summary>
public class ShapedTextResult
{
    /// <summary>The original input text.</summary>
    public string Text { get; }

    /// <summary>The font used for shaping.</summary>
    public EditorFont Font { get; }

    /// <summary>
    /// Whether this result was produced by a full shaper (true) or the pass-through default (false).
    /// </summary>
    public bool IsFullyShaped { get; }

    public ShapedTextResult(string text, EditorFont font, bool isFullyShaped)
    {
        Text = text;
        Font = font;
        IsFullyShaped = isFullyShaped;
    }
}

/// <summary>
/// Simple pass-through text shaper that does no complex shaping.
/// Suitable for Latin and other simple scripts.
/// </summary>
public class DefaultTextShaper : ITextShaper
{
    public static DefaultTextShaper Instance { get; } = new();

    public ShapedTextResult Shape(string text, EditorFont font)
        => new(text, font, isFullyShaped: false);
}
