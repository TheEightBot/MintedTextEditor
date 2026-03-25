using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Maui.Providers;

/// <summary>
/// Opens hyperlinks using the platform default launcher.
/// </summary>
public sealed class MauiHyperlinkHandler : IHyperlinkHandler
{
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
