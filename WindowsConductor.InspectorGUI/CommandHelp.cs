namespace WindowsConductor.InspectorGUI;

internal static class CommandHelp
{
    private static readonly (string Name, string Usage, string Description)[] Commands =
    [
        ("attach",      "attach <mainWindowTitleRegex> [mainWindowTimeout]",
            "Attaches to an already-running application by matching its window title."),
        ("attribute",   "attribute <name>",
            "Returns a named UIAutomation property of the currently selected element."),
        ("click",       "click",
            "Clicks the currently selected element."),
        ("close",       "close",
            "Closes the current application."),
        ("connect",     "connect [url]",
            "Connects to a WindowsConductor driver.\nDefaults to ws://localhost:8765/."),
        ("detach",      "detach",
            "Detaches from the current application without closing it."),
        ("disconnect",  "disconnect",
            "Disconnects from the driver."),
        ("doubleclick", "doubleclick",
            "Double-clicks the currently selected element."),
        ("exit",        "exit | quit",
            "Disconnects and exits the inspector."),
        ("focus",       "focus",
            "Sets keyboard focus on the currently selected element."),
        ("help",        "help [command]",
            "Shows help for all commands or a specific command."),
        ("launch",      "launch <path> [\"arg1\", ...] [detachedTitleRegex] [mainWindowTimeout]",
            "Launches an application and attaches to it."),
        ("locate",      "locate <selector> [>> <selector> ...]",
            "Finds elements matching the selector chain.\nSelectors are separated by >> for scoped searches.\nXPath selectors relative to the current element are supported."),
        ("parent",      "parent",
            "Navigates to the parent of the currently selected element."),
        ("rightclick",  "rightclick",
            "Right-clicks the currently selected element."),
        ("screenshot",  "screenshot",
            "Captures a screenshot of the currently selected element."),
        ("text",        "text",
            "Returns the visible text of the currently selected element."),
        ("type",        "type <text>",
            "Types text into the currently selected element."),
        ("unselect",    "unselect",
            "Clears the current element selection."),
        ("wscreenshot", "wscreenshot",
            "Captures a screenshot of the application window."),
    ];

    internal static string GetAll()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Available commands:");
        sb.AppendLine();
        foreach (var (name, usage, description) in Commands)
        {
            sb.AppendLine($"  {usage}");
            foreach (var line in description.Split('\n'))
                sb.AppendLine($"    {line}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    internal static string? GetFor(string commandName)
    {
        foreach (var (name, usage, description) in Commands)
        {
            if (string.Equals(name, commandName, StringComparison.OrdinalIgnoreCase)
                || (name == "exit" && string.Equals("quit", commandName, StringComparison.OrdinalIgnoreCase)))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"  {usage}");
                foreach (var line in description.Split('\n'))
                    sb.AppendLine($"    {line}");
                return sb.ToString().TrimEnd();
            }
        }
        return null;
    }
}
