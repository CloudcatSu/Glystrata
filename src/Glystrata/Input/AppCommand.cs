namespace Glystrata.Input;

public enum AppCommandId
{
    FileNew,
    FileOpen,
    FileSave,
    FileSaveAs,
    FileSnapshotHistory,
    FileCreateSnapshot,
    FileExit,
    SettingsOpen,
    EditUndo,
    EditRedo,
    EditFindReplace,
    EditReplace,
    ViewSplitHorizontal,
    ViewSplitVertical,
    ViewClosePane,
    ViewResetLayout,
    ViewNewView,
    ViewToggleReader,
    ViewCloseTab,
    GroupNew,
    GroupOpenFile,
    HelpMarkdownGuide,
    HelpAbout
}

/// <summary>
/// One row of the command table: what the menu says, what key runs it, and whether we are the one
/// who runs it. Keeping all three on a single line is the point — the menu text and the key handler
/// used to be two hand-maintained lists that could disagree without anything noticing.
/// </summary>
/// <param name="LocalizationKey">Menu label, or null for a command with no menu entry.</param>
/// <param name="Key"><see cref="System.Windows.Input.Key.None"/> for a command with no shortcut.</param>
/// <param name="GestureOnly">
/// The key belongs to someone else (AvalonEdit's own editing commands) and we only advertise it in
/// the menu. Intercepting it in the window's PreviewKeyDown would take the feature away from the
/// editor, which is the one place it actually works.
/// </param>
public sealed record AppCommandDefinition(
    AppCommandId Id,
    string? LocalizationKey,
    ModifierKeys Modifiers = ModifierKeys.None,
    Key Key = Key.None,
    bool GestureOnly = false);

public static class AppCommands
{
    public static readonly IReadOnlyList<AppCommandDefinition> All = new AppCommandDefinition[]
    {
        new(AppCommandId.FileNew, "file.new", ModifierKeys.Control, Key.N),
        new(AppCommandId.FileOpen, "file.open", ModifierKeys.Control, Key.O),
        new(AppCommandId.FileSave, "file.save", ModifierKeys.Control, Key.S),
        new(AppCommandId.FileSaveAs, "file.saveAs", ModifierKeys.Control | ModifierKeys.Shift, Key.S),
        new(AppCommandId.FileSnapshotHistory, "file.snapshotHistory", ModifierKeys.Control | ModifierKeys.Shift, Key.H),
        new(AppCommandId.FileCreateSnapshot, "file.createSnapshot", ModifierKeys.Control | ModifierKeys.Alt, Key.S),
        new(AppCommandId.FileExit, "file.exit"),
        new(AppCommandId.SettingsOpen, "settings.open", ModifierKeys.Control, Key.OemComma),

        // Undo/redo live in AvalonEdit; the menu only advertises the keys it already owns.
        new(AppCommandId.EditUndo, "edit.undo", ModifierKeys.Control, Key.Z, GestureOnly: true),
        new(AppCommandId.EditRedo, "edit.redo", ModifierKeys.Control, Key.Y, GestureOnly: true),
        new(AppCommandId.EditFindReplace, "edit.findReplace", ModifierKeys.Control, Key.F),
        new(AppCommandId.EditReplace, null, ModifierKeys.Control, Key.H),

        new(AppCommandId.ViewSplitHorizontal, "view.splitHorizontal", ModifierKeys.Control, Key.Oem5),
        new(AppCommandId.ViewSplitVertical, "view.splitVertical", ModifierKeys.Control | ModifierKeys.Alt, Key.Oem5),
        new(AppCommandId.ViewClosePane, "view.closePane", ModifierKeys.Control | ModifierKeys.Shift, Key.W),
        new(AppCommandId.ViewResetLayout, "view.resetLayout", ModifierKeys.Control | ModifierKeys.Alt, Key.D1),
        new(AppCommandId.ViewNewView, "view.newView", ModifierKeys.Control | ModifierKeys.Alt, Key.N),
        new(AppCommandId.ViewToggleReader, null, ModifierKeys.Control | ModifierKeys.Shift, Key.R),
        new(AppCommandId.ViewCloseTab, null, ModifierKeys.Control, Key.W),

        new(AppCommandId.GroupNew, "group.new", ModifierKeys.Control | ModifierKeys.Shift, Key.G),
        new(AppCommandId.GroupOpenFile, "group.openFile"),

        new(AppCommandId.HelpMarkdownGuide, "help.markdownGuide"),
        new(AppCommandId.HelpAbout, "help.about")
    };
}
