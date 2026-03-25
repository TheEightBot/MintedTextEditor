using MintedTextEditor.Core.Events;
using MintedTextEditor.Core.Formatting;

namespace SampleApp.Maui.Pages;

public partial class ImagesHyperlinksPage : ContentPage
{
    public ImagesHyperlinksPage()
    {
        InitializeComponent();
        Editor.LoadHtml(
            "<p>Use the toolbar above to insert hyperlinks and images.</p>" +
            "<p>Select some text and click <strong>Insert Link</strong>, or " +
            "leave the selection empty to insert a URL as the link text.</p>" +
            "<p>Images are inserted inline at the caret position.</p>");
    }

    private void OnInsertLinkClicked(object sender, EventArgs e)
    {
        var url = UrlEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(url)) return;

        HyperlinkEngine.InsertHyperlink(
            Editor.Document,
            Editor.Selection.Range,
            url);
    }

    private void OnInsertImageClicked(object sender, EventArgs e)
    {
        var src = ImageSrcEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(src)) return;

        ImageEngine.InsertImage(
            Editor.Document,
            Editor.Selection.Active,
            src,
            altText: "Inserted image",
            width: 120,
            height: 60);
    }

    private void OnHyperlinkClicked(object? sender, HyperlinkClickedEventArgs e)
        => LblHyperlinkResult.Text = $"Link tapped: {e.Url}";
}
