using MintedTextEditor.Core.Theming;
using MintedTextEditor.Core.Toolbar;

namespace SampleApp.Maui.Pages;

public partial class ThemingPage : ContentPage
{
    public ThemingPage()
    {
        InitializeComponent();
        Editor.UseSystemTheme = false;
        IconPackPicker.SelectedIndex = 0;
        Editor.LoadHtml(
            "<h2>Theming Demo</h2>" +
            "<p>Tap a button above to switch themes instantly.</p>" +
            "<p>Use the icon pack picker to preview <strong>Lucide</strong>, <strong>Heroicons</strong>, and <strong>Material Symbols</strong>.</p>" +
            "<p>The editor supports <strong>Light</strong>, <strong>Dark</strong>, and " +
            "<strong>High Contrast</strong> built-in themes.</p>" +
            "<p>Custom themes can be created by constructing an <code>EditorStyle</code> " +
            "record with your own colours and dimensions.</p>");
    }

    private void OnLightClicked(object sender, EventArgs e)
        => Editor.Theme = EditorTheme.CreateLight();

    private void OnDarkClicked(object sender, EventArgs e)
        => Editor.Theme = EditorTheme.CreateDark();

    private void OnHighContrastClicked(object sender, EventArgs e)
        => Editor.Theme = EditorTheme.CreateHighContrast();

    private void OnIconPackChanged(object sender, EventArgs e)
    {
        Editor.ToolbarIconPack = IconPackPicker.SelectedIndex switch
        {
            1 => ToolbarIconPack.Heroicons,
            2 => ToolbarIconPack.MaterialSymbols,
            _ => ToolbarIconPack.Lucide,
        };
    }
}
