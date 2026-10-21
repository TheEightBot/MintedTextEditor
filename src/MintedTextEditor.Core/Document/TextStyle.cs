using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Document;

/// <summary>
/// Immutable text style describing the visual appearance of a text run.
/// Use the <c>With*</c> methods (for example <see cref="WithBold"/>) to create modified copies.
/// </summary>
public sealed class TextStyle : IEquatable<TextStyle>
{
    /// <summary>Shared instance with default style values.</summary>
    public static TextStyle Default { get; } = new();

    /// <summary>Font family name.</summary>
    public string FontFamily { get; }
    /// <summary>Font size.</summary>
    public float FontSize { get; }
    /// <summary>Whether the text is bold.</summary>
    public bool IsBold { get; }
    /// <summary>Whether the text is italic.</summary>
    public bool IsItalic { get; }
    /// <summary>Whether the text is underlined.</summary>
    public bool IsUnderline { get; }
    /// <summary>Whether the text is struck through.</summary>
    public bool IsStrikethrough { get; }
    /// <summary>Whether the text is subscript.</summary>
    public bool IsSubscript { get; }
    /// <summary>Whether the text is superscript.</summary>
    public bool IsSuperscript { get; }
    /// <summary>Foreground text color.</summary>
    public EditorColor TextColor { get; }
    /// <summary>Background highlight color (transparent when none).</summary>
    public EditorColor HighlightColor { get; }
    /// <summary>Vertical offset of the text baseline.</summary>
    public float BaselineOffset { get; }

    /// <summary>
    /// Creates a text style. A <c>null</c> text color defaults to black and a <c>null</c>
    /// highlight color defaults to transparent.
    /// </summary>
    public TextStyle(
        string fontFamily = "Default",
        float fontSize = 14f,
        bool isBold = false,
        bool isItalic = false,
        bool isUnderline = false,
        bool isStrikethrough = false,
        bool isSubscript = false,
        bool isSuperscript = false,
        EditorColor? textColor = null,
        EditorColor? highlightColor = null,
        float baselineOffset = 0f)
    {
        FontFamily = fontFamily;
        FontSize = fontSize;
        IsBold = isBold;
        IsItalic = isItalic;
        IsUnderline = isUnderline;
        IsStrikethrough = isStrikethrough;
        IsSubscript = isSubscript;
        IsSuperscript = isSuperscript;
        TextColor = textColor ?? EditorColor.Black;
        HighlightColor = highlightColor ?? EditorColor.Transparent;
        BaselineOffset = baselineOffset;
    }

    /// <summary>Returns a copy with the given font family.</summary>
    public TextStyle WithFontFamily(string fontFamily) => new(fontFamily, FontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    /// <summary>Returns a copy with the given font size.</summary>
    public TextStyle WithFontSize(float fontSize) => new(FontFamily, fontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    /// <summary>Returns a copy with bold set to the given value.</summary>
    public TextStyle WithBold(bool bold) => new(FontFamily, FontSize, bold, IsItalic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    /// <summary>Returns a copy with italic set to the given value.</summary>
    public TextStyle WithItalic(bool italic) => new(FontFamily, FontSize, IsBold, italic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    /// <summary>Returns a copy with underline set to the given value.</summary>
    public TextStyle WithUnderline(bool underline) => new(FontFamily, FontSize, IsBold, IsItalic, underline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    /// <summary>Returns a copy with strikethrough set to the given value.</summary>
    public TextStyle WithStrikethrough(bool strikethrough) => new(FontFamily, FontSize, IsBold, IsItalic, IsUnderline, strikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    /// <summary>Returns a copy with subscript set to the given value and superscript cleared.</summary>
    public TextStyle WithSubscript(bool subscript) => new(FontFamily, FontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, subscript, false, TextColor, HighlightColor, BaselineOffset);
    /// <summary>Returns a copy with superscript set to the given value and subscript cleared.</summary>
    public TextStyle WithSuperscript(bool superscript) => new(FontFamily, FontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, false, superscript, TextColor, HighlightColor, BaselineOffset);
    /// <summary>Returns a copy with the given text color.</summary>
    public TextStyle WithTextColor(EditorColor color) => new(FontFamily, FontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, color, HighlightColor, BaselineOffset);
    /// <summary>Returns a copy with the given highlight color.</summary>
    public TextStyle WithHighlightColor(EditorColor color) => new(FontFamily, FontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, color, BaselineOffset);

    /// <inheritdoc/>
    public bool Equals(TextStyle? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return FontFamily == other.FontFamily
            && FontSize == other.FontSize
            && IsBold == other.IsBold
            && IsItalic == other.IsItalic
            && IsUnderline == other.IsUnderline
            && IsStrikethrough == other.IsStrikethrough
            && IsSubscript == other.IsSubscript
            && IsSuperscript == other.IsSuperscript
            && TextColor == other.TextColor
            && HighlightColor == other.HighlightColor
            && BaselineOffset == other.BaselineOffset;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TextStyle);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FontFamily);
        hash.Add(FontSize);
        hash.Add(IsBold);
        hash.Add(IsItalic);
        hash.Add(IsUnderline);
        hash.Add(IsStrikethrough);
        hash.Add(IsSubscript);
        hash.Add(IsSuperscript);
        hash.Add(TextColor);
        hash.Add(HighlightColor);
        hash.Add(BaselineOffset);
        return hash.ToHashCode();
    }

    /// <summary>Returns <c>true</c> when both styles are equal or both are <c>null</c>.</summary>
    public static bool operator ==(TextStyle? left, TextStyle? right) => Equals(left, right);
    /// <summary>Returns <c>true</c> when the styles differ.</summary>
    public static bool operator !=(TextStyle? left, TextStyle? right) => !Equals(left, right);
}
