using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Commands;

// ── Insert Image ──────────────────────────────────────────────────────────────

/// <summary>
/// Inserts an image at the current caret position.
/// Set <see cref="Source"/> (and optionally <see cref="AltText"/>, <see cref="Width"/>,
/// <see cref="Height"/>) before calling <see cref="Execute"/>.
/// </summary>
public sealed class InsertImageCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "InsertImage";
    /// <inheritdoc/>
    public string Description => "Inserts an inline image at the caret position.";

    /// <summary>Image source (URL or path). The command does nothing while this is empty or whitespace.</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>Alternative text for the image.</summary>
    public string AltText { get; set; } = string.Empty;
    /// <summary>Display width of the image.</summary>
    public float Width { get; set; }
    /// <summary>Display height of the image.</summary>
    public float Height { get; set; }

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        if (string.IsNullOrWhiteSpace(Source)) return;
        var action = new InsertImageAction(ctx.Document, ctx.Selection.Active, Source, AltText, Width, Height);
        var pos = ctx.UndoManager.Push(action);
        ctx.Selection.CollapseTo(pos);
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => !string.IsNullOrWhiteSpace(Source);
}

// ── Remove Image ──────────────────────────────────────────────────────────────

/// <summary>
/// Removes an image at or adjacent to the caret. Set <see cref="Image"/> before
/// calling <see cref="Execute"/>.
/// </summary>
public sealed class RemoveImageCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "RemoveImage";
    /// <inheritdoc/>
    public string Description => "Removes an inline image from the document.";

    /// <summary>The image to remove. Must be set before Execute is called.</summary>
    public ImageInline? Image { get; set; }

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        if (Image is null) return;
        ImageEngine.RemoveImage(ctx.Document, Image);
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => Image is not null;
}
