namespace MintedTextEditor.Core.Document;

/// <summary>
/// Represents a range within a document defined by a start and end position.
/// Start is always &lt;= End (normalized).
/// </summary>
public readonly struct TextRange : IEquatable<TextRange>
{
    public DocumentPosition Start { get; }
    public DocumentPosition End { get; }

    /// <summary>Whether the range is empty (start equals end).</summary>
    public bool IsEmpty => Start == End;

    /// <summary>An empty range at the document origin.</summary>
    public static TextRange Empty => default;

    public TextRange(DocumentPosition start, DocumentPosition end)
    {
        if (start <= end)
        {
            Start = start;
            End = end;
        }
        else
        {
            Start = end;
            End = start;
        }
    }

    public bool Contains(DocumentPosition position)
        => position >= Start && position <= End;

    public bool Equals(TextRange other) => Start == other.Start && End == other.End;
    public override bool Equals(object? obj) => obj is TextRange r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(Start, End);
    public static bool operator ==(TextRange left, TextRange right) => left.Equals(right);
    public static bool operator !=(TextRange left, TextRange right) => !left.Equals(right);

    public override string ToString() => $"[{Start} → {End}]";
}
