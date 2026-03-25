# Toolbar Customization

MintedTextEditor ships with a default toolbar that covers all standard formatting operations. You can replace the entire toolbar, add/remove groups, create custom commands, or hide the toolbar entirely.

## Default Toolbar

The default toolbar is automatically rendered above the editor canvas. It contains groups for:

1. History (Undo, Redo)
2. Character formatting (Bold, Italic, Underline, Strikethrough)
3. Script (Subscript, Superscript)
4. Font (Family, Size, Color, Highlight)
5. Paragraph (Alignment, Lists, Indent, Outdent)
6. Headings (H1–H6)
7. Insert (Hyperlink, Image, Table)

## Replacing the Toolbar Definition

The toolbar is described by a `ToolbarDefinition` — a list of `ToolbarGroup` entries, each holding one or more `ToolbarItemKind` values.

```csharp
editor.ToolbarDefinition = new ToolbarDefinition
{
    Groups =
    [
        new ToolbarGroup(ToolbarItemKind.Bold, ToolbarItemKind.Italic, ToolbarItemKind.Underline),
        new ToolbarGroup(ToolbarItemKind.BulletList, ToolbarItemKind.NumberedList),
        new ToolbarGroup(ToolbarItemKind.Undo, ToolbarItemKind.Redo),
    ]
};
```

## Available ToolbarItemKind Values

| Value | Description |
|---|---|
| `Undo` / `Redo` | History navigation |
| `Bold` / `Italic` / `Underline` / `Strikethrough` | Basic character formatting |
| `Subscript` / `Superscript` | Script formatting |
| `FontFamily` / `FontSize` | Font picker controls |
| `ForegroundColor` / `BackgroundColor` | Color pickers |
| `AlignLeft` / `AlignCenter` / `AlignRight` / `AlignJustify` | Paragraph alignment |
| `BulletList` / `NumberedList` | List toggling |
| `Indent` / `Outdent` | Indentation |
| `Heading1`–`Heading6` | Heading level shortcuts |
| `InsertHyperlink` | Hyperlink dialog |
| `InsertImage` | Image picker |
| `InsertTable` | Table insertion |
| `ClearFormatting` | Strip all character formatting from selection |

## Hiding the Toolbar

```csharp
editor.ShowToolbar = false;
```

## Custom Toolbar Commands

You can register custom commands and surface them as toolbar items.

### 1. Implement `IEditorCommand`

```csharp
public class WordCountCommand : IEditorCommand
{
    public string Id => "WordCount";
    public string Label => "Word Count";
    public bool CanExecute(EditorContext context) => true;

    public DocumentPosition Execute(EditorContext context)
    {
        var words = context.Document.Blocks
            .OfType<Paragraph>()
            .Sum(p => p.PlainText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

        // Show a toast, alert, etc.
        Console.WriteLine($"Words: {words}");

        return context.CaretPosition;
    }
}
```

### 2. Register the Command

```csharp
editor.CommandRegistry.Register(new WordCountCommand());
```

### 3. Add to Toolbar

```csharp
editor.ToolbarDefinition = new ToolbarDefinition
{
    Groups =
    [
        // ... existing groups ...
        new ToolbarGroup(new CustomToolbarItem("WordCount", label: "Words")),
    ]
};
```

## Toolbar Renderer

For complete control over appearance, you can replace the toolbar renderer:

```csharp
editor.ToolbarRenderer = new MyCustomToolbarRenderer();
```

Implement `IToolbarRenderer` to draw the toolbar canvas, handle hit testing, and raise command events.
