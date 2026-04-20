using WindowsConductor.Client;

namespace WindowsConductor.InspectorGUI;

internal abstract record ParsedCommand
{
    internal abstract string Name { get; }
    internal abstract string Usage { get; }
    internal abstract string Description { get; }
    internal abstract string[] Examples { get; }
}

internal sealed record AttachCommand(
    string MainWindowTitleRegex,
    uint? MainWindowTimeout) : ParsedCommand
{
    internal override string Name => "attach";
    internal override string Usage => "attach <mainWindowTitleRegex> [mainWindowTimeout]";
    internal override string Description => "Attaches to an already-running application by matching its window title.";
    internal override string[] Examples => ["attach \".*Notepad.*\"", "attach \"Calculator\" 5000"];
}

internal sealed record AttributeCommand(string AttributeName) : ParsedCommand
{
    internal override string Name => "attribute";
    internal override string Usage => "attribute <name|*>";
    internal override string Description => "Returns a named UIAutomation property of the currently selected element.\nUse '*' to list all attributes.";
    internal override string[] Examples => ["attribute classname", "attribute *"];
}

internal sealed record ClickCommand : ParsedCommand
{
    internal override string Name => "click";
    internal override string Usage => "click";
    internal override string Description => "Clicks the currently selected element.";
    internal override string[] Examples => [];
}

internal sealed record ClearCommand : ParsedCommand
{
    internal override string Name => "clear";
    internal override string Usage => "clear";
    internal override string Description => "Clears the output log.";
    internal override string[] Examples => [];
}

internal sealed record CloseCommand : ParsedCommand
{
    internal override string Name => "close";
    internal override string Usage => "close";
    internal override string Description => "Closes the current application.";
    internal override string[] Examples => [];
}

internal sealed record ConnectCommand(string Url, string? AuthToken = null) : ParsedCommand
{
    internal override string Name => "connect";
    internal override string Usage => "connect [url] [authToken]";
    internal override string Description => "Connects to a WindowsConductor driver.\nDefaults to ws://localhost:8765/.\nOptional auth token for bearer authentication.";
    internal override string[] Examples => ["connect", "connect ws://192.168.1.10:9000/", "connect ws://localhost:8765/ my-secret-token"];
}

internal sealed record DetachCommand : ParsedCommand
{
    internal override string Name => "detach";
    internal override string Usage => "detach";
    internal override string Description => "Detaches from the current application without closing it.";
    internal override string[] Examples => [];
}

internal sealed record DisconnectCommand : ParsedCommand
{
    internal override string Name => "disconnect";
    internal override string Usage => "disconnect";
    internal override string Description => "Disconnects from the driver.";
    internal override string[] Examples => [];
}

internal sealed record DoubleClickCommand : ParsedCommand
{
    internal override string Name => "doubleclick";
    internal override string Usage => "doubleclick";
    internal override string Description => "Double-clicks the currently selected element.";
    internal override string[] Examples => [];
}

internal sealed record ExitCommand : ParsedCommand
{
    internal override string Name => "exit";
    internal override string Usage => "exit | quit";
    internal override string Description => "Disconnects and exits the inspector.";
    internal override string[] Examples => [];
}

internal sealed record FocusCommand : ParsedCommand
{
    internal override string Name => "focus";
    internal override string Usage => "focus";
    internal override string Description => "Sets keyboard focus on the currently selected element.";
    internal override string[] Examples => [];
}

internal sealed record ForegroundCommand : ParsedCommand
{
    internal override string Name => "foreground";
    internal override string Usage => "foreground";
    internal override string Description => "Brings the currently selected element's window to the foreground.";
    internal override string[] Examples => [];
}

internal sealed record HelpCommand(string? CommandName) : ParsedCommand
{
    internal override string Name => "help";
    internal override string Usage => "help [command]";
    internal override string Description => "Shows help for all commands or a specific command.";
    internal override string[] Examples => ["help", "help connect"];
}

internal sealed record LaunchCommand(
    string Path,
    string[] Args,
    string? DetachedTitleRegex,
    uint? MainWindowTimeout) : ParsedCommand
{
    internal override string Name => "launch";
    internal override string Usage => "launch <path> [\"arg1\", ...] [detachedTitleRegex] [mainWindowTimeout]";
    internal override string Description => "Launches an application and attaches to it.";
    internal override string[] Examples => ["launch notepad.exe", "launch calc.exe [\"--silent\"] 3000"];
}

internal sealed record MatchIndexCommand(int Index) : ParsedCommand
{
    internal override string Name => "matchindex";
    internal override string Usage => "matchindex <N>";
    internal override string Description => "Selects the Nth match (1-based). Errors if N is out of bounds.";
    internal override string[] Examples => ["matchindex 3"];
}

internal sealed record LocateCommand(string[] Selectors) : ParsedCommand
{
    internal override string Name => "locate";
    internal override string Usage => "locate <selector> [>> <selector> ...]";
    internal override string Description =>
        "Finds elements matching the selector chain.\nSelectors are separated by >> for scoped searches.\nXPath selectors relative to the current element are supported.";
    internal override string[] Examples => ["locate [name=OK]", "locate type=Panel >> [automationid=btn1]", "locate //Window[ends-with(text(), 'Edge')]"];
}

internal sealed record NextMatchCommand(int Steps = 1) : ParsedCommand
{
    internal override string Name => "nextmatch";
    internal override string Usage => "nextmatch [N]";
    internal override string Description => "Moves forward N matches (default 1), cycling around.";
    internal override string[] Examples => ["nextmatch", "nextmatch 3"];
}

internal sealed record PrevMatchCommand(int Steps = 1) : ParsedCommand
{
    internal override string Name => "prevmatch";
    internal override string Usage => "prevmatch [N]";
    internal override string Description => "Moves back N matches (default 1), cycling around.";
    internal override string[] Examples => ["prevmatch", "prevmatch 2"];
}

internal sealed record ParentCommand : ParsedCommand
{
    internal override string Name => "parent";
    internal override string Usage => "parent";
    internal override string Description => "Navigates to the parent of the currently selected element.";
    internal override string[] Examples => [];
}

internal sealed record ChildrenCommand : ParsedCommand
{
    internal override string Name => "children";
    internal override string Usage => "children";
    internal override string Description => "Locates all direct children of the currently selected element.";
    internal override string[] Examples => [];
}

internal sealed record RefreshCommand : ParsedCommand
{
    internal override string Name => "refresh";
    internal override string Usage => "refresh";
    internal override string Description => "Refreshes the screenshot and attributes of the current state.";
    internal override string[] Examples => [];
}

internal sealed record ResetCommand : ParsedCommand
{
    internal override string Name => "reset";
    internal override string Usage => "reset";
    internal override string Description => "Unselects the current element and re-selects the application root.";
    internal override string[] Examples => [];
}

internal sealed record RightClickCommand : ParsedCommand
{
    internal override string Name => "rightclick";
    internal override string Usage => "rightclick";
    internal override string Description => "Right-clicks the currently selected element.";
    internal override string[] Examples => [];
}

internal sealed record SleepCommand(int Milliseconds) : ParsedCommand
{
    internal override string Name => "sleep";
    internal override string Usage => "sleep <milliseconds>";
    internal override string Description => "Pauses the inspector for the specified number of milliseconds.";
    internal override string[] Examples => ["sleep 1000", "sleep 5000"];
}

internal sealed record ScreenshotCommand : ParsedCommand
{
    internal override string Name => "screenshot";
    internal override string Usage => "screenshot";
    internal override string Description => "Captures a screenshot of the currently selected element.";
    internal override string[] Examples => [];
}

internal sealed record SnapshotCommand : ParsedCommand
{
    internal override string Name => "snapshot";
    internal override string Usage => "snapshot";
    internal override string Description => "Opens snapshot mode for the selected element's subtree.\nBlocks until snapshot mode is closed.";
    internal override string[] Examples => [];
}

internal sealed record TextCommand : ParsedCommand
{
    internal override string Name => "text";
    internal override string Usage => "text";
    internal override string Description => "Returns the visible text of the currently selected element.";
    internal override string[] Examples => [];
}

internal sealed record TypeCommand(string Text, KeyModifiers Modifiers = KeyModifiers.None) : ParsedCommand
{
    internal override string Name => "type";
    internal override string Usage => "type <text> [ctrl alt shift meta]";
    internal override string Description => "Types text into the currently selected element.\nOptional modifiers: ctrl, alt, shift, meta (in any order).";
    internal override string[] Examples => ["type \"Hello World\"", "type \"a\" [ctrl]", "type \"v\" [ctrl shift]"];
}

internal sealed record WindowStateCommand(WcWindowState? State = null) : ParsedCommand
{
    private static readonly string StateNames = string.Join(", ",
        Enum.GetValues<WcWindowState>().Select(s => s.ToString().ToLowerInvariant()));

    internal override string Name => "windowstate";
    internal override string Usage => $"windowstate [{StateNames}]";
    internal override string Description => $"Gets or sets the window state of the currently selected element's window.\nWithout a parameter, returns the current state.";
    internal override string[] Examples => ["windowstate", "windowstate normal", "windowstate maximized"];
}

internal sealed record ResolveCommand(string Selector) : ParsedCommand
{
    internal override string Name => "resolve";
    internal override string Usage => "resolve <xpath>";
    internal override string Description => "Resolves an XPath expression and prints the result in YAML format.\nAttribute selectors return named values; element selectors return text values.";
    internal override string[] Examples => ["resolve //button/@automationid", "resolve //button", "resolve ./@name"];
}

internal sealed record UnselectCommand : ParsedCommand
{
    internal override string Name => "unselect";
    internal override string Usage => "unselect";
    internal override string Description => "Clears the current element selection.";
    internal override string[] Examples => [];
}
