using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Extension methods for <see cref="IDrawingContext"/> that add styled text run drawing/measuring.
/// </summary>
public static class DrawingContextExtensions
{
    /// <summary>
    /// Draws a run of styled text at a position by constructing the appropriate paint from the <see cref="TextStyle"/>.
    /// </summary>
    public static void DrawTextRun(this IDrawingContext ctx, string text, float x, float y, TextStyle style)
    {
        var paint = CreatePaintFromStyle(style);
        ctx.DrawText(text, x, y, paint);
    }

    /// <summary>
    /// Measures a run of styled text by constructing the appropriate paint from the <see cref="TextStyle"/>.
    /// </summary>
    public static EditorSize MeasureTextRun(this IDrawingContext ctx, string text, TextStyle style)
    {
        var paint = CreatePaintFromStyle(style);
        return ctx.MeasureText(text, paint);
    }

    private static EditorPaint CreatePaintFromStyle(TextStyle style)
    {
        return new EditorPaint
        {
            Color = style.TextColor,
            IsAntiAlias = true,
            Font = new EditorFont(
                style.FontFamily,
                style.FontSize,
                style.IsBold,
                style.IsItalic)
        };
    }
}
