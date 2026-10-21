# Toolbar Customization

MintedTextEditor ships with a default toolbar that covers all standard formatting operations. You can replace the entire toolbar, pick a preset, add custom commands, or hide the toolbar entirely. Toolbar types live in `MintedTextEditor.Core.Toolbar`.

## Default Toolbar

The default toolbar (`ToolbarDefinition.CreateDefault()`) is drawn above the editor canvas. It contains groups for:

1. History (Undo, Redo)
2. Font (Family, Size)
3. Formatting (Bold, Italic, Underline, Strikethrough)
4. Colors (Text Color, Highlight Color)
5. Alignment (Left, Center, Right, Justify)
6. Lists (Bullet, Number, Decrease/Increase Indent)
7. Script (Subscript, Superscript)
8. Insert (Hyperlink, Image, Table)
9. Edit (Edit, Object and Table menus: copy/cut/paste/select all, open/remove link, remove image, row/column/table actions)
10. Paragraph (Heading level, Clear Formatting)

Other presets: `ToolbarDefinition.CreateFormattingOnly()`, `CreateInsertOnly()` and `CreateMinimal()`.

```csharp
editor.ToolbarDefinition = ToolbarDefinition.CreateMinimal();
```

## Replacing the Toolbar Definition

The toolbar is described by a `ToolbarDefinition` — a list of `ToolbarGroup` entries, each holding `ToolbarItem`s:

| Item | Description |
|---|---|
| `ToolbarButton` | A button that runs its `Command` (`IsToggle` buttons show `IsActive` state) |
| `ToolbarDropdown` | A list of string `Items` with `OnSelectionChanged` |
| `ToolbarColorPicker` | A color palette with `OnColorSelected` |
| `ToolbarSeparator` | A visual separator |

Every item has `Label`, `Icon`, `IsEnabled` and `IsVisible`.

```csharp
using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Toolbar;

var def = new ToolbarDefinition();
def.Groups.Add(new ToolbarGroup
{
    Name = "Formatting",
    Items =
    {
        new ToolbarButton { Label = "Bold",      Icon = "bold",      Command = new ToggleBoldCommand(),      IsToggle = true },
        new ToolbarButton { Label = "Italic",    Icon = "italic",    Command = new ToggleItalicCommand(),    IsToggle = true },
        new ToolbarButton { Label = "Underline", Icon = "underline", Command = new ToggleUnderlineCommand(), IsToggle = true },
    },
});
def.Groups.Add(new ToolbarGroup
{
    Name = "History",
    Items =
    {
        new ToolbarButton { Label = "Undo", Icon = "undo", Command = new UndoCommand() },
        new ToolbarButton { Label = "Redo", Icon = "redo", Command = new RedoCommand() },
    },
});

editor.ToolbarDefinition = def;
```

For a single-group toolbar, set `ToolbarItems` to a flat list instead; it takes precedence over `ToolbarDefinition`:

```csharp
editor.ToolbarItems = new List<ToolbarItem>
{
    new ToolbarButton { Label = "Bold", Icon = "bold", Command = new ToggleBoldCommand(), IsToggle = true },
    new ToolbarSeparator(),
    new ToolbarButton { Label = "Undo", Icon = "undo", Command = new UndoCommand() },
};
```

## Built-in Icon Keys

On a `ToolbarButton`, the `Icon` value selects a built-in icon from the current icon pack (`ToolbarIconPack.Lucide`, `Heroicons` or `MaterialSymbols`); a button whose key has no icon shows its `Label` instead. Some keys also give an item built-in behaviour:

| Keys | Notes |
|---|---|
| `undo`, `redo` | Run the editor's undo/redo |
| `bold`, `italic`, `underline`, `strikethrough`, `subscript`, `superscript`, `clear-formatting` | |
| `align-left`, `align-center`, `align-right`, `align-justify` | |
| `bullet-list`, `number-list`, `indent-decrease`, `indent-increase` | |
| `hyperlink`, `image`, `table` | Prompt for a URL / image source / table size |
| `font-family`, `font-size`, `heading`, `edit-actions`, `object-actions`, `table-actions` | On a `ToolbarDropdown`, the editor fills in the items and behaviour |
| `text-color`, `highlight-color` | On a `ToolbarColorPicker`, the editor applies the chosen color |

## Toolbar Layout

```csharp
def.LayoutMode = ToolbarLayoutMode.Wrap;   // Wrap (default), Scroll or Overflow
def.MaxRows = 1;                           // extra items go into an overflow (…) button; 0 = unlimited
editor.ToolbarDefinition = def;
```

`editor.ToolbarMaxRows` sets `MaxRows` on the toolbar currently shown; set it after assigning a new `ToolbarDefinition`, or set `MaxRows` on the definition as above.

## Hiding the Toolbar

```csharp
editor.ShowToolbar = false;
```

## Custom Toolbar Commands

You can surface your own commands as toolbar buttons.

### 1. Implement `IEditorCommand`

```csharp
public class WordCountCommand : IEditorCommand
{
    public string Name => "WordCount";
    public string Description => "Counts the words in the document.";
    public bool CanExecute(EditorContext context) => true;

    public void Execute(EditorContext context)
    {
        var words = context.Document.GetText()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        // Show a toast, alert, etc.
        Console.WriteLine($"Words: {words}");
    }
}
```

### 2. Add to Toolbar

```csharp
var def = ToolbarDefinition.CreateDefault();
def.Groups.Add(new ToolbarGroup
{
    Name = "Tools",
    Items = { new ToolbarButton { Label = "Words", Command = new WordCountCommand() } },
});
editor.ToolbarDefinition = def;
```

When tapped, the editor checks `CanExecute`, runs the command with the current `EditorContext`, then refreshes the caret and redraws. The sample app's Custom Command and Dynamic Toolbar pages show more.

## Toolbar Appearance

Toolbar colors come from the theme (`ToolbarBackground`, `ToolbarButtonColor`, `ToolbarActiveColor`, `ToolbarSeparatorColor`); see [Theming](theming.md). Icons are tinted with `ToolbarButtonColor` at draw time.
