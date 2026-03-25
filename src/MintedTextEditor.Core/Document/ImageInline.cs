namespace MintedTextEditor.Core.Document;

/// <summary>
/// An inline image element with source URI, alt text, and dimensions.
/// </summary>
public class ImageInline : Inline
{
    public string Source { get; set; }
    public string AltText { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    /// <summary>Image inlines have a logical length of 1 (object replacement character).</summary>
    public override int Length => 1;
    public override string GetText() => "\uFFFC"; // Unicode Object Replacement Character

    public ImageInline(string source, string altText = "", float width = 0, float height = 0)
    {
        Source = source;
        AltText = altText;
        Width = width;
        Height = height;
    }
}
