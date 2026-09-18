namespace Glystrata.Input;

/// <summary>
/// The one place that turns the command table into keyboard behaviour and menu text. Both used to be
/// written out by hand in two different methods, so a changed key only ever got fixed in one of them.
/// </summary>
public sealed class ShortcutService
{
    private readonly Dictionary<AppCommandId, AppCommandDefinition> _byId = new();
    private readonly List<AppCommandDefinition> _bound = new();
    private readonly List<string> _conflicts = new();

    public ShortcutService(IEnumerable<AppCommandDefinition>? commands = null)
    {
        foreach (var command in commands ?? AppCommands.All)
        {
            _byId[command.Id] = command;
            if (command.Key == Key.None || command.GestureOnly)
            {
                continue;
            }

            var clash = _bound.Find(other => other.Key == command.Key && other.Modifiers == command.Modifiers);
            if (clash is not null)
            {
                // Recorded rather than thrown: the table is static, so a clash is a bug for the
                // verification run to catch, not something to crash a user's editor over. First
                // one wins so the behaviour stays predictable either way.
                _conflicts.Add($"{FormatGesture(command.Modifiers, command.Key)}: {clash.Id} / {command.Id}");
                continue;
            }

            _bound.Add(command);
        }
    }

    /// <summary>Commands that asked for a key another command had already taken, and so are dead.</summary>
    public IReadOnlyList<string> Conflicts => _conflicts;

    /// <summary>The text a menu item shows on its right-hand side, or null when there is no key.</summary>
    public string? GetGestureText(AppCommandId id) =>
        _byId.TryGetValue(id, out var command) && command.Key != Key.None
            ? FormatGesture(command.Modifiers, command.Key)
            : null;

    public AppCommandId? Resolve(KeyEventArgs e)
    {
        // Alt-modified keys arrive as Key.System with the real key in SystemKey; DeadCharProcessed
        // hides it the same way.
        var key = e.Key is Key.System or Key.DeadCharProcessed ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        foreach (var command in _bound)
        {
            if (command.Key == key && command.Modifiers == modifiers)
            {
                return command.Id;
            }
        }

        return null;
    }

    public static string FormatGesture(ModifierKeys modifiers, Key key)
    {
        var text = new StringBuilder();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            text.Append("Ctrl+");
        }
        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            text.Append("Alt+");
        }
        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            text.Append("Shift+");
        }

        return text.Append(FormatKey(key)).ToString();
    }

    // Key.ToString() gives "D1" and "OemComma"; a menu has to show "1" and ",".
    private static string FormatKey(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.OemQuestion => "/",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        Key.OemTilde => "`",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.Oem5 or Key.OemBackslash => "\\",
        _ => key.ToString()
    };
}
