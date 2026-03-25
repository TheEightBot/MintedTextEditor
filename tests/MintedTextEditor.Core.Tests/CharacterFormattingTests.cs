using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Tests;

public class CharacterFormattingTests
{
    private readonly MockDrawingContext _context = new();
    private readonly TextLayoutEngine _engine = new();
    private readonly FormattingEngine _fmt = new();

    private static Paragraph Para(string text) => new Paragraph(text);

    private static TextRun FirstRun(EditorDocument doc, int block = 0)
        => (TextRun)((Paragraph)doc.Blocks[block]).Inlines[0];

    /// <summary>Returns a TextRange covering the entire first run in block 0.</summary>
    private static TextRange FullRange(EditorDocument doc, int block = 0)
    {
        var run = FirstRun(doc, block);
        return new TextRange(
            new DocumentPosition(block, 0, 0),
            new DocumentPosition(block, 0, run.Text.Length));
    }

    // ── ToggleBold ────────────────────────────────────────────────────────────

    [Fact]
    public void ToggleBold_AppliesBoldToRange()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleBold(doc, FullRange(doc));
        Assert.True(FirstRun(doc).Style.IsBold);
    }

    [Fact]
    public void ToggleBold_RemovesBoldWhenEntireRangeIsBold()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleBold(doc, FullRange(doc));   // apply
        _fmt.ToggleBold(doc, FullRange(doc));   // remove
        Assert.False(FirstRun(doc).Style.IsBold);
    }

    [Fact]
    public void ToggleBold_OnPartialRange_AppliesToAllRuns()
    {
        // Create "Hello" — then manually make only part of it bold
        var doc = new EditorDocument([Para("Hello")]);
        // Bold just chars 0-2 ("Hel")
        var partialRange = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 3));
        _fmt.ToggleBold(doc, partialRange);

        // Now select the whole word; since not ALL of it is bold, toggling should bold everything
        var para = (Paragraph)doc.Blocks[0];
        var wholeRange = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, para.Inlines.Count - 1, ((TextRun)para.Inlines[^1]).Text.Length));

        _fmt.ToggleBold(doc, wholeRange);

        foreach (var inline in para.Inlines)
        {
            if (inline is TextRun r)
                Assert.True(r.Style.IsBold);
        }
    }

    // ── ToggleItalic ──────────────────────────────────────────────────────────

    [Fact]
    public void ToggleItalic_AppliesItalicToRange()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleItalic(doc, FullRange(doc));
        Assert.True(FirstRun(doc).Style.IsItalic);
    }

    [Fact]
    public void ToggleItalic_RemovesItalicWhenEntireRangeIsItalic()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleItalic(doc, FullRange(doc));
        _fmt.ToggleItalic(doc, FullRange(doc));
        Assert.False(FirstRun(doc).Style.IsItalic);
    }

    // ── ToggleUnderline ───────────────────────────────────────────────────────

    [Fact]
    public void ToggleUnderline_AppliesUnderlineToRange()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleUnderline(doc, FullRange(doc));
        Assert.True(FirstRun(doc).Style.IsUnderline);
    }

    [Fact]
    public void ToggleUnderline_RemovesUnderlineWhenEntireRangeIsUnderlined()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleUnderline(doc, FullRange(doc));
        _fmt.ToggleUnderline(doc, FullRange(doc));
        Assert.False(FirstRun(doc).Style.IsUnderline);
    }

    // ── ToggleStrikethrough ───────────────────────────────────────────────────

    [Fact]
    public void ToggleStrikethrough_AppliesStrikethroughToRange()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleStrikethrough(doc, FullRange(doc));
        Assert.True(FirstRun(doc).Style.IsStrikethrough);
    }

    [Fact]
    public void ToggleStrikethrough_RemovesStrikethroughWhenEntireRangeIsStrikethrough()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleStrikethrough(doc, FullRange(doc));
        _fmt.ToggleStrikethrough(doc, FullRange(doc));
        Assert.False(FirstRun(doc).Style.IsStrikethrough);
    }

    // ── ToggleSubscript ───────────────────────────────────────────────────────

    [Fact]
    public void ToggleSubscript_AppliesSubscriptToRange()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleSubscript(doc, FullRange(doc));
        Assert.True(FirstRun(doc).Style.IsSubscript);
    }

    [Fact]
    public void ToggleSubscript_RemovesSubscriptWhenEntireRangeIsSubscript()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleSubscript(doc, FullRange(doc));
        _fmt.ToggleSubscript(doc, FullRange(doc));
        Assert.False(FirstRun(doc).Style.IsSubscript);
    }

    // ── ToggleSuperscript ─────────────────────────────────────────────────────

    [Fact]
    public void ToggleSuperscript_AppliesSuperscriptToRange()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleSuperscript(doc, FullRange(doc));
        Assert.True(FirstRun(doc).Style.IsSuperscript);
    }

    [Fact]
    public void ToggleSuperscript_RemovesSuperscriptWhenEntireRangeIsSuperscript()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleSuperscript(doc, FullRange(doc));
        _fmt.ToggleSuperscript(doc, FullRange(doc));
        Assert.False(FirstRun(doc).Style.IsSuperscript);
    }

    // ── ClearFormatting ───────────────────────────────────────────────────────

    [Fact]
    public void ClearFormatting_ResetsFormattedRunToDefault()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleBold(doc, FullRange(doc));
        _fmt.ToggleItalic(doc, FullRange(doc));

        _fmt.ClearFormatting(doc, FullRange(doc));

        Assert.Equal(TextStyle.Default, FirstRun(doc).Style);
    }

    [Fact]
    public void ClearFormatting_EmptyRange_SetsPendingStyleToDefault()
    {
        var doc = new EditorDocument([Para("Hello")]);
        var fmt = new FormattingEngine();
        fmt.ToggleBold(doc, TextRange.Empty); // sets pending bold
        Assert.True(fmt.PendingStyle!.IsBold);

        fmt.ClearFormatting(doc, TextRange.Empty); // clears pending
        Assert.Equal(TextStyle.Default, fmt.PendingStyle!);
    }

    // ── Pending style ─────────────────────────────────────────────────────────

    [Fact]
    public void Toggle_EmptyRange_SetsPendingStyle()
    {
        var doc = new EditorDocument([Para("Hello")]);
        var fmt = new FormattingEngine();
        fmt.ToggleBold(doc, TextRange.Empty);
        Assert.NotNull(fmt.PendingStyle);
        Assert.True(fmt.PendingStyle!.IsBold);
    }

    [Fact]
    public void Toggle_EmptyRangeTwice_ClearsPendingStyle()
    {
        var doc = new EditorDocument([Para("Hello")]);
        var fmt = new FormattingEngine();
        fmt.ToggleBold(doc, TextRange.Empty);
        fmt.ToggleBold(doc, TextRange.Empty);
        // Pending style should have IsBold = false
        Assert.False(fmt.PendingStyle!.IsBold);
    }

    [Fact]
    public void ConsumePendingStyle_ReturnsStyleAndClearsIt()
    {
        var doc = new EditorDocument([Para("Hello")]);
        var fmt = new FormattingEngine();
        fmt.ToggleBold(doc, TextRange.Empty);

        var consumed = fmt.ConsumePendingStyle();

        Assert.NotNull(consumed);
        Assert.True(consumed!.IsBold);
        Assert.Null(fmt.PendingStyle);  // cleared after consume
    }

    [Fact]
    public void PendingStyle_AppliesToNextTypedCharacter()
    {
        var doc = new EditorDocument([Para("Hello")]);
        var layout = _engine.Layout(doc, 800f, _context);
        var caret = new Caret();
        var caretRenderer = new CaretRenderer();
        var fmt = new FormattingEngine();
        var ctrl = new EditorInputController(caret, caretRenderer) { FormattingEngine = fmt };

        // Place caret at end of "Hello"
        caret.MoveTo(new DocumentPosition(0, 0, 5));

        // Toggle bold with no selection (sets pending)
        fmt.ToggleBold(doc, TextRange.Empty);

        // Type a character — should be bold
        ctrl.HandleTextInput("!", doc);

        // Find the run that contains "!"
        var para = (Paragraph)doc.Blocks[0];
        var boldRun = para.Inlines.OfType<TextRun>().FirstOrDefault(r => r.Text.Contains('!'));
        Assert.NotNull(boldRun);
        Assert.True(boldRun!.Style.IsBold);

        // Pending style should be cleared
        Assert.Null(fmt.PendingStyle);
    }

    [Fact]
    public void PendingStyle_SubsequentTypingUsesDefaultStyle()
    {
        var doc = new EditorDocument([Para("Hello")]);
        var layout = _engine.Layout(doc, 800f, _context);
        var caret = new Caret();
        var caretRenderer = new CaretRenderer();
        var fmt = new FormattingEngine();
        var ctrl = new EditorInputController(caret, caretRenderer) { FormattingEngine = fmt };

        caret.MoveTo(new DocumentPosition(0, 0, 5));
        fmt.ToggleBold(doc, TextRange.Empty);
        ctrl.HandleTextInput("!", doc); // bold

        // Second character should NOT be bold (pending was consumed)
        var posBefore = caret.Position;
        ctrl.HandleTextInput("?", doc);

        var para = (Paragraph)doc.Blocks[0];
        var questionRun = para.Inlines.OfType<TextRun>().FirstOrDefault(r => r.Text.Contains('?'));
        Assert.NotNull(questionRun);
        Assert.False(questionRun!.Style.IsBold);
    }

    // ── IsAppliedToEntireRange ────────────────────────────────────────────────

    [Fact]
    public void IsAppliedToEntireRange_ReturnsFalse_ForEmptyRange()
    {
        var doc = new EditorDocument([Para("Hello")]);
        var result = FormattingEngine.IsAppliedToEntireRange(doc, TextRange.Empty, s => s.IsBold);
        Assert.False(result);
    }

    [Fact]
    public void IsAppliedToEntireRange_ReturnsFalse_WhenNotAllBold()
    {
        var doc = new EditorDocument([Para("Hello")]);
        var result = FormattingEngine.IsAppliedToEntireRange(doc, FullRange(doc), s => s.IsBold);
        Assert.False(result);
    }

    [Fact]
    public void IsAppliedToEntireRange_ReturnsTrue_WhenAllBold()
    {
        var doc = new EditorDocument([Para("Hello")]);
        _fmt.ToggleBold(doc, FullRange(doc));
        var result = FormattingEngine.IsAppliedToEntireRange(doc, FullRange(doc), s => s.IsBold);
        Assert.True(result);
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private (EditorDocument doc, DocumentLayout layout, Caret caret, EditorInputController ctrl)
        SetupWithFormatting(params Block[] blocks)
    {
        var doc = blocks.Length > 0 ? new EditorDocument(blocks) : new EditorDocument();
        var layout = _engine.Layout(doc, 800f, _context);
        var caret = new Caret();
        var caretRenderer = new CaretRenderer();
        var fmt = new FormattingEngine();
        var ctrl = new EditorInputController(caret, caretRenderer) { FormattingEngine = fmt };
        return (doc, layout, caret, ctrl);
    }

    private static EditorKeyEventArgs Key(EditorKey key, InputModifiers mods = InputModifiers.None)
        => new(key, modifiers: mods);

    private static TextRange SelectAll(EditorDocument doc)
    {
        var para = (Paragraph)doc.Blocks[0];
        var lastInlineIdx = para.Inlines.Count - 1;
        var lastRun = (TextRun)para.Inlines[lastInlineIdx];
        return new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, lastInlineIdx, lastRun.Text.Length));
    }

    [Fact]
    public void KeyboardShortcut_CtrlB_TogglesBold()
    {
        var (doc, layout, caret, ctrl) = SetupWithFormatting(Para("Hello"));

        // Select all text manually by pushing Ctrl+A
        ctrl.HandleKeyDown(Key(EditorKey.A, InputModifiers.Control), doc, layout, _context);

        ctrl.HandleKeyDown(Key(EditorKey.B, InputModifiers.Control), doc, layout, _context);

        // At least one run should now be bold
        var para = (Paragraph)doc.Blocks[0];
        Assert.Contains(para.Inlines.OfType<TextRun>(), r => r.Style.IsBold);
    }

    [Fact]
    public void KeyboardShortcut_CtrlI_TogglesItalic()
    {
        var (doc, layout, caret, ctrl) = SetupWithFormatting(Para("Hello"));
        ctrl.HandleKeyDown(Key(EditorKey.A, InputModifiers.Control), doc, layout, _context);
        ctrl.HandleKeyDown(Key(EditorKey.I, InputModifiers.Control), doc, layout, _context);

        var para = (Paragraph)doc.Blocks[0];
        Assert.Contains(para.Inlines.OfType<TextRun>(), r => r.Style.IsItalic);
    }

    [Fact]
    public void KeyboardShortcut_CtrlU_TogglesUnderline()
    {
        var (doc, layout, caret, ctrl) = SetupWithFormatting(Para("Hello"));
        ctrl.HandleKeyDown(Key(EditorKey.A, InputModifiers.Control), doc, layout, _context);
        ctrl.HandleKeyDown(Key(EditorKey.U, InputModifiers.Control), doc, layout, _context);

        var para = (Paragraph)doc.Blocks[0];
        Assert.Contains(para.Inlines.OfType<TextRun>(), r => r.Style.IsUnderline);
    }

    [Fact]
    public void KeyboardShortcut_WithNoFormattingEngine_ReturnsFalse()
    {
        var doc = new EditorDocument([Para("Hello")]);
        var layout = _engine.Layout(doc, 800f, _context);
        var caret = new Caret();
        var caretRenderer = new CaretRenderer();
        // No FormattingEngine set
        var ctrl = new EditorInputController(caret, caretRenderer);

        var handled = ctrl.HandleKeyDown(Key(EditorKey.B, InputModifiers.Control), doc, layout, _context);
        Assert.False(handled);
    }

    // ── WithSubscript / WithSuperscript on TextStyle ──────────────────────────

    [Fact]
    public void TextStyle_WithSubscript_SetsSubscriptTrue()
    {
        var style = TextStyle.Default.WithSubscript(true);
        Assert.True(style.IsSubscript);
        Assert.False(style.IsSuperscript);
    }

    [Fact]
    public void TextStyle_WithSuperscript_SetsSuperscriptTrue()
    {
        var style = TextStyle.Default.WithSuperscript(true);
        Assert.True(style.IsSuperscript);
        Assert.False(style.IsSubscript);
    }

    [Fact]
    public void TextStyle_WithSubscript_ClearsSuperscript()
    {
        // Applying subscript should ensure superscript is false (mutually exclusive)
        var style = TextStyle.Default.WithSubscript(true);
        Assert.False(style.IsSuperscript);
    }

    [Fact]
    public void TextStyle_WithSuperscript_ClearsSubscript()
    {
        var style = TextStyle.Default.WithSuperscript(true);
        Assert.False(style.IsSubscript);
    }

}
