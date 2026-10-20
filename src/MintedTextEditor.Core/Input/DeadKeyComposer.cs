using System.Text;

namespace MintedTextEditor.Core.Input;

/// <summary>
/// Turns hardware-keyboard character codes into text, handling dead keys (accent keys on
/// international layouts). A dead key arrives as its combining accent tagged with
/// <see cref="CombiningAccentFlag"/> (Android's <c>KeyCharacterMap.COMBINING_ACCENT</c>);
/// it produces no text until the next key, which is combined with it when possible.
/// Never returns an invalid Unicode scalar.
/// </summary>
public sealed class DeadKeyComposer
{
    /// <summary>High bit marking a code as a dead-key accent.</summary>
    public const int CombiningAccentFlag = unchecked((int)0x80000000);

    /// <summary>Mask that extracts the accent code point from a dead-key code.</summary>
    public const int CombiningAccentMask = 0x7FFFFFFF;

    private readonly Func<int, int, int> _combine;
    private int _pendingAccent;

    /// <param name="combine">Combines an accent with a base character, returning 0 when they
    /// don't combine (Android: <c>KeyCharacterMap.getDeadChar</c>).</param>
    public DeadKeyComposer(Func<int, int, int> combine) => _combine = combine;

    /// <summary>Whether a dead key is waiting for the next character.</summary>
    public bool HasPendingAccent => _pendingAccent != 0;

    /// <summary>
    /// Processes one character code. Returns <see langword="null"/> when the key produces no
    /// character (not handled), an empty string when it was consumed as a dead key, or the
    /// text to insert.
    /// </summary>
    public string? Process(int unicodeChar)
    {
        if (unicodeChar == 0) return null;

        if ((unicodeChar & CombiningAccentFlag) != 0)
        {
            int accent = unicodeChar & CombiningAccentMask;
            // A second dead key flushes the first accent on its own.
            string flushed = _pendingAccent != 0 ? ToText(_pendingAccent) : string.Empty;
            _pendingAccent = accent;
            return flushed;
        }

        if (_pendingAccent != 0)
        {
            int accent = _pendingAccent;
            _pendingAccent = 0;
            int combined = _combine(accent, unicodeChar);
            return combined != 0 && Rune.IsValid(combined)
                ? ToText(combined)
                : ToText(accent) + ToText(unicodeChar);
        }

        return ToText(unicodeChar);
    }

    /// <summary>Discards a pending dead key (e.g. on focus loss).</summary>
    public void Reset() => _pendingAccent = 0;

    private static string ToText(int codePoint)
        => Rune.IsValid(codePoint) ? char.ConvertFromUtf32(codePoint) : string.Empty;
}
