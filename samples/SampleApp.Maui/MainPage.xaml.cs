namespace SampleApp.Maui;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        Editor.LoadHtml(
            "<h1>Welcome to MintedTextEditor</h1>" +
            "<p>This is a fully custom-drawn rich text editor for <strong>.NET MAUI</strong>.</p>" +
            "<p>Use the toolbar above to <em>format text</em> or navigate to a demo page via the menu.</p>");
    }
}

