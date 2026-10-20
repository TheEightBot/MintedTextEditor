using System.Runtime.InteropServices;
using HarfBuzzSharp;
using SkiaSharp;
using HBBuffer = HarfBuzzSharp.Buffer;
using HBFont = HarfBuzzSharp.Font;

namespace MintedTextEditor.SkiaSharp;

/// <summary>
/// The glyphs and pen positions produced by shaping a string, ready to draw with the
/// <see cref="SKFont"/> it was shaped for. Positions are relative to the run origin.
/// </summary>
internal sealed record ShapedRun(ushort[] Glyphs, SKPoint[] Positions, float Width);

/// <summary>
/// HarfBuzz text shaper for one typeface. Shaping applies the font's substitution and
/// positioning rules (GSUB/GPOS, or AAT morx/kerx on Apple fonts), which is what turns an
/// emoji ZWJ sequence such as 👨‍👩‍👧 into a single family glyph, a flag pair into one flag,
/// and gives kerning, ligatures and complex-script forms to ordinary text.
/// <para/>
/// Shapers are cached for the life of the process (building one costs milliseconds) and
/// are shared by every <see cref="SkiaDrawingContext"/>. A shaper never holds on to the
/// <see cref="SKTypeface"/> it was created from — drawing contexts dispose theirs every
/// frame — so the tables HarfBuzz needs are copied up front. Only shaping tables are
/// copied: glyph outlines and bitmaps (glyf, CFF, sbix, CBDT…) are never needed, which
/// keeps very large color-emoji fonts from being loaded into memory.
/// </summary>
internal sealed class HarfBuzzTextShaper
{
    // HarfBuzz works in integer units; shape at a fixed scale and rescale to the font size.
    private const int ShapingScale = 512;

    private static readonly string[] ShapingTables =
    {
        "cmap", "head", "hhea", "hmtx", "maxp", "OS/2", "post",
        "GSUB", "GPOS", "GDEF", "BASE",
        "morx", "mort", "kerx", "kern", "trak", "ankr", "feat",
        "vhea", "vmtx", "VORG",
        "fvar", "avar", "HVAR", "VVAR", "MVAR",
    };

    private static readonly Dictionary<ShaperKey, HarfBuzzTextShaper?> Cache = new();

    private readonly HBFont _font;

    private HarfBuzzTextShaper(HBFont font) => _font = font;

    /// <summary>
    /// Returns the shared shaper for <paramref name="typeface"/>, or <c>null</c> when the
    /// typeface exposes no font tables (callers then fall back to unshaped drawing).
    /// </summary>
    public static HarfBuzzTextShaper? Get(SKTypeface typeface)
    {
        // Typeface wrappers are recreated every frame, so identify the font by its
        // properties rather than by reference. The glyph count tells apart different
        // fonts that happen to share a family name and style.
        var key = new ShaperKey(typeface.FamilyName, typeface.FontWeight, typeface.FontWidth,
            typeface.FontSlant, typeface.GlyphCount);

        lock (Cache)
        {
            if (!Cache.TryGetValue(key, out var shaper))
            {
                shaper = Create(typeface);
                Cache[key] = shaper;
            }

            return shaper;
        }
    }

    private static HarfBuzzTextShaper? Create(SKTypeface typeface)
    {
        var tables = new Dictionary<Tag, byte[]>();
        foreach (var name in ShapingTables)
        {
            var tag = Tag.Parse(name);
            if (typeface.TryGetTableData(tag, out var data) && data is { Length: > 0 })
                tables[tag] = data;
        }

        if (!tables.ContainsKey(Tag.Parse("cmap")))
            return null;

        var face = new Face((_, tag) => tables.TryGetValue(tag, out var data) ? CreateBlob(data) : Blob.Empty)
        {
            UnitsPerEm = typeface.UnitsPerEm,
            GlyphCount = typeface.GlyphCount,
        };
        face.MakeImmutable();

        var font = new HBFont(face);
        font.SetScale(ShapingScale, ShapingScale);
        font.SetFunctionsOpenType();
        return new HarfBuzzTextShaper(font);
    }

    private static Blob CreateBlob(byte[] data)
    {
        // HarfBuzz reads the table from native memory, so pin the bytes for the blob's life.
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        return new Blob(handle.AddrOfPinnedObject(), data.Length, MemoryMode.ReadOnly, () => handle.Free());
    }

    /// <summary>
    /// Shapes <paramref name="text"/> at <paramref name="size"/> points. Results are cached:
    /// layout measures the same words repeatedly and every frame redraws the same lines.
    /// </summary>
    public ShapedRun Shape(string text, float size)
    {
        var key = (text, size);
        lock (_results)
        {
            if (_results.TryGetValue(key, out var cached))
                return cached;
        }

        var shaped = ShapeUncached(text, size);
        lock (_results)
        {
            // Bounded by clearing rather than LRU bookkeeping; the working set (visible
            // lines and the words of the paragraph being edited) refills it immediately.
            if (_results.Count >= MaxCachedResults)
                _results.Clear();
            _results[key] = shaped;
        }

        return shaped;
    }

    private const int MaxCachedResults = 4096;
    private readonly Dictionary<(string Text, float Size), ShapedRun> _results = new();

    private ShapedRun ShapeUncached(string text, float size)
    {
        using var buffer = new HBBuffer();
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        _font.Shape(buffer);

        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();
        float scale = size / ShapingScale;

        var glyphs = new ushort[infos.Length];
        var points = new SKPoint[infos.Length];
        float penX = 0f;
        for (int i = 0; i < infos.Length; i++)
        {
            glyphs[i] = (ushort)infos[i].Codepoint;
            points[i] = new SKPoint(penX + positions[i].XOffset * scale, -positions[i].YOffset * scale);
            penX += positions[i].XAdvance * scale;
        }

        return new ShapedRun(glyphs, points, penX);
    }

    private readonly record struct ShaperKey(string Family, int Weight, int Width, SKFontStyleSlant Slant, int GlyphCount);
}
