namespace SampleApp.Maui.Pages;

public partial class ReadOnlyPage : ContentPage
{
    public ReadOnlyPage()
    {
        InitializeComponent();
        Editor.LoadHtml(
            "<h2>Read-Only Mode</h2>" +
            "<p>Toggle the switch above to enable or disable editing.</p>" +
            "<p>In read-only mode the caret is hidden and no text input is accepted. " +
            "Selection and scrolling still work normally.</p>" +
            "<p>This is useful for document preview, approval workflows, or " +
            "displaying locked content.</p>");
    }

    private void OnReadOnlyToggled(object sender, ToggledEventArgs e)
    {
        Editor.IsReadOnly = e.Value;
        LblMode.Text = e.Value ? "On" : "Off";
    }
}
