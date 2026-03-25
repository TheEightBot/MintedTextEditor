namespace SampleApp.Maui.Pages;

public partial class FormattingPage : ContentPage
{
    public FormattingPage()
    {
        InitializeComponent();
        Editor.LoadHtml(
            "<p>Select any portion of this paragraph and tap a toolbar button to format it.</p>" +
            "<p>You can combine <strong>bold</strong> and <em>italic</em>, or apply " +
            "<u>underline</u> and <s>strikethrough</s>.</p>" +
            "<p>Superscript: E=mc<sup>2</sup>  Subscript: H<sub>2</sub>O</p>");
    }
}