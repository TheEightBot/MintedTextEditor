namespace MintedTextEditor.Core.Document;

/// <summary>
/// A hyperlink inline that wraps child inlines with a URL and optional title.
/// </summary>
public class HyperlinkInline : Inline
{
    public string Url { get; set; }
    public string? Title { get; set; }
    public List<Inline> Children { get; } = new();

    public override int Length => Children.Sum(c => c.Length);
    public override string GetText() => string.Concat(Children.Select(c => c.GetText()));

    public HyperlinkInline(string url, string? title = null)
    {
        Url = url;
        Title = title;
    }

    public void AddChild(Inline child)
    {
        child.Parent = Parent;
        Children.Add(child);
    }
}
