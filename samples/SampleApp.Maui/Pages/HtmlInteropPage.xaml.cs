namespace SampleApp.Maui.Pages;

public partial class HtmlInteropPage : ContentPage
{
    public HtmlInteropPage()
    {
        InitializeComponent();
        var initialHtml =
            "<h2>HTML Interop Demo</h2>" +
            "<p>This editor can <strong>import</strong> and <em>export</em> HTML.</p>" +
            "<p>Tap <b>Export HTML</b> to see the source below, edit it, then tap <b>Import HTML</b>.</p>";
        Editor.LoadHtml(initialHtml);
        HtmlSource.Text = initialHtml;
    }

    private void OnExportClicked(object sender, EventArgs e)
        => HtmlSource.Text = Editor.GetHtml();

    private void OnImportClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(HtmlSource.Text))
            Editor.LoadHtml(HtmlSource.Text);
    }

    private void OnAppendClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(HtmlSource.Text))
            Editor.AppendHtml(HtmlSource.Text);
    }
}
