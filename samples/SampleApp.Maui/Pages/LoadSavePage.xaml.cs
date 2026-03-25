namespace SampleApp.Maui.Pages;

public partial class LoadSavePage : ContentPage
{
    private const string SampleHtml =
        "<h2>Sample Document</h2>" +
        "<p>This content was loaded from an HTML string. " +
        "You can edit it and save it to a local file.</p>" +
        "<ul>" +
        "<li><strong>Save to File</strong> — exports the current HTML to a temp file.</li>" +
        "<li><strong>Load Sample</strong> — replaces the document with this sample.</li>" +
        "<li><strong>Clear</strong> — empties the editor.</li>" +
        "</ul>" +
        "<p>Real applications would integrate a file picker or a cloud storage API " +
        "to read and write documents.</p>";

    public LoadSavePage()
    {
        InitializeComponent();
    }

    private void OnLoadClicked(object sender, EventArgs e)
    {
        Editor.LoadHtml(SampleHtml);
        LblStatus.Text = "Sample document loaded.";
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        var html = Editor.GetHtml();
        var path = Path.Combine(FileSystem.CacheDirectory, "minted_export.html");
        File.WriteAllText(path, html);
        LblStatus.Text = $"Saved to: {path}";
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        Editor.LoadHtml(string.Empty);
        LblStatus.Text = "Editor cleared.";
    }
}
