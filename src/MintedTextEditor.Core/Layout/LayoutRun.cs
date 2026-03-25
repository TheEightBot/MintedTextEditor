using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Layout;

/// <summary>
/// A positioned text segment within a visual line.
/// </summary>
public class LayoutRun
{
    /// <summary>The text content of this run.</summary>
    public string Text { get; }

    /// <summary>Horizontal offset from the left edge of the line.</summary>
    public float X { get; }

    /// <summary>Width of this run in pixels.</summary>
    public float Width { get; }

    /// <summary>Height of this run in pixels. Meaningful for image runs; for text runs this
    /// reflects the measured glyph height of the fragment.</summary>
    public float Height { get; }

    /// <summary>
    /// Reference to the source <see cref="Inline"/> in the document model (null for
    /// non-text inlines). For text runs that are children of a <see cref="HyperlinkInline"/>
    /// this points to the <see cref="HyperlinkInline"/> so that inline-index lookups
    /// against <c>paragraph.Inlines</c> succeed.
    /// </summary>
    public Inline? SourceInline { get; }

    /// <summary>Style applied to this run.</summary>
    public TextStyle Style { get; }

    /// <summary>Character offset within the source inline where this run's text begins.</summary>
    public int SourceOffset { get; }

    /// <summary>True when this run represents an image placeholder rather than text.</summary>
    public bool IsImage { get; }

    /// <summary>
    /// Source URI / resource key for image runs (<see cref="IsImage"/> is true).
    /// Null for text runs.
    /// </summary>
    public string? ImageSource { get; }

    public LayoutRun(string text, float x, float width, Inline? sourceInline, TextStyle style, int sourceOffset,
        bool isImage = false, string? imageSource = null, float height = 0)
    {
        Text = text;
        X = x;
        Width = width;
        Height = height;
        SourceInline = sourceInline;
        Style = style;
        SourceOffset = sourceOffset;
        IsImage = isImage;
        ImageSource = imageSource;
    }

    /// <summary>Returns a copy of this run with a different X position.</summary>
    public LayoutRun WithX(float x) => new(Text, x, Width, SourceInline, Style, SourceOffset, IsImage, ImageSource, Height);
}
