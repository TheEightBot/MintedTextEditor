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
    public override string EditMenu       => "Editar";
    public override string ObjectMenu     => "Objeto";
    public override string TableMenu      => "Tabla";
    public override string DefaultFontFamily => "Predeterminada";
    public override string NormalText     => "Texto normal";
    public override string Heading1       => "Título 1";
    public override string Heading2       => "Título 2";
    public override string Heading3       => "Título 3";
    public override string Cut            => "Cortar";
    public override string Copy           => "Copiar";
    public override string Paste          => "Pegar";
    public override string SelectAll      => "Seleccionar todo";
    public override string HyperlinkDialogTitle   => "Insertar hipervínculo";
    public override string HyperlinkDialogMessage => "Escriba la URL:";
    public override string HyperlinkDialogInsert  => "Insertar";
    public override string HyperlinkDialogCancel  => "Cancelar";
    public override string EditHyperlinkDialogTitle => "Editar hipervínculo";
    public override string EditHyperlinkDialogSave  => "Guardar";
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
    public override string EditMenu       => "Édition";
    public override string ObjectMenu     => "Objet";
    public override string TableMenu      => "Tableau";
    public override string DefaultFontFamily => "Par défaut";
    public override string NormalText     => "Texte normal";
    public override string Heading1       => "Titre 1";
    public override string Heading2       => "Titre 2";
    public override string Heading3       => "Titre 3";
    public override string Cut            => "Couper";
    public override string Copy           => "Copier";
    public override string Paste          => "Coller";
    public override string SelectAll      => "Tout sélectionner";
    public override string HyperlinkDialogTitle   => "Insérer un lien hypertexte";
    public override string HyperlinkDialogMessage => "Saisissez l'URL :";
    public override string HyperlinkDialogInsert  => "Insérer";
    public override string HyperlinkDialogCancel  => "Annuler";
    public override string EditHyperlinkDialogTitle => "Modifier le lien hypertexte";
    public override string EditHyperlinkDialogSave  => "Enregistrer";
}

// ---------- page ---------------------------------------------------------------

public partial class LocalizationPage : ContentPage
{
    public LocalizationPage()
    {
        InitializeComponent();
        Editor.LoadHtml(
            "<p>The localization demo overrides <code>EditorStrings.Current</code>.</p>" +
            "<p>Switch languages above: the toolbar menus (Edit, Object, Table, font and heading pickers) " +
            "and the insert hyperlink prompt use the selected strings.</p>");
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
