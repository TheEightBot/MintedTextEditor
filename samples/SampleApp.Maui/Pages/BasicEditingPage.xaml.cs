namespace SampleApp.Maui.Pages;

public partial class BasicEditingPage : ContentPage
{
    public BasicEditingPage()
    {
        InitializeComponent();
        Editor.LoadHtml(
            "<p>This page demonstrates <strong>basic editing</strong>.</p>" +
            "<p>Tap anywhere to move the caret. Use the system keyboard to type.</p>" +
            "<p>Undo / redo are available via <em>Ctrl+Z</em> / <em>Ctrl+Y</em> on desktop.</p>");
    }
}
