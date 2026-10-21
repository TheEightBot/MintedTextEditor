namespace MintedTextEditor.Core.Document;

/// <summary>
/// Represents a position within a document: block index, inline index within that block, and character offset within that inline.
/// When <see cref="IsInTableCell"/> is <c>true</c>, <see cref="BlockIndex"/> is the index of the <see cref="TableBlock"/>
/// in the document and <see cref="CellRow"/>/<see cref="CellCol"/> identify the cell.
/// </summary>
public readonly struct DocumentPosition : IComparable<DocumentPosition>, IEquatable<DocumentPosition>
{
    /// <summary>Index of the block within the document.</summary>
    public int BlockIndex { get; }
    /// <summary>Index of the inline within the block.</summary>
    public int InlineIndex { get; }
    /// <summary>Character offset within the inline.</summary>
    public int Offset { get; }

    /// <summary>Row index of the table cell, or -1 when not inside a table cell.</summary>
    public int CellRow { get; }

    /// <summary>Column index of the table cell, or -1 when not inside a table cell.</summary>
    public int CellCol { get; }

    /// <summary>Returns <c>true</c> when this position is inside a table cell.</summary>
    public bool IsInTableCell => CellRow >= 0 && CellCol >= 0;

    /// <summary>Creates a position that is not inside a table cell.</summary>
    public DocumentPosition(int blockIndex, int inlineIndex, int offset)
    {
        BlockIndex = blockIndex;
        InlineIndex = inlineIndex;
        Offset = offset;
        CellRow = -1;
        CellCol = -1;
    }

    /// <summary>Creates a position that may be inside a table cell.</summary>
    /// <param name="blockIndex">Index of the block (the table block when inside a cell).</param>
    /// <param name="inlineIndex">Index of the inline within the block.</param>
    /// <param name="offset">Character offset within the inline.</param>
    /// <param name="cellRow">Row index of the table cell, or -1 when not in a cell.</param>
    /// <param name="cellCol">Column index of the table cell, or -1 when not in a cell.</param>
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

    /// <summary>
    /// Compares positions by block index, then cell row, cell column, inline index and offset.
    /// </summary>
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

    /// <inheritdoc/>
    public bool Equals(DocumentPosition other)
        => BlockIndex == other.BlockIndex && CellRow == other.CellRow && CellCol == other.CellCol
        && InlineIndex == other.InlineIndex && Offset == other.Offset;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DocumentPosition p && Equals(p);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(BlockIndex, CellRow, CellCol, InlineIndex, Offset);

    /// <summary>Returns <c>true</c> when both positions are equal.</summary>
    public static bool operator ==(DocumentPosition left, DocumentPosition right) => left.Equals(right);
    /// <summary>Returns <c>true</c> when the positions differ.</summary>
    public static bool operator !=(DocumentPosition left, DocumentPosition right) => !left.Equals(right);
    /// <summary>Returns <c>true</c> when <paramref name="left"/> comes before <paramref name="right"/>.</summary>
    public static bool operator <(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) < 0;
    /// <summary>Returns <c>true</c> when <paramref name="left"/> comes after <paramref name="right"/>.</summary>
    public static bool operator >(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) > 0;
    /// <summary>Returns <c>true</c> when <paramref name="left"/> comes before or equals <paramref name="right"/>.</summary>
    public static bool operator <=(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) <= 0;
    /// <summary>Returns <c>true</c> when <paramref name="left"/> comes after or equals <paramref name="right"/>.</summary>
    public static bool operator >=(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString()
        => IsInTableCell
            ? $"({BlockIndex}[{CellRow},{CellCol}], {InlineIndex}, {Offset})"
            : $"({BlockIndex}, {InlineIndex}, {Offset})";
}
