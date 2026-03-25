using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Input;

/// <summary>
/// Central input dispatcher that translates keyboard and pointer events
/// into caret navigation, text editing, and scroll operations.
/// </summary>
public class EditorInputController
{
    private readonly Caret _caret;
    private readonly CaretRenderer _caretRenderer;
    private readonly Selection _selection = new Selection();
    private bool _isDragging;

    public EditorInputController(Caret caret, CaretRenderer caretRenderer)
    {
        _caret = caret;
        _caretRenderer = caretRenderer;
    }

    /// <summary>The number of lines to jump for Page Up / Page Down.</summary>
    public int PageLineCount { get; set; } = 20;

    /// <summary>Current selection state. IsEmpty when no text is selected.</summary>
    public Selection Selection => _selection;

    /// <summary>
    /// Configurable keyboard shortcuts evaluated before built-in key handling.
    /// </summary>
    public IList<EditorKeyBinding> KeyBindings { get; } = new List<EditorKeyBinding>();

    /// <summary>
    /// Optional clipboard provider. When set, Ctrl/Cmd+C, Ctrl/Cmd+X, and Ctrl/Cmd+V
    /// are handled automatically by <see cref="HandleKeyDown"/>.
    /// </summary>
    public IClipboardProvider? ClipboardProvider { get; set; }

    /// <summary>
    /// Optional undo manager. When set, Ctrl+Z triggers undo and Ctrl+Y / Ctrl+Shift+Z
    /// trigger redo, with the caret moved to the restored position.
    /// </summary>
    public UndoManager? UndoManager { get; set; }

    /// <summary>
    /// Optional formatting engine. When set, Ctrl/Cmd+B/I/U toggle bold/italic/underline on the
    /// current selection, and the pending style is applied to the next typed character.
    /// </summary>
    public FormattingEngine? FormattingEngine { get; set; }

    /// <summary>
    /// Optional font-formatting provider for pending font changes applied at a collapsed caret.
    /// </summary>
    public FontFormattingEngine? FontFormattingEngine { get; set; }

    /// <summary>
    /// Handles a key-down event, dispatching to the appropriate navigation or edit action.
    /// Returns true if the event was handled.
    /// </summary>
    public bool HandleKeyDown(EditorKeyEventArgs e, Document.Document document, DocumentLayout layout, IDrawingContext context)
    {
        if (!e.IsKeyDown) return false;

        foreach (var keyBinding in KeyBindings)
        {
            if (!keyBinding.Matches(e)) continue;
            if (keyBinding.Handler(this, document, layout, context))
                return true;
        }

        bool ctrl = e.HasControlOrMeta;
        bool shift = e.Modifiers.HasFlag(InputModifiers.Shift);
        bool alt = e.Modifiers.HasFlag(InputModifiers.Alt);

        switch (e.Key)
        {
            case EditorKey.Delete when ctrl && shift:
            case EditorKey.Backspace when ctrl && shift:
                return TryDeleteCurrentTable(document);

            case EditorKey.Up when ctrl && alt:
                return TryInsertTableRow(document, below: false);

            case EditorKey.Down when ctrl && alt:
                return TryInsertTableRow(document, below: true);

            case EditorKey.Left when ctrl && alt:
                return TryInsertTableColumn(document, right: false);

            case EditorKey.Right when ctrl && alt:
                return TryInsertTableColumn(document, right: true);

            case EditorKey.Backspace when ctrl && alt:
                return TryDeleteCurrentTableRow(document);

            case EditorKey.Delete when ctrl && alt:
                return TryDeleteCurrentTableColumn(document);

            case EditorKey.Left:
                if (ctrl) MoveWordLeft(document, layout, context, extend: shift);
                else MoveLeft(document, layout, context, extend: shift);
                return true;

            case EditorKey.Right:
                if (ctrl) MoveWordRight(document, layout, context, extend: shift);
                else MoveRight(document, layout, context, extend: shift);
                return true;

            case EditorKey.Up:
                MoveUp(document, layout, context, extend: shift);
                return true;

            case EditorKey.Down:
                MoveDown(document, layout, context, extend: shift);
                return true;

            case EditorKey.Home:
                if (ctrl) MoveDocStart(extend: shift);
                else MoveHome(document, layout, context, extend: shift);
                return true;

            case EditorKey.End:
                if (ctrl) MoveDocEnd(document, extend: shift);
                else MoveEnd(document, layout, context, extend: shift);
                return true;

            case EditorKey.PageUp:
                MovePageUp(document, layout, context, extend: shift);
                return true;

            case EditorKey.PageDown:
                MovePageDown(document, layout, context, extend: shift);
                return true;

            case EditorKey.Z when ctrl:
                if (shift)
                {
                    if (UndoManager?.CanRedo == true)
                    {
                        var redoPos = UndoManager.Redo();
                        if (redoPos.HasValue) { _selection.CollapseTo(redoPos.Value); _caret.MoveTo(redoPos.Value); }
                        return true;
                    }
                    return false;
                }
                else
                {
                    if (UndoManager?.CanUndo == true)
                    {
                        var undoPos = UndoManager.Undo();
                        if (undoPos.HasValue) { _selection.CollapseTo(undoPos.Value); _caret.MoveTo(undoPos.Value); }
                        return true;
                    }
                    return false;
                }

            case EditorKey.Y when ctrl:
                if (UndoManager?.CanRedo == true)
                {
                    var yRedoPos = UndoManager.Redo();
                    if (yRedoPos.HasValue) { _selection.CollapseTo(yRedoPos.Value); _caret.MoveTo(yRedoPos.Value); }
                    return true;
                }
                return false;

            case EditorKey.A when ctrl:
                SelectAll(document);
                return true;

            case EditorKey.B when ctrl:
                if (FormattingEngine != null)
                {
                    var bRange = _selection.Range;
                    FormattingEngine.ToggleBold(document, bRange);
                    return true;
                }
                return false;

            case EditorKey.I when ctrl:
                if (FormattingEngine != null)
                {
                    var iRange = _selection.Range;
                    FormattingEngine.ToggleItalic(document, iRange);
                    return true;
                }
                return false;

            case EditorKey.U when ctrl:
                if (FormattingEngine != null)
                {
                    var uRange = _selection.Range;
                    FormattingEngine.ToggleUnderline(document, uRange);
                    return true;
                }
                return false;

            case EditorKey.C when ctrl && ClipboardProvider != null:
                _ = HandleCopyAsync(document);
                return true;

            case EditorKey.X when ctrl && ClipboardProvider != null:
                _ = HandleCutAsync(document);
                return true;

            case EditorKey.V when ctrl && ClipboardProvider != null:
                _ = HandlePasteAsync(document);
                return true;

            case EditorKey.Backspace:
                HandleBackspace(document);
                return true;

            case EditorKey.Delete:
                HandleDelete(document);
                return true;

            case EditorKey.Enter:
                HandleEnter(document);
                return true;

            case EditorKey.Tab:
                if (TryHandleTableTab(document, reverse: shift))
                    return true;
                HandleTextInput("\t", document);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Handles a text input event (printable characters from the platform keyboard).
    /// Replaces the active selection with the typed text.
    /// </summary>
    public void HandleTextInput(string text, Document.Document document)
    {
        if (string.IsNullOrEmpty(text)) return;

        DocumentPosition insertPos = _caret.Position;
        if (!_selection.IsEmpty)
        {
            insertPos = PushOrExecute(new DeleteRangeAction(document, _selection.Range));
            _selection.CollapseTo(insertPos);
        }

        var style = FormattingEngine?.ConsumePendingStyle();
        var pendingFontStyle = FontFormattingEngine?.ConsumePendingStyle();
        if (pendingFontStyle is not null)
            style = style is null ? pendingFontStyle : MergeStyles(style, pendingFontStyle);
        var newPos = PushOrExecute(new InsertTextAction(document, insertPos, text, style));
        _selection.CollapseTo(newPos);
        _caret.MoveTo(newPos);
    }

    private static TextStyle MergeStyles(TextStyle primary, TextStyle overlay)
    {
        return primary
            .WithFontFamily(overlay.FontFamily)
            .WithFontSize(overlay.FontSize)
            .WithTextColor(overlay.TextColor)
            .WithHighlightColor(overlay.HighlightColor);
    }

    // ── Undo helper ─────────────────────────────────────────────────

    /// <summary>Pushes to the undo stack (executing the action) if an UndoManager is present;
    /// otherwise executes the action directly without recording it.</summary>
    private DocumentPosition PushOrExecute(IUndoableAction action)
        => UndoManager != null ? UndoManager.Push(action) : action.Execute();

    // ── Clipboard helpers ──────────────────────────────────────────

    /// <summary>Copies the current selection to the clipboard. No-op if selection is empty.</summary>
    public async Task HandleCopyAsync(Document.Document document)
    {
        if (ClipboardProvider == null || _selection.IsEmpty) return;
        await ClipboardOperations.CopyAsync(document, _selection.Range, ClipboardProvider);
    }

    /// <summary>Cuts the current selection to the clipboard. No-op if selection is empty.</summary>
    public async Task HandleCutAsync(Document.Document document)
    {
        if (ClipboardProvider == null || _selection.IsEmpty) return;
        string text = DocumentEditor.GetSelectedText(document, _selection.Range);
        await ClipboardProvider.SetTextAsync(text);
        var pos = PushOrExecute(new DeleteRangeAction(document, _selection.Range));
        _selection.CollapseTo(pos);
        _caret.MoveTo(pos);
    }

    /// <summary>Pastes clipboard text at the caret, replacing any active selection.</summary>
    public async Task HandlePasteAsync(Document.Document document)
    {
        if (ClipboardProvider == null) return;
        string? pasteText = await ClipboardProvider.GetTextAsync();
        if (string.IsNullOrEmpty(pasteText)) return;

        DocumentPosition insertPos = _caret.Position;
        if (!_selection.IsEmpty)
        {
            insertPos = PushOrExecute(new DeleteRangeAction(document, _selection.Range));
            _selection.CollapseTo(insertPos);
        }

        var newPos = PushOrExecute(new InsertTextAction(document, insertPos, pasteText));
        _selection.CollapseTo(newPos);
        _caret.MoveTo(newPos);
    }

    /// <summary>
    /// Handles a pointer event: moves caret, extends selection (Shift), selects word/paragraph (double/triple click), and drag-selects.
    /// </summary>
    public void HandlePointerDown(EditorPointerEventArgs e, Document.Document document, DocumentLayout layout, IDrawingContext context)
    {
        if (e.Action == InputAction.Released)
        {
            _isDragging = false;
            return;
        }

        if (e.Action == InputAction.Moved)
        {
            if (_isDragging)
            {
                var dragResult = DocumentHitTester.HitTest(e.X, e.Y, layout, document, context);
                _selection.ExtendTo(dragResult.Position);
                _caret.MoveTo(dragResult.Position);
            }
            return;
        }

        if (e.Action != InputAction.Pressed) return;

        var result = DocumentHitTester.HitTest(e.X, e.Y, layout, document, context);

        if (e.ClickCount >= 3)
        {
            SelectBlock(result.Position.BlockIndex, document);
        }
        else if (e.ClickCount == 2)
        {
            SelectWord(result.Position, document);
        }
        else if (e.Modifiers.HasFlag(InputModifiers.Shift))
        {
            _selection.ExtendTo(result.Position);
            _caret.MoveTo(result.Position);
        }
        else
        {
            _selection.CollapseTo(result.Position);
            _caret.MoveTo(result.Position);
            _isDragging = true;
        }
    }

    // ── Editing helpers ───────────────────────────────────────────────

    private void HandleBackspace(Document.Document document)
    {
        if (!_selection.IsEmpty)
        {
            var pos = PushOrExecute(new DeleteRangeAction(document, _selection.Range));
            _selection.CollapseTo(pos);
            _caret.MoveTo(pos);
            return;
        }

        var caretPos = _caret.Position;
        int absOffset = GetAbsoluteOffset(document, caretPos);

        if (absOffset == 0)
        {
            // At start of block — merge with previous block (not allowed inside table cells)
            if (caretPos.IsInTableCell) return;
            if (caretPos.BlockIndex == 0) return;
            var mergePos = PushOrExecute(new MergeBlocksAction(document, caretPos.BlockIndex - 1));
            _selection.CollapseTo(mergePos);
            _caret.MoveTo(mergePos);
            return;
        }

        // Delete one character before the caret
        var startPos = AbsoluteOffsetToPosition(document, caretPos, absOffset - 1);
        var newPos = PushOrExecute(new DeleteRangeAction(document, new TextRange(startPos, caretPos)));
        _selection.CollapseTo(newPos);
        _caret.MoveTo(newPos);
    }

    private void HandleDelete(Document.Document document)
    {
        if (!_selection.IsEmpty)
        {
            var pos = PushOrExecute(new DeleteRangeAction(document, _selection.Range));
            _selection.CollapseTo(pos);
            _caret.MoveTo(pos);
            return;
        }

        var caretPos = _caret.Position;
        int absOffset = GetAbsoluteOffset(document, caretPos);

        // Compute length of the relevant text context (cell paragraph or outer block)
        int blockTextLength;
        if (caretPos.IsInTableCell)
            blockTextLength = GetParagraph(document, caretPos).Inlines.Sum(i => i.Length);
        else
            blockTextLength = GetBlockText(document, caretPos.BlockIndex).Length;

        if (absOffset >= blockTextLength)
        {
            // At end of block — merge with next block (not allowed inside table cells)
            if (caretPos.IsInTableCell) return;
            if (caretPos.BlockIndex + 1 >= document.Blocks.Count) return;
            int savedAbs = absOffset;
            PushOrExecute(new MergeBlocksAction(document, caretPos.BlockIndex));
            // Caret stays at the same text offset within the newly merged block
            var stayPos = AbsoluteOffsetToPosition(document, caretPos.BlockIndex, savedAbs);
            _selection.CollapseTo(stayPos);
            _caret.MoveTo(stayPos);
            return;
        }

        // Delete one character after the caret
        var endPos = AbsoluteOffsetToPosition(document, caretPos, absOffset + 1);
        var newPos = PushOrExecute(new DeleteRangeAction(document, new TextRange(caretPos, endPos)));
        _selection.CollapseTo(newPos);
        _caret.MoveTo(newPos);
    }

    private void HandleEnter(Document.Document document)
    {
        DocumentPosition insertPos = _caret.Position;
        if (!_selection.IsEmpty)
        {
            insertPos = PushOrExecute(new DeleteRangeAction(document, _selection.Range));
            _selection.CollapseTo(insertPos);
        }

        var newPos = PushOrExecute(new SplitBlockAction(document, insertPos));
        _selection.CollapseTo(newPos);
        _caret.MoveTo(newPos);
    }

    // ── Selection helpers ─────────────────────────────────────────────

    /// <summary>Selects all content in the document.</summary>
    public void SelectAll(Document.Document document)
    {
        _selection.SelectAll(document);
        _caret.MoveTo(_selection.Active);
    }

    private void SelectWord(DocumentPosition position, Document.Document document)
    {
        string blockText = GetBlockText(document, position.BlockIndex);
        int absOffset = GetAbsoluteOffset(document, position);

        int start = absOffset;
        while (start > 0 && IsWordChar(blockText[start - 1])) start--;

        int end = absOffset;
        while (end < blockText.Length && IsWordChar(blockText[end])) end++;

        // If cursor is on whitespace, fall back to selecting a whitespace run
        if (start == end)
        {
            while (start > 0 && !IsWordChar(blockText[start - 1])) start--;
            while (end < blockText.Length && !IsWordChar(blockText[end])) end++;
        }

        var anchor = AbsoluteOffsetToPosition(document, position.BlockIndex, start);
        var active = AbsoluteOffsetToPosition(document, position.BlockIndex, end);
        _selection.Set(anchor, active);
        _caret.MoveTo(active);
    }

    private void SelectBlock(int blockIndex, Document.Document document)
    {
        var para = GetParagraph(document, blockIndex);
        var anchor = new DocumentPosition(blockIndex, 0, 0);
        DocumentPosition active;
        if (para.Inlines.Count == 0)
        {
            active = anchor;
        }
        else
        {
            int lastInline = para.Inlines.Count - 1;
            active = new DocumentPosition(blockIndex, lastInline, para.Inlines[lastInline].Length);
        }
        _selection.Set(anchor, active);
        _caret.MoveTo(active);
    }

    // ── ApplyCaret: unified caret + selection update ───────────────────

    private void ApplyCaret(DocumentPosition newPos, bool extend, bool preserveX = false)
    {
        if (extend)
            _selection.ExtendTo(newPos);
        else
            _selection.CollapseTo(newPos);

        if (preserveX)
            _caret.MoveToPreservingX(newPos);
        else
            _caret.MoveTo(newPos);
    }

    // ── Character Navigation ──────────────────────────────────────────

    /// <summary>Moves the caret one character to the left.</summary>
    public void MoveLeft(Document.Document document, DocumentLayout layout, IDrawingContext context, bool extend = false)
    {
        var pos = _caret.Position;
        if (pos.IsInTableCell)
        {
            int abs = GetAbsoluteOffset(document, pos);
            if (abs == 0 && TryMoveOutOfTable(document, pos, toAfter: false, extend))
                return;
        }

        if (pos.Offset > 0)
        {
            // Move within current inline
            ApplyCaret(pos.With(pos.InlineIndex, pos.Offset - 1), extend);
        }
        else if (pos.InlineIndex > 0)
        {
            // Move to end of previous inline
            var paragraph = GetParagraph(document, pos);
            var prevInline = paragraph.Inlines[pos.InlineIndex - 1];
            ApplyCaret(pos.With(pos.InlineIndex - 1, prevInline.Length), extend);
        }
        else if (!pos.IsInTableCell && pos.BlockIndex > 0)
        {
            // Move to end of previous block
            var prevParagraph = GetParagraph(document, pos.BlockIndex - 1);
            int lastInlineIndex = Math.Max(0, prevParagraph.Inlines.Count - 1);
            int lastOffset = prevParagraph.Inlines.Count > 0 ? prevParagraph.Inlines[lastInlineIndex].Length : 0;
            ApplyCaret(new DocumentPosition(pos.BlockIndex - 1, lastInlineIndex, lastOffset), extend);
        }
    }

    /// <summary>Moves the caret one character to the right.</summary>
    public void MoveRight(Document.Document document, DocumentLayout layout, IDrawingContext context, bool extend = false)
    {
        var pos = _caret.Position;
        var paragraph = GetParagraph(document, pos);
        int absOffset = GetAbsoluteOffset(document, pos);
        int paragraphLength = paragraph.GetText().Length;

        if (pos.InlineIndex < paragraph.Inlines.Count)
        {
            var inline = paragraph.Inlines[pos.InlineIndex];
            if (pos.Offset < inline.Length)
            {
                ApplyCaret(pos.With(pos.InlineIndex, pos.Offset + 1), extend);
                return;
            }
        }

        // At end of current inline — move to next inline
        if (pos.InlineIndex + 1 < paragraph.Inlines.Count)
        {
            ApplyCaret(pos.With(pos.InlineIndex + 1, 0), extend);
        }
        else if (pos.IsInTableCell && absOffset >= paragraphLength)
        {
            TryMoveOutOfTable(document, pos, toAfter: true, extend);
        }
        else if (!pos.IsInTableCell && pos.BlockIndex + 1 < document.Blocks.Count)
        {
            // Move to start of next block
            ApplyCaret(new DocumentPosition(pos.BlockIndex + 1, 0, 0), extend);
        }
    }

    // ── Word Navigation ─────────────────────────────────────────────

    /// <summary>Moves the caret to the start of the previous word.</summary>
    public void MoveWordLeft(Document.Document document, DocumentLayout layout, IDrawingContext context, bool extend = false)
    {
        var pos = _caret.Position;
        string blockText = GetParagraph(document, pos).GetText();
        int absOffset = GetAbsoluteOffset(document, pos);

        if (absOffset <= 0)
        {
            // At start of block — move to end of previous block
            if (!pos.IsInTableCell && pos.BlockIndex > 0)
            {
                var prevParagraph = GetParagraph(document, pos.BlockIndex - 1);
                int lastInlineIdx = Math.Max(0, prevParagraph.Inlines.Count - 1);
                int lastOffset = prevParagraph.Inlines.Count > 0 ? prevParagraph.Inlines[lastInlineIdx].Length : 0;
                ApplyCaret(new DocumentPosition(pos.BlockIndex - 1, lastInlineIdx, lastOffset), extend);
            }
            return;
        }

        // Skip whitespace/punctuation backward, then skip word characters backward
        int newOffset = absOffset;
        while (newOffset > 0 && !IsWordChar(blockText[newOffset - 1]))
            newOffset--;
        while (newOffset > 0 && IsWordChar(blockText[newOffset - 1]))
            newOffset--;

        var newPos = AbsoluteOffsetToPosition(document, pos.BlockIndex, newOffset);
        ApplyCaret(newPos, extend);
    }

    /// <summary>Moves the caret to the end of the next word.</summary>
    public void MoveWordRight(Document.Document document, DocumentLayout layout, IDrawingContext context, bool extend = false)
    {
        var pos = _caret.Position;
        string blockText = GetParagraph(document, pos).GetText();
        int absOffset = GetAbsoluteOffset(document, pos);

        if (absOffset >= blockText.Length)
        {
            // At end of block — move to start of next block
            if (!pos.IsInTableCell && pos.BlockIndex + 1 < document.Blocks.Count)
            {
                ApplyCaret(new DocumentPosition(pos.BlockIndex + 1, 0, 0), extend);
            }
            return;
        }

        // Skip word characters forward, then skip whitespace/punctuation forward
        int newOffset = absOffset;
        while (newOffset < blockText.Length && IsWordChar(blockText[newOffset]))
            newOffset++;
        while (newOffset < blockText.Length && !IsWordChar(blockText[newOffset]))
            newOffset++;

        var newPos = AbsoluteOffsetToPosition(document, pos.BlockIndex, newOffset);
        ApplyCaret(newPos, extend);
    }

    // ── Line Navigation ─────────────────────────────────────────────

    /// <summary>Moves the caret up one line, preserving the preferred X position.</summary>
    public void MoveUp(Document.Document document, DocumentLayout layout, IDrawingContext context, bool extend = false)
    {
        EnsurePreferredX(document, layout, context);

        var (blockIdx, lineIdx) = FindCurrentLine(layout);

        if (lineIdx > 0)
        {
            // Move to previous line in same block
            var prevLine = layout.Blocks[blockIdx].Lines[lineIdx - 1];
            var hit = DocumentHitTester.HitTest(_caret.PreferredX, layout.Blocks[blockIdx].Y + prevLine.Y + prevLine.Height / 2, layout, document, context);
            ApplyCaret(hit.Position, extend, preserveX: true);
        }
        else if (blockIdx > 0)
        {
            // Move to last line of previous block
            var prevBlock = layout.Blocks[blockIdx - 1];
            if (prevBlock.Lines.Count > 0)
            {
                var lastLine = prevBlock.Lines[^1];
                var hit = DocumentHitTester.HitTest(_caret.PreferredX, prevBlock.Y + lastLine.Y + lastLine.Height / 2, layout, document, context);
                ApplyCaret(hit.Position, extend, preserveX: true);
            }
        }
    }

    /// <summary>Moves the caret down one line, preserving the preferred X position.</summary>
    public void MoveDown(Document.Document document, DocumentLayout layout, IDrawingContext context, bool extend = false)
    {
        EnsurePreferredX(document, layout, context);

        var (blockIdx, lineIdx) = FindCurrentLine(layout);
        var block = layout.Blocks[blockIdx];

        if (lineIdx < block.Lines.Count - 1)
        {
            // Move to next line in same block
            var nextLine = block.Lines[lineIdx + 1];
            var hit = DocumentHitTester.HitTest(_caret.PreferredX, block.Y + nextLine.Y + nextLine.Height / 2, layout, document, context);
            ApplyCaret(hit.Position, extend, preserveX: true);
        }
        else if (blockIdx + 1 < layout.Blocks.Count)
        {
            // Move to first line of next block
            var nextBlock = layout.Blocks[blockIdx + 1];
            if (nextBlock.Lines.Count > 0)
            {
                var firstLine = nextBlock.Lines[0];
                var hit = DocumentHitTester.HitTest(_caret.PreferredX, nextBlock.Y + firstLine.Y + firstLine.Height / 2, layout, document, context);
                ApplyCaret(hit.Position, extend, preserveX: true);
            }
        }
    }

    // ── Home / End ────────────────────────────────────────────────────

    /// <summary>Moves the caret to the beginning of the current line.</summary>
    public void MoveHome(Document.Document document, DocumentLayout layout, IDrawingContext context, bool extend = false)
    {
        var (blockIdx, lineIdx) = FindCurrentLine(layout);
        var block = layout.Blocks[blockIdx];
        var line = block.Lines[lineIdx];

        if (line.Runs.Count > 0)
        {
            var firstRun = line.Runs[0];
            var pos = RunStartToDocumentPosition(firstRun, blockIdx, document);
            ApplyCaret(pos, extend);
        }
        else
        {
            ApplyCaret(new DocumentPosition(blockIdx, 0, 0), extend);
        }
    }

    /// <summary>Moves the caret to the end of the current line.</summary>
    public void MoveEnd(Document.Document document, DocumentLayout layout, IDrawingContext context, bool extend = false)
    {
        var (blockIdx, lineIdx) = FindCurrentLine(layout);
        var block = layout.Blocks[blockIdx];
        var line = block.Lines[lineIdx];

        if (line.Runs.Count > 0)
        {
            var lastRun = line.Runs[^1];
            var pos = RunEndToDocumentPosition(lastRun, blockIdx, document);
            ApplyCaret(pos, extend);
        }
        else
        {
            ApplyCaret(new DocumentPosition(blockIdx, 0, 0), extend);
        }
    }

    // ── Document Start / End ──────────────────────────────────────────

    /// <summary>Moves the caret to the very beginning of the document.</summary>
    public void MoveDocStart(bool extend = false)
    {
        ApplyCaret(new DocumentPosition(0, 0, 0), extend);
    }

    /// <summary>Moves the caret to the very end of the document.</summary>
    public void MoveDocEnd(Document.Document document, bool extend = false)
    {
        if (document.Blocks.Count == 0)
        {
            ApplyCaret(new DocumentPosition(0, 0, 0), extend);
            return;
        }

        int lastBlockIdx = document.Blocks.Count - 1;

        if (document.Blocks[lastBlockIdx] is TableBlock lastTable && lastTable.Rows.Count > 0)
        {
            int lastRow = lastTable.Rows.Count - 1;
            int lastCol = lastTable.Rows[lastRow].Cells.Count - 1;
            var cell = lastTable.GetCell(lastRow, lastCol);
            if (cell?.Blocks.Count > 0 && cell.Blocks[^1] is Paragraph cellParagraph && cellParagraph.Inlines.Count > 0)
            {
                int cellLastInlineIdx = cellParagraph.Inlines.Count - 1;
                int cellLastOffset = cellParagraph.Inlines[cellLastInlineIdx].Length;
                ApplyCaret(new DocumentPosition(lastBlockIdx, cellLastInlineIdx, cellLastOffset, lastRow, lastCol), extend);
            }
            else
            {
                ApplyCaret(new DocumentPosition(lastBlockIdx, 0, 0, lastRow, lastCol), extend);
            }
            return;
        }

        var paragraph = GetParagraph(document, lastBlockIdx);
        if (paragraph.Inlines.Count == 0)
        {
            ApplyCaret(new DocumentPosition(lastBlockIdx, 0, 0), extend);
            return;
        }

        int lastInlineIdx = paragraph.Inlines.Count - 1;
        int lastOffset = paragraph.Inlines[lastInlineIdx].Length;
        ApplyCaret(new DocumentPosition(lastBlockIdx, lastInlineIdx, lastOffset), extend);
    }

    // ── Page Up / Down ────────────────────────────────────────────────

    /// <summary>Moves the caret up by PageLineCount lines.</summary>
    public void MovePageUp(Document.Document document, DocumentLayout layout, IDrawingContext context, bool extend = false)
    {
        EnsurePreferredX(document, layout, context);

        for (int i = 0; i < PageLineCount; i++)
        {
            var before = _caret.Position;
            MoveUp(document, layout, context, extend);
            if (_caret.Position == before) break; // already at top
        }
    }

    /// <summary>Moves the caret down by PageLineCount lines.</summary>
    public void MovePageDown(Document.Document document, DocumentLayout layout, IDrawingContext context, bool extend = false)
    {
        EnsurePreferredX(document, layout, context);

        for (int i = 0; i < PageLineCount; i++)
        {
            var before = _caret.Position;
            MoveDown(document, layout, context, extend);
            if (_caret.Position == before) break; // already at bottom
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Finds the (blockIndex, lineIndex) of the layout line containing the caret.
    /// </summary>
    internal (int BlockIndex, int LineIndex) FindCurrentLine(DocumentLayout layout)
    {
        int blockIdx = Math.Clamp(_caret.Position.BlockIndex, 0, layout.Blocks.Count - 1);
        var block = layout.Blocks[blockIdx];

        if (block.Lines.Count == 0)
            return (blockIdx, 0);

        var pos = _caret.Position;

        // Search for the line containing the caret's position
        for (int lineIdx = 0; lineIdx < block.Lines.Count; lineIdx++)
        {
            var line = block.Lines[lineIdx];
            foreach (var run in line.Runs)
            {
                if (run.SourceInline is null) continue;
                var paragraph = GetParagraph((Document.Document)block.Lines[0].Runs[0].SourceInline?.Parent?.Parent!, blockIdx);

                int inlineIdx = FindInlineIndex(run, paragraph);
                if (inlineIdx != pos.InlineIndex) continue;

                int runEnd = run.SourceOffset + run.Text.Length;
                if (pos.Offset >= run.SourceOffset && pos.Offset <= runEnd)
                    return (blockIdx, lineIdx);
            }
        }

        // Fallback: position at offset 0 maps to first line, otherwise last line
        return (blockIdx, pos.Offset == 0 && pos.InlineIndex == 0 ? 0 : block.Lines.Count - 1);
    }

    private void EnsurePreferredX(Document.Document document, DocumentLayout layout, IDrawingContext context)
    {
        if (_caret.PreferredX < 0)
            _caret.PreferredX = _caretRenderer.GetCaretX(_caret.Position, layout, document, context);
    }

    private bool TryHandleTableTab(Document.Document document, bool reverse)
    {
        var pos = _caret.Position;
        if (!TryGetCurrentTable(document, pos, out var table))
            return false;

        int row = Math.Clamp(pos.CellRow, 0, table.RowCount - 1);
        int col = Math.Clamp(pos.CellCol, 0, table.Rows[row].Cells.Count - 1);

        if (reverse)
        {
            if (col > 0)
            {
                ApplyCaret(new DocumentPosition(pos.BlockIndex, 0, 0, row, col - 1), extend: false);
                return true;
            }

            if (row > 0)
            {
                int prevRow = row - 1;
                int prevCol = Math.Max(0, table.Rows[prevRow].Cells.Count - 1);
                ApplyCaret(new DocumentPosition(pos.BlockIndex, 0, 0, prevRow, prevCol), extend: false);
                return true;
            }

            return TryMoveOutOfTable(document, pos, toAfter: false, extend: false);
        }

        if (col + 1 < table.Rows[row].Cells.Count)
        {
            ApplyCaret(new DocumentPosition(pos.BlockIndex, 0, 0, row, col + 1), extend: false);
            return true;
        }

        if (row + 1 < table.RowCount)
        {
            ApplyCaret(new DocumentPosition(pos.BlockIndex, 0, 0, row + 1, 0), extend: false);
            return true;
        }

        return TryMoveOutOfTable(document, pos, toAfter: true, extend: false);
    }

    private bool TryInsertTableRow(Document.Document document, bool below)
    {
        var pos = _caret.Position;
        if (!TryGetCurrentTable(document, pos, out var table))
            return false;

        int afterRow = below ? pos.CellRow : pos.CellRow - 1;
        PushOrExecute(new InsertRowAction(document, table, afterRow, pos.CellRow));

        int blockIndex = document.Blocks.IndexOf(table);
        if (blockIndex < 0) return true;

        int targetRow = below
            ? Math.Clamp(pos.CellRow + 1, 0, table.RowCount - 1)
            : Math.Clamp(pos.CellRow, 0, table.RowCount - 1);
        int targetCol = Math.Clamp(pos.CellCol, 0, table.Rows[targetRow].Cells.Count - 1);
        var target = new DocumentPosition(blockIndex, 0, 0, targetRow, targetCol);
        _selection.CollapseTo(target);
        _caret.MoveTo(target);
        return true;
    }

    private bool TryDeleteCurrentTableRow(Document.Document document)
    {
        var pos = _caret.Position;
        if (!TryGetCurrentTable(document, pos, out var table) || table.RowCount <= 1)
            return false;

        PushOrExecute(new DeleteRowAction(document, table, pos.CellRow));
        int blockIndex = document.Blocks.IndexOf(table);
        if (blockIndex < 0) return true;

        int targetRow = Math.Clamp(pos.CellRow, 0, table.RowCount - 1);
        int targetCol = Math.Clamp(pos.CellCol, 0, table.Rows[targetRow].Cells.Count - 1);
        var target = new DocumentPosition(blockIndex, 0, 0, targetRow, targetCol);
        _selection.CollapseTo(target);
        _caret.MoveTo(target);
        return true;
    }

    private bool TryInsertTableColumn(Document.Document document, bool right)
    {
        var pos = _caret.Position;
        if (!TryGetCurrentTable(document, pos, out var table))
            return false;

        int afterCol = right ? pos.CellCol : pos.CellCol - 1;
        PushOrExecute(new InsertColumnAction(document, table, afterCol, pos.CellCol));

        int blockIndex = document.Blocks.IndexOf(table);
        if (blockIndex < 0) return true;

        int targetCol = right
            ? Math.Clamp(pos.CellCol + 1, 0, table.ColumnCount - 1)
            : Math.Clamp(pos.CellCol, 0, table.ColumnCount - 1);
        int targetRow = Math.Clamp(pos.CellRow, 0, table.RowCount - 1);
        var target = new DocumentPosition(blockIndex, 0, 0, targetRow, targetCol);
        _selection.CollapseTo(target);
        _caret.MoveTo(target);
        return true;
    }

    private bool TryDeleteCurrentTableColumn(Document.Document document)
    {
        var pos = _caret.Position;
        if (!TryGetCurrentTable(document, pos, out var table) || table.ColumnCount <= 1)
            return false;

        PushOrExecute(new DeleteColumnAction(document, table, pos.CellCol));
        int blockIndex = document.Blocks.IndexOf(table);
        if (blockIndex < 0) return true;

        int targetRow = Math.Clamp(pos.CellRow, 0, table.RowCount - 1);
        int targetCol = Math.Clamp(pos.CellCol, 0, table.ColumnCount - 1);
        var target = new DocumentPosition(blockIndex, 0, 0, targetRow, targetCol);
        _selection.CollapseTo(target);
        _caret.MoveTo(target);
        return true;
    }

    private bool TryDeleteCurrentTable(Document.Document document)
    {
        var pos = _caret.Position;
        if (!TryGetCurrentTable(document, pos, out var table))
            return false;

        var target = PushOrExecute(new DeleteTableAction(document, table));
        _selection.CollapseTo(target);
        _caret.MoveTo(target);
        return true;
    }

    private bool TryMoveOutOfTable(Document.Document document, DocumentPosition pos, bool toAfter, bool extend)
    {
        if (!TryGetCurrentTable(document, pos, out var table))
            return false;

        bool atBoundary = toAfter ? IsLastCell(table, pos) : IsFirstCell(table, pos);
        if (!atBoundary)
            return false;

        int targetBlock = toAfter ? pos.BlockIndex + 1 : pos.BlockIndex - 1;
        if (targetBlock >= 0 && targetBlock < document.Blocks.Count)
        {
            var target = toAfter
                ? new DocumentPosition(targetBlock, 0, 0)
                : GetBlockEndPosition(document, targetBlock);
            ApplyCaret(target, extend);
            return true;
        }

        int insertAt = toAfter ? pos.BlockIndex + 1 : pos.BlockIndex;
        var paragraph = new Paragraph { Parent = document };
        document.Blocks.Insert(insertAt, paragraph);
        var insertedPos = new DocumentPosition(insertAt, 0, 0);
        document.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextInserted,
            new TextRange(insertedPos, insertedPos)));
        ApplyCaret(insertedPos, extend);
        return true;
    }

    private static bool TryGetCurrentTable(Document.Document document, DocumentPosition pos, out TableBlock table)
    {
        table = null!;
        if (!pos.IsInTableCell)
            return false;
        if (pos.BlockIndex < 0 || pos.BlockIndex >= document.Blocks.Count)
            return false;
        if (document.Blocks[pos.BlockIndex] is not TableBlock t)
            return false;

        table = t;
        return true;
    }

    private static bool IsFirstCell(TableBlock table, DocumentPosition pos)
        => pos.CellRow == 0 && pos.CellCol == 0;

    private static bool IsLastCell(TableBlock table, DocumentPosition pos)
    {
        if (table.RowCount == 0) return false;
        int lastRow = table.RowCount - 1;
        int lastCol = Math.Max(0, table.Rows[lastRow].Cells.Count - 1);
        return pos.CellRow == lastRow && pos.CellCol == lastCol;
    }

    private static DocumentPosition GetBlockEndPosition(Document.Document document, int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= document.Blocks.Count)
            return new DocumentPosition(0, 0, 0);

        if (document.Blocks[blockIndex] is TableBlock table && table.RowCount > 0)
        {
            int lastRow = table.RowCount - 1;
            int lastCol = Math.Max(0, table.Rows[lastRow].Cells.Count - 1);
            var cell = table.GetCell(lastRow, lastCol);
            if (cell?.Blocks.Count > 0 && cell.Blocks[^1] is Paragraph cellParagraph && cellParagraph.Inlines.Count > 0)
            {
                int cellLastInlineIdx = cellParagraph.Inlines.Count - 1;
                int cellLastOffset = cellParagraph.Inlines[cellLastInlineIdx].Length;
                return new DocumentPosition(blockIndex, cellLastInlineIdx, cellLastOffset, lastRow, lastCol);
            }

            return new DocumentPosition(blockIndex, 0, 0, lastRow, lastCol);
        }

        var paragraph = GetParagraph(document, blockIndex);
        if (paragraph.Inlines.Count == 0)
            return new DocumentPosition(blockIndex, 0, 0);

        int lastInline = paragraph.Inlines.Count - 1;
        return new DocumentPosition(blockIndex, lastInline, paragraph.Inlines[lastInline].Length);
    }

    private static Paragraph GetParagraph(Document.Document document, int blockIndex)
    {
        blockIndex = Math.Clamp(blockIndex, 0, document.Blocks.Count - 1);
        return document.Blocks[blockIndex] as Paragraph ?? new Paragraph();
    }

    private static Paragraph GetParagraph(Document.Document document, DocumentPosition position)
    {
        if (position.IsInTableCell)
        {
            int idx = Math.Clamp(position.BlockIndex, 0, document.Blocks.Count - 1);
            if (document.Blocks[idx] is TableBlock table)
            {
                var cell = table.GetCell(position.CellRow, position.CellCol);
                if (cell?.Blocks.Count > 0 && cell.Blocks[0] is Paragraph p) return p;
            }
        }
        return GetParagraph(document, position.BlockIndex);
    }

    /// <summary>Gets the full text of a block.</summary>
    private static string GetBlockText(Document.Document document, int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= document.Blocks.Count) return string.Empty;
        return document.Blocks[blockIndex].GetText();
    }

    /// <summary>Converts a DocumentPosition to an absolute character offset within its block.</summary>
    internal static int GetAbsoluteOffset(Document.Document document, DocumentPosition pos)
    {
        var paragraph = GetParagraph(document, pos);
        int offset = 0;
        for (int i = 0; i < pos.InlineIndex && i < paragraph.Inlines.Count; i++)
            offset += paragraph.Inlines[i].Length;
        offset += pos.Offset;
        return offset;
    }

    /// <summary>Converts an absolute character offset within a block back to a DocumentPosition.</summary>
    internal static DocumentPosition AbsoluteOffsetToPosition(Document.Document document, int blockIndex, int absoluteOffset)
    {
        var paragraph = GetParagraph(document, blockIndex);
        int remaining = absoluteOffset;

        for (int i = 0; i < paragraph.Inlines.Count; i++)
        {
            int len = paragraph.Inlines[i].Length;
            if (remaining <= len)
                return new DocumentPosition(blockIndex, i, remaining);
            remaining -= len;
        }

        // Past end — return end of last inline
        if (paragraph.Inlines.Count > 0)
        {
            int lastIdx = paragraph.Inlines.Count - 1;
            return new DocumentPosition(blockIndex, lastIdx, paragraph.Inlines[lastIdx].Length);
        }

        return new DocumentPosition(blockIndex, 0, 0);
    }

    /// <summary>
    /// Converts an absolute character offset back to a DocumentPosition, preserving cell context.
    /// </summary>
    internal static DocumentPosition AbsoluteOffsetToPosition(Document.Document document, DocumentPosition context, int absoluteOffset)
    {
        var paragraph = GetParagraph(document, context);
        int remaining = absoluteOffset;

        for (int i = 0; i < paragraph.Inlines.Count; i++)
        {
            int len = paragraph.Inlines[i].Length;
            if (remaining <= len)
                return context.With(i, remaining);
            remaining -= len;
        }

        // Past end — return end of last inline
        if (paragraph.Inlines.Count > 0)
        {
            int lastIdx = paragraph.Inlines.Count - 1;
            return context.With(lastIdx, paragraph.Inlines[lastIdx].Length);
        }

        return context.With(0, 0);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int FindInlineIndex(LayoutRun run, Paragraph paragraph)
    {
        for (int i = 0; i < paragraph.Inlines.Count; i++)
        {
            if (ReferenceEquals(paragraph.Inlines[i], run.SourceInline))
                return i;
        }
        return -1;
    }

    private static DocumentPosition RunStartToDocumentPosition(LayoutRun run, int blockIndex, Document.Document document)
    {
        if (run.SourceInline is null)
            return new DocumentPosition(blockIndex, 0, 0);

        var paragraph = GetParagraph(document, blockIndex);
        int inlineIdx = FindInlineIndex(run, paragraph);
        if (inlineIdx < 0) return new DocumentPosition(blockIndex, 0, 0);

        return new DocumentPosition(blockIndex, inlineIdx, run.SourceOffset);
    }

    private static DocumentPosition RunEndToDocumentPosition(LayoutRun run, int blockIndex, Document.Document document)
    {
        if (run.SourceInline is null)
            return new DocumentPosition(blockIndex, 0, 0);

        var paragraph = GetParagraph(document, blockIndex);
        int inlineIdx = FindInlineIndex(run, paragraph);
        if (inlineIdx < 0) return new DocumentPosition(blockIndex, 0, 0);

        int offset = run.SourceOffset + run.Text.Length;
        offset = Math.Clamp(offset, 0, run.SourceInline.Length);
        return new DocumentPosition(blockIndex, inlineIdx, offset);
    }
}
