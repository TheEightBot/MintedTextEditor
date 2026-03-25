using MintedTextEditor.Core.Events;
using System.Text;

namespace SampleApp.Maui.Pages;

public partial class EventMonitorPage : ContentPage
{
    private readonly StringBuilder _log = new();
    private int _eventCount;

    public EventMonitorPage()
    {
        InitializeComponent();
        Editor.LoadHtml(
            "<p>Interact with this editor to see events logged below.</p>" +
            "<p>Try typing, selecting text, or clicking a <a href=\"https://example.com\">hyperlink</a>.</p>");
    }

    private void Append(string message)
    {
        _eventCount++;
        _log.Insert(0, $"[{_eventCount:D3}] {message}\n");
        // Keep log manageable
        if (_eventCount > 100)
        {
            int lastNewline = _log.ToString().LastIndexOf('\n', _log.Length - 2);
            if (lastNewline > 0)
                _log.Remove(lastNewline, _log.Length - lastNewline);
        }
        LblLog.Text = _log.ToString();
    }

    private void OnTextChanged(object? sender, EditorTextChangedEventArgs e)
        => Append($"TextChanged  — range={e.AffectedRange}");

    private void OnSelectionChanged(object? sender, EditorSelectionChangedEventArgs e)
        => Append($"SelectionChanged — empty={e.IsEmpty}");

    private void OnHyperlinkClicked(object? sender, HyperlinkClickedEventArgs e)
        => Append($"HyperlinkClicked — url={e.Url}");
}
