namespace SampleApp.Maui.Pages;

public partial class MarkdownInteropPage : ContentPage
{
    public MarkdownInteropPage()
    {
        InitializeComponent();
        var initialMarkdown =
            "## Markdown Interop Demo\n\n" +
            "This editor can **import** and *export* Markdown.\n\n" +
            "Tap **Export Markdown** to see the source below, edit it, then tap **Import Markdown**.\n\n" +
            "### Supported features\n\n" +
            "- Headings (`#` through `######`)\n" +
            "- **Bold**, *italic*, ***bold italic***, ~~strikethrough~~\n" +
            "- Bullet and numbered lists\n" +
            "- [Hyperlinks](https://github.com) and images\n" +
            "- GFM tables\n" +
            "- Blockquotes\n\n" +
            "> Markdown support is built in — no extra NuGet packages required!";
        Editor.LoadMarkdown(initialMarkdown);
        MarkdownSource.Text = initialMarkdown;
    }

    private void OnExportClicked(object sender, EventArgs e)
        => MarkdownSource.Text = Editor.GetMarkdown();

    private void OnImportClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(MarkdownSource.Text))
            Editor.LoadMarkdown(MarkdownSource.Text);
    }

    private void OnAppendClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(MarkdownSource.Text))
            Editor.AppendMarkdown(MarkdownSource.Text);
    }
}
