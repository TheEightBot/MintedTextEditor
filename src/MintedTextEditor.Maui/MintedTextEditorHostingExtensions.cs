using MintedTextEditor.Maui.Input;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace MintedTextEditor.Maui;

/// <summary>
/// Extension methods for registering MintedTextEditor with a MAUI app.
/// </summary>
public static class MintedTextEditorHostingExtensions
{
    /// <summary>
    /// Register the SkiaSharp-powered MintedTextEditor with the MAUI app builder.
    /// Call this in MauiProgram.cs: <c>builder.UseMintedTextEditor();</c>
    /// </summary>
    public static MauiAppBuilder UseMintedTextEditor(this MauiAppBuilder builder)
    {
        builder.UseSkiaSharp();
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<KeyboardProxy, KeyboardProxyHandler>());
        return builder;
    }
}
