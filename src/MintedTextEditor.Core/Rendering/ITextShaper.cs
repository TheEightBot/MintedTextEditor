using System.Diagnostics.CodeAnalysis;

namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Interface for advanced text shaping (ligatures, complex scripts, bidirectional text).
/// Implementations can use HarfBuzz or platform-specific shapers.
/// </summary>
/// <remarks>
/// Experimental (<c>MTE003</c>): the editor does not use this interface yet. The SkiaSharp backend
/// shapes text internally with HarfBuzz; see <c>SkiaDrawingContext.ShapeAllText</c>.
/// </remarks>
[Experimental("MTE003", UrlFormat = "https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/architecture.md#experimental-apis")]
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
/// <remarks>
/// Experimental (<c>MTE003</c>): the editor does not use this type yet. The SkiaSharp backend
/// shapes text internally with HarfBuzz; see <c>SkiaDrawingContext.ShapeAllText</c>.
/// </remarks>
[Experimental("MTE003", UrlFormat = "https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/architecture.md#experimental-apis")]
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

    /// <summary>Creates a shaping result.</summary>
    /// <param name="text">The original input text.</param>
    /// <param name="font">The font used for shaping.</param>
    /// <param name="isFullyShaped">Whether a full shaper produced the result.</param>
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
/// <remarks>
/// Experimental (<c>MTE003</c>): the editor does not use this class yet. The SkiaSharp backend
/// shapes text internally with HarfBuzz; see <c>SkiaDrawingContext.ShapeAllText</c>.
/// </remarks>
[Experimental("MTE003", UrlFormat = "https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/architecture.md#experimental-apis")]
public class DefaultTextShaper : ITextShaper
{
    /// <summary>Shared instance.</summary>
    public static DefaultTextShaper Instance { get; } = new();

    /// <inheritdoc/>
    public ShapedTextResult Shape(string text, EditorFont font)
        => new(text, font, isFullyShaped: false);
}
