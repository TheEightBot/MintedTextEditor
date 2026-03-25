// Custom XAML namespace schema — allows XAML consumers to reference MintedTextEditor
// controls using a stable URI instead of a verbose clr-namespace declaration:
//
//   xmlns:minted="https://schemas.mintedtexteditor.com/2026"
//
// See: https://learn.microsoft.com/dotnet/maui/xaml/namespaces/custom-namespace-schemas

[assembly: Microsoft.Maui.Controls.XmlnsDefinition("https://schemas.mintedtexteditor.com/2026", "MintedTextEditor.Maui")]
[assembly: Microsoft.Maui.Controls.XmlnsPrefix("https://schemas.mintedtexteditor.com/2026", "minted")]
