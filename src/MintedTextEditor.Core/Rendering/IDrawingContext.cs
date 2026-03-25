namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Platform-independent drawing context abstraction.
/// Implementations map these calls to concrete rendering APIs (SkiaSharp, etc.).
/// </summary>
public interface IDrawingContext
{
    // ── Rectangles ───────────────────────────────────────────────

    void DrawRect(EditorRect rect, EditorPaint paint);
    void FillRect(EditorRect rect, EditorPaint paint);
    void DrawRoundRect(EditorRect rect, float cornerRadius, EditorPaint paint);
    void FillRoundRect(EditorRect rect, float cornerRadius, EditorPaint paint);

    // ── Lines ────────────────────────────────────────────────────

    void DrawLine(float x1, float y1, float x2, float y2, EditorPaint paint);

    // ── Text ─────────────────────────────────────────────────────

    void DrawText(string text, float x, float y, EditorPaint paint);

    void DrawTextInRect(string text, EditorRect rect, EditorPaint paint,
        TextAlignment hAlign = TextAlignment.Left,
        VerticalAlignment vAlign = VerticalAlignment.Center,
        bool clip = true);

    // ── Text measuring ───────────────────────────────────────────

    EditorSize MeasureText(string text, EditorPaint paint);
    EditorFontMetrics GetFontMetrics(EditorPaint paint);

    // ── Clipping & transforms ────────────────────────────────────

    void ClipRect(EditorRect rect);
    void Save();
    void Restore();
    void Translate(float dx, float dy);

    // ── Images ───────────────────────────────────────────────────

    void DrawImage(object image, EditorRect destRect);

    /// <summary>
    /// Draws <paramref name="image"/> tinted to <paramref name="tint"/>, preserving the
    /// image's alpha shape.  Used for theme-aware toolbar icons so a single black-ink
    /// template bitmap renders correctly on both light and dark toolbars.
    /// The default implementation ignores the tint and falls back to <see cref="DrawImage"/>;
    /// platform rendering layers should override this to apply a color filter.
    /// </summary>
    void DrawTintedImage(object image, EditorRect destRect, EditorColor tint)
        => DrawImage(image, destRect);
}
