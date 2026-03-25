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

    /// <summary>Gets or sets the active string provider. Defaults to built-in English strings.</summary>
    public static EditorStrings Current
    {
        get => _current;
        set => _current = value ?? throw new ArgumentNullException(nameof(value));
    }

    // ── Toolbar ─────────────────────────────────────────────────────────

    public virtual string Bold           => "Bold";
    public virtual string Italic         => "Italic";
    public virtual string Underline      => "Underline";
    public virtual string Strikethrough  => "Strikethrough";
    public virtual string AlignLeft      => "Align left";
    public virtual string AlignCenter    => "Align center";
    public virtual string AlignRight     => "Align right";
    public virtual string AlignJustify   => "Justify";
    public virtual string BulletList     => "Bullet list";
    public virtual string NumberedList   => "Numbered list";
    public virtual string IndentIncrease => "Increase indent";
    public virtual string IndentDecrease => "Decrease indent";
    public virtual string InsertLink     => "Insert hyperlink";
    public virtual string InsertImage    => "Insert image";
    public virtual string InsertTable    => "Insert table";
    public virtual string Undo           => "Undo";
    public virtual string Redo           => "Redo";
    public virtual string FontFamily     => "Font family";
    public virtual string FontSize       => "Font size";
    public virtual string TextColor      => "Text color";
    public virtual string Highlight      => "Highlight color";
    public virtual string ClearFormat    => "Clear formatting";

    // ── Heading levels ───────────────────────────────────────────────────

    public virtual string Heading1 => "Heading 1";
    public virtual string Heading2 => "Heading 2";
    public virtual string Heading3 => "Heading 3";
    public virtual string Heading4 => "Heading 4";
    public virtual string Heading5 => "Heading 5";
    public virtual string Heading6 => "Heading 6";
    public virtual string NormalText => "Normal text";

    // ── Context menu ─────────────────────────────────────────────────────

    public virtual string Cut              => "Cut";
    public virtual string Copy             => "Copy";
    public virtual string Paste            => "Paste";
    public virtual string SelectAll        => "Select all";
    public virtual string EditLink         => "Edit hyperlink…";
    public virtual string RemoveLink       => "Remove hyperlink";
    public virtual string OpenLink         => "Open hyperlink";
    public virtual string InsertRowAbove   => "Insert row above";
    public virtual string InsertRowBelow   => "Insert row below";
    public virtual string InsertColumnLeft => "Insert column to the left";
    public virtual string InsertColumnRight=> "Insert column to the right";
    public virtual string DeleteRow        => "Delete row";
    public virtual string DeleteColumn     => "Delete column";
    public virtual string DeleteTable      => "Delete table";

    // ── Dialog / prompt strings ──────────────────────────────────────────

    public virtual string HyperlinkDialogTitle        => "Insert hyperlink";
    public virtual string HyperlinkDialogUrlLabel     => "URL";
    public virtual string HyperlinkDialogTextLabel    => "Display text";
    public virtual string HyperlinkDialogOk           => "OK";
    public virtual string HyperlinkDialogCancel       => "Cancel";
    public virtual string HyperlinkDialogInvalidUrl   => "Please enter a valid URL.";

    // ── Accessibility labels ─────────────────────────────────────────────

    public virtual string EditorAccessibilityLabel    => "Rich text editor";
    public virtual string EditorAccessibilityHint     => "Double-tap to edit text";
    public virtual string ToolbarAccessibilityLabel   => "Formatting toolbar";
    public virtual string ContextMenuAccessibilityLabel => "Context menu";
}
