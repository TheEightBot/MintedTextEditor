namespace MintedTextEditor.Core.Markdown;

/// <summary>
/// Controls how <see cref="MarkdownExporter"/> serializes a document.
/// </summary>
public class MarkdownExportOptions
{
    /// <summary>When true, uses GFM extensions: tables and strikethrough (~~).</summary>
    public bool UseGfmExtensions { get; set; } = true;

    /// <summary>Line ending used in the output. Defaults to LF.</summary>
    public string LineEnding { get; set; } = "\n";
}
