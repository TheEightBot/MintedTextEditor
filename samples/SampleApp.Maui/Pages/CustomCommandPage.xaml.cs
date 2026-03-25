using MintedTextEditor.Core.Commands;

namespace SampleApp.Maui.Pages;

// A custom IEditorCommand that counts words in the document.
file sealed class WordCountCommand : IEditorCommand
{
    public string Name => "WordCount";
    public string Description => "Counts the number of words in the document.";

    public bool CanExecute(EditorContext _) => true;

    public void Execute(EditorContext ctx)
    {
        var text = ctx.Document.GetText();
        WordCount = string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public int WordCount { get; private set; }
}

// A custom IEditorCommand that counts paragraphs (blocks) in the document.
file sealed class ParagraphCountCommand : IEditorCommand
{
    public string Name => "ParagraphCount";
    public string Description => "Counts the number of paragraphs in the document.";

    public bool CanExecute(EditorContext _) => true;

    public void Execute(EditorContext ctx)
        => ParagraphCount = ctx.Document.BlockCount;

    public int ParagraphCount { get; private set; }
}

public partial class CustomCommandPage : ContentPage
{
    public CustomCommandPage()
    {
        InitializeComponent();
        Editor.LoadHtml(
            "<p>Custom commands implement the <code>IEditorCommand</code> interface.</p>" +
            "<p>They receive an <code>EditorContext</code> with access to the document, " +
            "selection, undo stack, and formatting engines.</p>" +
            "<p>Add more paragraphs here and click the buttons above.</p>" +
            "<p>A fourth paragraph for counting fun.</p>");
    }

    private EditorContext BuildContext() => new(
        Editor.Document,
        Editor.Selection,
        Editor.UndoManager,
        Editor.Formatting,
        Editor.FontFormatting);

    private void OnCountWordsClicked(object sender, EventArgs e)
    {
        var cmd = new WordCountCommand();
        var ctx = BuildContext();
        if (cmd.CanExecute(ctx))
        {
            cmd.Execute(ctx);
            LblResult.Text = $"Word count: {cmd.WordCount}";
        }
    }

    private void OnCountParagraphsClicked(object sender, EventArgs e)
    {
        var cmd = new ParagraphCountCommand();
        var ctx = BuildContext();
        if (cmd.CanExecute(ctx))
        {
            cmd.Execute(ctx);
            LblResult.Text = $"Paragraph count: {cmd.ParagraphCount}";
        }
    }
}
