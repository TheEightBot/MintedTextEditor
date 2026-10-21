namespace MintedTextEditor.Core.Document;

/// <summary>
/// An explicit line break within a paragraph (soft return).
/// </summary>
public class LineBreak : Inline
{
    /// <summary>A line break has a length of 1.</summary>
    public override int Length => 1;
    /// <summary>Returns a newline character.</summary>
    public override string GetText() => "\n";
}
