namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Platform-independent drawing context abstraction.
/// Implementations map these calls to concrete rendering APIs (SkiaSharp, etc.).
/// </summary>
public interface IDrawingContext
{
    // ── Rectangles ───────────────────────────────────────────────

    /// <summary>Draws the outline of <paramref name="rect"/>.</summary>
    /// <param name="rect">Rectangle to outline.</param>
    /// <param name="paint">Paint supplying the stroke color and width.</param>
    void DrawRect(EditorRect rect, EditorPaint paint);
    /// <summary>Fills <paramref name="rect"/>.</summary>
    /// <param name="rect">Rectangle to fill.</param>
    /// <param name="paint">Paint supplying the fill color.</param>
    void FillRect(EditorRect rect, EditorPaint paint);
    /// <summary>Draws the outline of a rectangle with rounded corners.</summary>
    /// <param name="rect">Bounds of the rectangle.</param>
    /// <param name="cornerRadius">Radius of each corner.</param>
    /// <param name="paint">Paint supplying the stroke color and width.</param>
    void DrawRoundRect(EditorRect rect, float cornerRadius, EditorPaint paint);
    /// <summary>Fills a rectangle with rounded corners.</summary>
    /// <param name="rect">Bounds of the rectangle.</param>
    /// <param name="cornerRadius">Radius of each corner.</param>
    /// <param name="paint">Paint supplying the fill color.</param>
    void FillRoundRect(EditorRect rect, float cornerRadius, EditorPaint paint);

    // ── Lines ────────────────────────────────────────────────────

    /// <summary>Draws a straight line from (<paramref name="x1"/>, <paramref name="y1"/>) to (<paramref name="x2"/>, <paramref name="y2"/>).</summary>
    /// <param name="x1">X coordinate of the start point.</param>
    /// <param name="y1">Y coordinate of the start point.</param>
    /// <param name="x2">X coordinate of the end point.</param>
    /// <param name="y2">Y coordinate of the end point.</param>
    /// <param name="paint">Paint supplying the stroke color and width.</param>
    void DrawLine(float x1, float y1, float x2, float y2, EditorPaint paint);

    // ── Text ─────────────────────────────────────────────────────

    /// <summary>Draws <paramref name="text"/> starting at <paramref name="x"/> with its baseline at <paramref name="y"/>.</summary>
    /// <param name="text">Text to draw.</param>
    /// <param name="x">X coordinate of the start of the text.</param>
    /// <param name="y">Y coordinate of the baseline.</param>
    /// <param name="paint">Paint supplying the color and font.</param>
    void DrawText(string text, float x, float y, EditorPaint paint);

    /// <summary>Draws a single line of <paramref name="text"/> aligned within <paramref name="rect"/>.</summary>
    /// <param name="text">Text to draw.</param>
    /// <param name="rect">Rectangle to align the text in.</param>
    /// <param name="paint">Paint supplying the color and font.</param>
    /// <param name="hAlign">Horizontal alignment within the rectangle.</param>
    /// <param name="vAlign">Vertical alignment within the rectangle.</param>
    /// <param name="clip">Whether to clip the text to the rectangle.</param>
    void DrawTextInRect(string text, EditorRect rect, EditorPaint paint,
        TextAlignment hAlign = TextAlignment.Left,
        VerticalAlignment vAlign = VerticalAlignment.Center,
        bool clip = true);

    // ── Text measuring ───────────────────────────────────────────

    /// <summary>Measures the width and height of <paramref name="text"/> drawn with <paramref name="paint"/>.</summary>
    /// <param name="text">Text to measure.</param>
    /// <param name="paint">Paint supplying the font.</param>
    /// <returns>The size of the text.</returns>
    EditorSize MeasureText(string text, EditorPaint paint);
    /// <summary>Returns the ascent, descent and leading of the font in <paramref name="paint"/>.</summary>
    EditorFontMetrics GetFontMetrics(EditorPaint paint);

    /// <summary>
    /// Returns the x offset of the caret before UTF-16 index <paramref name="index"/> in
    /// <paramref name="text"/>, measured from the start of the text as it is drawn whole.
    /// The default measures the prefix on its own. A context that shapes text should
    /// return the position within the shaped whole, so kerning and ligatures place the
    /// caret, and hit-testing thresholds, where the text is actually drawn.
    /// </summary>
    float MeasureCaretOffset(string text, int index, EditorPaint paint)
        => index <= 0 ? 0f : MeasureText(text[..Math.Min(index, text.Length)], paint).Width;

    // ── Clipping & transforms ────────────────────────────────────

    /// <summary>Intersects the current clip with <paramref name="rect"/>.</summary>
    void ClipRect(EditorRect rect);
    /// <summary>Saves the current clip and transform so a later <see cref="Restore"/> can return to them.</summary>
    void Save();
    /// <summary>Restores the clip and transform saved by the matching <see cref="Save"/>.</summary>
    void Restore();
    /// <summary>Offsets subsequent drawing by (<paramref name="dx"/>, <paramref name="dy"/>).</summary>
    void Translate(float dx, float dy);

    /// <summary>
    /// Scales subsequent drawing by (<paramref name="sx"/>, <paramref name="sy"/>). Used by the
    /// text magnifier. The default implementation does nothing (content is drawn unscaled);
    /// platform rendering layers should override it.
    /// </summary>
    void Scale(float sx, float sy) { }

    /// <summary>
    /// Intersects the clip with a rounded rectangle. The default implementation clips to the
    /// plain rectangle; platform rendering layers should override it.
    /// </summary>
    void ClipRoundRect(EditorRect rect, float cornerRadius) => ClipRect(rect);

    // ── Images ───────────────────────────────────────────────────

    /// <summary>Draws <paramref name="image"/> scaled to fill <paramref name="destRect"/>.</summary>
    /// <param name="image">Platform image object understood by the implementation (for example an SKImage or SKBitmap for SkiaSharp).</param>
    /// <param name="destRect">Destination rectangle.</param>
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
