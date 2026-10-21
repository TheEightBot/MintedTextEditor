namespace MintedTextEditor.Core.Document;

/// <summary>
/// A hyperlink inline that wraps child inlines with a URL and optional title.
/// </summary>
public class HyperlinkInline : Inline
{
    /// <summary>The link target URL.</summary>
    public string Url { get; set; }
    /// <summary>Optional link title.</summary>
    public string? Title { get; set; }
    /// <summary>The inlines that make up the link content.</summary>
    public List<Inline> Children { get; } = new();

    /// <inheritdoc/>
    public override int Length => Children.Sum(c => c.Length);
    /// <inheritdoc/>
    public override string GetText() => string.Concat(Children.Select(c => c.GetText()));

    /// <summary>Creates a hyperlink with the given URL and optional title.</summary>
    public HyperlinkInline(string url, string? title = null)
    {
        Url = url;
        Title = title;
    }

    /// <summary>Appends a child inline and gives it the same parent paragraph as this hyperlink.</summary>
    public void AddChild(Inline child)
    {
        child.Parent = Parent;
        Children.Add(child);
    }
}
