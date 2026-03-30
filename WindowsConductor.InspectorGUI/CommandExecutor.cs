using WindowsConductor.Client;

namespace WindowsConductor.InspectorGUI;

internal sealed class CommandExecutor(IInspectorSession session, ICommandOutput output)
{
    private string[]? _currentSelectors;
    private int _matchCount;
    private int _matchIndex;
    internal async Task ExecuteAsync(string input, CancellationToken ct = default)
    {
        ParsedCommand command;
        try
        {
            command = CommandParser.Parse(input);
        }
        catch (ArgumentException ex)
        {
            output.WriteError(ex.Message);
            return;
        }

        try
        {
            await ExecuteCommandAsync(command, ct);
        }
        catch (Exception ex)
        {
            output.WriteError(ex.Message);
        }
    }

    private async Task ExecuteCommandAsync(ParsedCommand command, CancellationToken ct)
    {
        switch (command)
        {
            case ClearCommand:
                output.ClearLog();
                break;

            case ConnectCommand cmd:
                await session.ConnectAsync(cmd.Url, ct);
                output.WriteInfo($"Connected to {cmd.Url}");
                break;

            case LaunchCommand cmd:
                RequireConnected();
                await session.LaunchAsync(cmd.Path, cmd.Args, cmd.DetachedTitleRegex, cmd.MainWindowTimeout, ct);
                output.WriteInfo($"Launched {cmd.Path}");
                await ShowWindowScreenshotAsync(ct);
                break;

            case AttachCommand cmd:
                RequireConnected();
                await session.AttachAsync(cmd.MainWindowTitleRegex, cmd.MainWindowTimeout ?? 0, ct);
                output.WriteInfo($"Attached to '{cmd.MainWindowTitleRegex}'");
                await ShowWindowScreenshotAsync(ct);
                break;

            case CloseCommand:
                RequireApp();
                await session.CloseAppAsync(ct);
                _currentSelectors = null;
                ResetMatchState();
                output.ClearScreenshot();
                output.ClearAttributes();
                output.WriteInfo("Application closed.");
                break;

            case DetachCommand:
                RequireApp();
                await session.DetachAppAsync();
                _currentSelectors = null;
                ResetMatchState();
                output.ClearScreenshot();
                output.ClearAttributes();
                output.WriteInfo("Detached from application.");
                break;

            case DisconnectCommand:
                RequireConnected();
                await session.DisconnectAsync();
                _currentSelectors = null;
                ResetMatchState();
                output.ClearScreenshot();
                output.ClearAttributes();
                output.WriteInfo("Disconnected.");
                break;

            case WindowScreenshotCommand:
                RequireApp();
                await ShowWindowScreenshotAsync(ct);
                break;

            case LocateCommand cmd:
                RequireApp();
                var firstTrimmed = cmd.Selectors[0].TrimStart();
                bool isRelative = session.HasSelectedElement
                    && IsXPath(firstTrimmed) && firstTrimmed != "/";
                int count;
                if (isRelative)
                {
                    BakeMatchIndex();
                    count = await session.LocateAllFromElementAsync(cmd.Selectors, ct);
                    _currentSelectors = CombineSelectors(_currentSelectors, cmd.Selectors);
                }
                else
                {
                    count = await session.LocateAllAsync(cmd.Selectors, ct);
                    _currentSelectors = cmd.Selectors;
                }
                if (count == 0)
                    throw new InvalidOperationException(
                        $"No element found for selector '{string.Join(" >> ", cmd.Selectors)}'.");
                _matchCount = count;
                _matchIndex = 0;
                output.WriteInfo(count == 1
                    ? "Located 1 element."
                    : $"Located {count} elements (showing 1 of {count}).");
                output.UpdateMatchNavigation(_matchIndex, _matchCount);
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case UnselectCommand:
                session.Unselect();
                _currentSelectors = null;
                ResetMatchState();
                output.ClearHighlight();
                output.ClearAttributes();
                output.WriteInfo("Element unselected.");
                break;

            case AttributeCommand cmd:
                RequireElement();
                var attrValue = await session.GetAttributeAsync(cmd.AttributeName, ct);
                output.WriteInfo($"{cmd.AttributeName} = {attrValue}");
                break;

            case ClickCommand:
                RequireElement();
                await session.ClickAsync(ct);
                output.WriteInfo("Clicked.");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case DoubleClickCommand:
                RequireElement();
                await session.DoubleClickAsync(ct);
                output.WriteInfo("Double-clicked.");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case RightClickCommand:
                RequireElement();
                await session.RightClickAsync(ct);
                output.WriteInfo("Right-clicked.");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case TypeCommand cmd:
                RequireElement();
                await session.TypeAsync(cmd.Text, cmd.Modifiers, ct);
                output.WriteInfo(cmd.Modifiers != KeyModifiers.None
                    ? $"Typed: {cmd.Text} (modifiers: {cmd.Modifiers})"
                    : $"Typed: {cmd.Text}");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case ParentCommand:
                RequireElement();
                BakeMatchIndex();
                ResetMatchState();
                var parentId = await session.ParentAsync(ct);
                if (parentId is null)
                {
                    output.WriteInfo("Already at application root.");
                    break;
                }
                _currentSelectors = CombineSelectors(_currentSelectors, [".."]);
                output.WriteInfo($"Navigated to parent: {parentId}");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case FocusCommand:
                RequireElement();
                await session.FocusAsync(ct);
                output.WriteInfo("Focused.");
                break;

            case TextCommand:
                RequireElement();
                var text = await session.GetTextAsync(ct);
                output.WriteInfo(text);
                break;

            case ScreenshotCommand:
                RequireElement();
                var imgData = await session.ScreenshotElementAsync(ct);
                output.ShowScreenshot(imgData);
                output.WriteInfo("Element screenshot captured.");
                break;

            case HelpCommand cmd:
                var helpText = cmd.CommandName is not null
                    ? CommandHelp.GetFor(cmd.CommandName) ?? $"Unknown command: '{cmd.CommandName}'."
                    : CommandHelp.GetAll();
                output.WriteInfo(helpText);
                break;

            case ExitCommand:
                if (session.IsConnected)
                    await session.DisconnectAsync();
                output.RequestExit();
                break;
        }
    }

    private async Task ShowWindowScreenshotAsync(CancellationToken ct)
    {
        var imgData = await session.WindowScreenshotAsync(ct);
        output.ShowScreenshot(imgData);
    }

    internal async Task NavigateMatchAsync(int direction, CancellationToken ct = default)
    {
        if (_matchCount <= 1) return;
        _matchIndex = (_matchIndex + direction + _matchCount) % _matchCount;
        await session.SelectMatchAsync(_matchIndex, ct);
        output.UpdateMatchNavigation(_matchIndex, _matchCount);
        await ShowWindowScreenshotWithHighlightAsync(ct);
        await ShowAttributesAsync(ct);
    }

    internal async Task RefreshAsync(CancellationToken ct = default)
    {
        if (!session.HasApp) return;
        await ShowWindowScreenshotWithHighlightAsync(ct);
        await ShowAttributesAsync(ct);
    }

    private async Task ShowAttributesAsync(CancellationToken ct)
    {
        if (!session.HasSelectedElement) return;
        var chain = _currentSelectors is not null
            ? string.Join(" >> ", _currentSelectors)
            : "";
        if (_matchCount > 1 && _matchIndex > 0)
            chain += $"[{_matchIndex + 1}]";
        var attrs = await session.GetAttributesAsync(ct);
        output.ShowAttributes(chain, attrs);
    }

    private async Task ShowWindowScreenshotWithHighlightAsync(CancellationToken ct)
    {
        if (!session.HasSelectedElement)
        {
            await ShowWindowScreenshotAsync(ct);
            return;
        }

        var imgData = await session.WindowScreenshotAsync(ct);
        var winRect = await session.GetWindowBoundingRectAsync(ct);
        var elRect = await session.GetElementBoundingRectAsync(ct);

        // Element rect is in screen coordinates; convert to window-relative.
        // Pass window dimensions so the renderer can compensate for DPI
        // scaling mismatches between UIAutomation coords and screenshot pixels.
        var highlight = new HighlightInfo(
            elRect.X - winRect.X,
            elRect.Y - winRect.Y,
            elRect.Width,
            elRect.Height,
            winRect.Width,
            winRect.Height);

        output.ShowScreenshot(imgData, highlight);
    }

    private void RequireConnected()
    {
        if (!session.IsConnected)
            throw new InvalidOperationException("Not connected. Use 'connect <URL>' first.");
    }

    private void RequireApp()
    {
        RequireConnected();
        if (!session.HasApp)
            throw new InvalidOperationException("No application. Use 'launch' or 'attach' first.");
    }

    private void RequireElement()
    {
        RequireApp();
        if (!session.HasSelectedElement)
            throw new InvalidOperationException("No element selected. Use 'locate' first.");
    }

    private void BakeMatchIndex()
    {
        if (_matchCount > 1 && _matchIndex > 0 && _currentSelectors is { Length: > 0 })
            _currentSelectors = [.. _currentSelectors[..^1], _currentSelectors[^1] + $"[{_matchIndex + 1}]"];
    }

    private void ResetMatchState()
    {
        _matchCount = 0;
        _matchIndex = 0;
        output.UpdateMatchNavigation(0, 0);
    }

    private static bool IsXPath(string selector)
    {
        var s = selector.TrimStart();
        return s.StartsWith('/') || s.StartsWith('.');
    }

    private static string[] CombineSelectors(string[]? current, string[] incoming)
    {
        if (current is { Length: > 0 } && IsXPath(current[^1]) && IsXPath(incoming[0]))
        {
            var left = current[^1];
            var right = incoming[0];
            // Strip trailing '/' from left when right already starts with '/'
            if (left.EndsWith('/') && right.TrimStart().StartsWith('/'))
                left = left.TrimEnd('/');
            var needsSlash = !left.EndsWith('/') && !right.TrimStart().StartsWith('/');
            var combined = left + (needsSlash ? "/" : "") + right;
            return [.. current[..^1], combined, .. incoming[1..]];
        }
        return [.. current ?? [], .. incoming];
    }
}
