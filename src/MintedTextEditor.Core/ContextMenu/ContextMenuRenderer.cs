using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.ContextMenu;

/// <summary>
/// Fully custom-drawn context menu popup.
/// </summary>
public sealed class ContextMenuRenderer
{
    public float ItemHeight      { get; set; } = 28f;
    public float SeparatorHeight { get; set; } = 9f;
    public float MinWidth        { get; set; } = 180f;
    public float Padding         { get; set; } = 4f;
    public float CornerRadius    { get; set; } = 6f;
    public float IconWidth       { get; set; } = 22f;

    public ContextMenuDefinition? Definition { get; private set; }
    public float X { get; private set; }
    public float Y { get; private set; }
    public int FocusedIndex { get; private set; } = -1;
    public bool IsOpen => Definition is not null;

    private readonly List<EditorRect> _itemRects = new();

    public void Open(ContextMenuDefinition definition, float x, float y, EditorRect viewport)
    {
        Definition   = definition ?? throw new ArgumentNullException(nameof(definition));
        FocusedIndex = -1;
        _itemRects.Clear();

        var size = MeasureSize(definition);
        X = Math.Min(x, viewport.X + viewport.Width  - size.Width);
        Y = Math.Min(y, viewport.Y + viewport.Height - size.Height);
        X = Math.Max(X, viewport.X);
        Y = Math.Max(Y, viewport.Y);
    }

    public void Close()
    {
        Definition   = null;
        FocusedIndex = -1;
        _itemRects.Clear();
    }

    public void Render(IDrawingContext ctx)
    {
        if (Definition is null) return;

        _itemRects.Clear();
        var visibleItems = Definition.VisibleItems.ToList();
        var totalSize    = MeasureSize(Definition);
        var popupRect    = new EditorRect(X, Y, totalSize.Width, totalSize.Height);

        // Shadow
        ctx.FillRoundRect(
            new EditorRect(popupRect.X + 2, popupRect.Y + 2, popupRect.Width, popupRect.Height),
            CornerRadius,
            new EditorPaint { Color = EditorColor.FromArgb(60, 0, 0, 0) });

        // Background + border
        ctx.FillRoundRect(popupRect, CornerRadius,
            new EditorPaint { Color = EditorColor.White });
        ctx.DrawRoundRect(popupRect, CornerRadius,
            new EditorPaint { Color = EditorColor.LightGray, StrokeWidth = 1f });

        float curY   = Y + Padding;
        float innerX = X + Padding;
        float itemW  = totalSize.Width - Padding * 2;

        for (int i = 0; i < visibleItems.Count; i++)
        {
            var item = visibleItems[i];

            if (i > 0 && visibleItems[i - 1].SeparatorAfter)
            {
                float midY = curY + SeparatorHeight / 2f;
                ctx.DrawLine(innerX, midY, innerX + itemW, midY,
                    new EditorPaint { Color = EditorColor.LightGray, StrokeWidth = 1f });
                curY += SeparatorHeight;
            }

            var rect = new EditorRect(innerX, curY, itemW, ItemHeight);
            _itemRects.Add(rect);

            if (i == FocusedIndex)
                ctx.FillRoundRect(rect, 3f, new EditorPaint { Color = EditorColor.CornflowerBlue });

            var textColor = item.IsEnabled
                ? (i == FocusedIndex ? EditorColor.White : EditorColor.Black)
                : EditorColor.Gray;

            ctx.DrawTextInRect(
                item.Label,
                new EditorRect(innerX + IconWidth + 4, curY, itemW - IconWidth - 4, ItemHeight),
                new EditorPaint { Color = textColor, Font = new EditorFont { Size = 13f } },
                hAlign: TextAlignment.Left,
                vAlign: VerticalAlignment.Center);

            curY += ItemHeight;
        }
    }

    public ContextMenuItem? HitTest(float x, float y)
    {
        if (Definition is null) return null;

        var visibleItems = Definition.VisibleItems.ToList();
        for (int i = 0; i < _itemRects.Count && i < visibleItems.Count; i++)
        {
            if (_itemRects[i].Contains(x, y))
            {
                FocusedIndex = i;
                return visibleItems[i];
            }
        }
        FocusedIndex = -1;
        return null;
    }

    public bool IsClickOutside(float x, float y)
    {
        if (Definition is null) return true;
        var size = MeasureSize(Definition);
        return !new EditorRect(X, Y, size.Width, size.Height).Contains(x, y);
    }

    /// <summary>
    /// Handles keyboard navigation. Returns the item to execute when Enter fires, null otherwise.
    /// </summary>
    public ContextMenuItem? HandleKey(ConsoleKey key)
    {
        if (Definition is null) return null;
        var items = Definition.VisibleItems.ToList();
        if (items.Count == 0) return null;

        switch (key)
        {
            case ConsoleKey.DownArrow:
                FocusedIndex = (FocusedIndex + 1) % items.Count;
                return null;
            case ConsoleKey.UpArrow:
                FocusedIndex = FocusedIndex <= 0 ? items.Count - 1 : FocusedIndex - 1;
                return null;
            case ConsoleKey.Enter:
                return (FocusedIndex >= 0 && FocusedIndex < items.Count)
                    ? items[FocusedIndex]
                    : null;
            case ConsoleKey.Escape:
                Close();
                return null;
            default:
                return null;
        }
    }

    private EditorSize MeasureSize(ContextMenuDefinition definition)
    {
        var items      = definition.VisibleItems.ToList();
        int separators = items.Take(items.Count - 1).Count(i => i.SeparatorAfter);
        float height   = Padding * 2 + items.Count * ItemHeight + separators * SeparatorHeight;
        return new EditorSize(MinWidth, height);
    }
}
