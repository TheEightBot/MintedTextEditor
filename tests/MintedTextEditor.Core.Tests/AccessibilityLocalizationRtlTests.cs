using MintedTextEditor.Core.Accessibility;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Html;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Localization;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Tests for Phase 19: Accessibility, Localization, and RTL.
/// </summary>
public class AccessibilityLocalizationRtlTests
{
    // ── AccessibilityProperties ──────────────────────────────────────────

    [Fact]
    public void AccessibilityProperties_DefaultsHaveEmptyLabelAndHint()
    {
        var props = new AccessibilityProperties();
        Assert.Equal(string.Empty, props.Label);
        Assert.Equal(string.Empty, props.Hint);
    }

    [Fact]
    public void AccessibilityProperties_WithLabel_SetsLabel()
    {
        var props = AccessibilityProperties.WithLabel("Bold");
        Assert.Equal("Bold", props.Label);
        Assert.Equal(string.Empty, props.Hint);
        Assert.False(props.IsHidden);
    }

    [Fact]
    public void AccessibilityProperties_WithLabelAndHint_SetsBoth()
    {
        var props = AccessibilityProperties.WithLabelAndHint("Bold", "Ctrl+B");
        Assert.Equal("Bold", props.Label);
        Assert.Equal("Ctrl+B", props.Hint);
    }

    [Fact]
    public void AccessibilityProperties_Hidden_IsHiddenTrue()
    {
        Assert.True(AccessibilityProperties.Hidden.IsHidden);
    }

    [Fact]
    public void AccessibilityProperties_DefaultLiveRegion_IsOff()
    {
        var props = new AccessibilityProperties();
        Assert.Equal(LiveRegionMode.Off, props.LiveRegion);
    }

    [Fact]
    public void AccessibilityProperties_SupportsWithExpression()
    {
        var orig = AccessibilityProperties.WithLabel("Undo");
        var modified = orig with { Hint = "Ctrl+Z" };
        Assert.Equal("Undo", modified.Label);
        Assert.Equal("Ctrl+Z", modified.Hint);
    }

    [Fact]
    public void AccessibilityProperties_PoliteAndAssertiveRountrip()
    {
        var polite = new AccessibilityProperties { LiveRegion = LiveRegionMode.Polite };
        var assertive = polite with { LiveRegion = LiveRegionMode.Assertive };
        Assert.Equal(LiveRegionMode.Polite, polite.LiveRegion);
        Assert.Equal(LiveRegionMode.Assertive, assertive.LiveRegion);
    }

    // ── EditorStrings ────────────────────────────────────────────────────

    [Fact]
    public void EditorStrings_Default_HasEnglishBold()
    {
        Assert.Equal("Bold", EditorStrings.Current.Bold);
    }

    [Fact]
    public void EditorStrings_Default_HasEnglishContextMenuStrings()
    {
        var s = EditorStrings.Current;
        Assert.Equal("Cut", s.Cut);
        Assert.Equal("Copy", s.Copy);
        Assert.Equal("Paste", s.Paste);
        Assert.Equal("Select all", s.SelectAll);
    }

    [Fact]
    public void EditorStrings_Default_HasToolbarStrings()
    {
        var s = EditorStrings.Current;
        Assert.Equal("Italic", s.Italic);
        Assert.Equal("Underline", s.Underline);
        Assert.Equal("Align left", s.AlignLeft);
        Assert.Equal("Align center", s.AlignCenter);
        Assert.Equal("Align right", s.AlignRight);
        Assert.Equal("Justify", s.AlignJustify);
    }

    [Fact]
    public void EditorStrings_Default_HasHeadingStrings()
    {
        var s = EditorStrings.Current;
        Assert.Equal("Heading 1", s.Heading1);
        Assert.Equal("Heading 6", s.Heading6);
        Assert.Equal("Normal text", s.NormalText);
    }

    [Fact]
    public void EditorStrings_Current_CanBeReplaced()
    {
        var original = EditorStrings.Current;
        try
        {
            var custom = new CustomTestStrings();
            EditorStrings.Current = custom;
            Assert.Equal("Négrita", EditorStrings.Current.Bold);
        }
        finally
        {
            // Always restore the singleton
            EditorStrings.Current = original;
        }
    }

    [Fact]
    public void EditorStrings_Current_ThrowsOnNullAssignment()
    {
        Assert.Throws<ArgumentNullException>(() => EditorStrings.Current = null!);
    }

    [Fact]
    public void EditorStrings_Default_HasAccessibilityLabels()
    {
        var s = EditorStrings.Current;
        Assert.False(string.IsNullOrWhiteSpace(s.EditorAccessibilityLabel));
        Assert.False(string.IsNullOrWhiteSpace(s.ToolbarAccessibilityLabel));
    }

    // ── RTL ParagraphStyle ───────────────────────────────────────────────

    [Fact]
    public void ParagraphStyle_Direction_DefaultsToLeftToRight()
    {
        var style = new ParagraphStyle();
        Assert.Equal(TextDirection.LeftToRight, style.Direction);
    }

    [Fact]
    public void ParagraphStyle_Direction_CanBeSetToRightToLeft()
    {
        var style = new ParagraphStyle { Direction = TextDirection.RightToLeft };
        Assert.Equal(TextDirection.RightToLeft, style.Direction);
    }

    [Fact]
    public void RtlParagraph_HtmlExport_EmitsRtlAttribute()
    {
        var doc = new EditorDocument();
        // Reuse the default paragraph so no extra block is prepended
        var para = (Paragraph)doc.Blocks[0];
        para.Style.Direction = TextDirection.RightToLeft;
        para.Inlines.Add(new TextRun("مرحباً بالعالم"));

        var exporter = new HtmlExporter();
        string html = exporter.Export(doc);

        // The exporter uses inline CSS: style="direction:rtl"
        Assert.Contains("direction:rtl", html);
    }

    [Fact]
    public void RtlParagraph_HtmlImport_ParsesDirectionCorrectly()
    {
        const string html = "<p dir=\"rtl\">مرحباً بالعالم</p>";
        var importer = new HtmlImporter();
        var doc = importer.Import(html);

        Assert.Single(doc.Blocks);
        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(TextDirection.RightToLeft, para.Style.Direction);
    }

    [Fact]
    public void LtrParagraph_HtmlExport_DoesNotEmitRtlAttribute()
    {
        var doc = new EditorDocument();
        var para = new Paragraph();
        // Direction is LTR by default — no dir attribute expected
        para.Inlines.Add(new TextRun("Hello world"));
        doc.Blocks.Add(para);

        var exporter = new HtmlExporter();
        string html = exporter.Export(doc);

        Assert.DoesNotContain("dir=\"rtl\"", html);
    }

    [Fact]
    public void MixedDirection_TwoParagraphs_EachHasCorrectDirection()
    {
        var doc = new EditorDocument();

        // Use the default paragraph as the LTR paragraph (it is already LTR)
        var ltr = (Paragraph)doc.Blocks[0];
        ltr.Style.Direction = TextDirection.LeftToRight;
        ltr.Inlines.Add(new TextRun("Hello"));

        // Add one RTL paragraph after it
        var rtl = new Paragraph();
        rtl.Style.Direction = TextDirection.RightToLeft;
        rtl.Inlines.Add(new TextRun("مرحبا"));
        doc.Blocks.Add(rtl);

        Assert.Equal(TextDirection.LeftToRight, ((Paragraph)doc.Blocks[0]).Style.Direction);
        Assert.Equal(TextDirection.RightToLeft, ((Paragraph)doc.Blocks[1]).Style.Direction);
    }

    // ── RTL Layout ───────────────────────────────────────────────────────

    [Fact]
    public void RtlLayout_EmptyParagraph_DefaultCaretXAtRightEdge()
    {
        const float viewportWidth = 500f;
        var doc = new EditorDocument();
        // Modify the default paragraph in-place — no extra block prepended
        var para = (Paragraph)doc.Blocks[0];
        para.Style.Direction = TextDirection.RightToLeft;

        var engine = new TextLayoutEngine();
        var ctx = new MockDrawingContext();
        var layout = engine.Layout(doc, viewportWidth, ctx);

        var firstLine = layout.Blocks[0].Lines[0];
        // Empty RTL paragraph: caret should be at the right edge (no indent)
        Assert.Equal(viewportWidth, firstLine.DefaultCaretX);
    }

    [Fact]
    public void LtrLayout_EmptyParagraph_DefaultCaretXIsZero()
    {
        const float viewportWidth = 500f;
        // A fresh document already contains one empty LTR paragraph
        var doc = new EditorDocument();

        var engine = new TextLayoutEngine();
        var ctx = new MockDrawingContext();
        var layout = engine.Layout(doc, viewportWidth, ctx);

        var firstLine = layout.Blocks[0].Lines[0];
        Assert.Equal(0f, firstLine.DefaultCaretX);
    }

    [Fact]
    public void RtlLayout_TextRunRuns_AreRightAligned()
    {
        // With viewport 500 and text "Hello" (5 chars * 8px = 40px wide),
        // the run should be positioned near the right edge (offset ~= 460).
        const float viewportWidth = 500f;
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.Direction = TextDirection.RightToLeft;
        para.Inlines.Add(new TextRun("Hello"));

        var engine = new TextLayoutEngine();
        var ctx = new MockDrawingContext { CharWidth = 8f };
        var layout = engine.Layout(doc, viewportWidth, ctx);

        var firstRun = layout.Blocks[0].Lines[0].Runs[0];
        // With no indent and Right alignment the run starts at (viewportWidth - textWidth)
        float expectedX = viewportWidth - "Hello".Length * 8f;
        Assert.Equal(expectedX, firstRun.X, precision: 1);
    }

    [Fact]
    public void RtlLayout_WithIndent_MirrorsIndentFromRight()
    {
        // IndentLevel=1 means 24px indent; for RTL that comes off the right side.
        // With viewport 500, available = 500-24 = 476.
        // "Hi" = 2*8 = 16px; Right-aligned within 476 → x = 476 - 16 = 460.
        const float viewportWidth = 500f;
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.Direction = TextDirection.RightToLeft;
        para.Style.IndentLevel = 1;
        para.Inlines.Add(new TextRun("Hi"));

        var engine = new TextLayoutEngine();
        var ctx = new MockDrawingContext { CharWidth = 8f };
        var layout = engine.Layout(doc, viewportWidth, ctx);

        var firstRun = layout.Blocks[0].Lines[0].Runs[0];
        // available = 500 - 24 = 476; "Hi" = 16px; Right-aligned → x = 476 - 16 = 460
        Assert.Equal(460f, firstRun.X, precision: 1);
    }

    // ── RTL Caret ────────────────────────────────────────────────────────

    [Fact]
    public void RtlCaret_EmptyParagraph_CaretXAtRightEdge()
    {
        const float viewportWidth = 500f;
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.Direction = TextDirection.RightToLeft;

        var engine = new TextLayoutEngine();
        var ctx = new MockDrawingContext();
        var layout = engine.Layout(doc, viewportWidth, ctx);

        var renderer = new CaretRenderer();
        var pos = new DocumentPosition(0, 0, 0);
        var rect = renderer.GetCaretRect(pos, layout, doc, ctx);

        // Caret should be at the right edge of the viewport
        Assert.Equal(viewportWidth, rect.X, precision: 1);
    }
}

/// <summary>Custom strings subclass used in localisation replacement test.</summary>
file sealed class CustomTestStrings : EditorStrings
{
    public override string Bold => "Négrita";
}
