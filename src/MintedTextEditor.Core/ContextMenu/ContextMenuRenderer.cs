using MintedTextEditor.Core.Rendering;
using MintedTextEditor.Core.Theming;

namespace MintedTextEditor.Core.ContextMenu;

/// <summary>
/// Fully custom-drawn context menu popup.
/// </summary>
public sealed class ContextMenuRenderer
{
    /// <summary>Height of each menu item.</summary>
    public float ItemHeight      { get; set; } = 28f;
    /// <summary>Vertical space taken by a separator between items.</summary>
    public float SeparatorHeight { get; set; } = 9f;
    /// <summary>Width of the popup.</summary>
    public float MinWidth        { get; set; } = 180f;
    /// <summary>Inner padding between the popup edge and its items.</summary>
    public float Padding         { get; set; } = 4f;
    /// <summary>Corner radius of the popup background.</summary>
    public float CornerRadius    { get; set; } = 6f;
    /// <summary>Horizontal space reserved for the icon column before each label.</summary>
    public float IconWidth       { get; set; } = 22f;

    /// <summary>Popup background colour. Defaults to the light theme's <see cref="EditorStyle.ContextMenuBackground"/>.</summary>
    public EditorColor BackgroundColor   { get; set; } = EditorColor.White;
    /// <summary>Colour of the popup border.</summary>
    public EditorColor BorderColor       { get; set; } = EditorColor.LightGray;
    /// <summary>Colour of the separator lines between item groups.</summary>
    public EditorColor SeparatorColor    { get; set; } = EditorColor.LightGray;
    /// <summary>Label colour of enabled items.</summary>
    public EditorColor TextColor         { get; set; } = EditorColor.Black;
    /// <summary>Label colour of disabled items.</summary>
    public EditorColor DisabledTextColor { get; set; } = EditorColor.Gray;
    /// <summary>Background of the focused (hovered or keyboard-selected) item.</summary>
    public EditorColor HoverColor        { get; set; } = new(220, 235, 255);

    /// <summary>The menu currently shown, or null when closed.</summary>
    public ContextMenuDefinition? Definition { get; private set; }
    /// <summary>Left edge of the popup after clamping to the viewport.</summary>
    public float X { get; private set; }
    /// <summary>Top edge of the popup after clamping to the viewport.</summary>
    public float Y { get; private set; }
    /// <summary>Index of the focused visible item, or -1 when none is focused.</summary>
    public int FocusedIndex { get; private set; } = -1;
    /// <summary>True while a menu is open.</summary>
    public bool IsOpen => Definition is not null;

    private readonly List<EditorRect> _itemRects = new();

    /// <summary>
    /// Takes the menu colours from <paramref name="style"/>: <see cref="EditorStyle.ContextMenuBackground"/>,
    /// <see cref="EditorStyle.ContextMenuTextColor"/> and <see cref="EditorStyle.ContextMenuHoverColor"/>,
    /// with <see cref="EditorStyle.ToolbarSeparatorColor"/> for the border and separators. Disabled labels
    /// use the text colour blended halfway into the background, so they stay readable in every theme.
    /// </summary>
    /// <param name="style">The theme to read colours from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> is null.</exception>
    public void ApplyStyle(EditorStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        BackgroundColor   = style.ContextMenuBackground;
        TextColor         = style.ContextMenuTextColor;
        HoverColor        = style.ContextMenuHoverColor;
        BorderColor       = style.ToolbarSeparatorColor;
        SeparatorColor    = style.ToolbarSeparatorColor;
        DisabledTextColor = Blend(style.ContextMenuTextColor, style.ContextMenuBackground, 0.5f);
    }

    /// <summary>Bounds of the open popup, or an empty rectangle when the menu is closed.</summary>
    public EditorRect Bounds
    {
        get
        {
            if (Definition is null) return default;
            var size = MeasureSize(Definition);
            return new EditorRect(X, Y, size.Width, size.Height);
        }
    }

    /// <summary>
    /// Opens <paramref name="definition"/> at the given point, shifting the popup so it stays inside
    /// <paramref name="viewport"/>, and clears focus.
    /// </summary>
    /// <param name="definition">The menu to show.</param>
    /// <param name="x">Requested left edge of the popup.</param>
    /// <param name="y">Requested top edge of the popup.</param>
    /// <param name="viewport">Area the popup must fit within.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
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

    /// <summary>Closes the menu and clears focus.</summary>
    public void Close()
    {
        Definition   = null;
        FocusedIndex = -1;
        _itemRects.Clear();
    }

    /// <summary>
    /// Draws the open menu and records item bounds for <see cref="HitTest"/>. Does nothing when closed.
    /// </summary>
    /// <param name="ctx">Drawing context to render into.</param>
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
            new EditorPaint { Color = BackgroundColor });
        ctx.DrawRoundRect(popupRect, CornerRadius,
            new EditorPaint { Color = BorderColor, StrokeWidth = 1f });

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
                    new EditorPaint { Color = SeparatorColor, StrokeWidth = 1f });
                curY += SeparatorHeight;
            }

            var rect = new EditorRect(innerX, curY, itemW, ItemHeight);
            _itemRects.Add(rect);

            if (i == FocusedIndex && item.IsEnabled)
                ctx.FillRoundRect(rect, 3f, new EditorPaint { Color = HoverColor });

            var textColor = item.IsEnabled ? TextColor : DisabledTextColor;

            ctx.DrawTextInRect(
                item.Label,
                new EditorRect(innerX + IconWidth + 4, curY, itemW - IconWidth - 4, ItemHeight),
                new EditorPaint { Color = textColor, Font = new EditorFont { Size = 13f } },
                hAlign: TextAlignment.Left,
                vAlign: VerticalAlignment.Center);

            curY += ItemHeight;
        }
    }

    /// <summary>
    /// Returns the visible item at the given point (enabled or not) and focuses it when it is enabled;
    /// clears focus when no enabled item is hit. Uses item bounds from the last <see cref="Render"/> call.
    /// </summary>
    /// <param name="x">X coordinate of the point.</param>
    /// <param name="y">Y coordinate of the point.</param>
    /// <returns>The item under the point, or null.</returns>
    public ContextMenuItem? HitTest(float x, float y)
    {
        if (Definition is null) return null;

        var visibleItems = Definition.VisibleItems.ToList();
        for (int i = 0; i < _itemRects.Count && i < visibleItems.Count; i++)
        {
            if (_itemRects[i].Contains(x, y))
            {
                FocusedIndex = visibleItems[i].IsEnabled ? i : -1;
                return visibleItems[i];
            }
        }
        FocusedIndex = -1;
        return null;
    }

    /// <summary>Returns true when the point lies outside the popup bounds, or when the menu is closed.</summary>
    /// <param name="x">X coordinate of the point.</param>
    /// <param name="y">Y coordinate of the point.</param>
    public bool IsClickOutside(float x, float y)
    {
        if (Definition is null) return true;
        var size = MeasureSize(Definition);
        return !new EditorRect(X, Y, size.Width, size.Height).Contains(x, y);
    }

    /// <summary>
    /// Handles keyboard navigation. Returns the item to execute when Enter fires, null otherwise.
    /// </summary>
    /// <param name="key">
    /// The pressed key. Up/Down move focus to the previous/next enabled item (wrapping, skipping disabled
    /// items), Enter selects, Escape closes the menu.
    /// </param>
    /// <returns>The focused item when <paramref name="key"/> is Enter and an enabled item is focused; otherwise null.</returns>
    public ContextMenuItem? HandleKey(ConsoleKey key)
    {
        if (Definition is null) return null;
        var items = Definition.VisibleItems.ToList();
        if (items.Count == 0) return null;

        switch (key)
        {
            case ConsoleKey.DownArrow:
                MoveFocus(items, +1);
                return null;
            case ConsoleKey.UpArrow:
                MoveFocus(items, -1);
                return null;
            case ConsoleKey.Enter:
                return (FocusedIndex >= 0 && FocusedIndex < items.Count && items[FocusedIndex].IsEnabled)
                    ? items[FocusedIndex]
                    : null;
            case ConsoleKey.Escape:
                Close();
                return null;
            default:
                return null;
        }
    }

    private void MoveFocus(List<ContextMenuItem> items, int step)
    {
        int start = FocusedIndex < 0 ? (step > 0 ? -1 : 0) : FocusedIndex;
        for (int n = 1; n <= items.Count; n++)
        {
            int i = ((start + step * n) % items.Count + items.Count) % items.Count;
            if (items[i].IsEnabled)
            {
                FocusedIndex = i;
                return;
            }
        }
    }

    private static EditorColor Blend(EditorColor from, EditorColor to, float amount)
    {
        static byte Mix(byte a, byte b, float t) => (byte)Math.Round(a + (b - a) * t);
        return new EditorColor(Mix(from.R, to.R, amount), Mix(from.G, to.G, amount),
            Mix(from.B, to.B, amount), Mix(from.A, to.A, amount));
    }

    private EditorSize MeasureSize(ContextMenuDefinition definition)
    {
        var items      = definition.VisibleItems.ToList();
        int separators = items.Take(items.Count - 1).Count(i => i.SeparatorAfter);
        float height   = Padding * 2 + items.Count * ItemHeight + separators * SeparatorHeight;
        return new EditorSize(MinWidth, height);
    }
}
