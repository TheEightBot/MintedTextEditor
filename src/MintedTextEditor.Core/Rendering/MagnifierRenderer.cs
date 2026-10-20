using MintedTextEditor.Core.Editing;

namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Draws the text magnifier: the editor content drawn again, zoomed, inside a lens. Drawing
/// the content again (rather than scaling a snapshot of the frame) keeps text sharp at any
/// zoom and looks the same on every platform.
/// </summary>
public sealed class MagnifierRenderer
{
    /// <summary>
    /// Draws the lens for <paramref name="magnifier"/>, if visible. Call last in a frame.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="magnifier">Magnifier state and geometry.</param>
    /// <param name="bounds">Area the lens must stay within (normally the whole control).</param>
    /// <param name="background">Fill behind the magnified content.</param>
    /// <param name="renderContent">Draws the content to magnify, in the same coordinates as the main frame.</param>
    public void Render(IDrawingContext context, Magnifier magnifier, EditorRect bounds,
        EditorColor background, Action<IDrawingContext> renderContent)
    {
        if (!magnifier.IsVisible) return;

        var options = magnifier.Options;
        var lens = magnifier.GetLensRect(bounds);
        float radius = Math.Min(options.CornerRadius, Math.Min(lens.Width, lens.Height) / 2f);

        if (options.ShadowColor.A > 0 && options.ShadowOffset > 0)
        {
            var shadow = new EditorRect(lens.X, lens.Y + options.ShadowOffset, lens.Width, lens.Height);
            context.FillRoundRect(shadow, radius, new EditorPaint { Color = options.ShadowColor });
        }

        context.Save();
        context.ClipRoundRect(lens, radius);
        context.FillRect(lens, new EditorPaint { Color = background });

        // Map the focus point to the lens centre, magnified.
        context.Translate(lens.X + lens.Width / 2f, lens.Y + lens.Height / 2f);
        context.Scale(options.Zoom, options.Zoom);
        context.Translate(-magnifier.FocusX, -magnifier.FocusY);
        renderContent(context);

        context.Restore();

        if (options.BorderColor.A > 0 && options.BorderWidth > 0)
        {
            context.DrawRoundRect(lens, radius, new EditorPaint
            {
                Color = options.BorderColor,
                StrokeWidth = options.BorderWidth,
                Style = PaintStyle.Stroke,
            });
        }
    }
}
