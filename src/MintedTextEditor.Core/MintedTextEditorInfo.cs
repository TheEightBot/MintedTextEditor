namespace MintedTextEditor.Core;

/// <summary>
/// Placeholder — core library entry point.
/// </summary>
public static class MintedTextEditorInfo
{
    /// <summary>
    /// Gets the current version of the MintedTextEditor.Core library.
    /// </summary>
    public static string Version => typeof(MintedTextEditorInfo).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        is [System.Reflection.AssemblyInformationalVersionAttribute attr, ..]
            ? attr.InformationalVersion
            : "0.0.0";
}
