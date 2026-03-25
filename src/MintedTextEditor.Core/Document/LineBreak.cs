namespace MintedTextEditor.Core.Document;

/// <summary>
/// An explicit line break within a paragraph (soft return).
/// </summary>
public class LineBreak : Inline
{
    public override int Length => 1;
    public override string GetText() => "\n";
}
