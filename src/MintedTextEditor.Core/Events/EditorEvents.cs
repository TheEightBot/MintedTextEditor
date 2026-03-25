using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Events;

// ── Base ─────────────────────────────────────────────────────────────────────

/// <summary>Base class for all editor event arguments.</summary>
public abstract class EditorEventArgs : EventArgs { }

// ── Selection ─────────────────────────────────────────────────────────────────

public sealed class EditorSelectionChangedEventArgs : EditorEventArgs
{
    public DocumentPosition Anchor { get; }
    public DocumentPosition Active { get; }
    public bool IsEmpty { get; }

    public EditorSelectionChangedEventArgs(DocumentPosition anchor, DocumentPosition active, bool isEmpty)
    {
        Anchor = anchor;
        Active = active;
        IsEmpty = isEmpty;
    }
}

// ── Content ───────────────────────────────────────────────────────────────────

public sealed class EditorTextChangedEventArgs : EditorEventArgs
{
    public TextRange AffectedRange { get; }

    public EditorTextChangedEventArgs(TextRange affectedRange) =>
        AffectedRange = affectedRange;
}

public sealed class ContentLoadedEventArgs : EditorEventArgs { }

// ── Font ──────────────────────────────────────────────────────────────────────

public sealed class FontFamilyChangedEventArgs : EditorEventArgs
{
    public string FontFamily { get; }

    public FontFamilyChangedEventArgs(string fontFamily) => FontFamily = fontFamily;
}

public sealed class FontSizeChangedEventArgs : EditorEventArgs
{
    public float FontSize { get; }

    public FontSizeChangedEventArgs(float fontSize) => FontSize = fontSize;
}

// ── Character Attributes ──────────────────────────────────────────────────────

public sealed class FontAttributesChangedEventArgs : EditorEventArgs
{
    public bool IsBold { get; }
    public bool IsItalic { get; }
    public bool IsSubscript { get; }
    public bool IsSuperscript { get; }

    public FontAttributesChangedEventArgs(bool isBold, bool isItalic, bool isSubscript, bool isSuperscript)
    {
        IsBold = isBold;
        IsItalic = isItalic;
        IsSubscript = isSubscript;
        IsSuperscript = isSuperscript;
    }
}

public sealed class TextDecorationsChangedEventArgs : EditorEventArgs
{
    public bool IsUnderline { get; }
    public bool IsStrikethrough { get; }

    public TextDecorationsChangedEventArgs(bool isUnderline, bool isStrikethrough)
    {
        IsUnderline = isUnderline;
        IsStrikethrough = isStrikethrough;
    }
}

// ── Paragraph Formatting ──────────────────────────────────────────────────────

public sealed class TextFormattingChangedEventArgs : EditorEventArgs
{
    /// <summary>Heading level (0 = body text) or paragraph style name.</summary>
    public string Format { get; }

    public TextFormattingChangedEventArgs(string format) => Format = format;
}

public sealed class HorizontalTextAlignmentChangedEventArgs : EditorEventArgs
{
    public TextAlignment Alignment { get; }

    public HorizontalTextAlignmentChangedEventArgs(TextAlignment alignment) =>
        Alignment = alignment;
}

public sealed class ListTypeChangedEventArgs : EditorEventArgs
{
    public ListType ListType { get; }

    public ListTypeChangedEventArgs(ListType listType) => ListType = listType;
}

// ── Colours ───────────────────────────────────────────────────────────────────

public sealed class TextColorChangedEventArgs : EditorEventArgs
{
    public EditorColor Color { get; }

    public TextColorChangedEventArgs(EditorColor color) => Color = color;
}

public sealed class HighlightTextColorChangedEventArgs : EditorEventArgs
{
    public EditorColor Color { get; }

    public HighlightTextColorChangedEventArgs(EditorColor color) => Color = color;
}

// ── Hyperlink ─────────────────────────────────────────────────────────────────

public sealed class HyperlinkClickedEventArgs : EditorEventArgs
{
    public string Url { get; }
    public bool Cancel { get; set; }

    public HyperlinkClickedEventArgs(string url) => Url = url;
}

public sealed class IsHyperlinkSelectedChangedEventArgs : EditorEventArgs
{
    public bool IsSelected { get; }
    public string? Url { get; }

    public IsHyperlinkSelectedChangedEventArgs(bool isSelected, string? url = null)
    {
        IsSelected = isSelected;
        Url = url;
    }
}

// ── Read-Only ─────────────────────────────────────────────────────────────────

public sealed class IsReadOnlyChangedEventArgs : EditorEventArgs
{
    public bool IsReadOnly { get; }

    public IsReadOnlyChangedEventArgs(bool isReadOnly) => IsReadOnly = isReadOnly;
}

// ── Image ─────────────────────────────────────────────────────────────────────

public sealed class ImageInsertedEventArgs : EditorEventArgs
{
    public ImageInline Image { get; }

    public ImageInsertedEventArgs(ImageInline image) => Image = image;
}

public sealed class ImageRemovedEventArgs : EditorEventArgs
{
    public string Source { get; }

    public ImageRemovedEventArgs(string source) => Source = source;
}

public sealed class ImageRequestedEventArgs : EditorEventArgs
{
    public string? Source { get; set; }
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

    public FocusChangedEventArgs(bool isFocused) => IsFocused = isFocused;
}
