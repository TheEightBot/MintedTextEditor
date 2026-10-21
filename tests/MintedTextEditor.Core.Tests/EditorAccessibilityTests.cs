using MintedTextEditor.Core.Accessibility;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Localization;

namespace MintedTextEditor.Core.Tests;

public class EditorAccessibilityTests
{
    private static readonly EditorStrings English = new();

    [Fact]
    public void ForEditor_Editable_NotEmpty_UsesLabelAndHint()
    {
        var props = EditorAccessibility.ForEditor(isReadOnly: false, isEmpty: false, "Start typing…", English);

        Assert.Equal("Rich text editor", props.Label);
        Assert.Equal("Double-tap to edit text", props.Hint);
        Assert.False(props.IsHidden);
    }

    [Fact]
    public void ForEditor_ReadOnly_AppendsStateAndDropsHint()
    {
        var props = EditorAccessibility.ForEditor(isReadOnly: true, isEmpty: false, "Start typing…", English);

        Assert.Equal("Rich text editor, read only", props.Label);
        Assert.Equal(string.Empty, props.Hint);
    }

    [Fact]
    public void ForEditor_Empty_AppendsPlaceholder()
    {
        var props = EditorAccessibility.ForEditor(isReadOnly: false, isEmpty: true, "Write a comment", English);

        Assert.Equal("Rich text editor, Write a comment", props.Label);
    }

    [Fact]
    public void ForEditor_EmptyAndReadOnly_IncludesBothInOrder()
    {
        var props = EditorAccessibility.ForEditor(isReadOnly: true, isEmpty: true, "Nothing here", English);

        Assert.Equal("Rich text editor, read only, Nothing here", props.Label);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForEditor_Empty_BlankPlaceholderIsOmitted(string? placeholder)
    {
        var props = EditorAccessibility.ForEditor(isReadOnly: false, isEmpty: true, placeholder, English);

        Assert.Equal("Rich text editor", props.Label);
    }

    [Fact]
    public void ForEditor_NotEmpty_IgnoresPlaceholder()
    {
        var props = EditorAccessibility.ForEditor(isReadOnly: false, isEmpty: false, "Write a comment", English);

        Assert.DoesNotContain("Write a comment", props.Label);
    }

    [Fact]
    public void ForEditor_UsesSuppliedStrings()
    {
        var props = EditorAccessibility.ForEditor(isReadOnly: true, isEmpty: false, null, new SpanishStrings());

        Assert.Equal("Editor de texto, solo lectura", props.Label);
        Assert.Equal(string.Empty, props.Hint);

        var editable = EditorAccessibility.ForEditor(isReadOnly: false, isEmpty: false, null, new SpanishStrings());
        Assert.Equal("Toca dos veces para editar", editable.Hint);
    }

    [Fact]
    public void ForEditor_BlankLabelString_IsSkippedWithoutLeadingSeparator()
    {
        var props = EditorAccessibility.ForEditor(isReadOnly: true, isEmpty: false, null, new BlankLabelStrings());

        Assert.Equal("read only", props.Label);
    }

    [Fact]
    public void ForEditor_CustomLabel_ReplacesDefaultLabelAndKeepsState()
    {
        var readOnly = EditorAccessibility.ForEditor(isReadOnly: true, isEmpty: false, null, English, label: "Message body");
        var editable = EditorAccessibility.ForEditor(isReadOnly: false, isEmpty: false, null, English, label: "Message body");

        Assert.Equal("Message body, read only", readOnly.Label);
        Assert.Equal("Message body", editable.Label);
        Assert.Equal("Double-tap to edit text", editable.Hint);
    }

    [Fact]
    public void ForEditor_NullCustomLabel_UsesStringsLabel()
    {
        var props = EditorAccessibility.ForEditor(isReadOnly: true, isEmpty: false, null, English, label: null);

        Assert.Equal("Rich text editor, read only", props.Label);
    }

    [Fact]
    public void ForEditor_BlankCustomLabel_IsSkipped()
    {
        var props = EditorAccessibility.ForEditor(isReadOnly: true, isEmpty: false, null, English, label: " ");

        Assert.Equal("read only", props.Label);
    }

    [Fact]
    public void IsEmpty_NewDocument_IsTrue()
    {
        Assert.True(EditorAccessibility.IsEmpty(new Document.Document()));
    }

    [Fact]
    public void IsEmpty_ParagraphWithText_IsFalse()
    {
        var doc = new Document.Document();
        ((Paragraph)doc.Blocks[0]).AppendRun("Hello");

        Assert.False(EditorAccessibility.IsEmpty(doc));
    }

    [Fact]
    public void IsEmpty_TwoEmptyParagraphs_IsFalse()
    {
        var doc = new Document.Document();
        doc.AddBlock(new Paragraph());

        Assert.False(EditorAccessibility.IsEmpty(doc));
    }

    [Fact]
    public void EditorStrings_Default_HasReadOnlyAccessibilityState()
    {
        Assert.Equal("read only", English.EditorReadOnlyAccessibilityState);
    }

    private sealed class SpanishStrings : EditorStrings
    {
        public override string EditorAccessibilityLabel => "Editor de texto";
        public override string EditorAccessibilityHint => "Toca dos veces para editar";
        public override string EditorReadOnlyAccessibilityState => "solo lectura";
    }

    private sealed class BlankLabelStrings : EditorStrings
    {
        public override string EditorAccessibilityLabel => "";
    }
}
