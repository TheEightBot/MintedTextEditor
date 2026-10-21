using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Document;

/// <summary>
/// Table-wide style defaults: border colour, cell padding, and header-row formatting.
/// </summary>
public class TableStyle
{
    /// <summary>The default <see cref="BorderColor"/>: a mid grey.</summary>
    public static EditorColor DefaultBorderColor { get; } = new(160, 160, 160);

    /// <summary>The header-row fill used when <see cref="HasHeaderRow"/> is set and <see cref="HeaderBackground"/> is null: a faint tint.</summary>
    public static EditorColor DefaultHeaderBackground { get; } = new(0, 0, 0, 20);

    /// <summary>
    /// Width (pixels) of the grid lines between and around cells. Also the default width of each
    /// side of a cell that has no <see cref="TableCell.Border"/>. 0 draws no grid.
    /// </summary>
    public float BorderWidth { get; set; } = 1f;

    /// <summary>Colour of the grid lines and of per-cell borders.</summary>
    public EditorColor BorderColor { get; set; } = DefaultBorderColor;

    /// <summary>Cell padding (pixels) on all sides of each cell's content.</summary>
    public float CellPadding { get; set; } = 4f;

    /// <summary>Whether the first row should be treated as a header row and drawn with <see cref="HeaderBackground"/>.</summary>
    public bool HasHeaderRow { get; set; }

    /// <summary>
    /// Background colour for the header row when <see cref="HasHeaderRow"/> is set;
    /// null uses <see cref="DefaultHeaderBackground"/>. A cell's own <see cref="TableCell.Background"/> takes precedence.
    /// </summary>
    public EditorColor? HeaderBackground { get; set; }
}
