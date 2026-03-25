using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Formatting;

/// <summary>
/// High-level operations for paragraph-level formatting.
/// All methods create undoable state changes through <see cref="DocumentEditor"/>.
/// </summary>
public static class ParagraphFormattingEngine
{
    /// <summary>Sets the text alignment for all paragraphs in <paramref name="range"/>.</summary>
    public static void SetAlignment(Document.Document doc, TextRange range, TextAlignment alignment)
        => DocumentEditor.ApplyParagraphStyle(doc, range, s => s.Alignment = alignment);

    /// <summary>
    /// Toggles bullet list for all paragraphs in <paramref name="range"/>.
    /// If every paragraph is already a bullet list, removes the list style; otherwise applies it.
    /// </summary>
    public static void ToggleBulletList(Document.Document doc, TextRange range)
    {
        bool allBullet = AreAllParagraphs(doc, range, s => s.ListType == ListType.Bullet);
        DocumentEditor.ApplyParagraphStyle(doc, range, s =>
        {
            s.ListType = allBullet ? ListType.None : ListType.Bullet;
        });
    }

    /// <summary>
    /// Toggles ordered (number) list for all paragraphs in <paramref name="range"/>.
    /// If every paragraph is already numbered, removes the list style; otherwise applies it.
    /// </summary>
    public static void ToggleNumberList(Document.Document doc, TextRange range)
    {
        bool allNumber = AreAllParagraphs(doc, range, s => s.ListType == ListType.Number);
        DocumentEditor.ApplyParagraphStyle(doc, range, s =>
        {
            s.ListType = allNumber ? ListType.None : ListType.Number;
        });
    }

    /// <summary>Increases the indent level by one for all paragraphs in <paramref name="range"/>.</summary>
    public static void IncreaseIndent(Document.Document doc, TextRange range)
        => DocumentEditor.ApplyParagraphStyle(doc, range, s => s.IndentLevel++);

    /// <summary>
    /// Decreases the indent level by one for all paragraphs in <paramref name="range"/>.
    /// Indent level is clamped to a minimum of zero.
    /// </summary>
    public static void DecreaseIndent(Document.Document doc, TextRange range)
        => DocumentEditor.ApplyParagraphStyle(doc, range, s =>
        {
            if (s.IndentLevel > 0) s.IndentLevel--;
        });

    /// <summary>
    /// Sets the heading level (0 = normal, 1–6 = H1–H6) for all paragraphs in <paramref name="range"/>.
    /// </summary>
    public static void SetHeadingLevel(Document.Document doc, TextRange range, int level)
    {
        int clamped = Math.Clamp(level, 0, 6);
        DocumentEditor.ApplyParagraphStyle(doc, range, s => s.HeadingLevel = clamped);
    }

    /// <summary>
    /// Applies a named paragraph format string to all paragraphs in <paramref name="range"/>.
    /// Supported values: "Normal", "Heading1"–"Heading6", "Quote".
    /// </summary>
    public static void SetParagraphFormat(Document.Document doc, TextRange range, string format)
    {
        switch (format)
        {
            case "Normal":
                DocumentEditor.ApplyParagraphStyle(doc, range, s =>
                {
                    s.HeadingLevel = 0;
                    s.IsBlockQuote = false;
                });
                break;
            case "Heading1":
            case "Heading2":
            case "Heading3":
            case "Heading4":
            case "Heading5":
            case "Heading6":
                int level = int.Parse(format[^1..]);
                DocumentEditor.ApplyParagraphStyle(doc, range, s =>
                {
                    s.HeadingLevel = level;
                    s.IsBlockQuote = false;
                });
                break;
            case "Quote":
                DocumentEditor.ApplyParagraphStyle(doc, range, s =>
                {
                    s.HeadingLevel = 0;
                    s.IsBlockQuote = true;
                });
                break;
        }
    }

    /// <summary>Sets the line spacing multiplier for all paragraphs in <paramref name="range"/>.</summary>
    public static void SetLineSpacing(Document.Document doc, TextRange range, float spacing)
        => DocumentEditor.ApplyParagraphStyle(doc, range, s => s.LineSpacing = spacing);

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static bool AreAllParagraphs(
        Document.Document doc, TextRange range, Func<ParagraphStyle, bool> predicate)
    {
        // TextRange always stores Start <= End
        for (int i = range.Start.BlockIndex; i <= range.End.BlockIndex; i++)
        {
            if (doc.Blocks[i] is Paragraph p && !predicate(p.Style))
                return false;
        }
        return true;
    }
}
