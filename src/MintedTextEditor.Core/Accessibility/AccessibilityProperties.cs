namespace MintedTextEditor.Core.Accessibility;

/// <summary>
/// Accessibility metadata that may be attached to editor UI elements (toolbar
/// buttons, context menu items, etc.) by the host layer.
/// These values are intended to be passed through to native accessibility APIs
/// (e.g. <c>AutomationProperties</c> on WinUI/MAUI).
/// </summary>
public sealed record AccessibilityProperties
{
    /// <summary>Short human-readable label announcing what the element is.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Additional hint read after the label (e.g. "Double-tap to activate").</summary>
    public string Hint { get; init; } = string.Empty;

    /// <summary>Whether this element should be excluded from the accessibility tree.</summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// Live region importance for screen-reader announcements.
    /// <see cref="LiveRegionMode.Off"/> means no automatic announcement.
    /// </summary>
    public LiveRegionMode LiveRegion { get; init; } = LiveRegionMode.Off;

    /// <summary>Empty accessibility properties (hidden from accessibility tree).</summary>
    public static readonly AccessibilityProperties Hidden = new() { IsHidden = true };

    /// <summary>Creates properties with only a label.</summary>
    public static AccessibilityProperties WithLabel(string label) => new() { Label = label };

    /// <summary>Creates properties with a label and hint.</summary>
    public static AccessibilityProperties WithLabelAndHint(string label, string hint)
        => new() { Label = label, Hint = hint };
}

/// <summary>
/// Controls how aggressively a screen reader announces changes to an element.
/// Mirrors the ARIA <c>aria-live</c> attribute semantics.
/// </summary>
public enum LiveRegionMode
{
    /// <summary>No automatic announcements.</summary>
    Off,

    /// <summary>Announcements are made when the user is idle (polite).</summary>
    Polite,

    /// <summary>Announcements interrupt the user immediately (assertive).</summary>
    Assertive
}
