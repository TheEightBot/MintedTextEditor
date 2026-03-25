using MintedTextEditor.Core.Localization;

namespace SampleApp.Maui.Pages;

// ---------- custom locale subclasses (file-scoped, not exported) ---------------

file sealed class SpanishEditorStrings : EditorStrings
{
    public override string Bold           => "Negrita";
    public override string Italic         => "Cursiva";
    public override string Underline      => "Subrayado";
    public override string Strikethrough  => "Tachado";
    public override string AlignLeft      => "Alinear a la izquierda";
    public override string AlignCenter    => "Centrar";
    public override string AlignRight     => "Alinear a la derecha";
    public override string AlignJustify   => "Justificar";
    public override string BulletList     => "Lista con viñetas";
    public override string NumberedList   => "Lista numerada";
    public override string Undo           => "Deshacer";
    public override string Redo           => "Rehacer";
    public override string InsertTable    => "Insertar tabla";
}

file sealed class FrenchEditorStrings : EditorStrings
{
    public override string Bold           => "Gras";
    public override string Italic         => "Italique";
    public override string Underline      => "Souligné";
    public override string Strikethrough  => "Barré";
    public override string AlignLeft      => "Aligner à gauche";
    public override string AlignCenter    => "Centrer";
    public override string AlignRight     => "Aligner à droite";
    public override string AlignJustify   => "Justifier";
    public override string BulletList     => "Liste à puces";
    public override string NumberedList   => "Liste numérotée";
    public override string Undo           => "Annuler";
    public override string Redo           => "Rétablir";
    public override string InsertTable    => "Insérer un tableau";
}

// ---------- page ---------------------------------------------------------------

public partial class LocalizationPage : ContentPage
{
    public LocalizationPage()
    {
        InitializeComponent();
        Editor.LoadHtml(
            "<p>The localization demo overrides <code>EditorStrings.Current</code>.</p>" +
            "<p>Switch languages above and observe the sample strings below update.</p>");
        RefreshLabel();
    }

    private void OnEnglishClicked(object sender, EventArgs e)
    {
        EditorStrings.Current = new EditorStrings();
        RefreshLabel();
    }

    private void OnSpanishClicked(object sender, EventArgs e)
    {
        EditorStrings.Current = new SpanishEditorStrings();
        RefreshLabel();
    }

    private void OnFrenchClicked(object sender, EventArgs e)
    {
        EditorStrings.Current = new FrenchEditorStrings();
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        var s = EditorStrings.Current;
        LblStrings.Text =
            $"Bold='{s.Bold}'  Italic='{s.Italic}'  Underline='{s.Underline}'\n" +
            $"Undo='{s.Undo}'  Redo='{s.Redo}'  InsertTable='{s.InsertTable}'";
    }
}
