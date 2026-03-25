namespace MintedTextEditor.Core.Commands;

/// <summary>
/// Maintains a keyed collection of <see cref="IEditorCommand"/> instances.
/// Commands are stored under their <see cref="IEditorCommand.Name"/>.
/// </summary>
public sealed class EditorCommandRegistry
{
    private readonly Dictionary<string, IEditorCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    // ── Registration ─────────────────────────────────────────────────────

    /// <summary>
    /// Adds <paramref name="command"/> to the registry.
    /// Overwrites any existing command with the same name.
    /// </summary>
    public void Register(IEditorCommand command) =>
        _commands[command.Name] = command;

    /// <summary>Removes the command with the given name. Returns true if it existed.</summary>
    public bool Unregister(string name) => _commands.Remove(name);

    // ── Lookup ───────────────────────────────────────────────────────────

    /// <summary>Returns the command registered under <paramref name="name"/>, or null.</summary>
    public IEditorCommand? Get(string name) =>
        _commands.TryGetValue(name, out var cmd) ? cmd : null;

    /// <summary>All registered commands.</summary>
    public IReadOnlyCollection<IEditorCommand> AllCommands => _commands.Values;

    // ── Execution ────────────────────────────────────────────────────────

    /// <summary>
    /// Looks up a command by name, checks <see cref="IEditorCommand.CanExecute"/>, and
    /// invokes it. Returns <c>true</c> when the command was found and executed.
    /// </summary>
    public bool Execute(string name, EditorContext context)
    {
        var cmd = Get(name);
        if (cmd is null || !cmd.CanExecute(context)) return false;
        cmd.Execute(context);
        return true;
    }

    // ── Default Registry ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a new registry pre-populated with all built-in commands.
    /// </summary>
    public static EditorCommandRegistry CreateDefault()
    {
        var reg = new EditorCommandRegistry();

        // Formatting
        reg.Register(new ToggleBoldCommand());
        reg.Register(new ToggleItalicCommand());
        reg.Register(new ToggleUnderlineCommand());
        reg.Register(new ToggleStrikethroughCommand());
        reg.Register(new ToggleSubscriptCommand());
        reg.Register(new ToggleSuperscriptCommand());
        reg.Register(new ClearFormattingCommand());

        // Paragraph
        reg.Register(new AlignLeftCommand());
        reg.Register(new AlignCenterCommand());
        reg.Register(new AlignRightCommand());
        reg.Register(new AlignJustifyCommand());
        reg.Register(new ToggleBulletListCommand());
        reg.Register(new ToggleNumberListCommand());
        reg.Register(new IncreaseIndentCommand());
        reg.Register(new DecreaseIndentCommand());

        // Edit
        reg.Register(new UndoCommand());
        reg.Register(new RedoCommand());
        reg.Register(new CopyCommand());
        reg.Register(new CutCommand());
        reg.Register(new SelectAllCommand());

        // Hyperlink
        reg.Register(new InsertHyperlinkCommand());
        reg.Register(new RemoveHyperlinkCommand());
        reg.Register(new OpenHyperlinkCommand());

        // Image
        reg.Register(new InsertImageCommand());
        reg.Register(new RemoveImageCommand());

        // Table
        reg.Register(new InsertTableCommand());
        reg.Register(new InsertRowAboveCommand());
        reg.Register(new InsertRowBelowCommand());
        reg.Register(new DeleteRowCommand());
        reg.Register(new InsertColumnLeftCommand());
        reg.Register(new InsertColumnRightCommand());
        reg.Register(new DeleteColumnCommand());
        reg.Register(new DeleteTableCommand());

        // Font
        reg.Register(new ApplyFontFamilyCommand());
        reg.Register(new ApplyFontSizeCommand());
        reg.Register(new ApplyTextColorCommand());
        reg.Register(new ApplyHighlightColorCommand());

        return reg;
    }
}
