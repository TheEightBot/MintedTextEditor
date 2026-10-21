namespace MintedTextEditor.Core.Localization;

/// <summary>
/// Default English UI strings used throughout the editor.
/// Callers may replace <see cref="Current"/> with a custom implementation to
/// provide localized strings without taking a dependency on any specific
/// localization framework.
/// </summary>
public class EditorStrings
{
    private static EditorStrings _current = new();

    /// <summary>
    /// Gets or sets the active string provider. Defaults to built-in English strings.
    /// The built-in toolbar labels are resolved from this value each time they are read, context
    /// menus and prompts read it when they are shown, and <see cref="CurrentChanged"/> is raised
    /// when it is replaced so views can redraw.
    /// </summary>
    public static EditorStrings Current
    {
        get => _current;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_current, value)) return;
            _current = value;
            CurrentChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Raised after <see cref="Current"/> is replaced with a different instance. The sender is null.
    /// Handlers may run on any thread (the thread that set <see cref="Current"/>).
    /// </summary>
    public static event EventHandler? CurrentChanged;

    // ── Toolbar ─────────────────────────────────────────────────────────

    /// <summary>Toolbar label for the bold command. Default: "Bold"</summary>
    public virtual string Bold           => "Bold";
    /// <summary>Toolbar label for the italic command. Default: "Italic"</summary>
    public virtual string Italic         => "Italic";
    /// <summary>Toolbar label for the underline command. Default: "Underline"</summary>
    public virtual string Underline      => "Underline";
    /// <summary>Toolbar label for the strikethrough command. Default: "Strikethrough"</summary>
    public virtual string Strikethrough  => "Strikethrough";
    /// <summary>Toolbar label for left alignment. Default: "Align left"</summary>
    public virtual string AlignLeft      => "Align left";
    /// <summary>Toolbar label for center alignment. Default: "Align center"</summary>
    public virtual string AlignCenter    => "Align center";
    /// <summary>Toolbar label for right alignment. Default: "Align right"</summary>
    public virtual string AlignRight     => "Align right";
    /// <summary>Toolbar label for justified alignment. Default: "Justify"</summary>
    public virtual string AlignJustify   => "Justify";
    /// <summary>Toolbar label for the bulleted list command. Default: "Bullet list"</summary>
    public virtual string BulletList     => "Bullet list";
    /// <summary>Toolbar label for the numbered list command. Default: "Numbered list"</summary>
    public virtual string NumberedList   => "Numbered list";
    /// <summary>Toolbar label for increasing the indent. Default: "Increase indent"</summary>
    public virtual string IndentIncrease => "Increase indent";
    /// <summary>Toolbar label for decreasing the indent. Default: "Decrease indent"</summary>
    public virtual string IndentDecrease => "Decrease indent";
    /// <summary>Toolbar label for inserting a hyperlink. Default: "Insert hyperlink"</summary>
    public virtual string InsertLink     => "Insert hyperlink";
    /// <summary>Toolbar label for inserting an image. Default: "Insert image"</summary>
    public virtual string InsertImage    => "Insert image";
    /// <summary>Toolbar label for inserting a table. Default: "Insert table"</summary>
    public virtual string InsertTable    => "Insert table";
    /// <summary>Toolbar label for the undo command. Default: "Undo"</summary>
    public virtual string Undo           => "Undo";
    /// <summary>Toolbar label for the redo command. Default: "Redo"</summary>
    public virtual string Redo           => "Redo";
    /// <summary>Toolbar label for the font family picker. Default: "Font family"</summary>
    public virtual string FontFamily     => "Font family";
    /// <summary>Toolbar label for the font size picker. Default: "Font size"</summary>
    public virtual string FontSize       => "Font size";
    /// <summary>Toolbar label for the text colour picker. Default: "Text color"</summary>
    public virtual string TextColor      => "Text color";
    /// <summary>Toolbar label for the highlight colour picker. Default: "Highlight color"</summary>
    public virtual string Highlight      => "Highlight color";
    /// <summary>Toolbar label for the clear formatting command. Default: "Clear formatting"</summary>
    public virtual string ClearFormat    => "Clear formatting";
    /// <summary>Toolbar label for the subscript command. Default: "Subscript"</summary>
    public virtual string Subscript      => "Subscript";
    /// <summary>Toolbar label for the superscript command. Default: "Superscript"</summary>
    public virtual string Superscript    => "Superscript";
    /// <summary>Toolbar label for the heading level picker. Default: "Heading level"</summary>
    public virtual string HeadingLevel   => "Heading level";
    /// <summary>Toolbar label for the edit actions menu (copy, cut, paste, select all). Default: "Edit"</summary>
    public virtual string EditMenu       => "Edit";
    /// <summary>Toolbar label for the object actions menu (hyperlink and image actions). Default: "Object"</summary>
    public virtual string ObjectMenu     => "Object";
    /// <summary>Toolbar label for the table actions menu. Default: "Table"</summary>
    public virtual string TableMenu      => "Table";
    /// <summary>Font family option that clears the font family (uses the editor default). Default: "Default"</summary>
    public virtual string DefaultFontFamily => "Default";
    /// <summary>Object menu item that removes the selected image. Default: "Remove image"</summary>
    public virtual string RemoveImage    => "Remove image";
    /// <summary>Table menu item that opens the insert table prompt. Default: "Insert table…"</summary>
    public virtual string InsertTableEllipsis => "Insert table…";

    // ── Heading levels ───────────────────────────────────────────────────

    /// <summary>Name of the heading level 1 paragraph style. Default: "Heading 1"</summary>
    public virtual string Heading1 => "Heading 1";
    /// <summary>Name of the heading level 2 paragraph style. Default: "Heading 2"</summary>
    public virtual string Heading2 => "Heading 2";
    /// <summary>Name of the heading level 3 paragraph style. Default: "Heading 3"</summary>
    public virtual string Heading3 => "Heading 3";
    /// <summary>Name of the heading level 4 paragraph style. Default: "Heading 4"</summary>
    public virtual string Heading4 => "Heading 4";
    /// <summary>Name of the heading level 5 paragraph style. Default: "Heading 5"</summary>
    public virtual string Heading5 => "Heading 5";
    /// <summary>Name of the heading level 6 paragraph style. Default: "Heading 6"</summary>
    public virtual string Heading6 => "Heading 6";
    /// <summary>Name of the normal (body text) paragraph style. Default: "Normal text"</summary>
    public virtual string NormalText => "Normal text";

    // ── Context menu ─────────────────────────────────────────────────────

    /// <summary>Context menu item that cuts the selection. Default: "Cut"</summary>
    public virtual string Cut              => "Cut";
    /// <summary>Context menu item that copies the selection. Default: "Copy"</summary>
    public virtual string Copy             => "Copy";
    /// <summary>Context menu item that pastes from the clipboard. Default: "Paste"</summary>
    public virtual string Paste            => "Paste";
    /// <summary>Context menu item that selects the whole document. Default: "Select all"</summary>
    public virtual string SelectAll        => "Select all";
    /// <summary>Context menu item that edits the hyperlink at the caret. Default: "Edit hyperlink…"</summary>
    public virtual string EditLink         => "Edit hyperlink…";
    /// <summary>Context menu item that removes the hyperlink at the caret. Default: "Remove hyperlink"</summary>
    public virtual string RemoveLink       => "Remove hyperlink";
    /// <summary>Context menu item that opens the hyperlink at the caret. Default: "Open hyperlink"</summary>
    public virtual string OpenLink         => "Open hyperlink";
    /// <summary>Context menu item that inserts a table row above the current row. Default: "Insert row above"</summary>
    public virtual string InsertRowAbove   => "Insert row above";
    /// <summary>Context menu item that inserts a table row below the current row. Default: "Insert row below"</summary>
    public virtual string InsertRowBelow   => "Insert row below";
    /// <summary>Context menu item that inserts a table column left of the current column. Default: "Insert column to the left"</summary>
    public virtual string InsertColumnLeft => "Insert column to the left";
    /// <summary>Context menu item that inserts a table column right of the current column. Default: "Insert column to the right"</summary>
    public virtual string InsertColumnRight=> "Insert column to the right";
    /// <summary>Context menu item that deletes the current table row. Default: "Delete row"</summary>
    public virtual string DeleteRow        => "Delete row";
    /// <summary>Context menu item that deletes the current table column. Default: "Delete column"</summary>
    public virtual string DeleteColumn     => "Delete column";
    /// <summary>Context menu item that deletes the current table. Default: "Delete table"</summary>
    public virtual string DeleteTable      => "Delete table";

    // ── Dialog / prompt strings ──────────────────────────────────────────

    /// <summary>Title of the hyperlink prompt. Default: "Insert hyperlink"</summary>
    public virtual string HyperlinkDialogTitle        => "Insert hyperlink";
    /// <summary>Message of the built-in hyperlink prompt. Default: "Enter URL:"</summary>
    public virtual string HyperlinkDialogMessage      => "Enter URL:";
    /// <summary>Confirm button text of the built-in hyperlink prompt. Default: "Insert"</summary>
    public virtual string HyperlinkDialogInsert       => "Insert";
    /// <summary>Cancel button text of the hyperlink prompt. Default: "Cancel"</summary>
    public virtual string HyperlinkDialogCancel       => "Cancel";
    /// <summary>Label of the URL field in a custom hyperlink dialog. Not used by the built-in prompt. Default: "URL"</summary>
    public virtual string HyperlinkDialogUrlLabel     => "URL";
    /// <summary>Label of the display text field in a custom hyperlink dialog. Not used by the built-in prompt. Default: "Display text"</summary>
    public virtual string HyperlinkDialogTextLabel    => "Display text";
    /// <summary>Confirm button text in a custom hyperlink dialog. Not used by the built-in prompt. Default: "OK"</summary>
    public virtual string HyperlinkDialogOk           => "OK";
    /// <summary>Validation message for an invalid URL in a custom hyperlink dialog. Not used by the built-in prompt. Default: "Please enter a valid URL."</summary>
    public virtual string HyperlinkDialogInvalidUrl   => "Please enter a valid URL.";

    /// <summary>Title of the built-in image prompt. Default: "Insert image"</summary>
    public virtual string ImageDialogTitle            => "Insert image";
    /// <summary>Message of the built-in image prompt. Default: "Enter image URL or path:"</summary>
    public virtual string ImageDialogMessage          => "Enter image URL or path:";
    /// <summary>Confirm button text of the built-in image prompt. Default: "Insert"</summary>
    public virtual string ImageDialogInsert           => "Insert";
    /// <summary>Cancel button text of the built-in image prompt. Default: "Cancel"</summary>
    public virtual string ImageDialogCancel           => "Cancel";

    /// <summary>Title of the built-in table prompts. Default: "Insert table"</summary>
    public virtual string TableDialogTitle            => "Insert table";
    /// <summary>Message of the built-in prompt that asks for the number of rows. Default: "Rows:"</summary>
    public virtual string TableDialogRows             => "Rows:";
    /// <summary>Message of the built-in prompt that asks for the number of columns. Default: "Columns:"</summary>
    public virtual string TableDialogColumns          => "Columns:";
    /// <summary>Button text that moves from the rows prompt to the columns prompt. Default: "Next"</summary>
    public virtual string TableDialogNext             => "Next";
    /// <summary>Confirm button text of the columns prompt. Default: "Insert"</summary>
    public virtual string TableDialogInsert           => "Insert";
    /// <summary>Cancel button text of the table prompts. Default: "Cancel"</summary>
    public virtual string TableDialogCancel           => "Cancel";

    // ── Accessibility labels ─────────────────────────────────────────────

    /// <summary>Accessibility label (screen reader name) of the editor surface. Default: "Rich text editor"</summary>
    public virtual string EditorAccessibilityLabel    => "Rich text editor";
    /// <summary>Accessibility hint of the editor surface. Default: "Double-tap to edit text"</summary>
    public virtual string EditorAccessibilityHint     => "Double-tap to edit text";
    /// <summary>Accessibility label of the formatting toolbar. Default: "Formatting toolbar"</summary>
    public virtual string ToolbarAccessibilityLabel   => "Formatting toolbar";
    /// <summary>Accessibility label of the context menu. Default: "Context menu"</summary>
    public virtual string ContextMenuAccessibilityLabel => "Context menu";
}
