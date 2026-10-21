namespace MintedTextEditor.Core.Document;

/// <summary>
/// Represents a range within a document defined by a start and end position.
/// Start is always &lt;= End (normalized).
/// </summary>
public readonly struct TextRange : IEquatable<TextRange>
{
    /// <summary>The start position (always &lt;= <see cref="End"/>).</summary>
    public DocumentPosition Start { get; }
    /// <summary>The end position.</summary>
    public DocumentPosition End { get; }

    /// <summary>Whether the range is empty (start equals end).</summary>
    public bool IsEmpty => Start == End;

    /// <summary>An empty range at the document origin (outside any table cell).</summary>
    public static TextRange Empty { get; } = new(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 0));

    /// <summary>
    /// Creates a range between two positions, swapping them if <paramref name="start"/>
    /// is after <paramref name="end"/>.
    /// </summary>
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

    /// <summary>Returns <c>true</c> when the position lies within the range, including both ends.</summary>
    public bool Contains(DocumentPosition position)
        => position >= Start && position <= End;

    /// <inheritdoc/>
    public bool Equals(TextRange other) => Start == other.Start && End == other.End;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TextRange r && Equals(r);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Start, End);
    /// <summary>Returns <c>true</c> when both ranges have the same start and end.</summary>
    public static bool operator ==(TextRange left, TextRange right) => left.Equals(right);
    /// <summary>Returns <c>true</c> when the ranges differ.</summary>
    public static bool operator !=(TextRange left, TextRange right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"[{Start} → {End}]";
}
