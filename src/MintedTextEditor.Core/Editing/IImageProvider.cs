namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Optional service for requesting platform-level image downsampling before
/// inserting or displaying images in the editor.
/// </summary>
public interface IImageProvider
{
    /// <summary>
    /// Downsamples <paramref name="imageData"/> so that neither dimension exceeds
    /// <paramref name="maxWidth"/> × <paramref name="maxHeight"/>.
    /// The aspect ratio must be preserved.
    /// Returns <c>null</c> if the operation is not supported on this platform or if
    /// the image is already within bounds.
    /// </summary>
    Task<byte[]?> DownsampleAsync(byte[] imageData, int maxWidth, int maxHeight);

    /// <summary>
    /// <c>true</c> when image-processing operations are available on this platform.
    /// When <c>false</c>, <see cref="DownsampleAsync"/> may always return <c>null</c>.
    /// </summary>
    bool IsAvailable { get; }
}
