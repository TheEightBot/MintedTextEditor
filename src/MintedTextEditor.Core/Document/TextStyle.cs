using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Document;

/// <summary>
/// Immutable text style describing the visual appearance of a text run.
/// Use <see cref="With"/> methods to create modified copies.
/// </summary>
public sealed class TextStyle : IEquatable<TextStyle>
{
    public static TextStyle Default { get; } = new();

    public string FontFamily { get; }
    public float FontSize { get; }
    public bool IsBold { get; }
    public bool IsItalic { get; }
    public bool IsUnderline { get; }
    public bool IsStrikethrough { get; }
    public bool IsSubscript { get; }
    public bool IsSuperscript { get; }
    public EditorColor TextColor { get; }
    public EditorColor HighlightColor { get; }
    public float BaselineOffset { get; }

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

    public TextStyle WithFontFamily(string fontFamily) => new(fontFamily, FontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    public TextStyle WithFontSize(float fontSize) => new(FontFamily, fontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    public TextStyle WithBold(bool bold) => new(FontFamily, FontSize, bold, IsItalic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    public TextStyle WithItalic(bool italic) => new(FontFamily, FontSize, IsBold, italic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    public TextStyle WithUnderline(bool underline) => new(FontFamily, FontSize, IsBold, IsItalic, underline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    public TextStyle WithStrikethrough(bool strikethrough) => new(FontFamily, FontSize, IsBold, IsItalic, IsUnderline, strikethrough, IsSubscript, IsSuperscript, TextColor, HighlightColor, BaselineOffset);
    public TextStyle WithSubscript(bool subscript) => new(FontFamily, FontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, subscript, false, TextColor, HighlightColor, BaselineOffset);
    public TextStyle WithSuperscript(bool superscript) => new(FontFamily, FontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, false, superscript, TextColor, HighlightColor, BaselineOffset);
    public TextStyle WithTextColor(EditorColor color) => new(FontFamily, FontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, color, HighlightColor, BaselineOffset);
    public TextStyle WithHighlightColor(EditorColor color) => new(FontFamily, FontSize, IsBold, IsItalic, IsUnderline, IsStrikethrough, IsSubscript, IsSuperscript, TextColor, color, BaselineOffset);

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

    public override bool Equals(object? obj) => Equals(obj as TextStyle);

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

    public static bool operator ==(TextStyle? left, TextStyle? right) => Equals(left, right);
    public static bool operator !=(TextStyle? left, TextStyle? right) => !Equals(left, right);
}
