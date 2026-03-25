using System.Reflection;

namespace MintedTextEditor.Core.Toolbar;

/// <summary>
/// Provides access to the built-in toolbar icon SVGs that are embedded in the Core assembly.
/// Each icon is stored as a black-ink-on-transparent SVG so consumers can tint it to the
/// current theme color at rasterization or draw time.
/// </summary>
public static class ToolbarIconResources
{
    private static readonly Assembly CoreAssembly = typeof(ToolbarIconResources).Assembly;

    // Canonical icon keys exposed by ToolbarDefinition.CreateDefault().
    private static readonly string[] AllKeys =
    [
        "bold", "italic", "underline", "strikethrough",
        "subscript", "superscript",
        "align-left", "align-center", "align-right", "align-justify",
        "bullet-list", "number-list",
        "indent-decrease", "indent-increase",
        "undo", "redo",
        "hyperlink", "image", "table", "clear-formatting",
    ];

    /// <summary>All icon keys that have embedded SVG resources.</summary>
    public static IReadOnlyList<string> Keys => AllKeys;

    /// <summary>
    /// Opens a stream for the SVG resource that matches <paramref name="iconKey"/> and
    /// <paramref name="pack"/>.  The caller is responsible for disposing the stream.
    /// Returns <c>null</c> when no matching resource is found.
    /// </summary>
    public static Stream? OpenIconStream(string iconKey, ToolbarIconPack pack)
    {
        string packFolder = pack switch
        {
            ToolbarIconPack.Heroicons      => "heroicons",
            ToolbarIconPack.MaterialSymbols => "material",
            _                              => "lucide",
        };

        // Hyphens in icon keys become underscores in file names (e.g. "align-left" → "align_left.svg").
        string fileName = iconKey.Replace('-', '_').ToLowerInvariant();

        // .NET embedded resource naming: {RootNamespace}.{folder.path}.{filename}
        // MintedTextEditor.Core assembly has RootNamespace = "MintedTextEditor.Core".
        string resourceName = $"MintedTextEditor.Core.Icons.{packFolder}.{fileName}.svg";

        return CoreAssembly.GetManifestResourceStream(resourceName);
    }
}
