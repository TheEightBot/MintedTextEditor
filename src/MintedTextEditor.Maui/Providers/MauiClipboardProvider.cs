using MintedTextEditor.Core.Editing;

namespace MintedTextEditor.Maui.Providers;

internal sealed class MauiClipboardProvider : IClipboardProvider
{
    public Task SetTextAsync(string text)
        => Clipboard.Default.SetTextAsync(text);

    public async Task<string?> GetTextAsync()
        => await Clipboard.Default.GetTextAsync();
}
