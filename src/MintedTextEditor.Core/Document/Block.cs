namespace MintedTextEditor.Core.Document;

/// <summary>
/// Base class for block-level elements (paragraph, heading, list item, etc.).
/// </summary>
public abstract class Block
{
    /// <summary>The parent document that owns this block.</summary>
    public Document? Parent { get; internal set; }

    /// <summary>Gets the combined plain text of all content in this block.</summary>
    public abstract string GetText();

    /// <summary>Gets the total character length of this block's content.</summary>
    public abstract int Length { get; }
}
