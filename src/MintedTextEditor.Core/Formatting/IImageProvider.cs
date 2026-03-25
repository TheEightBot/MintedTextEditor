namespace MintedTextEditor.Core.Formatting;

/// <summary>
/// Platform-specific contract for loading images from a source token.
/// The returned object is an opaque platform image handle passed to
/// <see cref="MintedTextEditor.Core.Rendering.IDrawingContext.DrawImage"/>.
/// </summary>
public interface IImageProvider
{
    /// <summary>
    /// Loads an image from the given <paramref name="source"/> URI or identifier.
    /// Returns a platform-specific image object (e.g. <c>SKImage</c>, <c>ImageSource</c>).
    /// </summary>
    Task<object> LoadImageAsync(string source);
}
