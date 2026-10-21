using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Events;

// ── Base ─────────────────────────────────────────────────────────────────────

/// <summary>Base class for all editor event arguments.</summary>
public abstract class EditorEventArgs : EventArgs { }

// ── Selection ─────────────────────────────────────────────────────────────────

/// <summary>Event data raised when the caret or selection moves.</summary>
public sealed class EditorSelectionChangedEventArgs : EditorEventArgs
{
    /// <summary>Fixed end of the selection, where it started.</summary>
    public DocumentPosition Anchor { get; }
    /// <summary>Moving end of the selection, at the caret.</summary>
    public DocumentPosition Active { get; }
    /// <summary>True when no text is selected (anchor and active positions are equal).</summary>
    public bool IsEmpty { get; }

    /// <summary>Creates selection change event data.</summary>
    /// <param name="anchor">Fixed end of the selection.</param>
    /// <param name="active">Moving end of the selection.</param>
    /// <param name="isEmpty">Whether the selection is empty.</param>
    public EditorSelectionChangedEventArgs(DocumentPosition anchor, DocumentPosition active, bool isEmpty)
    {
        Anchor = anchor;
        Active = active;
        IsEmpty = isEmpty;
    }
}

// ── Content ───────────────────────────────────────────────────────────────────

/// <summary>Event data raised when the document content changes.</summary>
public sealed class EditorTextChangedEventArgs : EditorEventArgs
{
    /// <summary>The range of the document affected by the change.</summary>
    public TextRange AffectedRange { get; }

    /// <summary>Creates text change event data.</summary>
    /// <param name="affectedRange">The range affected by the change.</param>
    public EditorTextChangedEventArgs(TextRange affectedRange) =>
        AffectedRange = affectedRange;
}

/// <summary>Event data signalling that document content has been loaded (for example by LoadHtml or LoadMarkdown).</summary>
public sealed class ContentLoadedEventArgs : EditorEventArgs { }

// ── Font ──────────────────────────────────────────────────────────────────────

/// <summary>Event data for a change of the current font family.</summary>
public sealed class FontFamilyChangedEventArgs : EditorEventArgs
{
    /// <summary>The new font family name.</summary>
    public string FontFamily { get; }

    /// <summary>Creates font family change event data.</summary>
    /// <param name="fontFamily">The new font family name.</param>
    public FontFamilyChangedEventArgs(string fontFamily) => FontFamily = fontFamily;
}

/// <summary>Event data for a change of the current font size.</summary>
public sealed class FontSizeChangedEventArgs : EditorEventArgs
{
    /// <summary>The new font size.</summary>
    public float FontSize { get; }

    /// <summary>Creates font size change event data.</summary>
    /// <param name="fontSize">The new font size.</param>
    public FontSizeChangedEventArgs(float fontSize) => FontSize = fontSize;
}

// ── Character Attributes ──────────────────────────────────────────────────────

/// <summary>Event data for a change of the current bold, italic, subscript or superscript state.</summary>
public sealed class FontAttributesChangedEventArgs : EditorEventArgs
{
    /// <summary>True when bold is applied.</summary>
    public bool IsBold { get; }
    /// <summary>True when italic is applied.</summary>
    public bool IsItalic { get; }
    /// <summary>True when subscript is applied.</summary>
    public bool IsSubscript { get; }
    /// <summary>True when superscript is applied.</summary>
    public bool IsSuperscript { get; }

    /// <summary>Creates font attribute change event data.</summary>
    /// <param name="isBold">Whether bold is applied.</param>
    /// <param name="isItalic">Whether italic is applied.</param>
    /// <param name="isSubscript">Whether subscript is applied.</param>
    /// <param name="isSuperscript">Whether superscript is applied.</param>
    public FontAttributesChangedEventArgs(bool isBold, bool isItalic, bool isSubscript, bool isSuperscript)
    {
        IsBold = isBold;
        IsItalic = isItalic;
        IsSubscript = isSubscript;
        IsSuperscript = isSuperscript;
    }
}

/// <summary>Event data for a change of the current underline or strikethrough state.</summary>
public sealed class TextDecorationsChangedEventArgs : EditorEventArgs
{
    /// <summary>True when underline is applied.</summary>
    public bool IsUnderline { get; }
    /// <summary>True when strikethrough is applied.</summary>
    public bool IsStrikethrough { get; }

    /// <summary>Creates text decoration change event data.</summary>
    /// <param name="isUnderline">Whether underline is applied.</param>
    /// <param name="isStrikethrough">Whether strikethrough is applied.</param>
    public TextDecorationsChangedEventArgs(bool isUnderline, bool isStrikethrough)
    {
        IsUnderline = isUnderline;
        IsStrikethrough = isStrikethrough;
    }
}

// ── Paragraph Formatting ──────────────────────────────────────────────────────

/// <summary>Event data for a change of the current paragraph format (heading level or paragraph style).</summary>
public sealed class TextFormattingChangedEventArgs : EditorEventArgs
{
    /// <summary>
    /// The paragraph format name: "Normal", "Heading1"–"Heading6" or "Quote"
    /// (see <see cref="FormattingState.ParagraphFormat"/>).
    /// </summary>
    public string Format { get; }

    /// <summary>Creates paragraph format change event data.</summary>
    /// <param name="format">The paragraph format name.</param>
    public TextFormattingChangedEventArgs(string format) => Format = format;
}

/// <summary>Event data for a change of the current paragraph alignment.</summary>
public sealed class HorizontalTextAlignmentChangedEventArgs : EditorEventArgs
{
    /// <summary>The new horizontal alignment.</summary>
    public TextAlignment Alignment { get; }

    /// <summary>Creates alignment change event data.</summary>
    /// <param name="alignment">The new horizontal alignment.</param>
    public HorizontalTextAlignmentChangedEventArgs(TextAlignment alignment) =>
        Alignment = alignment;
}

/// <summary>Event data for a change of the current paragraph's list type.</summary>
public sealed class ListTypeChangedEventArgs : EditorEventArgs
{
    /// <summary>The new list type.</summary>
    public ListType ListType { get; }

    /// <summary>Creates list type change event data.</summary>
    /// <param name="listType">The new list type.</param>
    public ListTypeChangedEventArgs(ListType listType) => ListType = listType;
}

// ── Colours ───────────────────────────────────────────────────────────────────

/// <summary>Event data for a change of the current text colour.</summary>
public sealed class TextColorChangedEventArgs : EditorEventArgs
{
    /// <summary>The new text colour.</summary>
    public EditorColor Color { get; }

    /// <summary>Creates text colour change event data.</summary>
    /// <param name="color">The new text colour.</param>
    public TextColorChangedEventArgs(EditorColor color) => Color = color;
}

/// <summary>Event data for a change of the current highlight (background) colour.</summary>
public sealed class HighlightTextColorChangedEventArgs : EditorEventArgs
{
    /// <summary>The new highlight colour.</summary>
    public EditorColor Color { get; }

    /// <summary>Creates highlight colour change event data.</summary>
    /// <param name="color">The new highlight colour.</param>
    public HighlightTextColorChangedEventArgs(EditorColor color) => Color = color;
}

// ── Hyperlink ─────────────────────────────────────────────────────────────────

/// <summary>Event data raised when the user taps or clicks a hyperlink.</summary>
public sealed class HyperlinkClickedEventArgs : EditorEventArgs
{
    /// <summary>The URL of the activated hyperlink.</summary>
    public string Url { get; }
    /// <summary>Set to true to stop the editor from opening the URL through its hyperlink handler.</summary>
    public bool Cancel { get; set; }

    /// <summary>Creates hyperlink click event data.</summary>
    /// <param name="url">The URL of the activated hyperlink.</param>
    public HyperlinkClickedEventArgs(string url) => Url = url;
}

/// <summary>Event data raised when the caret or selection enters or leaves a hyperlink.</summary>
public sealed class IsHyperlinkSelectedChangedEventArgs : EditorEventArgs
{
    /// <summary>True when the caret is inside a hyperlink.</summary>
    public bool IsSelected { get; }
    /// <summary>The URL of the hyperlink at the caret, or null when <see cref="IsSelected"/> is false.</summary>
    public string? Url { get; }

    /// <summary>Creates hyperlink selection change event data.</summary>
    /// <param name="isSelected">Whether the caret is inside a hyperlink.</param>
    /// <param name="url">The hyperlink's URL, if any.</param>
    public IsHyperlinkSelectedChangedEventArgs(bool isSelected, string? url = null)
    {
        IsSelected = isSelected;
        Url = url;
    }
}

// ── Read-Only ─────────────────────────────────────────────────────────────────

/// <summary>Event data for a change of the editor's read-only state.</summary>
public sealed class IsReadOnlyChangedEventArgs : EditorEventArgs
{
    /// <summary>True when the editor is now read-only.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Creates read-only change event data.</summary>
    /// <param name="isReadOnly">The new read-only state.</param>
    public IsReadOnlyChangedEventArgs(bool isReadOnly) => IsReadOnly = isReadOnly;
}

// ── Image ─────────────────────────────────────────────────────────────────────

/// <summary>Event data for an image inserted into the document.</summary>
public sealed class ImageInsertedEventArgs : EditorEventArgs
{
    /// <summary>The inserted image inline.</summary>
    public ImageInline Image { get; }

    /// <summary>Creates image insertion event data.</summary>
    /// <param name="image">The inserted image inline.</param>
    public ImageInsertedEventArgs(ImageInline image) => Image = image;
}

/// <summary>Event data for an image removed from the document.</summary>
public sealed class ImageRemovedEventArgs : EditorEventArgs
{
    /// <summary>The source URI or resource key of the removed image.</summary>
    public string Source { get; }

    /// <summary>Creates image removal event data.</summary>
    /// <param name="source">The source of the removed image.</param>
    public ImageRemovedEventArgs(string source) => Source = source;
}

/// <summary>
/// Event data raised when the user asks to insert an image, so the host can supply the source.
/// If not handled, the editor prompts the user for an image URL or path.
/// </summary>
public sealed class ImageRequestedEventArgs : EditorEventArgs
{
    /// <summary>The image URI or path to insert; set by the handler.</summary>
    public string? Source { get; set; }
    /// <summary>Set to true, together with a non-empty <see cref="Source"/>, to skip the editor's own prompt.</summary>
    public bool Handled { get; set; }
}

// ── Find / Replace ───────────────────────────────────────────────────────────

/// <summary>
/// Raised whenever a find operation completes.  <see cref="MatchCount"/> is 0
/// when no matches were found.
/// </summary>
public sealed class FindReplaceResultEventArgs : EditorEventArgs
{
    /// <summary>The term that was searched.</summary>
    public string SearchTerm { get; }

    /// <summary>Total number of matches found in the document.</summary>
    public int MatchCount { get; }

    /// <summary>1-based index of the currently highlighted match, or 0 when no match.</summary>
    public int CurrentMatchIndex { get; }

    /// <summary>Creates find result event data.</summary>
    /// <param name="searchTerm">The term that was searched.</param>
    /// <param name="matchCount">Total number of matches.</param>
    /// <param name="currentMatchIndex">1-based index of the current match, or 0 when none.</param>
    public FindReplaceResultEventArgs(string searchTerm, int matchCount, int currentMatchIndex)
    {
        SearchTerm        = searchTerm;
        MatchCount        = matchCount;
        CurrentMatchIndex = currentMatchIndex;
    }
}

// ── Focus ────────────────────────────────────────────────────────────────────

/// <summary>Raised when the editor gains or loses input focus.</summary>
public sealed class FocusChangedEventArgs : EditorEventArgs
{
    /// <summary><c>true</c> if the editor just received focus; <c>false</c> if it lost focus.</summary>
    public bool IsFocused { get; }

    /// <summary>Creates focus change event data.</summary>
    /// <param name="isFocused">Whether the editor now has focus.</param>
    public FocusChangedEventArgs(bool isFocused) => IsFocused = isFocused;
}
