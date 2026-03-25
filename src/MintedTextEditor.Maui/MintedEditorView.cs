using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Events;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Html;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;
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

    // Persistent 1×1 off-screen surface used for text measurement calls (hit-testing)
    // that happen outside of a PaintSurface callback.
    private SKSurface? _measureSurface;
    private SkiaDrawingContext? _measureContext;

    private DocumentLayout _lastLayout = new();
    private IDispatcherTimer? _blinkTimer;
    private IDispatcherTimer? _inertiaTimer;
    private float _scrollOffset;
    private readonly InertialScroller _inertialScroller = new();
    private bool _isHyperlinkSelected;
    private string? _selectedHyperlinkUrl;
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



    // ── Bindable Properties ──────────────────────────────────────────────────────

    /// <summary>Visual theme applied to the editor.</summary>
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

    /// <summary>Selected built-in theme mode used when <see cref="UseSystemTheme"/> is false.</summary>
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

    /// <summary>
    /// When true, the editor automatically uses light/dark built-in themes based on system app theme.
    /// </summary>
    public static readonly BindableProperty UseSystemThemeProperty =
        BindableProperty.Create(
            nameof(UseSystemTheme),
            typeof(bool),
            typeof(MintedEditorView),
            false,
            propertyChanged: static (b, _, _) => ((MintedEditorView)b).InvalidateCanvas());

    /// <summary>Whether the editor is read-only (disables caret and text input).</summary>
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
                v.InvalidateCanvas();
            });

    /// <summary>Placeholder text shown when the document is empty.</summary>
    public static readonly BindableProperty PlaceholderTextProperty =
        BindableProperty.Create(
            nameof(PlaceholderText),
            typeof(string),
            typeof(MintedEditorView),
            "Start typing…",
            propertyChanged: static (b, _, _) => ((MintedEditorView)b).InvalidateCanvas());

    public EditorStyle Theme
    {
        get => (EditorStyle)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public EditorThemeMode ThemeMode
    {
        get => (EditorThemeMode)GetValue(ThemeModeProperty);
        set => SetValue(ThemeModeProperty, value);
    }

    public bool UseSystemTheme
    {
        get => (bool)GetValue(UseSystemThemeProperty);
        set => SetValue(UseSystemThemeProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Enables or disables word-wrapping (maps to <see cref="TextLayoutEngine.WordWrap"/>).</summary>
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

    /// <summary>Controls whether line numbers are rendered in the left gutter.</summary>
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

    /// <summary>Controls whether an overlay scrollbar is rendered when content overflows.</summary>
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

    public bool IsWordWrap
    {
        get => (bool)GetValue(IsWordWrapProperty);
        set => SetValue(IsWordWrapProperty, value);
    }

    public bool ShowLineNumbers
    {
        get => (bool)GetValue(ShowLineNumbersProperty);
        set => SetValue(ShowLineNumbersProperty, value);
    }

    public bool ShowScrollbar
    {
        get => (bool)GetValue(ShowScrollbarProperty);
        set => SetValue(ShowScrollbarProperty, value);
    }

    /// <summary>Shows or hides the built-in toolbar rendered at the top of the canvas.</summary>
    public static readonly BindableProperty ShowToolbarProperty =
        BindableProperty.Create(
            nameof(ShowToolbar),
            typeof(bool),
            typeof(MintedEditorView),
            true,
            propertyChanged: static (b, _, _) => ((MintedEditorView)b).InvalidateCanvas());

    public bool ShowToolbar
    {
        get => (bool)GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    /// <summary>
    /// Maximum number of toolbar rows to display before remaining items are collapsed
    /// into an overflow (…) button.  0 means unlimited (show all rows).
    /// </summary>
    public static readonly BindableProperty ToolbarMaxRowsProperty =
        BindableProperty.Create(
            nameof(ToolbarMaxRows),
            typeof(int),
            typeof(MintedEditorView),
            0,
            propertyChanged: static (b, _, nv) =>
            {
                var v = (MintedEditorView)b;
                v._toolbarRenderer.Definition.MaxRows = (int)nv!;
                v.InvalidateCanvas();
            });

    public int ToolbarMaxRows
    {
        get => (int)GetValue(ToolbarMaxRowsProperty);
        set => SetValue(ToolbarMaxRowsProperty, value);
    }

    /// <summary>
    /// Selects the toolbar icon pack style and optional bitmap asset namespace.
    /// </summary>
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

    public ToolbarIconPack ToolbarIconPack
    {
        get => (ToolbarIconPack)GetValue(ToolbarIconPackProperty);
        set => SetValue(ToolbarIconPackProperty, value);
    }

    /// <summary>
    /// Optional custom <see cref="Core.Toolbar.ToolbarDefinition"/>.
    /// When <see langword="null"/> the editor uses <see cref="Core.Toolbar.ToolbarDefinition.CreateDefault()"/>.
    /// </summary>
    public static readonly BindableProperty ToolbarDefinitionProperty =
        BindableProperty.Create(
            nameof(ToolbarDefinition),
            typeof(ToolbarDefinition),
            typeof(MintedEditorView),
            null,
            propertyChanged: static (b, _, n) =>
                ((MintedEditorView)b).OnToolbarDefinitionChanged((ToolbarDefinition?)n));

    public ToolbarDefinition? ToolbarDefinition
    {
        get => (ToolbarDefinition?)GetValue(ToolbarDefinitionProperty);
        set => SetValue(ToolbarDefinitionProperty, value);
    }

    /// <summary>
    /// Optional flat toolbar item collection. When set, it takes precedence over <see cref="ToolbarDefinition"/>.
    /// </summary>
    public static readonly BindableProperty ToolbarItemsProperty =
        BindableProperty.Create(
            nameof(ToolbarItems),
            typeof(IList<Core.Toolbar.ToolbarItem>),
            typeof(MintedEditorView),
            null,
            propertyChanged: static (b, _, n) =>
                ((MintedEditorView)b).OnToolbarItemsChanged((IList<Core.Toolbar.ToolbarItem>?)n));

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

        var definition = def ?? ToolbarDefinition.CreateDefault();
        _toolbarRenderer = new ToolbarRenderer(definition);
        _toolbarRenderer.IconPack = ToolbarIconPack;
        _toolbarRenderer.IconResolver = key =>
            _iconCache.TryGetValue(key, out var bmp) ? bmp : null;
        WireToolbarCallbacks(definition);
        InvalidateCanvas();
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
        _toolbarRenderer = new ToolbarRenderer(definition);
        _toolbarRenderer.IconPack = ToolbarIconPack;
        _toolbarRenderer.IconResolver = key =>
            _iconCache.TryGetValue(key, out var bmp) ? bmp : null;
        WireToolbarCallbacks(definition);
        InvalidateCanvas();
    }

    private void WireToolbarCallbacks(ToolbarDefinition def)
    {
        string[] fontFamilies =
        [
            "Default", "Arial", "Times New Roman", "Courier New",
            "Georgia", "Verdana", "Trebuchet MS", "Comic Sans MS",
        ];
        string[] fontSizes =
        [
            "8", "9", "10", "11", "12", "14", "16", "18",
            "20", "24", "28", "32", "36", "48", "72",
        ];
        string[] headingLevels = ["Normal", "H1", "H2", "H3", "H4", "H5", "H6"];

        foreach (var item in def.AllItems)
        {
            if (item is ToolbarDropdown dd)
            {
                switch (dd.Icon)
                {
                    case "font-family":
                        dd.Items.Clear();
                        foreach (var f in fontFamilies) dd.Items.Add(f);
                        dd.SelectedIndex = 0;
                        dd.OnSelectionChanged = idx =>
                        {
                            if ((uint)idx < (uint)dd.Items.Count)
                                _fontFormattingEngine.ApplyFontFamily(_document, _inputController.Selection.Range, dd.Items[idx] == "Default" ? "" : dd.Items[idx]);
                            InvalidateCanvas();
                        };
                        break;

                    case "font-size":
                        dd.Items.Clear();
                        foreach (var s in fontSizes) dd.Items.Add(s);
                        dd.SelectedIndex = 4; // 12pt default
                        dd.OnSelectionChanged = idx =>
                        {
                            if ((uint)idx < (uint)dd.Items.Count && float.TryParse(dd.Items[idx], out float sz))
                                _fontFormattingEngine.ApplyFontSize(_document, _inputController.Selection.Range, sz);
                            InvalidateCanvas();
                        };
                        break;

                    case "heading":
                        dd.Items.Clear();
                        foreach (var h in headingLevels) dd.Items.Add(h);
                        dd.SelectedIndex = 0;
                        dd.OnSelectionChanged = idx =>
                        {
                            if ((uint)idx < (uint)dd.Items.Count)
                                ParagraphFormattingEngine.SetHeadingLevel(_document, _inputController.Selection.Range, idx);
                            InvalidateCanvas();
                        };
                        break;

                    case "edit-actions":
                        dd.Items.Clear();
                        dd.Items.Add("Copy");
                        dd.Items.Add("Cut");
                        dd.Items.Add("Paste");
                        dd.Items.Add("Select All");
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
                        dd.Items.Clear();
                        dd.Items.Add("Open Link");
                        dd.Items.Add("Remove Link");
                        dd.Items.Add("Remove Image");
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
                        dd.Items.Clear();
                        dd.Items.Add("Insert Table...");
                        dd.Items.Add("Insert Row Above");
                        dd.Items.Add("Insert Row Below");
                        dd.Items.Add("Delete Row");
                        dd.Items.Add("Insert Column Left");
                        dd.Items.Add("Insert Column Right");
                        dd.Items.Add("Delete Column");
                        dd.Items.Add("Delete Table");
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
                            _fontFormattingEngine.ApplyTextColor(_document, _inputController.Selection.Range, color);
                            InvalidateCanvas();
                        };
                        break;

                    case "highlight-color":
                        cp.OnColorSelected = color =>
                        {
                            _fontFormattingEngine.ApplyHighlightColor(_document, _inputController.Selection.Range, color);
                            InvalidateCanvas();
                        };
                        break;
                }
            }
        }
    }

    private void ExecuteToolbarCommand(IEditorCommand command)
    {
        var ctx = GetEditorContext();
        if (!command.CanExecute(ctx))
            return;

        command.Execute(ctx);

        // Re-normalize the selection after the command may have split or merged inlines,
        // which can leave the stored (inlineIndex, offset) pair pointing past the end of
        // a run or into a run that no longer exists.
        var anchor = DocumentEditor.NormalizePosition(_document, _inputController.Selection.Anchor);
        var active = DocumentEditor.NormalizePosition(_document, _inputController.Selection.Active);
        _inputController.Selection.Set(anchor, active);

        _caret.MoveTo(_inputController.Selection.Active);
        RaiseSelectionChanged();
        InvalidateCanvas();
        _keyboardProxy.Focus();
    }

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

    /// <summary>Platform handler used to open hyperlinks when activation is not cancelled.</summary>
    public IHyperlinkHandler HyperlinkHandler { get; set; } = new MauiHyperlinkHandler();

    /// <summary>Platform image loader used by the renderer for document images.</summary>
    public FormattingImageProvider ImageProvider { get; set; } = new MauiImageProvider();

    // ── Constructor ──────────────────────────────────────────────────────────────

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
        _inputController.ClipboardProvider = new MauiClipboardProvider();

        _toolbarRenderer = new ToolbarRenderer(ToolbarDefinition.CreateDefault());
        _toolbarRenderer.IconPack = ToolbarIconPack;
        _toolbarRenderer.IconResolver = key =>
            _iconCache.TryGetValue(key, out var bmp) ? bmp : null;
        WireToolbarCallbacks(_toolbarRenderer.Definition);

        var grid = new Grid();
        grid.Add(_canvas);
        grid.Add(_keyboardProxy);
        Content = grid;
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
    }

    /// <summary>Appends the parsed HTML at the end of the current document.</summary>
    public void AppendHtml(string html)
    {
        _document.AppendHtml(html);
        InvalidateCanvas();
    }

    // ── Undo / Redo ──────────────────────────────────────────────────────────────

    public void Undo()
    {
        if (_undoManager.CanUndo)
        {
            var pos = _undoManager.Undo();
            if (pos.HasValue) { _inputController.Selection.CollapseTo(pos.Value); _caret.MoveTo(pos.Value); }
            InvalidateCanvas();
        }
    }

    public void Redo()
    {
        if (_undoManager.CanRedo)
        {
            var pos = _undoManager.Redo();
            if (pos.HasValue) { _inputController.Selection.CollapseTo(pos.Value); _caret.MoveTo(pos.Value); }
            InvalidateCanvas();
        }
    }

    // ── Insert Hyperlink / Image ──────────────────────────────────────────────────

    private async Task HandleInsertHyperlinkAsync()
    {
        var page = GetHostPage();
        if (page == null) return;

        string? url = await page.DisplayPromptAsync(
            "Insert Hyperlink",
            "Enter URL:",
            accept: "Insert",
            cancel: "Cancel",
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

            source = await page.DisplayPromptAsync(
                "Insert Image",
                "Enter image URL or path:",
                accept: "Insert",
                cancel: "Cancel",
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

        string? rowsInput = await page.DisplayPromptAsync(
            "Insert Table",
            "Rows:",
            accept: "Next",
            cancel: "Cancel",
            initialValue: "2",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(rowsInput) || !int.TryParse(rowsInput, out int rows) || rows < 1)
            return;

        string? colsInput = await page.DisplayPromptAsync(
            "Insert Table",
            "Columns:",
            accept: "Insert",
            cancel: "Cancel",
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

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is not null)
        {
            StartBlinkTimer();
            _ = LoadToolbarIconsAsync();
        }
        else
        {
            StopBlinkTimer();
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

            if (ImageProvider is not null)
            {
                try
                {
                    var loaded = await ImageProvider.LoadImageAsync(source).ConfigureAwait(false);
                    if (loaded is SKBitmap providerBitmap)
                        bmp = providerBitmap;
                }
                catch
                {
                    // Fall back to built-in source resolvers below.
                }
            }

            // Prefer app-package assets, then fall back to absolute/relative file paths.
            if (!source.Contains('/') && !source.Contains('\\'))
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
            _toolbarRenderer.UpdateToggleStates(GetEditorContext());
            _toolbarRenderer.Render(dc, new EditorRect(0, 0, logicalW, logicalH));
            toolbarH = _toolbarRenderer.ToolbarHeight;
        }

        var viewport = new EditorRect(0, toolbarH, logicalW, logicalH - toolbarH);

        // Configure renderer and caret renderer from the current theme
        _renderer.BackgroundColor         = theme.Background;
        _renderer.DefaultTextColor        = theme.DefaultTextColor;
        _renderer.PreferThemeTextColorForDefaultBlack = true;
        _renderer.ScrollOffset            = _scrollOffset;
        _renderer.ScrollbarWidth          = theme.ScrollbarWidth;
        _renderer.ScrollbarTrackColor     = theme.ScrollbarTrackColor;
        _renderer.ScrollbarThumbColor     = theme.ScrollbarThumbColor;
        _renderer.LineNumbersGutterWidth  = theme.LineNumbersGutterWidth;
        _renderer.LineNumbersGutterColor  = theme.LineNumbersGutterColor;
        _renderer.LineNumbersTextColor    = theme.LineNumbersTextColor;
        _caretRenderer.CaretColor         = theme.CaretColor;
        _caretRenderer.CaretWidth         = theme.CaretWidth;
        _selectionRenderer.SelectionColor = theme.SelectionHighlightColor;
        _layoutEngine.HyperlinkTextColor = theme.HyperlinkColor;
        _layoutEngine.UnderlineHyperlinks = true;

        // Layout → render (with selection behind text) → caret
        _lastLayout = _layoutCache.GetLayout(_document, logicalW, dc);

        // Wire selection into the pre-text layer so highlights appear behind text.
        var currentSelection = _inputController.Selection;
        var currentLayout    = _lastLayout;
        _renderer.PreTextLayer = currentSelection.IsEmpty ? null : ctx =>
            _selectionRenderer.Render(currentSelection, currentLayout, _document, ctx);
        _renderer.ImageResolver = ResolveDocumentImage;

        _renderer.Render(_lastLayout, viewport, dc);

        // Caret only — selection is now rendered inside the document renderer.
        dc.Save();
        dc.ClipRect(viewport);
        dc.Translate(viewport.X, viewport.Y - _scrollOffset);

        if (_selectedImage is not null)
            RenderImageSelectionHandles(dc);

        if (!IsReadOnly)
        {
            _caret.UpdateBlink();
            _caretRenderer.Render(_caret, _lastLayout, _document, dc);
        }

        dc.Restore();

        // Placeholder — shown only when the document is an empty single paragraph
        if (_document.Blocks.Count == 1
            && _document.Blocks[0] is Paragraph p && p.Inlines.Count == 0
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
    }

    // ── Touch ────────────────────────────────────────────────────────────────────

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (IsReadOnly) return;

        float scale    = GetDisplayScale();
        float rawX     = e.Location.X / scale;
        float rawY     = e.Location.Y / scale;
        float toolbarH = ShowToolbar ? _toolbarRenderer.ToolbarHeight : 0f;

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
                InvalidateCanvas();
                RaiseSelectionChanged();
                break;
            }
            case SKTouchAction.Moved when e.InContact:
            {
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
        }

        e.Handled = true;
    }

    // ── Blink Timer ──────────────────────────────────────────────────────────────

    private void StartBlinkTimer()
    {
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

    public new bool Focus()
    {
        _keyboardProxy.Focus();
        var result = base.Focus();
        EditorFocusChanged?.Invoke(this, new FocusChangedEventArgs(true));
        return result;
    }

    public new void Unfocus()
    {
        base.Unfocus();
        EditorFocusChanged?.Invoke(this, new FocusChangedEventArgs(false));
    }

    // ── Document changes ──────────────────────────────────────────────────────────

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        _layoutCache.OnDocumentChanged(_document, e);
        TextChanged?.Invoke(this, new EditorTextChangedEventArgs(e.AffectedRange));
        InvalidateCanvas();
    }

    private void RaiseSelectionChanged()
    {
        var sel = _inputController.Selection;
        SelectionChanged?.Invoke(this,
            new EditorSelectionChangedEventArgs(sel.Anchor, sel.Active, sel.IsEmpty));
        RaiseHyperlinkSelectionChanged(sel.Active);
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
            InvalidateCanvas();
            RaiseSelectionChanged();
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
