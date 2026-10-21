namespace MintedTextEditor.Core.Document;

/// <summary>
/// An inline image element with source URI, alt text, and dimensions.
/// </summary>
public class ImageInline : Inline
{
    /// <summary>The image source URI or path.</summary>
    public string Source { get; set; }
    /// <summary>Alternative text describing the image.</summary>
    public string AltText { get; set; }
    /// <summary>Display width of the image (0 when unspecified).</summary>
    public float Width { get; set; }
    /// <summary>Display height of the image (0 when unspecified).</summary>
    public float Height { get; set; }

    /// <summary>Image inlines have a logical length of 1 (object replacement character).</summary>
    public override int Length => 1;
    /// <summary>Returns the Unicode object replacement character (U+FFFC).</summary>
    public override string GetText() => "\uFFFC"; // Unicode Object Replacement Character

    /// <summary>Creates an image inline with the given source, alt text and dimensions.</summary>
    public ImageInline(string source, string altText = "", float width = 0, float height = 0)
    {
        Source = source;
        AltText = altText;
        Width = width;
        Height = height;
    }
}
