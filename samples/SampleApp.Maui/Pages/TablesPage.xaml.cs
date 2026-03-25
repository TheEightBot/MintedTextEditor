using MintedTextEditor.Core.Formatting;

namespace SampleApp.Maui.Pages;

public partial class TablesPage : ContentPage
{
    public TablesPage()
    {
        InitializeComponent();
        Editor.LoadHtml(
            "<p>Position the caret then choose rows/columns and tap <b>Insert Table</b>.</p>");
    }

    private void OnInsertTableClicked(object sender, EventArgs e)
    {
        if (!int.TryParse(RowsEntry.Text, out int rows) || rows < 1) rows = 2;
        if (!int.TryParse(ColsEntry.Text, out int cols) || cols < 1) cols = 2;
        TableEngine.InsertTable(Editor.Document, Editor.Selection.Active, rows, cols);
    }
}
