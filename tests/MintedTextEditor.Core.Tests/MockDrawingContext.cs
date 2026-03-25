using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Mock drawing context for testing layout. Uses a fixed character width model
/// so tests are deterministic: each character is 8px wide, font height = fontSize.
/// </summary>
internal class MockDrawingContext : IDrawingContext
{
    /// <summary>Fixed width per character (all characters same width).</summary>
    public float CharWidth { get; set; } = 8f;

    // Track draw calls for verification
    public List<DrawTextCall> DrawTextCalls { get; } = new();
    public List<FillRectCall> FillRectCalls { get; } = new();

    public EditorSize MeasureText(string text, EditorPaint paint)
    {
        float fontSize = paint.Font?.Size ?? 14f;
        float width = text.Length * CharWidth;
        float height = fontSize;
        return new EditorSize(width, height);
    }

    public EditorFontMetrics GetFontMetrics(EditorPaint paint)
    {
        float fontSize = paint.Font?.Size ?? 14f;
        // Ascent is negative, descent is positive
        float ascent = -fontSize * 0.8f;
        float descent = fontSize * 0.2f;
        float leading = fontSize * 0.1f;
        return new EditorFontMetrics(ascent, descent, leading);
    }

    public void DrawText(string text, float x, float y, EditorPaint paint)
    {
        DrawTextCalls.Add(new DrawTextCall(text, x, y, paint));
    }

    public void DrawTextInRect(string text, EditorRect rect, EditorPaint paint,
        TextAlignment hAlign = TextAlignment.Left,
        VerticalAlignment vAlign = VerticalAlignment.Center,
        bool clip = true) { }

    public void FillRect(EditorRect rect, EditorPaint paint)
    {
        FillRectCalls.Add(new FillRectCall(rect, paint));
    }

    public void DrawRect(EditorRect rect, EditorPaint paint) { }
    public void DrawRoundRect(EditorRect rect, float cornerRadius, EditorPaint paint) { }
    public void FillRoundRect(EditorRect rect, float cornerRadius, EditorPaint paint) { }
    public void DrawLine(float x1, float y1, float x2, float y2, EditorPaint paint) { }
    public void ClipRect(EditorRect rect) { }
    public void Save() { }
    public void Restore() { }
    public void Translate(float dx, float dy) { }
    public void DrawImage(object image, EditorRect destRect) { }

    public record DrawTextCall(string Text, float X, float Y, EditorPaint Paint);
    public record FillRectCall(EditorRect Rect, EditorPaint Paint);
}
