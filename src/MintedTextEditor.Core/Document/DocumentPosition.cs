namespace MintedTextEditor.Core.Document;

/// <summary>
/// Represents a position within a document: block index, inline index within that block, and character offset within that inline.
/// When <see cref="IsInTableCell"/> is <c>true</c>, <see cref="BlockIndex"/> is the index of the <see cref="TableBlock"/>
/// in the document and <see cref="CellRow"/>/<see cref="CellCol"/> identify the cell.
/// </summary>
public readonly struct DocumentPosition : IComparable<DocumentPosition>, IEquatable<DocumentPosition>
{
    public int BlockIndex { get; }
    public int InlineIndex { get; }
    public int Offset { get; }

    /// <summary>Row index of the table cell, or -1 when not inside a table cell.</summary>
    public int CellRow { get; }

    /// <summary>Column index of the table cell, or -1 when not inside a table cell.</summary>
    public int CellCol { get; }

    /// <summary>Returns <c>true</c> when this position is inside a table cell.</summary>
    public bool IsInTableCell => CellRow >= 0 && CellCol >= 0;

    public DocumentPosition(int blockIndex, int inlineIndex, int offset)
    {
        BlockIndex = blockIndex;
        InlineIndex = inlineIndex;
        Offset = offset;
        CellRow = -1;
        CellCol = -1;
    }

    public DocumentPosition(int blockIndex, int inlineIndex, int offset, int cellRow, int cellCol)
    {
        BlockIndex = blockIndex;
        InlineIndex = inlineIndex;
        Offset = offset;
        CellRow = cellRow;
        CellCol = cellCol;
    }

    /// <summary>
    /// Returns a new position at the same block and cell context but with a different inline index and offset.
    /// </summary>
    public DocumentPosition With(int inlineIndex, int offset)
        => new DocumentPosition(BlockIndex, inlineIndex, offset, CellRow, CellCol);

    public int CompareTo(DocumentPosition other)
    {
        int cmp = BlockIndex.CompareTo(other.BlockIndex);
        if (cmp != 0) return cmp;
        cmp = CellRow.CompareTo(other.CellRow);
        if (cmp != 0) return cmp;
        cmp = CellCol.CompareTo(other.CellCol);
        if (cmp != 0) return cmp;
        cmp = InlineIndex.CompareTo(other.InlineIndex);
        if (cmp != 0) return cmp;
        return Offset.CompareTo(other.Offset);
    }

    public bool Equals(DocumentPosition other)
        => BlockIndex == other.BlockIndex && CellRow == other.CellRow && CellCol == other.CellCol
        && InlineIndex == other.InlineIndex && Offset == other.Offset;

    public override bool Equals(object? obj) => obj is DocumentPosition p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(BlockIndex, CellRow, CellCol, InlineIndex, Offset);

    public static bool operator ==(DocumentPosition left, DocumentPosition right) => left.Equals(right);
    public static bool operator !=(DocumentPosition left, DocumentPosition right) => !left.Equals(right);
    public static bool operator <(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) < 0;
    public static bool operator >(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) > 0;
    public static bool operator <=(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) <= 0;
    public static bool operator >=(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) >= 0;

    public override string ToString()
        => IsInTableCell
            ? $"({BlockIndex}[{CellRow},{CellCol}], {InlineIndex}, {Offset})"
            : $"({BlockIndex}, {InlineIndex}, {Offset})";
}
