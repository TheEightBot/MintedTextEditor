using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Localization;

namespace MintedTextEditor.Core.Accessibility;

/// <summary>
/// Computes the screen-reader description and hint of the editor surface from its state,
/// using the accessibility strings on <see cref="EditorStrings"/>. Host controls apply the
/// result to their native accessibility APIs (e.g. MAUI <c>SemanticProperties</c>).
/// </summary>
public static class EditorAccessibility
{
    /// <summary>Separator placed between the parts of the editor description.</summary>
    public const string DescriptionSeparator = ", ";

    /// <summary>
    /// Returns accessibility properties for the editor surface.
    /// <para>
    /// <see cref="AccessibilityProperties.Label"/> is <paramref name="label"/> (by default
    /// <see cref="EditorStrings.EditorAccessibilityLabel"/>), followed by <see cref="EditorStrings.EditorReadOnlyAccessibilityState"/> when
    /// <paramref name="isReadOnly"/> is true, and by <paramref name="placeholderText"/> when
    /// <paramref name="isEmpty"/> is true and the placeholder is not blank (the text a sighted
    /// user sees in an empty editor). Parts are joined with <see cref="DescriptionSeparator"/>.
    /// </para>
    /// <para>
    /// <see cref="AccessibilityProperties.Hint"/> is <see cref="EditorStrings.EditorAccessibilityHint"/>,
    /// or empty when the editor is read-only, since it cannot be edited.
    /// </para>
    /// </summary>
    /// <param name="isReadOnly">Whether the editor rejects input.</param>
    /// <param name="isEmpty">Whether the document is empty (see <see cref="IsEmpty"/>).</param>
    /// <param name="placeholderText">Placeholder drawn while the document is empty, or null.</param>
    /// <param name="strings">String provider; defaults to <see cref="EditorStrings.Current"/>.</param>
    /// <param name="label">
    /// First part of the label in place of <see cref="EditorStrings.EditorAccessibilityLabel"/>,
    /// e.g. a description the app set on the editor itself; null uses the string provider's label.
    /// </param>
    public static AccessibilityProperties ForEditor(
        bool isReadOnly, bool isEmpty, string? placeholderText, EditorStrings? strings = null,
        string? label = null)
    {
        strings ??= EditorStrings.Current;

        var parts = new List<string>(3);
        AddIfNotBlank(parts, label ?? strings.EditorAccessibilityLabel);
        if (isReadOnly)
            AddIfNotBlank(parts, strings.EditorReadOnlyAccessibilityState);
        if (isEmpty)
            AddIfNotBlank(parts, placeholderText);

        return new AccessibilityProperties
        {
            Label = string.Join(DescriptionSeparator, parts),
            Hint = isReadOnly ? string.Empty : strings.EditorAccessibilityHint ?? string.Empty,
        };
    }

    /// <summary>
    /// Whether <paramref name="document"/> is empty in the sense used for the placeholder:
    /// a single paragraph with no inline content.
    /// </summary>
    /// <param name="document">The document to inspect.</param>
    public static bool IsEmpty(Document.Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Blocks.Count == 1
            && document.Blocks[0] is Paragraph p
            && p.Inlines.Count == 0;
    }

    private static void AddIfNotBlank(List<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add(value.Trim());
    }
}
