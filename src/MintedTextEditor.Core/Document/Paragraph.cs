namespace MintedTextEditor.Core.Document;

/// <summary>
/// A paragraph block that holds a collection of <see cref="Inline"/> elements.
/// </summary>
public class Paragraph : Block
{
    /// <summary>The inline elements of the paragraph, in order.</summary>
    public List<Inline> Inlines { get; } = new();
    /// <summary>Paragraph-level formatting.</summary>
    public ParagraphStyle Style { get; set; } = new();

    /// <inheritdoc/>
    public override int Length => Inlines.Sum(i => i.Length);
    /// <inheritdoc/>
    public override string GetText() => string.Concat(Inlines.Select(i => i.GetText()));

    /// <summary>Creates an empty paragraph.</summary>
    public Paragraph() { }

    /// <summary>
    /// Creates a paragraph containing a single text run, or an empty paragraph when
    /// <paramref name="text"/> is null or empty.
    /// </summary>
    public Paragraph(string text, TextStyle? style = null)
    {
        if (!string.IsNullOrEmpty(text))
            AppendRun(text, style);
    }

    /// <summary>Appends a new <see cref="TextRun"/> with the given text and style.</summary>
    public void AppendRun(string text, TextStyle? style = null)
    {
        var run = new TextRun(text, style) { Parent = this };
        Inlines.Add(run);
    }

    /// <summary>Inserts an inline at the given index and sets its parent.</summary>
    public void InsertInline(int index, Inline inline)
    {
        inline.Parent = this;
        Inlines.Insert(index, inline);
    }

    /// <summary>Removes the inline at the given index. Does nothing if the index is out of range.</summary>
    public void RemoveInline(int index)
    {
        if (index >= 0 && index < Inlines.Count)
        {
            Inlines[index].Parent = null;
            Inlines.RemoveAt(index);
        }
    }

    /// <summary>Appends an inline and sets its parent.</summary>
    public void AddInline(Inline inline)
    {
        inline.Parent = this;
        Inlines.Add(inline);
    }
}
