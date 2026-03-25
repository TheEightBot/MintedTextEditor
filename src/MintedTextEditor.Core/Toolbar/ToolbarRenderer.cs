using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Toolbar;

/// <summary>
/// Renders a <see cref="ToolbarDefinition"/> using an <see cref="IDrawingContext"/> and
/// provides hit-testing so the platform layer can translate pointer events into toolbar actions.
/// </summary>
public sealed class ToolbarRenderer
{
    public ToolbarDefinition Definition    { get; }
    public float             ButtonSize    { get; set; } = 36f;
    public float             ButtonPadding { get; set; } = 5f;
    public float             GroupSpacing  { get; set; } = 8f;
    public float             RowSpacing    { get; set; } = 6f;
    public float             DropdownWidth { get; set; } = 128f;
    public bool              PreferVectorIcons { get; set; } = true;
    public ToolbarIconPack   IconPack { get; set; } = ToolbarIconPack.Lucide;

    /// <summary>
    /// Total height the toolbar occupies (button area + top/bottom padding).
    /// Use this to offset the document viewport below the toolbar.
    /// </summary>
    public float ToolbarHeight => _measuredToolbarHeight > 0f
        ? _measuredToolbarHeight
        : ButtonSize + ButtonPadding * 2;

    // ── Theme colors (set from EditorStyle before each Render) ───────────────

    public EditorColor BackgroundColor { get; set; } = new EditorColor(245, 245, 245);
    public EditorColor ButtonColor     { get; set; } = new EditorColor( 60,  60,  60);
    public EditorColor ActiveColor     { get; set; } = new EditorColor(190, 210, 240);
    public EditorColor SeparatorColor  { get; set; } = new EditorColor(200, 200, 200);
    public EditorColor DisabledColor   { get; set; } = new EditorColor(180, 180, 180);

    /// <summary>
    /// Optional icon resolver.  Given an icon key (e.g. "bold") returns a
    /// platform-specific image object understood by <see cref="IDrawingContext.DrawImage"/>,
    /// or <c>null</c> if no image is available (renderer falls back to text label).
    /// </summary>
    public Func<string, object?>? IconResolver { get; set; }

    // Populated during Render so that HitTest can work without a re-render.
    private readonly Dictionary<ToolbarItem, EditorRect> _itemRects = new();

    // ── Overflow ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Items that did not fit within <see cref="ToolbarDefinition.MaxRows"/> rows.
    /// Populated after each <see cref="Render"/> call.
    /// </summary>
    public IReadOnlyList<ToolbarItem> OverflowItems => _overflowItems;
    private readonly List<ToolbarItem> _overflowItems = new();

    /// <summary>True when the last render produced overflow items.</summary>
    public bool HasOverflow => _overflowItems.Count > 0;

    // Hit-rect of the "…" overflow button (valid only when HasOverflow is true).
    private EditorRect _overflowButtonRect;

    // Whether the overflow panel (showing overflow items) is currently open.
    private bool _overflowPanelOpen;

    // ── Overlay (open dropdown or colour-picker panel) ────────────────────────

    private ToolbarDropdown?   _activeDropdown;
    private ToolbarColorPicker? _activeColorPicker;
    private EditorRect          _overlayRect;
    private float               _overlayItemH = 24f;
    private float               _measuredToolbarHeight;

    /// <summary>
    /// True when a dropdown list or colour-picker grid is currently open and being rendered
    /// on top of the document area.
    /// </summary>
    public bool HasOpenOverlay => _activeDropdown is not null || _activeColorPicker is not null;

    /// <summary>
    /// Toggles the item's overlay open/closed.  Pass a <see cref="ToolbarDropdown"/> or
    /// <see cref="ToolbarColorPicker"/>; anything else (or null) just closes any open overlay.
    /// </summary>
    public void ToggleOverlay(ToolbarItem? item)
    {
        if (item is ToolbarDropdown dd)
        {
            _activeDropdown   = ReferenceEquals(_activeDropdown, dd) ? null : dd;
            _activeColorPicker = null;
        }
        else if (item is ToolbarColorPicker cp)
        {
            _activeColorPicker = ReferenceEquals(_activeColorPicker, cp) ? null : cp;
            _activeDropdown   = null;
        }
        else
        {
            _activeDropdown    = null;
            _activeColorPicker = null;
        }
    }

    /// <summary>Closes any open overlay without toggling.</summary>
    public void CloseOverlay()
    {
        _activeDropdown    = null;
        _activeColorPicker = null;
    }

    /// <summary>
    /// Handles a tap while an overlay is open. Dispatches the selection callback if the
    /// tap landed on an item, then closes the overlay. Returns <c>true</c> if the tap was
    /// consumed (regardless of whether an item was hit).
    /// </summary>
    public bool HandleOverlayTap(float x, float y)
    {
        if (!HasOpenOverlay) return false;

        if (_overlayRect.Contains(x, y))
        {
            int idx = ComputeOverlayIndex(x, y);
            if (idx >= 0)
            {
                if (_activeDropdown is not null)
                {
                    _activeDropdown.SelectedIndex = idx;
                    _activeDropdown.OnSelectionChanged?.Invoke(idx);
                }
                else if (_activeColorPicker is not null)
                {
                    _activeColorPicker.SelectedColor = _activeColorPicker.Colors[idx];
                    _activeColorPicker.OnColorSelected?.Invoke(_activeColorPicker.SelectedColor);
                }
            }
        }
        CloseOverlay();
        return true; // tap was consumed — don't route to document
    }

    private int ComputeOverlayIndex(float x, float y)
    {
        if (_activeDropdown is not null)
        {
            int idx = (int)((y - _overlayRect.Y) / _overlayItemH);
            return (idx >= 0 && idx < _activeDropdown.Items.Count) ? idx : -1;
        }
        if (_activeColorPicker is not null)
        {
            const int cols = 5;
            float swatchSize = _overlayRect.Width / cols;
            int col = (int)((x - _overlayRect.X) / swatchSize);
            int row = (int)((y - _overlayRect.Y) / swatchSize);
            int idx = row * cols + col;
            return (idx >= 0 && idx < _activeColorPicker.Colors.Count) ? idx : -1;
        }
        return -1;
    }

    /// <summary>
    /// Renders the open overlay panel on top of the document.
    /// Call this after the document has been rendered so the panel draws above it.
    /// </summary>
    public void RenderOverlay(IDrawingContext ctx, float totalWidth, float totalHeight)
    {
        if (_activeDropdown is not null)
            RenderDropdownList(ctx, _activeDropdown, totalWidth, totalHeight);
        else if (_activeColorPicker is not null)
            RenderColorPickerGrid(ctx, _activeColorPicker, totalWidth, totalHeight);
    }

    private void RenderDropdownList(IDrawingContext ctx, ToolbarDropdown dd,
        float totalWidth, float totalHeight)
    {
        if (!_itemRects.TryGetValue(dd, out var anchor)) return;

        const int maxVisible = 10;
        int itemCount = Math.Min(dd.Items.Count, maxVisible);
        float panelW = Math.Max(anchor.Width, 120f);
        float panelH = itemCount * _overlayItemH + 4f;

        // Position below the dropdown button, clamped inside canvas
        float panelX = anchor.X;
        float panelY = anchor.Y + anchor.Height + 2f;
        if (panelX + panelW > totalWidth)  panelX = totalWidth - panelW;
        if (panelY + panelH > totalHeight) panelY = anchor.Y - panelH - 2f;

        _overlayRect = new EditorRect(panelX, panelY, panelW, panelH);

        // Panel shadow / background
        var shadowPaint  = new EditorPaint { Color = new EditorColor(0, 0, 0, 40) };
        var bgPaint      = new EditorPaint { Color = EditorColor.White };
        var borderPaint  = new EditorPaint { Color = SeparatorColor };
        var textPaint    = new EditorPaint { Color = ButtonColor };
        var activePaint  = new EditorPaint { Color = ActiveColor };

        ctx.FillRoundRect(_overlayRect.Inflate(2, 2), 5f, shadowPaint);
        ctx.FillRoundRect(_overlayRect, 4f, bgPaint);
        ctx.DrawRoundRect(_overlayRect, 4f, borderPaint);

        float iy = panelY + 2f;
        for (int i = 0; i < itemCount; i++)
        {
            var itemRect = new EditorRect(panelX + 1, iy, panelW - 2, _overlayItemH);
            if (i == dd.SelectedIndex)
                ctx.FillRoundRect(itemRect, 3f, activePaint);
            ctx.DrawTextInRect(dd.Items[i], itemRect.Inflate(-4, -1), textPaint,
                TextAlignment.Left, VerticalAlignment.Center);
            iy += _overlayItemH;
        }
    }

    private void RenderColorPickerGrid(IDrawingContext ctx, ToolbarColorPicker cp,
        float totalWidth, float totalHeight)
    {
        if (!_itemRects.TryGetValue(cp, out var anchor)) return;

        const int cols = 5;
        int rows = (int)Math.Ceiling((double)cp.Colors.Count / cols);
        float swatchSize = 24f;
        float panelW = cols * swatchSize + 2f;
        float panelH = rows * swatchSize + 2f;

        float panelX = anchor.X;
        float panelY = anchor.Y + anchor.Height + 2f;
        if (panelX + panelW > totalWidth)  panelX = totalWidth - panelW;
        if (panelY + panelH > totalHeight) panelY = anchor.Y - panelH - 2f;

        _overlayRect = new EditorRect(panelX, panelY, panelW, panelH);

        var bgPaint     = new EditorPaint { Color = EditorColor.White };
        var borderPaint = new EditorPaint { Color = SeparatorColor };

        ctx.FillRoundRect(_overlayRect, 4f, bgPaint);
        ctx.DrawRoundRect(_overlayRect, 4f, borderPaint);

        float ox = panelX + 1f;
        float oy = panelY + 1f;
        for (int i = 0; i < cp.Colors.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            var swatchRect = new EditorRect(
                ox + col * swatchSize, oy + row * swatchSize,
                swatchSize, swatchSize);
            ctx.FillRect(swatchRect.Inflate(-1, -1), new EditorPaint { Color = cp.Colors[i] });
            if (cp.Colors[i].Equals(cp.SelectedColor))
                ctx.DrawRect(swatchRect.Inflate(-1, -1), borderPaint);
        }
    }

    public ToolbarRenderer(ToolbarDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    // ── Toggle state ─────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the current selection from <paramref name="ctx"/> and updates each
    /// toggle button's <see cref="ToolbarButton.IsActive"/> flag accordingly.
    /// </summary>
    public void UpdateToggleStates(EditorContext ctx)
    {
        var range = ctx.Selection.Range;

        foreach (var group in Definition.Groups)
        {
            foreach (var item in group.Items)
            {
                if (item is not ToolbarButton btn || !btn.IsToggle)
                    continue;

                btn.IsActive = btn.Command?.Name switch
                {
                    "ToggleBold"          => FormattingEngine.IsAppliedToEntireRange(ctx.Document, range, s => s.IsBold),
                    "ToggleItalic"        => FormattingEngine.IsAppliedToEntireRange(ctx.Document, range, s => s.IsItalic),
                    "ToggleUnderline"     => FormattingEngine.IsAppliedToEntireRange(ctx.Document, range, s => s.IsUnderline),
                    "ToggleStrikethrough" => FormattingEngine.IsAppliedToEntireRange(ctx.Document, range, s => s.IsStrikethrough),
                    "ToggleSubscript"     => FormattingEngine.IsAppliedToEntireRange(ctx.Document, range, s => s.IsSubscript),
                    "ToggleSuperscript"   => FormattingEngine.IsAppliedToEntireRange(ctx.Document, range, s => s.IsSuperscript),
                    _                    => btn.IsActive,
                };
            }
        }
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    /// <summary>Renders the entire toolbar into <paramref name="bounds"/>.</summary>
    public void Render(IDrawingContext ctx, EditorRect bounds)
    {
        _itemRects.Clear();
        _overflowItems.Clear();
        _overflowButtonRect = default;

        float left = bounds.X + ButtonPadding;
        float right = bounds.X + bounds.Width - ButtonPadding;
        float x = left;
        float y = bounds.Y + ButtonPadding;
        float h = ButtonSize;
        bool canWrap = Definition.LayoutMode == ToolbarLayoutMode.Wrap
                    || Definition.LayoutMode == ToolbarLayoutMode.Overflow;

        int  maxRows       = Definition.MaxRows; // 0 = unlimited
        float overflowBtnW = ButtonSize + ButtonPadding;
        int  currentRow   = 0;
        bool inOverflow   = false;

        var activePaint   = new EditorPaint { Color = ActiveColor };
        var normalPaint   = new EditorPaint { Color = new EditorColor(255, 255, 255, 18) };
        var controlBorder = new EditorPaint { Color = SeparatorColor, Style = PaintStyle.Stroke, StrokeWidth = 1f };
        var disabledPaint = new EditorPaint { Color = DisabledColor };
        var sepPaint      = new EditorPaint { Color = SeparatorColor };
        var textPaint     = new EditorPaint { Color = ButtonColor };
        var disabledTextPaint = new EditorPaint { Color = DisabledColor };

        bool firstGroup = true;
        foreach (var group in Definition.Groups)
        {
            var visibleItems = group.Items.Where(i => i.IsVisible).ToList();
            if (visibleItems.Count == 0) continue;

            if (!firstGroup)
            {
                // The effective right boundary of the current row: shrink by overflow button width
                // on the last allowed row so the "…" button always has space.
                float effRight = (maxRows > 0 && currentRow == maxRows - 1)
                    ? right - overflowBtnW : right;

                bool needsWrap = canWrap && x > left && x + GroupSpacing > effRight;
                if (needsWrap && !inOverflow)
                {
                    currentRow++;
                    if (maxRows > 0 && currentRow >= maxRows)
                        inOverflow = true;
                    else
                    {
                        x = left;
                        y += h + RowSpacing;
                    }
                }
                else if (x > left && !inOverflow)
                {
                    float sepX = x + 2;
                    ctx.DrawLine(sepX, y + 3, sepX, y + h - 3, sepPaint);
                    x += GroupSpacing;
                }
            }
            firstGroup = false;

            foreach (var item in visibleItems)
            {
                if (inOverflow)
                {
                    _overflowItems.Add(item);
                    continue;
                }

                if (item is ToolbarSeparator)
                {
                    const float sepWidth = 10f;
                    float effRight = (maxRows > 0 && currentRow == maxRows - 1)
                        ? right - overflowBtnW : right;
                    if (canWrap && x > left && x + sepWidth > effRight)
                    {
                        currentRow++;
                        if (maxRows > 0 && currentRow >= maxRows)
                        {
                            inOverflow = true;
                            continue;
                        }
                        x = left;
                        y += h + RowSpacing;
                    }
                    if (x > left)
                    {
                        float sepX = x + 4;
                        ctx.DrawLine(sepX, y + 3, sepX, y + h - 3, sepPaint);
                        x += sepWidth;
                    }
                    continue;
                }

                float w = item is ToolbarDropdown ? DropdownWidth : ButtonSize;
                float rowEffRight = (maxRows > 0 && currentRow == maxRows - 1)
                    ? right - overflowBtnW : right;

                if (canWrap && x > left && x + w > rowEffRight)
                {
                    currentRow++;
                    if (maxRows > 0 && currentRow >= maxRows)
                    {
                        inOverflow = true;
                        _overflowItems.Add(item);
                        if (_overflowButtonRect == default)
                            _overflowButtonRect = new EditorRect(right - ButtonSize, y, ButtonSize, h);
                        continue;
                    }
                    x = left;
                    y += h + RowSpacing;
                }

                var rect = new EditorRect(x, y, w, h);
                _itemRects[item] = rect;

                x += w + ButtonPadding;
            }
        }

        // If overflow was triggered, record the button rect (may not have been set yet if last item
        // of the last allowed row pushed overflow on next iteration).
        if (inOverflow && _overflowButtonRect == default)
            _overflowButtonRect = new EditorRect(right - ButtonSize, y, ButtonSize, h);

        _measuredToolbarHeight = Math.Max(ButtonSize + ButtonPadding * 2, (y - bounds.Y) + h + ButtonPadding);

        var paintBounds = new EditorRect(bounds.X, bounds.Y, bounds.Width, _measuredToolbarHeight);
        ctx.FillRect(paintBounds, new EditorPaint { Color = BackgroundColor });

        // Redraw items over toolbar background after background fill.
        ReRenderItems(ctx, left, right, bounds.Y + ButtonPadding, h, canWrap,
            activePaint, normalPaint, controlBorder, sepPaint, textPaint, disabledTextPaint);

        // Draw a subtle bottom border at the measured toolbar edge.
        var borderPaint = new EditorPaint { Color = SeparatorColor };
        ctx.DrawLine(bounds.X, bounds.Y + _measuredToolbarHeight - 1,
                     bounds.X + bounds.Width, bounds.Y + _measuredToolbarHeight - 1, borderPaint);
    }

    private void ReRenderItems(
        IDrawingContext ctx,
        float left,
        float right,
        float startY,
        float h,
        bool canWrap,
        EditorPaint activePaint,
        EditorPaint normalPaint,
        EditorPaint controlBorder,
        EditorPaint sepPaint,
        EditorPaint textPaint,
        EditorPaint disabledTextPaint)
    {
        int  maxRows       = Definition.MaxRows;
        float overflowBtnW = ButtonSize + ButtonPadding;
        int  currentRow   = 0;
        bool inOverflow   = false;

        float x = left;
        float y = startY;
        bool firstGroup = true;

        foreach (var group in Definition.Groups)
        {
            var visibleItems = group.Items.Where(i => i.IsVisible).ToList();
            if (visibleItems.Count == 0) continue;

            if (!firstGroup)
            {
                float effRight = (maxRows > 0 && currentRow == maxRows - 1)
                    ? right - overflowBtnW : right;
                bool needsWrap = canWrap && x > left && x + GroupSpacing > effRight;
                if (needsWrap && !inOverflow)
                {
                    currentRow++;
                    if (maxRows > 0 && currentRow >= maxRows)
                        inOverflow = true;
                    else
                    {
                        x = left;
                        y += h + RowSpacing;
                    }
                }
                else if (x > left && !inOverflow)
                {
                    float sepX = x + 2;
                    ctx.DrawLine(sepX, y + 3, sepX, y + h - 3, sepPaint);
                    x += GroupSpacing;
                }
            }
            firstGroup = false;

            foreach (var item in visibleItems)
            {
                if (inOverflow) continue; // rendered in overflow panel

                if (item is ToolbarSeparator)
                {
                    const float sepWidth = 10f;
                    float effRight = (maxRows > 0 && currentRow == maxRows - 1)
                        ? right - overflowBtnW : right;
                    if (canWrap && x > left && x + sepWidth > effRight)
                    {
                        currentRow++;
                        if (maxRows > 0 && currentRow >= maxRows) { inOverflow = true; continue; }
                        x = left;
                        y += h + RowSpacing;
                    }
                    if (x > left)
                    {
                        float sepX = x + 4;
                        ctx.DrawLine(sepX, y + 3, sepX, y + h - 3, sepPaint);
                        x += sepWidth;
                    }
                    continue;
                }

                float w = item is ToolbarDropdown ? DropdownWidth : ButtonSize;
                float rowEffRight = (maxRows > 0 && currentRow == maxRows - 1)
                    ? right - overflowBtnW : right;
                if (canWrap && x > left && x + w > rowEffRight)
                {
                    currentRow++;
                    if (maxRows > 0 && currentRow >= maxRows)
                    {
                        inOverflow = true;
                        continue;
                    }
                    x = left;
                    y += h + RowSpacing;
                }

                var rect = new EditorRect(x, y, w, h);

                if (item is ToolbarButton btn)
                {
                    var fill = btn.IsActive ? activePaint : normalPaint;
                    ctx.FillRoundRect(rect, 5f, fill);
                    ctx.DrawRoundRect(rect, 5f, controlBorder);

                    var labelPaint = item.IsEnabled ? textPaint : disabledTextPaint;

                    var iconImage = !string.IsNullOrEmpty(btn.Icon)
                        ? IconResolver?.Invoke(btn.Icon)
                        : null;

                    bool drewVectorIcon = !string.IsNullOrEmpty(btn.Icon)
                                          && PreferVectorIcons
                                          && iconImage is null
                                          && DrawVectorIcon(ctx, btn.Icon!, rect.Inflate(-5f, -5f), labelPaint, IconPack);

                    if (!drewVectorIcon)
                    {
                        if (iconImage is not null)
                        {
                            float iconSize = MathF.Round(Math.Max(12f, Math.Min(rect.Width, rect.Height) * 0.72f));
                            float iconX = MathF.Round(rect.X + (rect.Width  - iconSize) / 2f);
                            float iconY = MathF.Round(rect.Y + (rect.Height - iconSize) / 2f);
                            var iconTint = btn.IsEnabled ? labelPaint.Color : DisabledColor;
                            ctx.DrawTintedImage(iconImage, new EditorRect(iconX, iconY, iconSize, iconSize), iconTint);
                        }
                        else
                        {
                            ctx.DrawTextInRect(btn.Label, rect.Inflate(-2, -2), labelPaint,
                                TextAlignment.Center, VerticalAlignment.Center);
                        }
                    }
                }
                else if (item is ToolbarDropdown dd)
                {
                    ctx.FillRoundRect(rect, 5f, normalPaint);
                    ctx.DrawRoundRect(rect, 5f, controlBorder);
                    var display = dd.SelectedIndex >= 0 && dd.SelectedIndex < dd.Items.Count
                        ? dd.Items[dd.SelectedIndex]
                        : dd.Label;
                    ctx.DrawTextInRect(display, rect.Inflate(-4, -2), textPaint,
                        TextAlignment.Left, VerticalAlignment.Center);
                    float cx = rect.X + rect.Width - 10f;
                    float cy = rect.Y + rect.Height / 2f;
                    ctx.DrawLine(cx - 3, cy - 2, cx, cy + 2, textPaint);
                    ctx.DrawLine(cx, cy + 2, cx + 3, cy - 2, textPaint);
                }
                else if (item is ToolbarColorPicker cp)
                {
                    var swatchColor = cp.SelectedColor.A > 0 ? cp.SelectedColor : EditorColor.White;
                    ctx.FillRoundRect(rect, 5f, new EditorPaint { Color = swatchColor });
                    ctx.DrawRoundRect(rect, 5f, controlBorder);
                }

                x += w + ButtonPadding;
            }
        }

        // Draw the "…" overflow button if needed.
        if (HasOverflow && _overflowButtonRect != default)
        {
            var overflowBtnPaint   = _overflowPanelOpen ? activePaint : normalPaint;
            var overflowBorderPaint = new EditorPaint { Color = SeparatorColor, Style = PaintStyle.Stroke, StrokeWidth = 1f };
            ctx.FillRoundRect(_overflowButtonRect, 5f, overflowBtnPaint);
            ctx.DrawRoundRect(_overflowButtonRect, 5f, overflowBorderPaint);
            ctx.DrawTextInRect("…", _overflowButtonRect, textPaint, TextAlignment.Center, VerticalAlignment.Center);
        }
    }

    private static bool DrawVectorIcon(IDrawingContext ctx, string icon, EditorRect rect, EditorPaint paint, ToolbarIconPack pack)
    {
        icon = icon.ToLowerInvariant();
        var iconPaint = CloneWithPackWeight(paint, pack);

        // All vector drawing is clipped to the allocated icon rectangle so that
        // no glyph or line can paint outside the button boundary.
        ctx.Save();
        ctx.ClipRect(rect);

        // Proportional font size for text-glyph icons (≈ 72 % of icon height).
        float textSize = Math.Max(9f, rect.Height * 0.72f);
        var textPaint = new EditorPaint
        {
            Color = iconPaint.Color,
            IsAntiAlias = true,
            Font = new EditorFont("Default", textSize),
        };

        bool drawn = true;
        switch (icon)
        {
            case "bold":
                textPaint.Font = new EditorFont("Default", textSize, true);
                ctx.DrawTextInRect("B", rect, textPaint, TextAlignment.Center, VerticalAlignment.Center);
                break;
            case "italic":
                textPaint.Font = new EditorFont("Default", textSize, false, true);
                ctx.DrawTextInRect("I", rect, textPaint, TextAlignment.Center, VerticalAlignment.Center);
                break;
            case "underline":
            {
                var upperRect = new EditorRect(rect.X, rect.Y, rect.Width, rect.Height * 0.82f);
                ctx.DrawTextInRect("U", upperRect, textPaint, TextAlignment.Center, VerticalAlignment.Center);
                float lineY = rect.Bottom - rect.Height * 0.08f;
                ctx.DrawLine(rect.X + rect.Width * 0.1f, lineY, rect.Right - rect.Width * 0.1f, lineY, iconPaint);
                break;
            }
            case "strikethrough":
            {
                ctx.DrawTextInRect("S", rect, textPaint, TextAlignment.Center, VerticalAlignment.Center);
                float lineY = rect.Y + rect.Height * 0.5f;
                ctx.DrawLine(rect.X + rect.Width * 0.05f, lineY, rect.Right - rect.Width * 0.05f, lineY, iconPaint);
                break;
            }
            case "subscript":
            {
                var mainRect = new EditorRect(rect.X, rect.Y, rect.Width * 0.72f, rect.Height * 0.75f);
                ctx.DrawTextInRect("X", mainRect, textPaint, TextAlignment.Center, VerticalAlignment.Center);
                var subRect = new EditorRect(rect.X + rect.Width * 0.52f, rect.Y + rect.Height * 0.52f, rect.Width * 0.42f, rect.Height * 0.42f);
                var smallPaint = new EditorPaint { Color = iconPaint.Color, IsAntiAlias = true, Font = new EditorFont("Default", textSize * 0.58f) };
                ctx.DrawTextInRect("2", subRect, smallPaint, TextAlignment.Center, VerticalAlignment.Center);
                break;
            }
            case "superscript":
            {
                var mainRect = new EditorRect(rect.X, rect.Y + rect.Height * 0.25f, rect.Width * 0.72f, rect.Height * 0.75f);
                ctx.DrawTextInRect("X", mainRect, textPaint, TextAlignment.Center, VerticalAlignment.Center);
                var supRect = new EditorRect(rect.X + rect.Width * 0.52f, rect.Y, rect.Width * 0.42f, rect.Height * 0.42f);
                var smallPaint = new EditorPaint { Color = iconPaint.Color, IsAntiAlias = true, Font = new EditorFont("Default", textSize * 0.58f) };
                ctx.DrawTextInRect("2", supRect, smallPaint, TextAlignment.Center, VerticalAlignment.Center);
                break;
            }
            case "undo":
                DrawUndoRedoIcon(ctx, rect, iconPaint, undo: true);
                break;
            case "redo":
                DrawUndoRedoIcon(ctx, rect, iconPaint, undo: false);
                break;
            case "align-left":
                DrawHorizontalBars(ctx, rect, iconPaint, leftAligned: true,  centered: false, rightAligned: false);
                break;
            case "align-center":
                DrawHorizontalBars(ctx, rect, iconPaint, leftAligned: false, centered: true,  rightAligned: false);
                break;
            case "align-right":
                DrawHorizontalBars(ctx, rect, iconPaint, leftAligned: false, centered: false, rightAligned: true);
                break;
            case "align-justify":
                DrawHorizontalBars(ctx, rect, iconPaint, leftAligned: true,  centered: false, rightAligned: true);
                break;
            case "bullet-list":
                DrawListIcon(ctx, rect, iconPaint, numbered: false);
                break;
            case "number-list":
                DrawListIcon(ctx, rect, iconPaint, numbered: true);
                break;
            case "indent-decrease":
                DrawIndentIcon(ctx, rect, iconPaint, increase: false);
                break;
            case "indent-increase":
                DrawIndentIcon(ctx, rect, iconPaint, increase: true);
                break;
            case "hyperlink":
                DrawLinkIcon(ctx, rect, iconPaint);
                break;
            case "image":
                DrawImageIcon(ctx, rect, iconPaint);
                break;
            case "table":
                DrawTableIcon(ctx, rect, iconPaint);
                break;
            case "clear-formatting":
            {
                ctx.DrawTextInRect(pack == ToolbarIconPack.MaterialSymbols ? "Tt" : "Tx", rect, textPaint, TextAlignment.Center, VerticalAlignment.Center);
                float margin = rect.Width * 0.1f;
                ctx.DrawLine(rect.X + margin, rect.Bottom - margin, rect.Right - margin, rect.Y + margin, iconPaint);
                break;
            }
            default:
                drawn = false;
                break;
        }

        ctx.Restore();
        return drawn;
    }

    private static void DrawUndoRedoIcon(IDrawingContext ctx, EditorRect rect, EditorPaint paint, bool undo)
    {
        // Proportional arrow: shaft + arrowhead tip
        float w = rect.Width;
        float h = rect.Height;
        float midY = rect.Y + h * 0.5f;

        // Arrow shaft spans ~60 % of width; arrowhead covers the remaining ~25 %
        float shaftLen = w * 0.60f;
        float headW    = w * 0.25f;
        float headH    = h * 0.30f;

        if (undo)
        {
            float tipX   = rect.X + w * 0.18f;   // arrowhead tip at left
            float shaftX = tipX + shaftLen;       // shaft right end
            ctx.DrawLine(tipX, midY, shaftX, midY, paint);
            ctx.DrawLine(tipX, midY, tipX + headW, midY - headH, paint);
            ctx.DrawLine(tipX, midY, tipX + headW, midY + headH, paint);
        }
        else
        {
            float tipX   = rect.Right - w * 0.18f; // arrowhead tip at right
            float shaftX = tipX - shaftLen;         // shaft left end
            ctx.DrawLine(shaftX, midY, tipX, midY, paint);
            ctx.DrawLine(tipX, midY, tipX - headW, midY - headH, paint);
            ctx.DrawLine(tipX, midY, tipX - headW, midY + headH, paint);
        }
    }

    private static EditorPaint CloneWithPackWeight(EditorPaint paint, ToolbarIconPack pack)
    {
        float strokeWidth = pack switch
        {
            ToolbarIconPack.Lucide => 1.8f,
            ToolbarIconPack.Heroicons => 2.2f,
            ToolbarIconPack.MaterialSymbols => 2.0f,
            _ => 1.8f,
        };

        return new EditorPaint
        {
            Color = paint.Color,
            Style = paint.Style,
            StrokeWidth = strokeWidth,
            IsAntiAlias = paint.IsAntiAlias,
            Font = paint.Font,
        };
    }

    private static void DrawHorizontalBars(IDrawingContext ctx, EditorRect rect, EditorPaint paint,
        bool leftAligned, bool centered, bool rightAligned)
    {
        float h = rect.Height;
        // Three bars at ~15 %, 50 %, and 85 % of height
        float[] ys =
        [
            rect.Y + h * 0.15f,
            rect.Y + h * 0.50f,
            rect.Y + h * 0.85f,
        ];

        for (int i = 0; i < ys.Length; i++)
        {
            // Middle bar is slightly wider than top/bottom bars
            float frac = (i == 1) ? 0.88f : 0.70f;
            float barW = rect.Width * frac;
            float x1;
            if (centered)
                x1 = rect.X + (rect.Width - barW) / 2f;
            else if (rightAligned && !leftAligned)
                x1 = rect.Right - barW;
            else
                x1 = rect.X;

            float x2 = leftAligned && rightAligned ? rect.Right : x1 + barW;
            ctx.DrawLine(x1, ys[i], x2, ys[i], paint);
        }
    }

    private static void DrawListIcon(IDrawingContext ctx, EditorRect rect, EditorPaint paint, bool numbered)
    {
        float h = rect.Height;
        float w = rect.Width;

        // Three evenly-spaced rows at 15 %, 50 %, and 85 % of height
        float y1 = rect.Y + h * 0.15f;
        float y2 = rect.Y + h * 0.50f;
        float y3 = rect.Y + h * 0.85f;

        // Bullet/number column takes ~25 % of width; text lines take the remaining ~65 %
        float bulletW = w * 0.25f;
        float lineStart = rect.X + w * 0.38f;

        if (numbered)
        {
            float numSize = Math.Max(6f, h * 0.28f);
            var numPaint = new EditorPaint { Color = paint.Color, IsAntiAlias = true, Font = new EditorFont("Default", numSize) };
            ctx.DrawTextInRect("1", new EditorRect(rect.X, y1 - numSize / 2f, bulletW, numSize), numPaint, TextAlignment.Center, VerticalAlignment.Center);
            ctx.DrawTextInRect("2", new EditorRect(rect.X, y2 - numSize / 2f, bulletW, numSize), numPaint, TextAlignment.Center, VerticalAlignment.Center);
            ctx.DrawTextInRect("3", new EditorRect(rect.X, y3 - numSize / 2f, bulletW, numSize), numPaint, TextAlignment.Center, VerticalAlignment.Center);
        }
        else
        {
            // Filled square bullets proportional to the rect
            float dotSize = Math.Max(2f, h * 0.10f);
            float dotX = rect.X + (bulletW - dotSize) / 2f;
            ctx.FillRect(new EditorRect(dotX, y1 - dotSize / 2f, dotSize, dotSize), paint);
            ctx.FillRect(new EditorRect(dotX, y2 - dotSize / 2f, dotSize, dotSize), paint);
            ctx.FillRect(new EditorRect(dotX, y3 - dotSize / 2f, dotSize, dotSize), paint);
        }

        ctx.DrawLine(lineStart, y1, rect.Right, y1, paint);
        ctx.DrawLine(lineStart, y2, rect.Right, y2, paint);
        ctx.DrawLine(lineStart, y3, rect.Right, y3, paint);
    }

    private static void DrawIndentIcon(IDrawingContext ctx, EditorRect rect, EditorPaint paint, bool increase)
    {
        float h = rect.Height;
        float w = rect.Width;

        // Three text-line bars
        float y1 = rect.Y + h * 0.15f;
        float y2 = rect.Y + h * 0.50f;
        float y3 = rect.Y + h * 0.85f;

        // Arrow occupies ~30 % of width; text lines occupy the rest
        float arrowW = w * 0.30f;
        float lineInset = increase ? arrowW + w * 0.08f : 0f;
        float lineEnd   = increase ? w : w - arrowW - w * 0.08f;

        ctx.DrawLine(rect.X + lineInset, y1, rect.X + lineEnd, y1, paint);
        ctx.DrawLine(rect.X + lineInset, y2, rect.X + lineEnd, y2, paint);
        ctx.DrawLine(rect.X + lineInset, y3, rect.X + lineEnd, y3, paint);

        // Arrow pointing right (increase) or left (decrease)
        float headH = h * 0.25f;
        if (increase)
        {
            float tipX  = rect.X + arrowW;
            float baseX = rect.X;
            ctx.DrawLine(baseX, y2, tipX, y2, paint);
            ctx.DrawLine(tipX, y2, tipX - arrowW * 0.45f, y2 - headH, paint);
            ctx.DrawLine(tipX, y2, tipX - arrowW * 0.45f, y2 + headH, paint);
        }
        else
        {
            float tipX  = rect.Right - arrowW;
            float baseX = rect.Right;
            ctx.DrawLine(baseX, y2, tipX, y2, paint);
            ctx.DrawLine(tipX, y2, tipX + arrowW * 0.45f, y2 - headH, paint);
            ctx.DrawLine(tipX, y2, tipX + arrowW * 0.45f, y2 + headH, paint);
        }
    }

    private static void DrawLinkIcon(IDrawingContext ctx, EditorRect rect, EditorPaint paint)
    {
        // Two overlapping rounded chain-link rectangles
        float h = rect.Height;
        float w = rect.Width;
        float linkH = h * 0.38f;
        float linkW = w * 0.52f;
        float midY  = rect.Y + (h - linkH) / 2f;
        float cornerR = linkH * 0.42f;

        var leftLink  = new EditorRect(rect.X, midY, linkW, linkH);
        var rightLink = new EditorRect(rect.X + w - linkW, midY, linkW, linkH);
        ctx.DrawRoundRect(leftLink,  cornerR, paint);
        ctx.DrawRoundRect(rightLink, cornerR, paint);
    }

    private static void DrawImageIcon(IDrawingContext ctx, EditorRect rect, EditorPaint paint)
    {
        float margin = Math.Max(1f, rect.Width * 0.06f);
        var frame = new EditorRect(rect.X + margin, rect.Y + margin, rect.Width - 2 * margin, rect.Height - 2 * margin);
        ctx.DrawRect(frame, paint);

        // Mountain / landscape triangle
        float midX = frame.X + frame.Width * 0.5f;
        float midY = frame.Y + frame.Height * 0.5f;
        float inset = frame.Width * 0.12f;
        ctx.DrawLine(frame.X + inset,       frame.Bottom - inset, midX, midY, paint);
        ctx.DrawLine(midX, midY, frame.Right - inset, frame.Bottom - inset, paint);

        // Sun circle in top-right
        float sunR = frame.Height * 0.15f;
        float sunX = frame.Right - frame.Width * 0.25f;
        float sunY = frame.Y    + frame.Height * 0.25f;
        ctx.DrawRoundRect(new EditorRect(sunX - sunR, sunY - sunR, sunR * 2, sunR * 2), sunR, paint);
    }

    private static void DrawTableIcon(IDrawingContext ctx, EditorRect rect, EditorPaint paint)
    {
        float margin = Math.Max(1f, rect.Width * 0.05f);
        var frame = new EditorRect(rect.X + margin, rect.Y + margin, rect.Width - 2 * margin, rect.Height - 2 * margin);
        ctx.DrawRect(frame, paint);

        // 2 vertical dividers → 3 columns; 1 horizontal divider → 2 rows
        float c1 = frame.X + frame.Width / 3f;
        float c2 = frame.X + frame.Width * 2f / 3f;
        float r1 = frame.Y + frame.Height * 0.42f; // header row slightly shorter
        ctx.DrawLine(c1, frame.Y, c1, frame.Bottom, paint);
        ctx.DrawLine(c2, frame.Y, c2, frame.Bottom, paint);
        ctx.DrawLine(frame.X, r1, frame.Right, r1, paint);
    }

    // ── Hit testing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="ToolbarItem"/> at screen coordinates (<paramref name="x"/>, <paramref name="y"/>),
    /// or <c>null</c> if no item covers that point. Requires <see cref="Render"/> to have been called first.
    /// </summary>
    public ToolbarItem? HitTest(float x, float y)
    {
        foreach (var (item, rect) in _itemRects)
        {
            if (x >= rect.X && x <= rect.X + rect.Width &&
                y >= rect.Y && y <= rect.Y + rect.Height)
                return item;
        }
        return null;
    }

    /// <summary>
    /// Returns <c>true</c> and toggles the overflow panel if the tap landed on the overflow "…"
    /// button.  Call before <see cref="HitTest"/> or <see cref="HandleOverlayTap"/>.
    /// </summary>
    public bool HandleOverflowButtonTap(float x, float y)
    {
        if (!HasOverflow) return false;
        if (_overflowButtonRect.Contains(x, y))
        {
            _overflowPanelOpen = !_overflowPanelOpen;
            return true;
        }
        // Tap outside the panel while it is open → close it and let the caller decide whether
        // the tap routes to HitTest or to the document.
        if (_overflowPanelOpen)
        {
            _overflowPanelOpen = false;
            // Returning false so the tap may still reach normal toolbar items.
        }
        return false;
    }

    /// <summary>Whether the overflow panel is currently open.</summary>
    public bool IsOverflowPanelOpen => _overflowPanelOpen;

    /// <summary>
    /// Renders the overflow items panel (below the "…" button) onto the canvas.
    /// Call after the document layer so it draws on top.
    /// </summary>
    public void RenderOverflowPanel(IDrawingContext ctx, float totalWidth, float totalHeight)
    {
        if (!_overflowPanelOpen || _overflowItems.Count == 0) return;

        float h    = ButtonSize;
        float pad  = ButtonPadding;
        int cols   = Math.Max(1, (int)((totalWidth - pad * 2) / (h + pad)));
        int rows   = (int)Math.Ceiling((double)_overflowItems.Count / cols);
        float panelW = totalWidth;
        float panelH = rows * h + (rows + 1) * pad;

        float panelX = 0f;
        float panelY = _overflowButtonRect.Bottom + 2f;
        if (panelY + panelH > totalHeight) panelY = _overflowButtonRect.Y - panelH - 2f;

        var panelRect = new EditorRect(panelX, panelY, panelW, panelH);

        var bgPaint     = new EditorPaint { Color = BackgroundColor };
        var borderPaint = new EditorPaint { Color = SeparatorColor };
        var normalPaint = new EditorPaint { Color = new EditorColor(255, 255, 255, 18) };
        var activePaint = new EditorPaint { Color = ActiveColor };
        var controlBorder = new EditorPaint { Color = SeparatorColor, Style = PaintStyle.Stroke, StrokeWidth = 1f };
        var textPaint   = new EditorPaint { Color = ButtonColor };
        var disabledTextPaint = new EditorPaint { Color = DisabledColor };
        var shadowPaint = new EditorPaint { Color = new EditorColor(0, 0, 0, 40) };

        ctx.FillRoundRect(panelRect.Inflate(2, 2), 6f, shadowPaint);
        ctx.FillRoundRect(panelRect, 6f, bgPaint);
        ctx.DrawRoundRect(panelRect, 6f, borderPaint);

        float x = panelX + pad;
        float y = panelY + pad;
        for (int i = 0; i < _overflowItems.Count; i++)
        {
            var item = _overflowItems[i];
            float w = item is ToolbarDropdown ? DropdownWidth : h;

            if (x + w > panelX + panelW - pad)
            {
                x = panelX + pad;
                y += h + pad;
            }

            var rect = new EditorRect(x, y, w, h);
            _itemRects[item] = rect; // make overflow items hit-testable

            if (item is ToolbarButton btn)
            {
                var fill = btn.IsActive ? activePaint : normalPaint;
                ctx.FillRoundRect(rect, 5f, fill);
                ctx.DrawRoundRect(rect, 5f, controlBorder);
                var labelPaint = item.IsEnabled ? textPaint : disabledTextPaint;
                var iconImage  = !string.IsNullOrEmpty(btn.Icon) ? IconResolver?.Invoke(btn.Icon) : null;
                bool drewVector = !string.IsNullOrEmpty(btn.Icon) && PreferVectorIcons && iconImage is null
                                  && DrawVectorIcon(ctx, btn.Icon!, rect.Inflate(-5f, -5f), labelPaint, IconPack);
                if (!drewVector)
                {
                    if (iconImage is not null)
                    {
                        float iconSize = MathF.Round(Math.Max(12f, Math.Min(rect.Width, rect.Height) * 0.72f));
                        var iconTint = item.IsEnabled ? labelPaint.Color : DisabledColor;
                        ctx.DrawTintedImage(iconImage, new EditorRect(
                            MathF.Round(rect.X + (rect.Width  - iconSize) / 2f),
                            MathF.Round(rect.Y + (rect.Height - iconSize) / 2f),
                            iconSize, iconSize), iconTint);
                    }
                    else
                        ctx.DrawTextInRect(btn.Label, rect.Inflate(-2, -2), labelPaint, TextAlignment.Center, VerticalAlignment.Center);
                }
            }
            else if (item is ToolbarDropdown dd)
            {
                ctx.FillRoundRect(rect, 5f, normalPaint);
                ctx.DrawRoundRect(rect, 5f, controlBorder);
                var display = dd.SelectedIndex >= 0 && dd.SelectedIndex < dd.Items.Count ? dd.Items[dd.SelectedIndex] : dd.Label;
                ctx.DrawTextInRect(display, rect.Inflate(-4, -2), textPaint, TextAlignment.Left, VerticalAlignment.Center);
                float cx = rect.X + rect.Width - 10f, cy = rect.Y + rect.Height / 2f;
                ctx.DrawLine(cx - 3, cy - 2, cx, cy + 2, textPaint);
                ctx.DrawLine(cx, cy + 2, cx + 3, cy - 2, textPaint);
            }
            else if (item is ToolbarColorPicker cp)
            {
                var swatchColor = cp.SelectedColor.A > 0 ? cp.SelectedColor : EditorColor.White;
                ctx.FillRoundRect(rect, 5f, new EditorPaint { Color = swatchColor });
                ctx.DrawRoundRect(rect, 5f, controlBorder);
            }

            x += w + pad;
        }
    }
}

