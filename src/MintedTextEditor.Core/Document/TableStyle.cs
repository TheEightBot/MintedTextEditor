using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Document;

/// <summary>
/// Table-wide style defaults: border colour, cell padding, and header-row formatting.
/// </summary>
public class TableStyle
{
    /// <summary>Uniform border width (pixels) applied when no per-cell border is specified.</summary>
    public float BorderWidth { get; set; } = 1f;

    /// <summary>Border colour.</summary>
    public EditorColor BorderColor { get; set; } = EditorColor.Black;

    /// <summary>Cell padding (pixels) on all sides when no per-cell value is set.</summary>
    public float CellPadding { get; set; } = 4f;

    /// <summary>Whether the first row should be treated as a header row.</summary>
    public bool HasHeaderRow { get; set; }

    /// <summary>Optional background colour for the header row.</summary>
    public EditorColor? HeaderBackground { get; set; }
}
