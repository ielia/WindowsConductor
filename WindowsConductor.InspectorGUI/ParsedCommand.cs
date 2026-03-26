namespace WindowsConductor.InspectorGUI;

internal abstract record ParsedCommand;

internal sealed record ConnectCommand(string Url) : ParsedCommand;

internal sealed record LaunchCommand(
    string Path,
    string[] Args,
    string? DetachedTitleRegex,
    uint? MainWindowTimeout) : ParsedCommand;

internal sealed record AttachCommand(
    string MainWindowTitleRegex,
    uint? MainWindowTimeout) : ParsedCommand;

internal sealed record CloseCommand : ParsedCommand;

internal sealed record WindowScreenshotCommand : ParsedCommand;

internal sealed record LocateCommand(string[] Selectors) : ParsedCommand;

internal sealed record UnselectCommand : ParsedCommand;

internal sealed record AttributeCommand(string AttributeName) : ParsedCommand;

internal sealed record ClickCommand : ParsedCommand;

internal sealed record DoubleClickCommand : ParsedCommand;

internal sealed record TypeCommand(string Text) : ParsedCommand;

internal sealed record FocusCommand : ParsedCommand;

internal sealed record TextCommand : ParsedCommand;

internal sealed record ScreenshotCommand : ParsedCommand;
