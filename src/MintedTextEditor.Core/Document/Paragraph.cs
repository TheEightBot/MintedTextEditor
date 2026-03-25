namespace MintedTextEditor.Core.Document;

/// <summary>
/// A paragraph block that holds a collection of <see cref="Inline"/> elements.
/// </summary>
public class Paragraph : Block
{
    public List<Inline> Inlines { get; } = new();
    public ParagraphStyle Style { get; set; } = new();

    public override int Length => Inlines.Sum(i => i.Length);
    public override string GetText() => string.Concat(Inlines.Select(i => i.GetText()));

    public Paragraph() { }

    public Paragraph(string text, TextStyle? style = null)
    {
        if (!string.IsNullOrEmpty(text))
            AppendRun(text, style);
    }

    public void AppendRun(string text, TextStyle? style = null)
    {
        var run = new TextRun(text, style) { Parent = this };
        Inlines.Add(run);
    }

    public void InsertInline(int index, Inline inline)
    {
        inline.Parent = this;
        Inlines.Insert(index, inline);
    }

    public void RemoveInline(int index)
    {
        if (index >= 0 && index < Inlines.Count)
        {
            Inlines[index].Parent = null;
            Inlines.RemoveAt(index);
        }
    }

    public void AddInline(Inline inline)
    {
        inline.Parent = this;
        Inlines.Add(inline);
    }
}
