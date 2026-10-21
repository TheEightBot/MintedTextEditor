namespace MintedTextEditor.Core.Document;

/// <summary>
/// A contiguous span of text with a single <see cref="TextStyle"/>.
/// </summary>
public class TextRun : Inline
{
    private string _text;

    /// <summary>The run's text. Assigning <c>null</c> stores an empty string.</summary>
    public string Text
    {
        get => _text;
        set => _text = value ?? string.Empty;
    }

    /// <summary>The style applied to the whole run.</summary>
    public TextStyle Style { get; set; }

    /// <inheritdoc/>
    public override int Length => _text.Length;
    /// <inheritdoc/>
    public override string GetText() => _text;

    /// <summary>Creates a text run. A <c>null</c> style uses <see cref="TextStyle.Default"/>.</summary>
    public TextRun(string text, TextStyle? style = null)
    {
        _text = text ?? string.Empty;
        Style = style ?? TextStyle.Default;
    }

    /// <summary>
    /// Splits this run at the given character offset, returning the right-hand portion.
    /// This run is truncated to contain only [0..offset).
    /// </summary>
    public TextRun Split(int offset)
    {
        if (offset < 0 || offset > _text.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var right = new TextRun(_text[offset..], Style);
        _text = _text[..offset];
        return right;
    }

    /// <summary>
    /// Merges another TextRun into this one (appends text). Both must share the same style.
    /// </summary>
    public void Merge(TextRun other)
    {
        if (other.Style != Style)
            throw new InvalidOperationException("Cannot merge TextRuns with different styles.");
        _text += other.Text;
    }
}
