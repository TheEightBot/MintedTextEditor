namespace MintedTextEditor.Core.Document;

/// <summary>
/// Base class for all inline content within a paragraph.
/// </summary>
public abstract class Inline
{
    /// <summary>The parent paragraph that owns this inline.</summary>
    public Paragraph? Parent { get; internal set; }

    /// <summary>Gets the plain-text length of this inline element.</summary>
    public abstract int Length { get; }

    /// <summary>Gets the plain text content of this inline element.</summary>
    public abstract string GetText();
}
