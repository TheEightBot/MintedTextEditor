using MintedTextEditor.Core.Accessibility;
using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Events;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Html;
using MintedTextEditor.Core.Markdown;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Localization;
using MintedTextEditor.Core.Rendering;
using MintedTextEditor.Core.Theming;
using MintedTextEditor.Core.Toolbar;
using MintedTextEditor.Maui.Input;
using MintedTextEditor.Maui.Providers;
using MintedTextEditor.SkiaSharp;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Svg.Skia;
using EditorDoc = MintedTextEditor.Core.Document.Document;
using FormattingImageProvider = MintedTextEditor.Core.Formatting.IImageProvider;

namespace MintedTextEditor.Maui;

/// <summary>
/// A high-performance, SkiaSharp-rendered rich text editor for .NET MAUI.
/// Add this control to any XAML layout or compose it from C#.
/// </summary>
public class MintedEditorView : ContentView
{
    // ── Rendering pipeline ───────────────────────────────────────────────────────

    private readonly SKCanvasView _canvas;
    private readonly KeyboardProxy _keyboardProxy;
    private readonly EditorDoc _document;
    private readonly TextLayoutEngine _layoutEngine = new();
    private readonly LayoutCache _layoutCache;
    private readonly DocumentRenderer _renderer = new();
    private readonly Caret _caret = new();
    private readonly CaretRenderer _caretRenderer = new();
    private readonly SelectionRenderer _selectionRenderer;
    private readonly FormattingEngine _formattingEngine = new();
    private readonly FontFormattingEngine _fontFormattingEngine = new();
    private readonly UndoManager _undoManager = new();
    private readonly EditorInputController _inputController;
    private readonly EditorFindReplace _findReplace;

    // Persistent 1×1 off-screen surface used for text measurement calls (hit-testing)
    // that happen outside of a PaintSurface callback.
    private SKSurface? _measureSurface;
    private SkiaDrawingContext? _measureContext;

    private DocumentLayout _lastLayout = new();
    private IDispatcherTimer? _blinkTimer;
    private bool _isEditorFocused;
    private IDispatcherTimer? _inertiaTimer;
    private float _scrollOffset;
    private readonly InertialScroller _inertialScroller = new();
    private bool _isHyperlinkSelected;
    private HyperlinkInline? _hoveredHyperlink; // link under the mouse pointer (desktop hover)
    private (float X, float Y)? _pointerPosition; // last mouse position over the canvas, for re-checking hover
    private string? _selectedHyperlinkUrl;
    private readonly FormattingStateTracker _formattingStateTracker;
    private readonly DocumentImageTracker _imageTracker = new();
    private bool _documentStateUpdatePending;
    private ImageInline? _selectedImage;
    private EditorRect _selectedImageRect;
    private bool _isResizingImage;
    private int _activeImageHandle = -1;
    private float _imageResizeStartWidth;
    private float _imageResizeStartHeight;
    private float _imageResizeAnchorX;
    private float _imageResizeAnchorY;

    private bool _isResizingTableColumn;
    private TableBlock? _resizingTable;
    private int _resizingTableBlockIndex = -1;
    private int _resizingColumnIndex = -1;
    private float _tableResizeStartX;
    private float _tableLeftStartWidth;
    private float _tableRightStartWidth;

    private bool _isResizingTableRow;

    // Long-press caret drag with the text magnifier (loupe).
    private readonly LongPressDetector _longPress = new();
    private readonly System.Diagnostics.Stopwatch _gestureClock = System.Diagnostics.Stopwatch.StartNew();
    private IDispatcherTimer? _longPressTimer;
    private bool _isCaretDragging;
    private long _gestureTouchId = -1; // the finger that owns the long press / caret drag
    // Fingers that went down during a caret drag: ignored until they lift, even if the drag
    // ends first, so they can never move the caret, select or tap a link.
    private readonly HashSet<long> _ignoredTouchIds = new();
    private readonly Magnifier _magnifier = new();
    private readonly MagnifierRenderer _magnifierRenderer = new();
    private int _resizingRowIndex = -1;
    private float _tableResizeStartY;
    private float _tableTopStartHeight;
    private float _tableBottomStartHeight;

    private InputModifiers _lastKeyboardModifiers;

    // ── Toolbar ──────────────────────────────────────────────────────────────────
    private ToolbarRenderer _toolbarRenderer = null!;  // initialised in constructor
    private readonly Dictionary<string, SKBitmap> _iconCache = new();
    // Cache for user-inserted document images, keyed by source URI/path.
    private readonly Dictionary<string, SKBitmap> _imageCache = new();
    private const int DefaultSvgRasterSize = 512;
    private int _iconLoadVersion;
    // Weak, so an editor that is discarded without being unloaded can still be collected.
    private IDisposable? _stringsSubscription;

    // ── Accessibility ────────────────────────────────────────────────────────────
    // Last SemanticProperties values this view applied to itself. A different current value
    // means the app set its own, which is then left alone.
    private string? _appliedSemanticDescription;
    private string? _appliedSemanticHint;

    // Whether the keyboard-capture view is the editor's accessible element (Windows; see the
    // constructor).
    private static bool ProxyCarriesSemantics => DeviceInfo.Platform == DevicePlatform.WinUI;



    // ── Bindable Properties ──────────────────────────────────────────────────────

    /// <summary>Bindable property for <see cref="Theme"/>.</summary>
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(
            nameof(Theme),
            typeof(EditorStyle),
            typeof(MintedEditorView),
            EditorTheme.CreateLight(),
            propertyChanged: static (b, _, _) =>
            {
                var v = (MintedEditorView)b;
                // If a concrete theme is assigned, respect it immediately.
                // System-theme following remains opt-in via UseSystemTheme=true.
                if (v.UseSystemTheme)
                    v.UseSystemTheme = false;
                v.InvalidateCanvas();
            });

    /// <summary>Bindable property for <see cref="ThemeMode"/>.</summary>
    public static readonly BindableProperty ThemeModeProperty =
        BindableProperty.Create(
            nameof(ThemeMode),
            typeof(EditorThemeMode),
            typeof(MintedEditorView),
            EditorThemeMode.Light,
            propertyChanged: static (b, _, nv) =>
            {
                var v = (MintedEditorView)b;
                v.Theme = EditorTheme.Create((EditorThemeMode)nv!);
                v.InvalidateCanvas();
            });

    /// <summary>Bindable property for <see cref="UseSystemTheme"/>.</summary>
    public static readonly BindableProperty UseSystemThemeProperty =
        BindableProperty.Create(
            nameof(UseSystemTheme),
            typeof(bool),
            typeof(MintedEditorView),
            false,
            propertyChanged: static (b, _, _) => ((MintedEditorView)b).InvalidateCanvas());

    /// <summary>Bindable property for <see cref="IsReadOnly"/>.</summary>
    public static readonly BindableProperty IsReadOnlyProperty =
        BindableProperty.Create(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(MintedEditorView),
            false,
            propertyChanged: static (b, _, nv) =>
            {
                var v = (MintedEditorView)b;
                v._caret.BlinkEnabled = !(bool)nv;
                // Touch is ignored while read-only, so a release could never end the gesture.
                if ((bool)nv)
                {
                    v.CancelCaretGesture();
                    // Pointer moves are ignored while read-only, so hover could never clear.
                    v.ClearPointerHover();
                }
                v.UpdateSemantics(announce: true);
                v.InvalidateCanvas();
                v.IsReadOnlyChanged?.Invoke(v, new IsReadOnlyChangedEventArgs((bool)nv));
            });

    /// <summary>Bindable property for <see cref="PlaceholderText"/>.</summary>
    public static readonly BindableProperty PlaceholderTextProperty =
        BindableProperty.Create(
            nameof(PlaceholderText),
            typeof(string),
            typeof(MintedEditorView),
            "Start typing…",
            propertyChanged: static (b, _, _) =>
            {
                var v = (MintedEditorView)b;
                v.UpdateSemantics();
                v.InvalidateCanvas();
            });

    /// <summary>
    /// Visual theme applied to the editor. Defaults to the built-in light theme.
    /// Assigning a theme sets <see cref="UseSystemTheme"/> to false.
    /// </summary>
    public EditorStyle Theme
    {
        get => (EditorStyle)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>
    /// Built-in theme mode. Setting it replaces <see cref="Theme"/> with the matching built-in
    /// theme (which also sets <see cref="UseSystemTheme"/> to false). Defaults to <see cref="EditorThemeMode.Light"/>.
    /// </summary>
    public EditorThemeMode ThemeMode
    {
        get => (EditorThemeMode)GetValue(ThemeModeProperty);
        set => SetValue(ThemeModeProperty, value);
    }

    /// <summary>
    /// When true, the editor ignores <see cref="Theme"/> and uses the built-in light or dark theme
    /// matching the application's requested theme. Defaults to false.
    /// </summary>
    public bool UseSystemTheme
    {
        get => (bool)GetValue(UseSystemThemeProperty);
        set => SetValue(UseSystemThemeProperty, value);
    }

    /// <summary>Whether the editor is read-only: keyboard and touch input are ignored and the caret is not drawn. Defaults to false.</summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Text shown when the document is a single empty paragraph. Defaults to "Start typing…".</summary>
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Bindable property for <see cref="IsWordWrap"/>.</summary>
    public static readonly BindableProperty IsWordWrapProperty =
        BindableProperty.Create(
            nameof(IsWordWrap),
            typeof(bool),
            typeof(MintedEditorView),
            true,
            propertyChanged: static (b, _, nv) =>
            {
                var v = (MintedEditorView)b;
                v._layoutEngine.WordWrap = (bool)nv;
                v._layoutCache.InvalidateAll();
                v.InvalidateCanvas();
            });

    /// <summary>Bindable property for <see cref="ShowLineNumbers"/>.</summary>
    public static readonly BindableProperty ShowLineNumbersProperty =
        BindableProperty.Create(
            nameof(ShowLineNumbers),
            typeof(bool),
            typeof(MintedEditorView),
            false,
            propertyChanged: static (b, _, nv) =>
            {
                var v = (MintedEditorView)b;
                v._renderer.ShowLineNumbers = (bool)nv;
                v.InvalidateCanvas();
            });

    /// <summary>Bindable property for <see cref="ShowScrollbar"/>.</summary>
    public static readonly BindableProperty ShowScrollbarProperty =
        BindableProperty.Create(
            nameof(ShowScrollbar),
            typeof(bool),
            typeof(MintedEditorView),
            true,
            propertyChanged: static (b, _, nv) =>
            {
                var v = (MintedEditorView)b;
                v._renderer.ShowScrollbar = (bool)nv;
                v.InvalidateCanvas();
            });

    /// <summary>Enables or disables word-wrapping (maps to <see cref="TextLayoutEngine.WordWrap"/>). Defaults to true.</summary>
    public bool IsWordWrap
    {
        get => (bool)GetValue(IsWordWrapProperty);
        set => SetValue(IsWordWrapProperty, value);
    }

    /// <summary>Whether line numbers are rendered in the left gutter. Defaults to false.</summary>
    public bool ShowLineNumbers
    {
        get => (bool)GetValue(ShowLineNumbersProperty);
        set => SetValue(ShowLineNumbersProperty, value);
    }

    /// <summary>Whether an overlay scrollbar is rendered when content overflows. Defaults to true.</summary>
    public bool ShowScrollbar
    {
        get => (bool)GetValue(ShowScrollbarProperty);
        set => SetValue(ShowScrollbarProperty, value);
    }

    /// <summary>Bindable property for <see cref="ShowToolbar"/>.</summary>
    public static readonly BindableProperty ShowToolbarProperty =
        BindableProperty.Create(
            nameof(ShowToolbar),
            typeof(bool),
            typeof(MintedEditorView),
            true,
            propertyChanged: static (b, _, _) => ((MintedEditorView)b).InvalidateCanvas());

    /// <summary>Shows or hides the built-in toolbar rendered at the top of the canvas. Defaults to true.</summary>
    public bool ShowToolbar
    {
        get => (bool)GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    /// <summary>Bindable property for <see cref="ShowFocusRing"/>.</summary>
    public static readonly BindableProperty ShowFocusRingProperty =
        BindableProperty.Create(
            nameof(ShowFocusRing),
            typeof(bool),
            typeof(MintedEditorView),
            false,
            propertyChanged: static (b, _, _) => ((MintedEditorView)b).InvalidateCanvas());

    /// <summary>
    /// Whether a thin ring in the theme's <see cref="EditorStyle.FocusRingColor"/> is drawn around
    /// the editor while it has keyboard focus. Defaults to false (opt in), so existing layouts gain no new visual. A <c>Transparent</c>
    /// <see cref="EditorStyle.FocusRingColor"/> also hides it.
    /// </summary>
    public bool ShowFocusRing
    {
        get => (bool)GetValue(ShowFocusRingProperty);
        set => SetValue(ShowFocusRingProperty, value);
    }

    /// <summary>Bindable property for <see cref="IsMagnifierEnabled"/>.</summary>
    public static readonly BindableProperty IsMagnifierEnabledProperty =
        BindableProperty.Create(
            nameof(IsMagnifierEnabled),
            typeof(bool),
            typeof(MintedEditorView),
            true,
            propertyChanged: static (b, _, nv) =>
            {
                var v = (MintedEditorView)b;
                if (!(bool)nv && v._magnifier.IsVisible)
                {
                    v._magnifier.Hide();
                    v.InvalidateCanvas();
                }
            });

    /// <summary>
    /// Shows a magnifier (loupe) above the finger while the caret is dragged after a long
    /// press. Touch input only. Default: <c>true</c>.
    /// </summary>
    public bool IsMagnifierEnabled
    {
        get => (bool)GetValue(IsMagnifierEnabledProperty);
        set => SetValue(IsMagnifierEnabledProperty, value);
    }

    /// <summary>Bindable property for <see cref="MagnifierOptions"/>.</summary>
    public static readonly BindableProperty MagnifierOptionsProperty =
        BindableProperty.Create(
            nameof(MagnifierOptions),
            typeof(MagnifierOptions),
            typeof(MintedEditorView),
            defaultValueCreator: static _ => DeviceInfo.Platform == DevicePlatform.Android
                ? MagnifierOptions.Android
                : MagnifierOptions.Apple);

    /// <summary>
    /// Appearance of the magnifier. Defaults to an approximation of the platform's native
    /// magnifier (<see cref="MagnifierOptions.Android"/> on Android,
    /// <see cref="MagnifierOptions.Apple"/> elsewhere).
    /// </summary>
    public MagnifierOptions MagnifierOptions
    {
        get => (MagnifierOptions)GetValue(MagnifierOptionsProperty);
        set => SetValue(MagnifierOptionsProperty, value);
    }

    /// <summary>Bindable property for <see cref="ToolbarMaxRows"/>.</summary>
    public static readonly BindableProperty ToolbarMaxRowsProperty =
        BindableProperty.Create(
            nameof(ToolbarMaxRows),
            typeof(int),
            typeof(MintedEditorView),
            0,
            propertyChanged: static (b, _, nv) =>
            {
                var v = (MintedEditorView)b;
                v.ApplyToolbarMaxRows();
                v.InvalidateCanvas();
            },
            // Setting the current value again, or clearing a value equal to the default, raises
            // no property change, yet it switches between the explicit limit and the definition's
            // MaxRows. MAUI calls coerceValue on every SetValue and ClearValue (before IsSet is
            // updated), so request a repaint here; painting re-applies the override.
            coerceValue: static (b, value) =>
            {
                ((MintedEditorView)b).InvalidateCanvas();
                return value;
            });

    /// <summary>
    /// Maximum number of toolbar rows to display before remaining items are collapsed
    /// into an overflow (…) button. 0 (the default) means unlimited.
    /// </summary>
    /// <remarks>
    /// Once set (to any value, including 0), this property takes precedence over
    /// <see cref="Core.Toolbar.ToolbarDefinition.MaxRows"/> and stays in effect when
    /// <see cref="ToolbarDefinition"/>, <see cref="ToolbarItems"/> or <see cref="ToolbarIconPack"/>
    /// change. While it has never been set (or after <c>ClearValue(ToolbarMaxRowsProperty)</c>),
    /// the current definition's <see cref="Core.Toolbar.ToolbarDefinition.MaxRows"/> is used.
    /// The definition object itself is never modified.
    /// </remarks>
    public int ToolbarMaxRows
    {
        get => (int)GetValue(ToolbarMaxRowsProperty);
        set => SetValue(ToolbarMaxRowsProperty, value);
    }

    /// <summary>Bindable property for <see cref="ToolbarIconPack"/>.</summary>
    public static readonly BindableProperty ToolbarIconPackProperty =
        BindableProperty.Create(
            nameof(ToolbarIconPack),
            typeof(ToolbarIconPack),
            typeof(MintedEditorView),
            ToolbarIconPack.Lucide,
            propertyChanged: static (b, _, nv) =>
            {
                var v = (MintedEditorView)b;
                v._toolbarRenderer.IconPack = (ToolbarIconPack)nv!;
                if (v.Handler is not null)
                    _ = v.LoadToolbarIconsAsync();
                v.InvalidateCanvas();
            });

    /// <summary>
    /// Style of the built-in toolbar icons. Changing it reloads the icons.
    /// Defaults to <see cref="Core.Toolbar.ToolbarIconPack.Lucide"/>.
    /// </summary>
    public ToolbarIconPack ToolbarIconPack
    {
        get => (ToolbarIconPack)GetValue(ToolbarIconPackProperty);
        set => SetValue(ToolbarIconPackProperty, value);
    }

    /// <summary>Bindable property for <see cref="ToolbarDefinition"/>.</summary>
    public static readonly BindableProperty ToolbarDefinitionProperty =
        BindableProperty.Create(
            nameof(ToolbarDefinition),
            typeof(ToolbarDefinition),
            typeof(MintedEditorView),
            null,
            propertyChanged: static (b, _, n) =>
                ((MintedEditorView)b).OnToolbarDefinitionChanged((ToolbarDefinition?)n));

    /// <summary>
    /// Optional custom <see cref="Core.Toolbar.ToolbarDefinition"/>.
    /// When <see langword="null"/> the editor uses <see cref="Core.Toolbar.ToolbarDefinition.CreateDefault()"/>.
    /// Ignored while <see cref="ToolbarItems"/> is non-empty. The definition's
    /// <see cref="Core.Toolbar.ToolbarDefinition.MaxRows"/> applies only while <see cref="ToolbarMaxRows"/> is unset.
    /// </summary>
    public ToolbarDefinition? ToolbarDefinition
    {
        get => (ToolbarDefinition?)GetValue(ToolbarDefinitionProperty);
        set => SetValue(ToolbarDefinitionProperty, value);
    }

    /// <summary>Bindable property for <see cref="ToolbarItems"/>.</summary>
    public static readonly BindableProperty ToolbarItemsProperty =
        BindableProperty.Create(
            nameof(ToolbarItems),
            typeof(IList<Core.Toolbar.ToolbarItem>),
            typeof(MintedEditorView),
            null,
            propertyChanged: static (b, _, n) =>
                ((MintedEditorView)b).OnToolbarItemsChanged((IList<Core.Toolbar.ToolbarItem>?)n));

    /// <summary>
    /// Optional flat toolbar item collection, shown as a single group. When non-empty, it takes
    /// precedence over <see cref="ToolbarDefinition"/>.
    /// </summary>
    public IList<Core.Toolbar.ToolbarItem>? ToolbarItems
    {
        get => (IList<Core.Toolbar.ToolbarItem>?)GetValue(ToolbarItemsProperty);
        set => SetValue(ToolbarItemsProperty, value);
    }

    private void OnToolbarDefinitionChanged(ToolbarDefinition? def)
    {
        if (ToolbarItems is { Count: > 0 })
        {
            // A custom flat item collection is explicitly configured.
            return;
        }

        ApplyToolbarDefinition(def ?? ToolbarDefinition.CreateDefault());
    }

    private void OnToolbarItemsChanged(IList<Core.Toolbar.ToolbarItem>? items)
    {
        if (items is { Count: > 0 })
        {
            ApplyToolbarDefinition(ToolbarDefinition.CreateFromItems(items));
            return;
        }

        OnToolbarDefinitionChanged(ToolbarDefinition);
    }

    private void ApplyToolbarDefinition(ToolbarDefinition definition)
    {
        CreateToolbarRenderer(definition);
        InvalidateCanvas();
    }

    // Every toolbar rebuild goes through here so view-level settings (icon pack,
    // icon resolver, row limit) carry over to the new renderer.
    private void CreateToolbarRenderer(ToolbarDefinition definition)
    {
        _toolbarRenderer = new ToolbarRenderer(definition);
        _toolbarRenderer.IconPack = ToolbarIconPack;
        _toolbarRenderer.IconResolver = key =>
            _iconCache.TryGetValue(key, out var bmp) ? bmp : null;
        ApplyToolbarMaxRows();
        WireToolbarCallbacks(definition);
    }

    // Font families offered by the font-family dropdown after the localized "Default" entry.
    private static readonly string[] s_fontFamilies =
    [
        "Arial", "Times New Roman", "Courier New",
        "Georgia", "Verdana", "Trebuchet MS", "Comic Sans MS",
    ];

    /// <summary>
    /// Fills the options of the built-in dropdowns (font family, heading level, edit, object and
    /// table menus) from <see cref="EditorStrings.Current"/>, keeping each selection. Called when the
    /// toolbar is wired and again whenever <see cref="EditorStrings.Current"/> changes.
    /// </summary>
    private static void ApplyLocalizedDropdownItems(ToolbarDefinition def)
    {
        var s = EditorStrings.Current;
        foreach (var dd in def.AllItems.OfType<ToolbarDropdown>())
        {
            string[]? items = dd.Icon switch
            {
                "font-family"    => [s.DefaultFontFamily, .. s_fontFamilies],
                "heading"        => [s.NormalText, s.Heading1, s.Heading2, s.Heading3, s.Heading4, s.Heading5, s.Heading6],
                "edit-actions"   => [s.Copy, s.Cut, s.Paste, s.SelectAll],
                "object-actions" => [s.OpenLink, s.RemoveLink, s.RemoveImage],
                "table-actions"  =>
                [
                    s.InsertTableEllipsis, s.InsertRowAbove, s.InsertRowBelow, s.DeleteRow,
                    s.InsertColumnLeft, s.InsertColumnRight, s.DeleteColumn, s.DeleteTable,
                ],
                _ => null,
            };
            if (items is null) continue;

            dd.Items.Clear();
            foreach (var text in items) dd.Items.Add(text);
        }
    }

    private void OnEditorStringsChanged()
    {
        // EditorStrings.Current may be replaced from any thread.
        Dispatcher.Dispatch(RefreshLocalizedStrings);
    }

    private void RefreshLocalizedStrings()
    {
        ApplyLocalizedDropdownItems(_toolbarRenderer.Definition);
        InvalidateCanvas();
    }

    /// <summary>
    /// Starts listening for <see cref="EditorStrings.CurrentChanged"/> (if not already) and applies
    /// strings that were replaced while the editor was not listening.
    /// </summary>
    private void SubscribeToEditorStrings()
    {
        if (_stringsSubscription is not null) return;
        _stringsSubscription = EditorStrings.SubscribeCurrentChangedWeak(
            this, static view => view.OnEditorStringsChanged());
        RefreshLocalizedStrings();
    }

    private void UnsubscribeFromEditorStrings()
    {
        _stringsSubscription?.Dispose();
        _stringsSubscription = null;
    }

    private void OnViewLoaded(object? sender, EventArgs e) => SubscribeToEditorStrings();

    private void OnViewUnloaded(object? sender, EventArgs e) => UnsubscribeFromEditorStrings();

    // ToolbarMaxRows wins once the app has set it; otherwise the definition's MaxRows applies.
    private void ApplyToolbarMaxRows() =>
        _toolbarRenderer.MaxRowsOverride = IsSet(ToolbarMaxRowsProperty) ? ToolbarMaxRows : null;

    private void WireToolbarCallbacks(ToolbarDefinition def)
    {
        string[] fontSizes =
        [
            "8", "9", "10", "11", "12", "14", "16", "18",
            "20", "24", "28", "32", "36", "48", "72",
        ];

        ApplyLocalizedDropdownItems(def);

        foreach (var item in def.AllItems)
        {
            if (item is ToolbarDropdown dd)
            {
                switch (dd.Icon)
                {
                    case "font-family":
                        dd.SelectedIndex = 0;
                        dd.OnSelectionChanged = idx =>
                        {
                            // Index 0 is the localized "Default" entry, which clears the font family.
                            if ((uint)idx < (uint)dd.Items.Count)
                            {
                                string family = idx == 0 ? "" : dd.Items[idx];
                                ApplyToolbarCharacterFormatting("Font",
                                    r => _fontFormattingEngine.ApplyFontFamily(_document, r, family));
                            }
                            else
                            {
                                InvalidateCanvas();
                            }
                        };
                        break;

                    case "font-size":
                        dd.Items.Clear();
                        foreach (var s in fontSizes) dd.Items.Add(s);
                        dd.SelectedIndex = 4; // 12pt default
                        dd.OnSelectionChanged = idx =>
                        {
                            if ((uint)idx < (uint)dd.Items.Count && float.TryParse(dd.Items[idx], out float sz))
                            {
                                ApplyToolbarCharacterFormatting("Font Size",
                                    r => _fontFormattingEngine.ApplyFontSize(_document, r, sz));
                            }
                            else
                                InvalidateCanvas();
                        };
                        break;

                    case "heading":
                        dd.SelectedIndex = 0;
                        dd.OnSelectionChanged = idx =>
                        {
                            if ((uint)idx < (uint)dd.Items.Count)
                                new SetHeadingLevelCommand(idx).Execute(GetEditorContext());
                            UpdateFormattingState();
                            InvalidateCanvas();
                        };
                        break;

                    case "edit-actions":
                        dd.SelectedIndex = -1;
                        dd.OnSelectionChanged = async idx =>
                        {
                            switch (idx)
                            {
                                case 0:
                                    await _inputController.HandleCopyAsync(_document);
                                    break;
                                case 1:
                                    await _inputController.HandleCutAsync(_document);
                                    break;
                                case 2:
                                    await _inputController.HandlePasteAsync(_document);
                                    break;
                                case 3:
                                    ExecuteToolbarCommand(new SelectAllCommand());
                                    break;
                            }
                            dd.SelectedIndex = -1;
                            InvalidateCanvas();
                        };
                        break;

                    case "object-actions":
                        dd.SelectedIndex = -1;
                        dd.OnSelectionChanged = idx =>
                        {
                            switch (idx)
                            {
                                case 0:
                                {
                                    var cmd = new OpenHyperlinkCommand
                                    {
                                        OnOpen = url => HyperlinkHandler.OpenUrl(url),
                                    };
                                    ExecuteToolbarCommand(cmd);
                                    break;
                                }
                                case 1:
                                    ExecuteToolbarCommand(new RemoveHyperlinkCommand());
                                    break;
                                case 2:
                                    if (_selectedImage is not null)
                                        ExecuteToolbarCommand(new RemoveImageCommand { Image = _selectedImage });
                                    break;
                            }
                            dd.SelectedIndex = -1;
                            InvalidateCanvas();
                        };
                        break;

                    case "table-actions":
                        dd.SelectedIndex = -1;
                        dd.OnSelectionChanged = async idx =>
                        {
                            switch (idx)
                            {
                                case 0:
                                    await HandleInsertTableAsync();
                                    break;
                                case 1:
                                    ExecuteToolbarCommand(new InsertRowAboveCommand());
                                    break;
                                case 2:
                                    ExecuteToolbarCommand(new InsertRowBelowCommand());
                                    break;
                                case 3:
                                    ExecuteToolbarCommand(new DeleteRowCommand());
                                    break;
                                case 4:
                                    ExecuteToolbarCommand(new InsertColumnLeftCommand());
                                    break;
                                case 5:
                                    ExecuteToolbarCommand(new InsertColumnRightCommand());
                                    break;
                                case 6:
                                    ExecuteToolbarCommand(new DeleteColumnCommand());
                                    break;
                                case 7:
                                    ExecuteToolbarCommand(new DeleteTableCommand());
                                    break;
                            }
                            dd.SelectedIndex = -1;
                            InvalidateCanvas();
                        };
                        break;
                }
            }
            else if (item is ToolbarColorPicker cp)
            {
                switch (cp.Icon)
                {
                    case "text-color":
                        cp.OnColorSelected = color =>
                        {
                            ApplyToolbarCharacterFormatting("Text Color",
                                r => _fontFormattingEngine.ApplyTextColor(_document, r, color));
                        };
                        break;

                    case "highlight-color":
                        cp.OnColorSelected = color =>
                        {
                            ApplyToolbarCharacterFormatting("Highlight Color",
                                r => _fontFormattingEngine.ApplyHighlightColor(_document, r, color));
                        };
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Applies a toolbar dropdown or colour-picker format to the selection as one undo step
    /// (or to the pending style when the selection is empty), keeping the selection on the same text,
    /// then reads the formatting state back from the formatted text.
    /// </summary>
    private void ApplyToolbarCharacterFormatting(string description, Action<TextRange> apply)
    {
        GetEditorContext().ApplyCharacterFormatting(description, apply);
        _caret.MoveTo(_inputController.Selection.Active);
        RaiseSelectionChanged();
        NotifyImeSelectionChanged();
        UpdateFormattingState();
        InvalidateCanvas();
    }

    private void ExecuteToolbarCommand(IEditorCommand command)
    {
        var ctx = GetEditorContext();
        if (!command.CanExecute(ctx))
            return;

        if (command is ICharacterFormattingCommand)
        {
            // Character-formatting commands toggle inline styles and may split or merge
            // TextRun objects.  Use the robust capture/normalize/rebuild path so the
            // selection endpoints never drift across repeated toggle cycles.
            var (newAnchor, newActive) = DocumentEditor.ApplyCharacterFormatting(
                _document,
                _inputController.Selection.Anchor,
                _inputController.Selection.Active,
                range =>
                {
                    // Temporarily set the selection to the range before calling Execute so
                    // the command sees the correct (pre-operation) range via ctx.Selection.Range.
                    _inputController.Selection.Set(range.Start, range.End);
                    command.Execute(ctx);
                });
            _inputController.Selection.Set(newAnchor, newActive);
        }
        else
        {
            // Non-formatting commands (SelectAll, InsertTable, etc.): execute normally and
            // use NormalizePosition for structural safety only — these commands may
            // intentionally relocate the selection, so do not override their result.
            command.Execute(ctx);
            var anchor = DocumentEditor.NormalizePosition(_document, _inputController.Selection.Anchor);
            var active = DocumentEditor.NormalizePosition(_document, _inputController.Selection.Active);
            _inputController.Selection.Set(anchor, active);
        }

        _caret.MoveTo(_inputController.Selection.Active);
        RaiseSelectionChanged();
        NotifyImeSelectionChanged();
        InvalidateCanvas();
        _keyboardProxy.Focus();
    }

    /// <summary>
    /// Signals the platform IME that the editor's selection/content has changed so it
    /// stops operating on a stale model.  On Android this triggers UpdateSelection /
    /// RestartInput; no-op on other platforms (no subscriber).
    /// </summary>
    private void NotifyImeSelectionChanged() =>
        _keyboardProxy.NotifySelectionChanged();

    private void HandleToolbarItemTap(Core.Toolbar.ToolbarItem item)
    {
        if (item is ToolbarButton btn && btn.IsEnabled)
        {
            if (btn.Icon == "undo")           Undo();
            else if (btn.Icon == "redo")      Redo();
            else if (btn.Icon == "hyperlink") _ = HandleInsertHyperlinkAsync();
            else if (btn.Icon == "image")     _ = HandleInsertImageAsync();
            else if (btn.Icon == "table")     _ = HandleInsertTableAsync();
            else if (btn.Command is not null) ExecuteToolbarCommand(btn.Command);
        }
        else if (item is ToolbarDropdown or ToolbarColorPicker)
        {
            _toolbarRenderer.ToggleOverlay(item);
        }
    }

    // ── Public Accessors ─────────────────────────────────────────────────────────

    /// <summary>The underlying document model.</summary>
    public EditorDoc Document => _document;

    /// <summary>Undo / redo stack.</summary>
    public UndoManager UndoManager => _undoManager;

    /// <summary>Character formatting (bold, italic, underline …).</summary>
    public FormattingEngine Formatting => _formattingEngine;

    /// <summary>Font family, size and text-color formatting.</summary>
    public FontFormattingEngine FontFormatting => _fontFormattingEngine;

    /// <summary>Current caret state.</summary>
    public Caret Caret => _caret;

    /// <summary>Current selection state.</summary>
    public Selection Selection => _inputController.Selection;

    /// <summary>
    /// Formatting at the caret or selection, as last reported by the formatting "changed"
    /// events (<see cref="FontFamilyChanged"/>, <see cref="FontAttributesChanged"/> and the others).
    /// </summary>
    public FormattingState CurrentFormatting => _formattingStateTracker.Current;

    // ── Events ───────────────────────────────────────────────────────────────────

    /// <summary>Raised whenever the document content changes.</summary>
    public event EventHandler<EditorTextChangedEventArgs>? TextChanged;

    /// <summary>Raised when the caret or selection moves.</summary>
    public event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised when the user activates a hyperlink.</summary>
    public event EventHandler<HyperlinkClickedEventArgs>? HyperlinkClicked;

    /// <summary>Raised when caret/selection enters or leaves a hyperlink.</summary>
    public event EventHandler<IsHyperlinkSelectedChangedEventArgs>? IsHyperlinkSelectedChanged;

    /// <summary>Raised when the editor gains or loses input focus.</summary>
    public event EventHandler<FocusChangedEventArgs>? EditorFocusChanged;

    /// <summary>Raised when image insertion is requested so hosts can provide a source.</summary>
    public event EventHandler<ImageRequestedEventArgs>? ImageRequested;

    /// <summary>Raised after <see cref="LoadHtml"/> or <see cref="LoadMarkdown"/> replaces the document content.</summary>
    public event EventHandler<ContentLoadedEventArgs>? ContentLoaded;

    /// <summary>Raised when <see cref="IsReadOnly"/> changes.</summary>
    public event EventHandler<IsReadOnlyChangedEventArgs>? IsReadOnlyChanged;

    /// <summary>Raised when the font family at the caret or selection changes.</summary>
    public event EventHandler<FontFamilyChangedEventArgs>? FontFamilyChanged;

    /// <summary>Raised when the font size at the caret or selection changes.</summary>
    public event EventHandler<FontSizeChangedEventArgs>? FontSizeChanged;

    /// <summary>Raised when bold, italic, subscript or superscript at the caret or selection changes.</summary>
    public event EventHandler<FontAttributesChangedEventArgs>? FontAttributesChanged;

    /// <summary>Raised when underline or strikethrough at the caret or selection changes.</summary>
    public event EventHandler<TextDecorationsChangedEventArgs>? TextDecorationsChanged;

    /// <summary>Raised when the paragraph format (heading level or block quote) at the caret or selection changes.</summary>
    public event EventHandler<TextFormattingChangedEventArgs>? TextFormattingChanged;

    /// <summary>Raised when the paragraph alignment at the caret or selection changes.</summary>
    public event EventHandler<HorizontalTextAlignmentChangedEventArgs>? HorizontalTextAlignmentChanged;

    /// <summary>Raised when the list type of the paragraph at the caret or selection changes.</summary>
    public event EventHandler<ListTypeChangedEventArgs>? ListTypeChanged;

    /// <summary>Raised when the text colour at the caret or selection changes.</summary>
    public event EventHandler<TextColorChangedEventArgs>? TextColorChanged;

    /// <summary>Raised when the highlight colour at the caret or selection changes.</summary>
    public event EventHandler<HighlightTextColorChangedEventArgs>? HighlightTextColorChanged;

    /// <summary>Raised once per image after an edit, undo or redo adds images to the document.</summary>
    public event EventHandler<ImageInsertedEventArgs>? ImageInserted;

    /// <summary>Raised once per image after an edit, undo or redo removes images from the document.</summary>
    public event EventHandler<ImageRemovedEventArgs>? ImageRemoved;

    /// <summary>
    /// Raised after <see cref="FindNext"/>, <see cref="FindPrevious"/>, <see cref="Replace"/> or
    /// <see cref="ReplaceAll"/> completes, with the match count and the 1-based index of the
    /// selected match (0 when none) for the query after the operation.
    /// </summary>
    public event EventHandler<FindReplaceResultEventArgs>? FindReplaceCompleted;

    /// <summary>Platform handler used to open hyperlinks when activation is not cancelled.</summary>
    public IHyperlinkHandler HyperlinkHandler { get; set; } = new MauiHyperlinkHandler();

    /// <summary>Platform image loader used by the renderer for document images.</summary>
    public FormattingImageProvider ImageProvider { get; set; } = new MauiImageProvider();

    // ── Constructor ──────────────────────────────────────────────────────────────

    /// <summary>Creates an editor with an empty document and the default toolbar.</summary>
    public MintedEditorView()
    {
        _document = new EditorDoc();
        _layoutCache = new LayoutCache(_layoutEngine);
        _selectionRenderer = new SelectionRenderer(_caretRenderer);
        _inputController = new EditorInputController(_caret, _caretRenderer)
        {
            UndoManager = _undoManager,
            FormattingEngine = _formattingEngine,
            FontFormattingEngine = _fontFormattingEngine,
        };

        _findReplace = new EditorFindReplace(_document, _inputController.Selection, _undoManager);

        _document.Changed += OnDocumentChanged;

        _canvas = new SKCanvasView { EnableTouchEvents = true };
        _canvas.PaintSurface += OnPaintSurface;
        _canvas.Touch += OnCanvasTouch;

        _keyboardProxy = new KeyboardProxy
        {
            WidthRequest = 1,
            HeightRequest = 1,
            Opacity = 0,
            InputTransparent = false,
            ZIndex = -1,
        };
        _keyboardProxy.TextInput += OnKeyboardTextInput;
        _keyboardProxy.KeyDown += OnKeyboardKeyDown;
        _keyboardProxy.NativeFocusChanged += OnNativeFocusChanged;
        _keyboardProxy.ImeSink = new EditorImeSink(this);
        _keyboardProxy.ImeContextProvider = () => _inputController.GetImeTextContext(_document);
        // The editor starts unfocused: no caret and no blink timer until the
        // platform capture view actually receives keyboard focus.
        _caret.IsFocused = false;
        _inputController.ClipboardProvider = new MauiClipboardProvider();

        CreateToolbarRenderer(ToolbarDefinition.CreateDefault());

        _formattingStateTracker = new FormattingStateTracker(ComputeFormattingState());
        _imageTracker.Reset(_document);

        // The editor is announced as one element; the canvas would otherwise show up as an
        // unlabeled node. On Android and iOS that element is this view: double-tapping it makes
        // the screen reader tap its centre, which lands on the canvas and focuses the editor, so
        // the hidden keyboard-capture view is excluded too. Windows has no such fallback, so there
        // the capture view (a TextBox, which Narrator can focus and the user can Tab to) stays in
        // the tree and gets the editor's description and hint (see UpdateSemantics).
        AutomationProperties.SetIsInAccessibleTree(_canvas, false);
        if (!ProxyCarriesSemantics)
            AutomationProperties.SetIsInAccessibleTree(_keyboardProxy, false);

        var grid = new Grid();
        grid.Add(_canvas);
        grid.Add(_keyboardProxy);
        Content = grid;

        Loaded += OnViewLoaded;
        Unloaded += OnViewUnloaded;
        UpdateSemantics();
    }

    // ── HTML API ─────────────────────────────────────────────────────────────────

    /// <summary>Exports the entire document as an HTML string.</summary>
    public string GetHtml() => _document.GetHtml();

    /// <summary>Replaces document content with the parsed HTML.</summary>
    public void LoadHtml(string html)
    {
        var imported = new HtmlImporter().Import(html);
        _document.Blocks.Clear();
        foreach (var block in imported.Blocks)
            _document.AddBlock(block);
        if (_document.Blocks.Count == 0)
            _document.AddBlock(new Paragraph());

        _document.NotifyChanged(
            new DocumentChangedEventArgs(DocumentChangeType.TextInserted, TextRange.Empty));
        _inputController.Selection.CollapseTo(new DocumentPosition(0, 0, 0));
        _caret.Position = new DocumentPosition(0, 0, 0);
        InvalidateCanvas();
        OnContentLoaded();
    }

    /// <summary>Appends the parsed HTML at the end of the current document.</summary>
    public void AppendHtml(string html)
    {
        _document.AppendHtml(html);
        InvalidateCanvas();
    }

    /// <summary>Exports the entire document as a Markdown string.</summary>
    public string GetMarkdown(MarkdownExportOptions? options = null) => _document.GetMarkdown(options);

    /// <summary>Replaces document content with the parsed Markdown.</summary>
    public void LoadMarkdown(string markdown)
    {
        var imported = new MarkdownImporter().Import(markdown);
        _document.Blocks.Clear();
        foreach (var block in imported.Blocks)
            _document.AddBlock(block);
        if (_document.Blocks.Count == 0)
            _document.AddBlock(new Paragraph());

        _document.NotifyChanged(
            new DocumentChangedEventArgs(DocumentChangeType.TextInserted, TextRange.Empty));
        _inputController.Selection.CollapseTo(new DocumentPosition(0, 0, 0));
        _caret.Position = new DocumentPosition(0, 0, 0);
        InvalidateCanvas();
        OnContentLoaded();
    }

    /// <summary>Appends the parsed Markdown at the end of the current document.</summary>
    public void AppendMarkdown(string markdown)
    {
        _document.AppendMarkdown(markdown);
        InvalidateCanvas();
    }

    // ── Undo / Redo ──────────────────────────────────────────────────────────────

    /// <summary>Undoes the most recent change, if any, and moves the caret to the restored position.</summary>
    public void Undo()
    {
        if (_undoManager.CanUndo)
        {
            var pos = _undoManager.Undo();
            if (pos.HasValue) { _inputController.Selection.CollapseTo(pos.Value); _caret.MoveTo(pos.Value); }
            InvalidateCanvas();
        }
    }

    /// <summary>Redoes the most recently undone change, if any, and moves the caret to the restored position.</summary>
    public void Redo()
    {
        if (_undoManager.CanRedo)
        {
            var pos = _undoManager.Redo();
            if (pos.HasValue) { _inputController.Selection.CollapseTo(pos.Value); _caret.MoveTo(pos.Value); }
            InvalidateCanvas();
        }
    }

    // ── Find / Replace ───────────────────────────────────────────────────────────

    /// <summary>
    /// Selects and scrolls to the next occurrence of <paramref name="query"/> after the current
    /// selection, wrapping to the start of the document when <paramref name="wrapAround"/> is
    /// <c>true</c>. Returns <c>false</c> (leaving the selection unchanged) when there is no match.
    /// </summary>
    /// <param name="query">The text to find.</param>
    /// <param name="matchCase">When <c>true</c>, the search is case-sensitive.</param>
    /// <param name="wholeWord">When <c>true</c>, only whole-word occurrences match.</param>
    /// <param name="wrapAround">When <c>true</c>, continues from the start after the last match.</param>
    public bool FindNext(string query, bool matchCase = false, bool wholeWord = false, bool wrapAround = true)
    {
        var match = _findReplace.FindNext(query, CreateFindOptions(matchCase, wholeWord, wrapAround));
        if (match is not null)
            OnFindReplaceSelectionChanged();
        RaiseFindReplaceCompleted(query);
        return match is not null;
    }

    /// <summary>
    /// Selects and scrolls to the previous occurrence of <paramref name="query"/> before the
    /// current selection, wrapping to the end of the document when <paramref name="wrapAround"/>
    /// is <c>true</c>. Returns <c>false</c> (leaving the selection unchanged) when there is no match.
    /// </summary>
    /// <param name="query">The text to find.</param>
    /// <param name="matchCase">When <c>true</c>, the search is case-sensitive.</param>
    /// <param name="wholeWord">When <c>true</c>, only whole-word occurrences match.</param>
    /// <param name="wrapAround">When <c>true</c>, continues from the end before the first match.</param>
    public bool FindPrevious(string query, bool matchCase = false, bool wholeWord = false, bool wrapAround = true)
    {
        var match = _findReplace.FindPrevious(query, CreateFindOptions(matchCase, wholeWord, wrapAround));
        if (match is not null)
            OnFindReplaceSelectionChanged();
        RaiseFindReplaceCompleted(query);
        return match is not null;
    }

    /// <summary>
    /// Replaces the selected occurrence of <paramref name="query"/> (or, when the selection is not
    /// a match, the next one) with <paramref name="replacement"/> as a single undo step, then
    /// selects the following match. Returns <c>true</c> when a replacement was made; always
    /// <c>false</c> when <see cref="IsReadOnly"/> is set.
    /// </summary>
    /// <param name="query">The text to find.</param>
    /// <param name="replacement">The replacement text; empty deletes the match.</param>
    /// <param name="matchCase">When <c>true</c>, the search is case-sensitive.</param>
    /// <param name="wholeWord">When <c>true</c>, only whole-word occurrences match.</param>
    public bool Replace(string query, string replacement, bool matchCase = false, bool wholeWord = false)
    {
        if (IsReadOnly) return false;
        bool replaced = _findReplace.Replace(query, replacement, CreateFindOptions(matchCase, wholeWord, wrapAround: true));
        if (replaced)
            OnFindReplaceSelectionChanged();
        RaiseFindReplaceCompleted(query);
        return replaced;
    }

    /// <summary>
    /// Replaces every occurrence of <paramref name="query"/> with <paramref name="replacement"/>
    /// as a single undo step and returns the number of replacements; always 0 when
    /// <see cref="IsReadOnly"/> is set.
    /// </summary>
    /// <param name="query">The text to find.</param>
    /// <param name="replacement">The replacement text; empty deletes the matches.</param>
    /// <param name="matchCase">When <c>true</c>, the search is case-sensitive.</param>
    /// <param name="wholeWord">When <c>true</c>, only whole-word occurrences match.</param>
    public int ReplaceAll(string query, string replacement, bool matchCase = false, bool wholeWord = false)
    {
        if (IsReadOnly) return 0;
        int count = _findReplace.ReplaceAll(query, replacement, CreateFindOptions(matchCase, wholeWord, wrapAround: true));
        if (count > 0)
            OnFindReplaceSelectionChanged();
        RaiseFindReplaceCompleted(query);
        return count;
    }

    private static FindOptions CreateFindOptions(bool matchCase, bool wholeWord, bool wrapAround) =>
        new() { MatchCase = matchCase, WholeWord = wholeWord, WrapAround = wrapAround };

    private void OnFindReplaceSelectionChanged()
    {
        _caret.MoveTo(_inputController.Selection.Active);
        ScrollCaretIntoView();
        RaiseSelectionChanged();
        NotifyImeSelectionChanged();
        InvalidateCanvas();
    }

    private void RaiseFindReplaceCompleted(string query) =>
        FindReplaceCompleted?.Invoke(this, new FindReplaceResultEventArgs(
            query ?? string.Empty, _findReplace.MatchCount, _findReplace.CurrentMatchIndex + 1));

    // ── Insert Hyperlink / Image ──────────────────────────────────────────────────

    private async Task HandleInsertHyperlinkAsync()
    {
        var page = GetHostPage();
        if (page == null) return;

        var strings = EditorStrings.Current;
        string? url = await page.DisplayPromptAsync(
            strings.HyperlinkDialogTitle,
            strings.HyperlinkDialogMessage,
            accept: strings.HyperlinkDialogInsert,
            cancel: strings.HyperlinkDialogCancel,
            placeholder: "https://",
            keyboard: Keyboard.Url);

        if (string.IsNullOrWhiteSpace(url)) return;

        var cmd = new InsertHyperlinkCommand { Url = url };
        cmd.Execute(GetEditorContext());
        _caret.MoveTo(_inputController.Selection.Active);
        InvalidateCanvas();
        _keyboardProxy.Focus();
    }

    private async Task HandleInsertImageAsync()
    {
        string? source = null;
        var requestedArgs = new ImageRequestedEventArgs();
        ImageRequested?.Invoke(this, requestedArgs);
        if (requestedArgs.Handled && !string.IsNullOrWhiteSpace(requestedArgs.Source))
            source = requestedArgs.Source;

        if (string.IsNullOrWhiteSpace(source))
        {
            var page = GetHostPage();
            if (page == null) return;

            var strings = EditorStrings.Current;
            source = await page.DisplayPromptAsync(
                strings.ImageDialogTitle,
                strings.ImageDialogMessage,
                accept: strings.ImageDialogInsert,
                cancel: strings.ImageDialogCancel,
                placeholder: "https://",
                keyboard: Keyboard.Url);
        }

        if (string.IsNullOrWhiteSpace(source)) return;

        var cmd = new InsertImageCommand { Source = source };
        cmd.Execute(GetEditorContext());
        _caret.MoveTo(_inputController.Selection.Active);
        InvalidateCanvas();
        _keyboardProxy.Focus();
    }

    private async Task HandleInsertTableAsync()
    {
        var page = GetHostPage();
        if (page == null) return;

        var strings = EditorStrings.Current;
        string? rowsInput = await page.DisplayPromptAsync(
            strings.TableDialogTitle,
            strings.TableDialogRows,
            accept: strings.TableDialogNext,
            cancel: strings.TableDialogCancel,
            initialValue: "2",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(rowsInput) || !int.TryParse(rowsInput, out int rows) || rows < 1)
            return;

        string? colsInput = await page.DisplayPromptAsync(
            strings.TableDialogTitle,
            strings.TableDialogColumns,
            accept: strings.TableDialogInsert,
            cancel: strings.TableDialogCancel,
            initialValue: "2",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(colsInput) || !int.TryParse(colsInput, out int cols) || cols < 1)
            return;

        var cmd = new InsertTableCommand { Rows = rows, Columns = cols };
        ExecuteToolbarCommand(cmd);
    }

    private Page? GetHostPage()
    {
        if (Window?.Page is Page windowPage)
            return windowPage;

        return Application.Current?.Windows.FirstOrDefault()?.Page;
    }

    // ── Scroll ───────────────────────────────────────────────────────────────────

    /// <summary>Scrolls the viewport to the given document Y offset.</summary>
    public void ScrollTo(float y)
    {
        _scrollOffset = Math.Max(0f, y);
        _renderer.ScrollOffset = _scrollOffset;
        InvalidateCanvas();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// When a handler is attached, loads the toolbar icons, starts the caret blink timer if the
    /// editor is focused, and starts listening for <see cref="EditorStrings.CurrentChanged"/>; when
    /// it is removed, stops the blink timer and the listener. The listener is also started on
    /// <see cref="VisualElement.Loaded"/> and stopped on <see cref="VisualElement.Unloaded"/>, and
    /// holds the view only weakly, so an editor that is discarded without either can still be
    /// garbage collected.
    /// </remarks>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is not null)
        {
            // Also picks up strings replaced while the view had no handler.
            SubscribeToEditorStrings();
            // The blink timer is driven by focus (OnNativeFocusChanged), not by the
            // handler lifetime — an idle editor should not repaint 10x per second.
            if (_isEditorFocused)
                StartBlinkTimer();
            // Picks up an EditorStrings.Current assigned after construction.
            UpdateSemantics();
            _ = LoadToolbarIconsAsync();
        }
        else
        {
            StopBlinkTimer();
            UnsubscribeFromEditorStrings();
        }
    }

    private async Task LoadToolbarIconsAsync()
    {
        int loadVersion = Interlocked.Increment(ref _iconLoadVersion);
        int pixelSize = Math.Max(96, (int)Math.Ceiling(_toolbarRenderer.ButtonSize * 2f * GetDisplayScale()));

        var loadedIcons = await Task.Run(() =>
            EmbeddedIconLoader.LoadAll(ToolbarIconPack, pixelSize)).ConfigureAwait(false);

        if (loadVersion != _iconLoadVersion)
        {
            foreach (var bmp in loadedIcons.Values)
                bmp.Dispose();
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            foreach (var bmp in _iconCache.Values)
                bmp.Dispose();
            _iconCache.Clear();
            foreach (var (key, bmp) in loadedIcons)
                _iconCache[key] = bmp;
            InvalidateCanvas();
        });
    }

    private static SKBitmap? DecodeSvgBitmap(Stream svgStream, int minDimension = 0)
    {
        var svg = new SKSvg();
        var picture = svg.Load(svgStream);
        if (picture is null)
            return null;

        var cull = picture.CullRect;
        float width = cull.Width > 1f ? cull.Width : DefaultSvgRasterSize;
        float height = cull.Height > 1f ? cull.Height : DefaultSvgRasterSize;

        if (minDimension > 0)
        {
            float scale = Math.Max(minDimension / width, minDimension / height);
            if (scale > 1f)
            {
                width *= scale;
                height *= scale;
            }
        }

        int outWidth = Math.Max(1, (int)Math.Ceiling(width));
        int outHeight = Math.Max(1, (int)Math.Ceiling(height));

        var bitmap = new SKBitmap(outWidth, outHeight, true);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        if (cull.Width > 1f && cull.Height > 1f)
        {
            var matrix = SKMatrix.CreateScale(outWidth / cull.Width, outHeight / cull.Height);
            canvas.SetMatrix(matrix);
            canvas.DrawPicture(picture, -cull.Left, -cull.Top);
        }
        else
        {
            canvas.DrawPicture(picture);
        }
        canvas.Flush();
        return bitmap;
    }

    // ── Rendering ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Synchronous image resolver wired to <see cref="DocumentRenderer.ImageResolver"/>.
    /// Returns a cached <see cref="SKBitmap"/> or starts async loading and returns null
    /// (paint will show a placeholder on this frame, and the canvas is invalidated once
    /// the bitmap arrives so the next frame shows the real image).
    /// </summary>
    private object? ResolveDocumentImage(string source)
    {
        if (_imageCache.TryGetValue(source, out var cached))
            return cached;

        // Start background load; placeholder shown until it completes.
        _ = LoadDocumentImageAsync(source);
        return null;
    }

    private async Task LoadDocumentImageAsync(string source)
    {
        try
        {
            SKBitmap? bmp = null;
            SKBitmap? providerBitmap = null;

            if (ImageProvider is not null)
            {
                try
                {
                    var loaded = await ImageProvider.LoadImageAsync(source).ConfigureAwait(false);
                    if (loaded is SKBitmap loadedBitmap)
                        bmp = providerBitmap = loadedBitmap;
                }
                catch
                {
                    // Fall back to built-in source resolvers below.
                }
            }

            // Prefer app-package assets, then fall back to absolute/relative file paths.
            if (bmp is null && !source.Contains('/') && !source.Contains('\\'))
            {
                try
                {
                    await using var stream = await FileSystem.OpenAppPackageFileAsync(source).ConfigureAwait(false);
                    bmp = SKBitmap.Decode(stream);
                }
                catch { /* not an asset name */ }
            }

            if (bmp is null && (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                                || Path.IsPathRooted(source)))
            {
                var path = source.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(source).LocalPath : source;
                await using var stream = File.OpenRead(path);
                bmp = SKBitmap.Decode(stream);
            }

            if (bmp is null && (source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                                || source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
            {
                using var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(source).ConfigureAwait(false);
                bmp = SKBitmap.Decode(bytes);
            }

            if (bmp is not null)
            {
                // Images decoded here are never modified, so mark them immutable to let the
                // renderer cache their SKImage. A provider's bitmap is left as it is: if the
                // provider keeps changing it, those changes must keep showing.
                if (!ReferenceEquals(bmp, providerBitmap))
                    bmp.SetImmutable();
                _imageCache[source] = bmp;
                InvalidateCanvas();
            }
        }
        catch { /* silently ignore — placeholder will remain */ }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var skCanvas = e.Surface.Canvas;
        skCanvas.Clear();

        float scale = GetDisplayScale();
        float logicalW = e.Info.Width / scale;
        float logicalH = e.Info.Height / scale;
        skCanvas.Scale(scale);

        var dc = new SkiaDrawingContext(skCanvas);
        var theme = ResolveTheme();

        // ── Toolbar ──────────────────────────────────────────────────────────────
        float toolbarH = 0f;
        if (ShowToolbar)
        {
            _toolbarRenderer.BackgroundColor = theme.ToolbarBackground;
            _toolbarRenderer.ButtonColor     = theme.ToolbarButtonColor;
            _toolbarRenderer.ActiveColor     = theme.ToolbarActiveColor;
            _toolbarRenderer.SeparatorColor  = theme.ToolbarSeparatorColor;
            _toolbarRenderer.DisabledColor   = theme.ToolbarSeparatorColor;
            _toolbarRenderer.HoverColor      = theme.ToolbarHoverColor;
            ApplyToolbarMaxRows(); // picks up set/clear transitions that raise no property change (see coerceValue)
            _toolbarRenderer.UpdateToggleStates(GetEditorContext());
            _toolbarRenderer.Render(dc, new EditorRect(0, 0, logicalW, logicalH));
            toolbarH = _toolbarRenderer.ToolbarHeight;
        }

        var viewport = new EditorRect(0, toolbarH, logicalW, logicalH - toolbarH);

        // Configure renderer and caret renderer from the current theme
        _renderer.BackgroundColor         = theme.Background;
        _renderer.DefaultTextColor        = theme.DefaultTextColor;
        _renderer.PreferThemeTextColorForDefaultBlack = true;
        _renderer.ScrollbarWidth          = theme.ScrollbarWidth;
        _renderer.ScrollbarTrackColor     = theme.ScrollbarTrackColor;
        _renderer.ScrollbarThumbColor     = theme.ScrollbarThumbColor;
        _renderer.LineNumbersGutterWidth  = theme.LineNumbersGutterWidth;
        _renderer.LineNumbersGutterColor  = theme.LineNumbersGutterColor;
        _renderer.LineNumbersTextColor    = theme.LineNumbersTextColor;
        _caretRenderer.CaretColor         = theme.CaretColor;
        _caretRenderer.CaretWidth         = theme.CaretWidth;
        _selectionRenderer.SelectionColor = theme.SelectionHighlightColor;
        _renderer.SelectionTextColor      = theme.SelectionTextColor;
        _renderer.HyperlinkHoverColor     = theme.HyperlinkHoverColor;
        _layoutEngine.UnderlineHyperlinks = true;

        // Link colour and spacing are baked into cached block layouts: re-lay out when they change.
        if (_layoutEngine.HyperlinkTextColor != theme.HyperlinkColor
            || _layoutEngine.DefaultLineSpacing != theme.LineSpacing
            || _layoutEngine.DefaultParagraphSpacing != theme.ParagraphSpacing)
        {
            _layoutEngine.HyperlinkTextColor      = theme.HyperlinkColor;
            _layoutEngine.DefaultLineSpacing      = theme.LineSpacing;
            _layoutEngine.DefaultParagraphSpacing = theme.ParagraphSpacing;
            _layoutCache.InvalidateAll();
        }

        // Layout → render (with selection behind text) → caret
        _lastLayout = _layoutCache.GetLayout(_document, logicalW, dc);

        // The document may have become shorter (edits, spacing or theme changes): keep the
        // scroll position within it so the view never shows empty space below the end.
        float maxScroll = Math.Max(0f, _lastLayout.TotalHeight - viewport.Height);
        if (_scrollOffset > maxScroll)
            _scrollOffset = maxScroll;
        _renderer.ScrollOffset = _scrollOffset;

        // Text under a resting mouse pointer moves when the view scrolls or the layout
        // changes: hit-test the hovered link again before drawing.
        if (_pointerPosition is { } pointer)
            UpdateHyperlinkHover(pointer.X, pointer.Y);
        _renderer.HoveredHyperlink = _hoveredHyperlink;

        // Wire selection into the pre-text layer so highlights appear behind text.
        var currentSelection = _inputController.Selection;
        var currentLayout    = _lastLayout;
        _renderer.PreTextLayer = currentSelection.IsEmpty ? null : ctx =>
            _selectionRenderer.Render(currentSelection, currentLayout, _document, ctx);
        // Selected text is redrawn in the theme's selection text colour within these rects.
        _renderer.SelectionRects = currentSelection.IsEmpty || theme.SelectionTextColor.A == 0
            ? null
            : _selectionRenderer.GetSelectionRects(currentSelection, currentLayout, _document, dc);
        _renderer.ImageResolver = ResolveDocumentImage;

        if (!IsReadOnly)
            _caret.UpdateBlink();

        RenderDocumentLayer(dc, viewport);

        // Placeholder — shown only when the document is an empty single paragraph
        if (EditorAccessibility.IsEmpty(_document)
            && !string.IsNullOrEmpty(PlaceholderText))
        {
            var placeholder = new EditorPaint
            {
                Color = new EditorColor(160, 160, 160),
                Font = new EditorFont(theme.DefaultFontFamily, theme.DefaultFontSize),
            };
            dc.DrawText(PlaceholderText, theme.Padding.Left, toolbarH + theme.Padding.Top + theme.DefaultFontSize, placeholder);
        }

        // Render any open dropdown/colour-picker overlay on top of everything
        if (ShowToolbar && _toolbarRenderer.HasOpenOverlay)
            _toolbarRenderer.RenderOverlay(dc, logicalW, logicalH);

        // Render overflow panel on top of document (it can extend below the toolbar)
        if (ShowToolbar && _toolbarRenderer.IsOverflowPanelOpen)
            _toolbarRenderer.RenderOverflowPanel(dc, logicalW, logicalH);

        // Toolbar items may have moved (resize, overflow): hit-test the hovered item again now
        // that every item's rect is known, and redraw if it changed.
        if (_pointerPosition is { } toolbarPointer && UpdateToolbarHover(toolbarPointer.X, toolbarPointer.Y))
            InvalidateCanvas();

        // Focus ring around the whole control. The native view that holds keyboard focus is
        // invisible, so the platform draws no focus visual of its own for the editor.
        if (ShowFocusRing && _isEditorFocused && theme.FocusRingColor.A != 0)
        {
            const float ringWidth = 1.5f;
            var ringPaint = new EditorPaint
            {
                Color = theme.FocusRingColor,
                Style = PaintStyle.Stroke,
                StrokeWidth = ringWidth,
                IsAntiAlias = true,
            };
            float inset = ringWidth / 2f;
            dc.DrawRect(new EditorRect(inset, inset, logicalW - ringWidth, logicalH - ringWidth), ringPaint);
        }

        // Magnifier last, above everything: the document drawn again, zoomed, in a lens.
        _magnifierRenderer.Render(dc, _magnifier, new EditorRect(0, 0, logicalW, logicalH),
            theme.Background, ctx => RenderDocumentLayer(ctx, viewport));
    }

    /// <summary>
    /// Draws the document (selection, text, images) with image handles and the caret.
    /// Called once per frame, and again inside the magnifier lens.
    /// </summary>
    private void RenderDocumentLayer(IDrawingContext dc, EditorRect viewport)
    {
        _renderer.Render(_lastLayout, viewport, dc);

        // Caret only — selection is now rendered inside the document renderer.
        dc.Save();
        dc.ClipRect(viewport);
        dc.Translate(viewport.X, viewport.Y - _scrollOffset);

        if (_selectedImage is not null)
            RenderImageSelectionHandles(dc);

        if (!IsReadOnly)
            _caretRenderer.Render(_caret, _lastLayout, _document, dc);

        dc.Restore();
    }

    // ── Touch ────────────────────────────────────────────────────────────────────

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (IsReadOnly) return;

        float scale    = GetDisplayScale();
        float rawX     = e.Location.X / scale;
        float rawY     = e.Location.Y / scale;
        float toolbarH = ShowToolbar ? _toolbarRenderer.ToolbarHeight : 0f;

        // A finger that goes down during a caret drag is ignored for its whole contact,
        // wherever it lands (document or toolbar) and even after the drag ends, so it can
        // never run a command, move the caret, select or tap a link.
        if (_isCaretDragging && e.ActionType == SKTouchAction.Pressed && e.Id != _gestureTouchId)
            _ignoredTouchIds.Add(e.Id);

        if (_ignoredTouchIds.Contains(e.Id))
        {
            if (e.ActionType is SKTouchAction.Released or SKTouchAction.Cancelled)
                _ignoredTouchIds.Remove(e.Id);
            e.Handled = true;
            return;
        }

        // Remember where the mouse is, so hover can be re-checked after a scroll or layout change.
        if (e.DeviceType == SKTouchDeviceType.Mouse && e.ActionType != SKTouchAction.Exited)
            _pointerPosition = (rawX, rawY);

        // Mouse hover (reported where the platform sends pointer moves with no button pressed).
        if (e.ActionType == SKTouchAction.Moved && !e.InContact)
        {
            UpdatePointerHover(rawX, rawY);
            e.Handled = true;
            return;
        }

        if (e.ActionType == SKTouchAction.Exited)
        {
            ClearPointerHover();
            e.Handled = true;
            return;
        }

        // Route taps in the toolbar band to the toolbar
        if (ShowToolbar && e.ActionType == SKTouchAction.Pressed)
        {
            // If an overlay is open, any tap (anywhere on canvas) should be routed to it first.
            // An overlay may extend below the toolbar into the document area.
            if (_toolbarRenderer.HasOpenOverlay)
            {
                _toolbarRenderer.HandleOverlayTap(rawX, rawY);
                InvalidateCanvas();
                _keyboardProxy.Focus();
                e.Handled = true;
                return;
            }

            // If the overflow panel is open, route any tap to it (could be an item or close).
            if (_toolbarRenderer.IsOverflowPanelOpen)
            {
                // Check if tap is on a visible item in the overflow panel (they're in _itemRects).
                var overflowHit = _toolbarRenderer.HitTest(rawX, rawY);
                if (overflowHit is null)
                {
                    // Tap outside panel - close it.
                    _toolbarRenderer.HandleOverflowButtonTap(rawX, rawY); // will close
                }
                else
                {
                    HandleToolbarItemTap(overflowHit);
                    _toolbarRenderer.HandleOverflowButtonTap(rawX, rawY); // close panel
                }
                InvalidateCanvas();
                _keyboardProxy.Focus();
                e.Handled = true;
                return;
            }

            if (rawY < toolbarH)
            {
                // Check overflow button first.
                if (_toolbarRenderer.HandleOverflowButtonTap(rawX, rawY))
                {
                    InvalidateCanvas();
                    _keyboardProxy.Focus();
                    e.Handled = true;
                    return;
                }

                var toolbarHit = _toolbarRenderer.HitTest(rawX, rawY);
                if (toolbarHit is not null)
                    HandleToolbarItemTap(toolbarHit);
                InvalidateCanvas();
                _keyboardProxy.Focus();
                e.Handled = true;
                return;
            }
        }

        // Document-space coordinates (below toolbar, accounting for scroll)
        float x = rawX;
        float y = rawY - toolbarH + _scrollOffset;
        float docViewHeight = (Height > 0 ? (float)Height : 400f) - toolbarH;

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
            {
                StopInertiaTimer();

                if (TryBeginImageResize(x, y) || TryBeginTableColumnResize(x, y) || TryBeginTableRowResize(x, y))
                {
                    _keyboardProxy.Focus();
                    InvalidateCanvas();
                    e.Handled = true;
                    return;
                }

                _inertialScroller.BeginPan(rawY);

                var ctx = GetMeasureContext();
                var hit = DocumentHitTester.HitTest(x, y, _lastLayout, _document, ctx);
                var hitPosition = NormalizeTapPosition(hit.Position, hit.IsAfterLastBlock);
                if (hit.Run is { IsImage: true, SourceInline: ImageInline selectedImage })
                {
                    _selectedImage = selectedImage;
                    TryFindImageRect(selectedImage, out _selectedImageRect);
                }
                else
                {
                    _selectedImage = null;
                }
                _caret.Position = hitPosition;
                _caret.ResetBlink();
                _inputController.Selection.CollapseTo(hitPosition);
                _keyboardProxy.Focus();
                // Bring up the soft keyboard on tap. ShowKeyboard() re-shows it even when
                // the proxy already holds focus (e.g. dismissed via the back button).
                _keyboardProxy.ShowKeyboard();
                // Notify the IME of the new selection position so it stops composing
                // against a stale cursor offset.
                NotifyImeSelectionChanged();
                InvalidateCanvas();
                RaiseSelectionChanged();

                // Holding still starts a caret drag (with the magnifier), as on iOS/Android.
                if (e.DeviceType == SKTouchDeviceType.Touch)
                    StartLongPress(e.Id, rawX, rawY);
                break;
            }
            case SKTouchAction.Moved when e.InContact:
            {
                if (_isCaretDragging)
                {
                    if (e.Id == _gestureTouchId)
                        UpdateCaretDrag(rawX, rawY);
                    break;
                }

                if (e.Id == _gestureTouchId)
                    _longPress.Move(rawX, rawY);

                if (_isResizingImage)
                {
                    UpdateImageResize(x, y);
                    InvalidateCanvas();
                    break;
                }

                if (_isResizingTableColumn)
                {
                    UpdateTableColumnResize(x);
                    InvalidateCanvas();
                    break;
                }

                if (_isResizingTableRow)
                {
                    UpdateTableRowResize(y);
                    InvalidateCanvas();
                    break;
                }

                // Pan gesture — scroll if moved primarily vertically
                float delta = _inertialScroller.Pan(rawY);
                if (Math.Abs(delta) > 0.5f)
                {
                    float maxScroll = Math.Max(0f, _lastLayout.TotalHeight - docViewHeight);
                    _scrollOffset = _inertialScroller.ClampScroll(_scrollOffset - delta, maxScroll);
                    _renderer.ScrollOffset = _scrollOffset;
                    InvalidateCanvas();
                }
                else
                {
                    var ctx = GetMeasureContext();
                    var hit = DocumentHitTester.HitTest(x, y, _lastLayout, _document, ctx);
                    _inputController.Selection.ExtendTo(hit.Position);
                    _caret.Position = hit.Position;
                    InvalidateCanvas();
                    RaiseSelectionChanged();
                }
                break;
            }
            case SKTouchAction.Released:
            {
                if (e.Id == _gestureTouchId)
                    _longPress.Cancel();
                if (_isCaretDragging)
                {
                    // Only the finger that owns the drag ends it.
                    if (e.Id == _gestureTouchId)
                        EndCaretDrag();
                    break;
                }

                if (_isResizingImage)
                {
                    _isResizingImage = false;
                    _activeImageHandle = -1;
                    InvalidateCanvas();
                    break;
                }

                if (_isResizingTableColumn)
                {
                    _isResizingTableColumn = false;
                    _resizingTable = null;
                    _resizingTableBlockIndex = -1;
                    _resizingColumnIndex = -1;
                    InvalidateCanvas();
                    break;
                }

                if (_isResizingTableRow)
                {
                    _isResizingTableRow = false;
                    _resizingTable = null;
                    _resizingTableBlockIndex = -1;
                    _resizingRowIndex = -1;
                    InvalidateCanvas();
                    break;
                }

                _inertialScroller.EndPan();
                if (_inertialScroller.IsScrolling)
                {
                    StartInertiaTimer();
                }
                else
                {
                    // Tap (no inertia): check whether the finger lifted over a hyperlink.
                    var ctx = GetMeasureContext();
                    var hit = DocumentHitTester.HitTest(x, y, _lastLayout, _document, ctx);
                    if (hit.Run?.SourceInline is HyperlinkInline tappedLink)
                    {
                        var args = new HyperlinkClickedEventArgs(tappedLink.Url);
                        HyperlinkClicked?.Invoke(this, args);
                        bool openRequested = _lastKeyboardModifiers.HasFlag(InputModifiers.Control)
                                             || _lastKeyboardModifiers.HasFlag(InputModifiers.Meta);
                        if (!args.Cancel && openRequested)
                            HyperlinkHandler.OpenUrl(tappedLink.Url);
                    }
                }
                break;
            }
            case SKTouchAction.Cancelled:
            {
                if (e.Id == _gestureTouchId)
                    CancelCaretGesture();
                break;
            }
        }

        e.Handled = true;
    }

    // ── Pointer hover ────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the hovered toolbar item and hyperlink for a mouse pointer at the given
    /// canvas coordinates, redrawing when either changes.
    /// </summary>
    private void UpdatePointerHover(float rawX, float rawY)
    {
        _pointerPosition = (rawX, rawY);
        bool changed = UpdateToolbarHover(rawX, rawY) | UpdateHyperlinkHover(rawX, rawY);
        if (changed)
            InvalidateCanvas();
    }

    /// <summary>Whether a pointer at canvas height <paramref name="rawY"/> is over the document, not the toolbar or its panels.</summary>
    private bool IsPointerOverDocument(float rawY)
    {
        if (!ShowToolbar) return true;
        // A drop-down list, colour grid or the overflow panel covers what is beneath it.
        return !_toolbarRenderer.HasOpenOverlay
               && !_toolbarRenderer.IsOverflowPanelOpen
               && rawY >= _toolbarRenderer.ToolbarHeight;
    }

    /// <summary>Sets the hovered toolbar item; returns <c>true</c> when it changed.</summary>
    private bool UpdateToolbarHover(float rawX, float rawY)
    {
        if (ShowToolbar && !_toolbarRenderer.HasOpenOverlay && !IsPointerOverDocument(rawY))
            return _toolbarRenderer.UpdateHover(rawX, rawY);
        return _toolbarRenderer.ClearHover();
    }

    /// <summary>Sets the hovered hyperlink; returns <c>true</c> when it changed.</summary>
    private bool UpdateHyperlinkHover(float rawX, float rawY)
    {
        HyperlinkInline? link = null;
        if (IsPointerOverDocument(rawY))
        {
            float toolbarH = ShowToolbar ? _toolbarRenderer.ToolbarHeight : 0f;
            link = DocumentHitTester.HitTestHyperlink(rawX, rawY - toolbarH + _scrollOffset, _lastLayout);
        }

        if (ReferenceEquals(link, _hoveredHyperlink)) return false;
        _hoveredHyperlink = link;
        return true;
    }

    private void ClearPointerHover()
    {
        _pointerPosition = null;
        bool changed = _toolbarRenderer.ClearHover() | (_hoveredHyperlink is not null);
        _hoveredHyperlink = null;
        if (changed)
            InvalidateCanvas();
    }

    // ── Long-press caret drag & magnifier ────────────────────────────────────────

    private void StartLongPress(long touchId, float rawX, float rawY)
    {
        _gestureTouchId = touchId;
        _longPress.Begin(rawX, rawY, _gestureClock.Elapsed);

        if (_longPressTimer is null)
        {
            _longPressTimer = Dispatcher.CreateTimer();
            _longPressTimer.IsRepeating = false;
            _longPressTimer.Tick += (_, _) => OnLongPressTimerTick();
        }

        _longPressTimer.Stop();
        _longPressTimer.Interval = _longPress.Duration;
        _longPressTimer.Start();
    }

    private void OnLongPressTimerTick()
    {
        if (IsReadOnly)
            _longPress.Cancel();

        if (!_longPress.IsTracking)
            return;

        if (!_longPress.TryFire(_gestureClock.Elapsed))
        {
            // The timer fired a little early; check again shortly.
            _longPressTimer!.Interval = TimeSpan.FromMilliseconds(16);
            _longPressTimer.Start();
            return;
        }

        _isCaretDragging = true;
        _inertialScroller.Stop();
        _selectedImage = null;
        UpdateCaretDrag(_longPress.X, _longPress.Y);
    }

    /// <summary>
    /// Moves the caret to the touch point (view coordinates) and keeps the magnifier on the
    /// caret's line.
    /// </summary>
    private void UpdateCaretDrag(float rawX, float rawY)
    {
        float toolbarH = ShowToolbar ? _toolbarRenderer.ToolbarHeight : 0f;
        var ctx = GetMeasureContext();
        // Keep the target on the visible part of the document (the finger may slide over the
        // toolbar or past the bottom edge); off-screen lines are not drawn, so the lens
        // would have nothing to show.
        float docViewHeight = (Height > 0 ? (float)Height : 400f) - toolbarH;
        float viewY = Math.Clamp(rawY - toolbarH, 0f, Math.Max(0f, docViewHeight - 1f));
        var hit = DocumentHitTester.HitTest(rawX, viewY + _scrollOffset, _lastLayout, _document, ctx);
        var position = NormalizeTapPosition(hit.Position, hit.IsAfterLastBlock);

        _caret.Position = position;
        _caret.ResetBlink();
        _inputController.Selection.CollapseTo(position);

        if (IsMagnifierEnabled)
        {
            // Magnify around the caret, like the native loupes, rather than the finger: the
            // lens shows whole lines while the finger wanders up and down, and keeps the
            // caret in view when it snaps to the end of a short line.
            var caretRect = _caretRenderer.GetCaretRect(position, _lastLayout, _document, ctx);
            _magnifier.Options = MagnifierOptions;
            // Keep the focus on the visible document: a partly scrolled-off line's centre can
            // lie above it, where nothing is drawn for the lens to show.
            float focusY = Math.Clamp(caretRect.Y + caretRect.Height / 2f - _scrollOffset,
                0f, Math.Max(0f, docViewHeight)) + toolbarH;
            _magnifier.Show(caretRect.X + caretRect.Width / 2f, focusY);
        }

        InvalidateCanvas();
        RaiseSelectionChanged();
    }

    /// <summary>Abandons a pending long press or an active caret drag.</summary>
    private void CancelCaretGesture()
    {
        _longPress.Cancel();
        _ignoredTouchIds.Clear();
        if (_isCaretDragging)
            EndCaretDrag();
    }

    private void EndCaretDrag()
    {
        _isCaretDragging = false;
        _magnifier.Hide();
        _inertialScroller.Stop();
        NotifyImeSelectionChanged();
        InvalidateCanvas();
    }

    // ── Blink Timer ──────────────────────────────────────────────────────────────

    private void StartBlinkTimer()
    {
        if (_blinkTimer is not null) return;
        _blinkTimer = Dispatcher.CreateTimer();
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(100);
        _blinkTimer.Tick += (_, _) =>
        {
            if (!IsReadOnly)
                InvalidateCanvas();
        };
        _blinkTimer.Start();
    }

    private void StopBlinkTimer()
    {
        _blinkTimer?.Stop();
        _blinkTimer = null;
    }

    private void StartInertiaTimer()
    {
        if (_inertiaTimer is not null) return;
        _inertiaTimer = Dispatcher.CreateTimer();
        _inertiaTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 fps
        _inertiaTimer.Tick += (_, _) =>
        {
            float delta = _inertialScroller.Tick();
            if (Math.Abs(delta) < 0.1f || !_inertialScroller.IsScrolling)
            {
                StopInertiaTimer();
                return;
            }
            float toolbarH   = ShowToolbar ? _toolbarRenderer.ToolbarHeight : 0f;
            float maxScroll  = Math.Max(0f, _lastLayout.TotalHeight - (Height > 0 ? (float)Height : 400f) + toolbarH);
            _scrollOffset = _inertialScroller.ClampScroll(_scrollOffset - delta, maxScroll);
            _renderer.ScrollOffset = _scrollOffset;
            InvalidateCanvas();
        };
        _inertiaTimer.Start();
    }

    private void StopInertiaTimer()
    {
        _inertiaTimer?.Stop();
        _inertiaTimer = null;
        _inertialScroller.Stop();
    }

    // ── Focus ────────────────────────────────────────────────────────────────────

    /// <summary>Gets whether the editor currently holds native keyboard focus.</summary>
    public bool IsEditorFocused => _isEditorFocused;

    /// <summary>
    /// Gives the editor keyboard focus and shows the soft keyboard.
    /// <see cref="EditorFocusChanged"/> is raised when the platform confirms focus.
    /// </summary>
    public new bool Focus()
    {
        bool result = _keyboardProxy.Focus();
        _keyboardProxy.ShowKeyboard();
        return result;
    }

    /// <summary>
    /// Removes keyboard focus from the editor and hides the soft keyboard.
    /// <see cref="EditorFocusChanged"/> is raised when the platform confirms focus loss.
    /// </summary>
    public new void Unfocus()
    {
        _keyboardProxy.HideKeyboard();
        _keyboardProxy.Unfocus();
        base.Unfocus();
    }

    /// <summary>
    /// Single source of truth for editor focus: the platform capture view reports native
    /// focus gain/loss here — including focus moving to a sibling native control (#6).
    /// </summary>
    private void OnNativeFocusChanged(object? sender, bool isFocused)
    {
        if (_isEditorFocused == isFocused) return;
        _isEditorFocused = isFocused;

        _caret.IsFocused = isFocused;
        if (isFocused)
            StartBlinkTimer();
        else
            StopBlinkTimer();

        InvalidateCanvas();
        EditorFocusChanged?.Invoke(this, new FocusChangedEventArgs(isFocused));
    }

    // ── Document changes ──────────────────────────────────────────────────────────

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        _layoutCache.OnDocumentChanged(_document, e);
        TextChanged?.Invoke(this, new EditorTextChangedEventArgs(e.AffectedRange));
        ScheduleDocumentStateUpdate();
        UpdateSemantics();
        InvalidateCanvas();
    }

    // ── Accessibility ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies the editor's screen-reader description and hint (see
    /// <see cref="EditorAccessibility.ForEditor"/>) to this view's <see cref="SemanticProperties"/>,
    /// unless the app has set its own value. With <paramref name="announce"/> (used when
    /// <see cref="IsReadOnly"/> toggles), the new state is announced, also when the app owns the
    /// description: then the app's description followed by the read-only state.
    /// </summary>
    private void UpdateSemantics(bool announce = false)
    {
        bool isEmpty = EditorAccessibility.IsEmpty(_document);
        var props = EditorAccessibility.ForEditor(IsReadOnly, isEmpty, PlaceholderText);
        string? description = string.IsNullOrEmpty(props.Label) ? null : props.Label;
        string? hint = string.IsNullOrEmpty(props.Hint) ? null : props.Hint;

        bool ownsDescription = SemanticProperties.GetDescription(this) == _appliedSemanticDescription;
        if (ownsDescription && description != _appliedSemanticDescription)
        {
            _appliedSemanticDescription = description;
            SemanticProperties.SetDescription(this, description);
        }

        if (SemanticProperties.GetHint(this) == _appliedSemanticHint
            && hint != _appliedSemanticHint)
        {
            _appliedSemanticHint = hint;
            SemanticProperties.SetHint(this, hint);
        }

        if (announce && Handler is not null)
        {
            string announcement = ownsDescription
                ? description ?? string.Empty
                : EditorAccessibility.ForEditor(IsReadOnly, isEmpty, PlaceholderText,
                    label: SemanticProperties.GetDescription(this)).Label;
            if (announcement.Length > 0)
                Microsoft.Maui.Accessibility.SemanticScreenReader.Default.Announce(announcement);
        }

        SyncProxySemantics();
    }

    /// <summary>
    /// On Windows, copies this view's current description and hint (the editor's or the app's)
    /// to the keyboard-capture view, which is the element Narrator reads and focuses there.
    /// </summary>
    private void SyncProxySemantics()
    {
        if (!ProxyCarriesSemantics) return;
        SemanticProperties.SetDescription(_keyboardProxy, SemanticProperties.GetDescription(this));
        SemanticProperties.SetHint(_keyboardProxy, SemanticProperties.GetHint(this));
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        // An app-set description or hint must reach the Windows keyboard-capture view too.
        // _keyboardProxy is null while the base constructor sets properties.
        if (_keyboardProxy is not null
            && (propertyName == SemanticProperties.DescriptionProperty.PropertyName
                || propertyName == SemanticProperties.HintProperty.PropertyName))
            SyncProxySemantics();
    }

    private void RaiseSelectionChanged()
    {
        var sel = _inputController.Selection;
        SelectionChanged?.Invoke(this,
            new EditorSelectionChangedEventArgs(sel.Anchor, sel.Active, sel.IsEmpty));
        RaiseHyperlinkSelectionChanged(sel.Active);
        UpdateFormattingState();
    }

    /// <summary>
    /// Takes freshly loaded content as the new baseline (its images are not reported as
    /// inserted), then raises <see cref="ContentLoaded"/>.
    /// </summary>
    private void OnContentLoaded()
    {
        _imageTracker.Reset(_document);
        UpdateFormattingState();
        ContentLoaded?.Invoke(this, new ContentLoadedEventArgs());
    }

    /// <summary>
    /// Queues one image and formatting state check for the end of the current operation: one
    /// edit can send several document notifications, some before the selection has moved.
    /// </summary>
    private void ScheduleDocumentStateUpdate()
    {
        if (_documentStateUpdatePending)
            return;
        _documentStateUpdatePending = true;

        IDispatcher? dispatcher;
        try { dispatcher = Dispatcher; }
        catch (InvalidOperationException) { dispatcher = null; } // no dispatcher on this thread
        if (dispatcher is null || !dispatcher.Dispatch(UpdateDocumentState))
            UpdateDocumentState();
    }

    private void UpdateDocumentState()
    {
        _documentStateUpdatePending = false;

        var changes = _imageTracker.Update(_document);
        foreach (var image in changes.Removed)
            ImageRemoved?.Invoke(this, new ImageRemovedEventArgs(image.Source));
        foreach (var image in changes.Inserted)
            ImageInserted?.Invoke(this, new ImageInsertedEventArgs(image));

        UpdateFormattingState();
    }

    private FormattingState ComputeFormattingState()
    {
        // Merge the pending styles the same way typed input does: character toggles from
        // one, font family, size and colours from the other.
        var pending = _formattingEngine.PendingStyle;
        var pendingFont = _fontFormattingEngine.PendingStyle;
        if (pending is not null && pendingFont is not null)
        {
            pending = pending
                .WithFontFamily(pendingFont.FontFamily)
                .WithFontSize(pendingFont.FontSize)
                .WithTextColor(pendingFont.TextColor)
                .WithHighlightColor(pendingFont.HighlightColor);
        }
        return FormattingState.Compute(_document, _inputController.Selection.Range, pending ?? pendingFont);
    }

    /// <summary>
    /// Recomputes the formatting at the caret or selection and raises the matching "changed"
    /// event for each part that differs from the last state.
    /// </summary>
    private void UpdateFormattingState()
    {
        var state = ComputeFormattingState();
        var changes = _formattingStateTracker.Update(state);
        if (changes == FormattingStateChanges.None)
            return;

        if (changes.HasFlag(FormattingStateChanges.FontFamily))
            FontFamilyChanged?.Invoke(this, new FontFamilyChangedEventArgs(state.FontFamily));
        if (changes.HasFlag(FormattingStateChanges.FontSize))
            FontSizeChanged?.Invoke(this, new FontSizeChangedEventArgs(state.FontSize));
        if (changes.HasFlag(FormattingStateChanges.FontAttributes))
            FontAttributesChanged?.Invoke(this, new FontAttributesChangedEventArgs(
                state.IsBold, state.IsItalic, state.IsSubscript, state.IsSuperscript));
        if (changes.HasFlag(FormattingStateChanges.TextDecorations))
            TextDecorationsChanged?.Invoke(this, new TextDecorationsChangedEventArgs(
                state.IsUnderline, state.IsStrikethrough));
        if (changes.HasFlag(FormattingStateChanges.ParagraphFormat))
            TextFormattingChanged?.Invoke(this, new TextFormattingChangedEventArgs(state.ParagraphFormat));
        if (changes.HasFlag(FormattingStateChanges.Alignment))
            HorizontalTextAlignmentChanged?.Invoke(this, new HorizontalTextAlignmentChangedEventArgs(state.Alignment));
        if (changes.HasFlag(FormattingStateChanges.ListType))
            ListTypeChanged?.Invoke(this, new ListTypeChangedEventArgs(state.ListType));
        if (changes.HasFlag(FormattingStateChanges.TextColor))
            TextColorChanged?.Invoke(this, new TextColorChangedEventArgs(state.TextColor));
        if (changes.HasFlag(FormattingStateChanges.HighlightColor))
            HighlightTextColorChanged?.Invoke(this, new HighlightTextColorChangedEventArgs(state.HighlightColor));
    }

    private void RaiseHyperlinkSelectionChanged(DocumentPosition position)
    {
        var link = HyperlinkEngine.GetHyperlinkAtPosition(_document, position);
        bool isSelected = link is not null;
        string? url = link?.Url;

        if (isSelected == _isHyperlinkSelected && string.Equals(url, _selectedHyperlinkUrl, StringComparison.Ordinal))
            return;

        _isHyperlinkSelected = isSelected;
        _selectedHyperlinkUrl = url;
        IsHyperlinkSelectedChanged?.Invoke(this, new IsHyperlinkSelectedChangedEventArgs(isSelected, url));
    }

    private void RenderImageSelectionHandles(IDrawingContext context)
    {
        if (_selectedImage is null || !TryFindImageRect(_selectedImage, out var imageRect))
            return;

        _selectedImageRect = imageRect;

        var borderPaint = new EditorPaint
        {
            Color = new EditorColor(0x1D, 0x4E, 0x89),
            Style = PaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntiAlias = true,
        };
        context.DrawRect(imageRect, borderPaint);

        foreach (var handleRect in GetImageHandleRects(imageRect))
        {
            var handleFill = new EditorPaint
            {
                Color = new EditorColor(0xFF, 0xFF, 0xFF),
                Style = PaintStyle.Fill,
                IsAntiAlias = true,
            };
            var handleStroke = new EditorPaint
            {
                Color = new EditorColor(0x1D, 0x4E, 0x89),
                Style = PaintStyle.Stroke,
                StrokeWidth = 1f,
                IsAntiAlias = true,
            };

            context.FillRect(handleRect, handleFill);
            context.DrawRect(handleRect, handleStroke);
        }
    }

    private bool TryFindImageRect(ImageInline image, out EditorRect rect)
    {
        rect = default;
        foreach (var block in _lastLayout.Blocks)
        {
            foreach (var line in block.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (!run.IsImage || !ReferenceEquals(run.SourceInline, image))
                        continue;

                    float y = block.Y + line.Y + (line.Height - run.Height) / 2f;
                    rect = new EditorRect(run.X, y, run.Width, run.Height);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryBeginImageResize(float x, float y)
    {
        if (_selectedImage is null)
            return false;
        if (!TryFindImageRect(_selectedImage, out var imageRect))
            return false;

        var handles = GetImageHandleRects(imageRect);
        for (int i = 0; i < handles.Length; i++)
        {
            if (!handles[i].Contains(x, y))
                continue;

            _isResizingImage = true;
            _activeImageHandle = i;
            _imageResizeStartWidth = Math.Max(1f, _selectedImage.Width > 0 ? _selectedImage.Width : imageRect.Width);
            _imageResizeStartHeight = Math.Max(1f, _selectedImage.Height > 0 ? _selectedImage.Height : imageRect.Height);

            _imageResizeAnchorX = i switch
            {
                0 or 3 or 5 => imageRect.X + imageRect.Width,
                1 or 2 or 7 => imageRect.X,
                _ => imageRect.X + imageRect.Width / 2f,
            };
            _imageResizeAnchorY = i switch
            {
                0 or 1 or 6 => imageRect.Y + imageRect.Height,
                2 or 3 or 4 => imageRect.Y,
                _ => imageRect.Y + imageRect.Height / 2f,
            };
            return true;
        }

        return false;
    }

    private void UpdateImageResize(float x, float y)
    {
        if (!_isResizingImage || _selectedImage is null)
            return;

        float aspect = _imageResizeStartWidth / Math.Max(1f, _imageResizeStartHeight);
        float newWidth = _imageResizeStartWidth;
        float newHeight = _imageResizeStartHeight;

        switch (_activeImageHandle)
        {
            case 0:
            case 1:
            case 2:
            case 3:
                newWidth = Math.Max(16f, Math.Abs(x - _imageResizeAnchorX));
                newHeight = Math.Max(16f, Math.Abs(y - _imageResizeAnchorY));
                break;
            case 4:
            case 6:
                newHeight = Math.Max(16f, Math.Abs(y - _imageResizeAnchorY));
                break;
            case 5:
            case 7:
                newWidth = Math.Max(16f, Math.Abs(x - _imageResizeAnchorX));
                break;
        }

        bool maintainAspectRatio = !_lastKeyboardModifiers.HasFlag(InputModifiers.Shift);
        if (maintainAspectRatio)
        {
            if (_activeImageHandle is 4 or 6)
                newWidth = Math.Max(16f, newHeight * aspect);
            else
                newHeight = Math.Max(16f, newWidth / aspect);
        }

        ImageEngine.ResizeImage(_document, _selectedImage, newWidth, newHeight, maintainAspectRatio: false);
    }

    private static EditorRect[] GetImageHandleRects(EditorRect imageRect)
    {
        const float size = 8f;
        float half = size / 2f;
        float left = imageRect.X;
        float right = imageRect.X + imageRect.Width;
        float top = imageRect.Y;
        float bottom = imageRect.Y + imageRect.Height;
        float centerX = imageRect.X + imageRect.Width / 2f;
        float centerY = imageRect.Y + imageRect.Height / 2f;

        return
        [
            new EditorRect(left - half, top - half, size, size),
            new EditorRect(right - half, top - half, size, size),
            new EditorRect(right - half, bottom - half, size, size),
            new EditorRect(left - half, bottom - half, size, size),
            new EditorRect(centerX - half, top - half, size, size),
            new EditorRect(right - half, centerY - half, size, size),
            new EditorRect(centerX - half, bottom - half, size, size),
            new EditorRect(left - half, centerY - half, size, size),
        ];
    }

    private bool TryBeginTableColumnResize(float x, float y)
    {
        const float borderWidth = 1f;
        const float grabRadius = 6f;

        foreach (var block in _lastLayout.Blocks)
        {
            if (block is not TableLayoutBlock tableLayout)
                continue;
            if (y < tableLayout.Y || y > tableLayout.Y + tableLayout.TotalHeight)
                continue;

            float borderX = borderWidth;
            for (int col = 0; col < tableLayout.ColumnWidths.Length - 1; col++)
            {
                borderX += tableLayout.ColumnWidths[col];
                if (Math.Abs(x - borderX) <= grabRadius)
                {
                    if (_document.Blocks[tableLayout.BlockIndex] is not TableBlock modelTable)
                        return false;

                    if (modelTable.ColumnWidths.Count != tableLayout.ColumnWidths.Length)
                    {
                        modelTable.ColumnWidths.Clear();
                        foreach (var w in tableLayout.ColumnWidths)
                            modelTable.ColumnWidths.Add(w);
                    }

                    _isResizingTableColumn = true;
                    _resizingTable = modelTable;
                    _resizingTableBlockIndex = tableLayout.BlockIndex;
                    _resizingColumnIndex = col;
                    _tableResizeStartX = x;
                    _tableLeftStartWidth = modelTable.ColumnWidths[col];
                    _tableRightStartWidth = modelTable.ColumnWidths[col + 1];
                    return true;
                }

                borderX += borderWidth;
            }
        }

        return false;
    }

    private bool TryBeginTableRowResize(float x, float y)
    {
        const float borderWidth = 1f;
        const float grabRadius = 6f;

        foreach (var block in _lastLayout.Blocks)
        {
            if (block is not TableLayoutBlock tableLayout)
                continue;

            float tableBottom = tableLayout.Y + tableLayout.TotalHeight;
            if (y < tableLayout.Y || y > tableBottom)
                continue;

            float tableRight = borderWidth + tableLayout.ColumnWidths.Sum() + tableLayout.ColumnWidths.Length * borderWidth;
            if (x < 0f || x > tableRight)
                continue;

            float borderY = tableLayout.Y + borderWidth;
            for (int row = 0; row < tableLayout.RowHeights.Length - 1; row++)
            {
                borderY += tableLayout.RowHeights[row];
                if (Math.Abs(y - borderY) <= grabRadius)
                {
                    if (_document.Blocks[tableLayout.BlockIndex] is not TableBlock modelTable)
                        return false;

                    _isResizingTableRow = true;
                    _resizingTable = modelTable;
                    _resizingTableBlockIndex = tableLayout.BlockIndex;
                    _resizingRowIndex = row;
                    _tableResizeStartY = y;
                    _tableTopStartHeight = tableLayout.RowHeights[row];
                    _tableBottomStartHeight = tableLayout.RowHeights[row + 1];
                    return true;
                }

                borderY += borderWidth;
            }
        }

        return false;
    }

    private void UpdateTableColumnResize(float x)
    {
        if (!_isResizingTableColumn || _resizingTable is null || _resizingColumnIndex < 0)
            return;

        float delta = x - _tableResizeStartX;
        float left = Math.Max(24f, _tableLeftStartWidth + delta);
        float right = Math.Max(24f, _tableRightStartWidth - delta);

        _resizingTable.ColumnWidths[_resizingColumnIndex] = left;
        _resizingTable.ColumnWidths[_resizingColumnIndex + 1] = right;

        var pos = new DocumentPosition(_resizingTableBlockIndex, 0, 0);
        _document.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.StyleChanged,
            new TextRange(pos, pos)));
    }

    private void UpdateTableRowResize(float y)
    {
        if (!_isResizingTableRow || _resizingTable is null || _resizingRowIndex < 0)
            return;

        float delta = y - _tableResizeStartY;
        float top = Math.Max(24f, _tableTopStartHeight + delta);
        float bottom = Math.Max(24f, _tableBottomStartHeight - delta);

        if (_resizingRowIndex < _resizingTable.Rows.Count)
            _resizingTable.Rows[_resizingRowIndex].Height = top;
        if (_resizingRowIndex + 1 < _resizingTable.Rows.Count)
            _resizingTable.Rows[_resizingRowIndex + 1].Height = bottom;

        var pos = new DocumentPosition(_resizingTableBlockIndex, 0, 0);
        _document.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.StyleChanged,
            new TextRange(pos, pos)));
    }

    private DocumentPosition NormalizeTapPosition(DocumentPosition hitPosition, bool isAfterLastBlock)
    {
        if (!isAfterLastBlock)
            return hitPosition;

        if (_document.Blocks.Count == 0)
        {
            _document.AddBlock(new Paragraph());
            return new DocumentPosition(0, 0, 0);
        }

        int lastIndex = _document.Blocks.Count - 1;
        if (_document.Blocks[lastIndex] is not TableBlock)
            return hitPosition;

        _document.AddBlock(new Paragraph());
        var target = new DocumentPosition(_document.Blocks.Count - 1, 0, 0);
        _document.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.BlockSplit,
            new TextRange(target, target)));
        return target;
    }

    private void InvalidateCanvas() => _canvas.InvalidateSurface();

    private EditorStyle ResolveTheme()
    {
        if (!UseSystemTheme)
            return Theme;

        var appTheme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        return appTheme == AppTheme.Dark
            ? EditorTheme.CreateDark()
            : EditorTheme.CreateLight();
    }

    // ── Keyboard input ───────────────────────────────────────────────────────────

    private void OnKeyboardTextInput(object? sender, string text)
    {
        if (IsReadOnly) return;
        _inputController.HandleTextInput(text, _document);
        ScrollCaretIntoView();
        InvalidateCanvas();
        RaiseSelectionChanged();
    }

    private void OnKeyboardKeyDown(object? sender, EditorKeyEventArgs e)
    {
        if (IsReadOnly) return;
        _lastKeyboardModifiers = e.Modifiers;
        bool handled = _inputController.HandleKeyDown(e, _document, _lastLayout, GetMeasureContext());
        if (handled)
        {
            ScrollCaretIntoView();
            // Notify the IME when a Ctrl+B/I/U formatting shortcut (or any handled key
            // that may restructure runs) is applied so Android doesn't compose against
            // a stale model.
            if (e.HasControlOrMeta)
                NotifyImeSelectionChanged();
            InvalidateCanvas();
            RaiseSelectionChanged();
        }
    }

    /// <summary>
    /// Applies IME composition edits to the document as exact UTF-16 ranges in the caret's
    /// paragraph, then refreshes the view the same way typed input does.
    /// </summary>
    private sealed class EditorImeSink(MintedEditorView editor) : IImeEditSink
    {
        public void InsertText(string text) =>
            Apply(() => editor._inputController.HandleTextInput(text, editor._document));

        public void DeleteBefore(int count) =>
            Apply(() => editor._inputController.DeleteImeTextBefore(count, editor._document));

        public void DeleteAfter(int count) =>
            Apply(() => editor._inputController.DeleteImeTextAfter(count, editor._document));

        public void SetSelection(int start, int end) =>
            Apply(() => editor._inputController.SetImeSelection(start, end, editor._document));

        public void Enter() =>
            Apply(() => editor._inputController.HandleTextInput("\n", editor._document));

        private void Apply(Action edit)
        {
            if (editor.IsReadOnly) return;
            edit();
            editor.ScrollCaretIntoView();
            editor.InvalidateCanvas();
            editor.RaiseSelectionChanged();
        }
    }

    private void ScrollCaretIntoView()
    {
        var ctx = GetMeasureContext();
        var caretRect = _caretRenderer.GetCaretRect(_caret.Position, _lastLayout, _document, ctx);
        float toolbarH   = ShowToolbar ? _toolbarRenderer.ToolbarHeight : 0f;
        float viewHeight = (Height > 0 ? (float)Height : 400f) - toolbarH;
        float caretTop    = caretRect.Y - _scrollOffset;
        float caretBottom = caretTop + caretRect.Height;
        float margin      = caretRect.Height;

        if (caretTop < margin)
            _scrollOffset = Math.Max(0f, caretRect.Y - margin);
        else if (caretBottom > viewHeight - margin)
            _scrollOffset = caretRect.Y + caretRect.Height + margin - viewHeight;

        float maxScroll = Math.Max(0f, _lastLayout.TotalHeight - viewHeight);
        _scrollOffset = Math.Clamp(_scrollOffset, 0f, maxScroll);
        _renderer.ScrollOffset = _scrollOffset;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private float GetDisplayScale()
    {
        try { return (float)DeviceDisplay.Current.MainDisplayInfo.Density; }
        catch { return 1f; }
    }

    private EditorContext GetEditorContext() => new(
        _document,
        _inputController.Selection,
        _undoManager,
        _formattingEngine,
        _fontFormattingEngine,
        _inputController.ClipboardProvider);

    /// <summary>
    /// Returns a measurement context backed by a persistent off-screen surface.
    /// This surface is 1×1 px and is only used for <see cref="IDrawingContext.MeasureText"/>
    /// calls — no pixels are actually drawn to it.
    /// </summary>
    private IDrawingContext GetMeasureContext()
    {
        if (_measureSurface is null || _measureContext is null)
        {
            _measureSurface = SKSurface.Create(new SKImageInfo(1, 1));
            _measureContext = new SkiaDrawingContext(_measureSurface.Canvas);
        }
        return _measureContext;
    }
}
