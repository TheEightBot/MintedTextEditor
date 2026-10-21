using System.Diagnostics.CodeAnalysis;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Planned integration point for platform print services.
/// </summary>
/// <remarks>
/// Experimental (<c>MTE002</c>): the editor does not use this interface yet, so implementing it
/// has no effect. Its shape may change once printing is wired into the control.
/// </remarks>
[Experimental("MTE002", UrlFormat = "https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/architecture.md#experimental-apis")]
public interface IPrintProvider
{
    /// <summary>
    /// Begins a print job for the given HTML representation of the document.
    /// The implementation is responsible for invoking the platform print dialog.
    /// </summary>
    /// <param name="html">HTML snapshot of the current document content.</param>
    /// <param name="jobName">Display name shown in the print queue.</param>
    void Print(string html, string jobName = "Document");

    /// <summary>
    /// Returns <c>true</c> when printing is currently available on this platform.
    /// </summary>
    bool IsAvailable { get; }
}
