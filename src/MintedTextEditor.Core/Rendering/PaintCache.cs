using System.Collections.Concurrent;

namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Caches commonly used <see cref="EditorPaint"/> instances to avoid per-frame allocations.
/// Thread-safe for concurrent access during rendering.
/// </summary>
public class PaintCache
{
    private readonly ConcurrentDictionary<long, EditorPaint> _textPaints = new();

    /// <summary>
    /// Gets or creates a cached paint for text rendering with the given parameters.
    /// </summary>
    public EditorPaint GetTextPaint(EditorColor color, string fontFamily, float fontSize, bool bold, bool italic)
    {
        long key = ComputeKey(color, fontFamily, fontSize, bold, italic);
        return _textPaints.GetOrAdd(key, _ => new EditorPaint
        {
            Color = color,
            IsAntiAlias = true,
            Font = new EditorFont(fontFamily, fontSize, bold, italic)
        });
    }

    /// <summary>
    /// Clears all cached paints. Call when styles change significantly.
    /// </summary>
    public void Clear() => _textPaints.Clear();

    /// <summary>Number of cached paint objects.</summary>
    public int Count => _textPaints.Count;

    private static long ComputeKey(EditorColor color, string fontFamily, float fontSize, bool bold, bool italic)
    {
        int hash1 = HashCode.Combine(color.R, color.G, color.B, color.A, fontFamily, fontSize);
        int hash2 = HashCode.Combine(bold, italic);
        return ((long)hash1 << 32) | (uint)hash2;
    }
}
