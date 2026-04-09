using WindowsConductor.Client;

namespace WindowsConductor.InspectorGUI;

internal sealed class CommandExecutor(IInspectorSession session, ICommandOutput output)
{
    internal bool StopChainOnError { get; set; }

    private string[]? _currentSelectors;
    private readonly Stack<string[]> _selectorHistory = new();
    private int _matchCount;
    private int _matchIndex;
    private bool _isAtRoot;
    private CancellationTokenSource? _chainCts;

    internal IInspectorSession Session => session;
    internal bool IsAtRoot => _isAtRoot;
    internal bool CanGoBack => _selectorHistory.Count > 0;
    internal bool HasMultipleMatches => _matchCount > 1;
    internal async Task ExecuteAsync(string input, CancellationToken ct = default)
    {
        var commands = CommandParser.SplitCommands(input);
        if (commands.Length == 0)
        {
            output.WriteError("Command cannot be empty.");
            return;
        }
        bool isChain = commands.Length > 1;
        using var chainCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _chainCts = chainCts;
        try
        {
            foreach (var cmd in commands)
            {
                if (chainCts.Token.IsCancellationRequested)
                    return;

                if (isChain)
                    output.WriteCommand(cmd);

                ParsedCommand command;
                try
                {
                    command = CommandParser.Parse(cmd);
                }
                catch (ArgumentException ex)
                {
                    output.WriteError(ex.Message);
                    if (!isChain || StopChainOnError) return;
                    continue;
                }

                try
                {
                    await ExecuteCommandAsync(command, chainCts.Token);
                }
                catch (Exception ex)
                {
                    output.WriteError(ex.Message);
                    if (!isChain || StopChainOnError) return;
                }
            }
        }
        finally
        {
            _chainCts = null;
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
                if (session.IsConnected)
                    throw new InvalidOperationException("Already connected. Use 'disconnect' first.");
                await session.ConnectAsync(cmd.Url, ct);
                output.SetConnectionUrl(cmd.Url);
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
                session.Unselect();
                _currentSelectors = null;
                ResetMatchState();
                await LocateRootAsync(ct);
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
                output.SetConnectionUrl(null);
                output.WriteInfo("Disconnected.");
                break;

            case LocateCommand cmd:
                RequireApp();
                var firstTrimmed = cmd.Selectors[0].TrimStart();
                bool isRelative = session.HasSelectedElement
                    && IsRelativeXPath(firstTrimmed);
                var previousSelectors = _currentSelectors;
                var previousMatchCount = _matchCount;
                var previousMatchIndex = _matchIndex;
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
                {
                    _currentSelectors = previousSelectors;
                    _matchCount = previousMatchCount;
                    _matchIndex = previousMatchIndex;
                    throw new InvalidOperationException(
                        $"No element found for selector '{string.Join(" >> ", cmd.Selectors)}'.");
                }
                if (previousSelectors is not null)
                    _selectorHistory.Push(previousSelectors);
                _isAtRoot = await session.IsSelectedElementRootAsync(ct);
                _matchCount = count;
                _matchIndex = 0;
                output.WriteInfo(count == 1
                    ? "Located 1 element."
                    : $"Located {count} elements (showing 1 of {count}).");
                output.UpdateMatchNavigation(_matchIndex, _matchCount);
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case RefreshCommand:
                RequireApp();
                await RefreshAsync(ct);
                output.WriteInfo("Refreshed.");
                break;

            case ResetCommand:
                RequireApp();
                session.Unselect();
                _currentSelectors = null;
                ResetMatchState();
                await LocateRootAsync(ct);
                output.WriteInfo("Reset to application root.");
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
                if (cmd.AttributeName == "*")
                {
                    var allAttrs = await session.GetAttributesAsync(ct);
                    foreach (var (key, value) in allAttrs)
                        output.WriteBulletInfo($"{key} = {value}");
                }
                else
                {
                    var attrValue = await session.GetAttributeAsync(cmd.AttributeName, ct);
                    output.WriteInfo($"{cmd.AttributeName} = {attrValue}");
                }
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
                var parentPreviousSelectors = _currentSelectors;
                ResetMatchState();
                var parentId = await session.ParentAsync(ct);
                if (parentId is null)
                {
                    _isAtRoot = true;
                    output.WriteInfo("Already at application root.");
                    break;
                }
                if (parentPreviousSelectors is not null)
                    _selectorHistory.Push(parentPreviousSelectors);
                _currentSelectors = CombineSelectors(parentPreviousSelectors, [".."]);
                _isAtRoot = await session.IsSelectedElementRootAsync(ct);
                output.WriteInfo($"Navigated to parent: {parentId}");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case ChildrenCommand:
                RequireElement();
                BakeMatchIndex();
                var childPreviousSelectors = _currentSelectors;
                var childSelectors = new[] { "./*" };
                var childCount = await session.LocateAllFromElementAsync(childSelectors, ct);
                if (childCount == 0)
                    throw new InvalidOperationException("No children found.");
                if (childPreviousSelectors is not null)
                    _selectorHistory.Push(childPreviousSelectors);
                _currentSelectors = CombineSelectors(childPreviousSelectors, childSelectors);
                _isAtRoot = false;
                _matchCount = childCount;
                _matchIndex = 0;
                output.WriteInfo(childCount == 1
                    ? "Located 1 child."
                    : $"Located {childCount} children (showing 1 of {childCount}).");
                output.UpdateMatchNavigation(_matchIndex, _matchCount);
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case PrevMatchCommand cmd:
                RequireElement();
                if (_matchCount > 1)
                    await NavigateMatchAsync(-cmd.Steps, ct);
                break;

            case NextMatchCommand cmd:
                RequireElement();
                if (_matchCount > 1)
                    await NavigateMatchAsync(cmd.Steps, ct);
                break;

            case MatchIndexCommand cmd:
                RequireElement();
                if (cmd.Index < 1 || cmd.Index > _matchCount)
                    throw new InvalidOperationException($"Match index {cmd.Index} is out of bounds (1–{_matchCount}).");
                _matchIndex = cmd.Index - 1;
                await session.SelectMatchAsync(_matchIndex, ct);
                output.UpdateMatchNavigation(_matchIndex, _matchCount);
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

            case SnapshotCommand:
                RequireElement();
                await output.RunSnapshotAsync();
                break;

            case SleepCommand sleepCmd:
                output.ShowSleepCancel(sleepCmd.Milliseconds, () => _chainCts?.Cancel());
                try
                {
                    await Task.Delay(sleepCmd.Milliseconds, ct);
                    output.WriteInfo($"Slept {sleepCmd.Milliseconds}ms.");
                }
                catch (OperationCanceledException)
                {
                    output.WriteInfo("Sleep and all remaining commands stopped.");
                }
                finally
                {
                    await output.HideSleepCancelAsync();
                }
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
        var winRect = await session.GetWindowBoundingRectAsync(ct);
        output.ShowScreenshot(imgData, windowDimensions: new WindowDimensions(winRect.X, winRect.Y, winRect.Width, winRect.Height));
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

    internal async Task GoBackAsync(CancellationToken ct = default)
    {
        if (_selectorHistory.Count == 0) return;
        var selectors = _selectorHistory.Pop();
        session.Unselect();
        var count = await session.LocateAllAsync(selectors, ct);
        _currentSelectors = selectors;
        _isAtRoot = await session.IsSelectedElementRootAsync(ct);
        _matchCount = count;
        _matchIndex = 0;
        output.UpdateMatchNavigation(_matchIndex, _matchCount);
        await ShowWindowScreenshotWithHighlightAsync(ct);
        await ShowAttributesAsync(ct);
    }

    private async Task LocateRootAsync(CancellationToken ct)
    {
        var selectors = new[] { "." };
        var count = await session.LocateAllAsync(selectors, ct);
        if (count == 0)
            throw new InvalidOperationException("No root element found.");
        _currentSelectors = selectors;
        _isAtRoot = await session.IsSelectedElementRootAsync(ct);
        _matchCount = count;
        _matchIndex = 0;
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

        var imgData = await session.ElementWindowScreenshotAsync(ct);
        var winRect = await session.GetElementWindowBoundingRectAsync(ct);

        HighlightInfo? highlight = null;
        try
        {
            var elRect = await session.GetElementBoundingRectAsync(ct);
            // Element rect is in screen coordinates; convert to window-relative.
            // Pass window dimensions so the renderer can compensate for DPI
            // scaling mismatches between UIAutomation coords and screenshot pixels.
            highlight = new HighlightInfo(
                elRect.X - winRect.X,
                elRect.Y - winRect.Y,
                elRect.Width,
                elRect.Height,
                winRect.Width,
                winRect.Height);
        }
        catch
        {
            // Some elements (e.g. Desktop root) do not support bounding rect.
        }

        output.ShowScreenshot(imgData, highlight, new WindowDimensions(winRect.X, winRect.Y, winRect.Width, winRect.Height));
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
        _isAtRoot = false;
        _selectorHistory.Clear();
        output.UpdateMatchNavigation(0, 0);
    }

    private static bool IsXPath(string selector)
    {
        var s = selector.TrimStart();
        return s.StartsWith('/') || s.StartsWith('.');
    }

    private static bool IsRelativeXPath(string selector)
    {
        var s = selector.TrimStart();
        return s.StartsWith('.') || s.StartsWith("//");
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
            combined = NormalizeDotSegments(combined);
            return [.. current[..^1], combined, .. incoming[1..]];
        }
        return [.. current ?? [], .. incoming];
    }

    private static string NormalizeDotSegments(string path)
    {
        // Collapse "/./" to "/" and strip trailing "/."
        string previous;
        do
        {
            previous = path;
            path = path.Replace("/./", "/");
        } while (path != previous);

        if (path.EndsWith("/."))
            path = path[..^2];

        return path;
    }
}
