namespace Glystrata.Controls;

/// <summary>Wires the near-universal "Enter confirms, Esc cancels" convention onto a popup window.</summary>
public static class DialogKeys
{
    /// <summary>Escape closes the window. Safe on both modal (ShowDialog) and non-modal (Show) windows.</summary>
    public static void AttachEscapeToClose(Window window)
    {
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                window.Close();
                e.Handled = true;
            }
        };
    }

    /// <summary>Escape closes the window; Enter invokes <paramref name="confirm"/> unless focus is in a
    /// multi-line text box, where Enter must insert a newline instead.</summary>
    public static void AttachConfirmCancel(Window window, Action confirm)
    {
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                window.Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && Keyboard.FocusedElement is not TextBox { AcceptsReturn: true })
            {
                confirm();
                e.Handled = true;
            }
        };
    }
}
