using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Maui.Providers;

/// <summary>
/// Opens hyperlinks using the platform default launcher.
/// </summary>
public sealed class MauiHyperlinkHandler : IHyperlinkHandler
{
    /// <inheritdoc/>
    /// <remarks>
    /// A URL that is not a valid absolute URI is retried with <c>https://</c> prepended. Blank or unparseable
    /// URLs and launcher failures are ignored.
    /// </remarks>
    public async void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        Uri? uri = null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            uri = parsed;
        }
        else if (Uri.TryCreate($"https://{url}", UriKind.Absolute, out var parsedWithScheme))
        {
            uri = parsedWithScheme;
        }

        if (uri is null) return;

        try
        {
            await Launcher.Default.OpenAsync(uri);
        }
        catch
        {
            // Ignore launcher failures and let host apps handle link-open telemetry.
        }
    }
}
