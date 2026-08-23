using System.Globalization;
using SkiaSharp;
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
        output.ShowCancel(() => _chainCts?.Cancel(), isChain);
        try
        {
            foreach (var cmd in commands)
            {
                if (chainCts.Token.IsCancellationRequested)
                    return;

                output.ResetCancelCommandTimer();
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
                catch (OperationCanceledException)
                {
                    output.WriteCancellation("Cancelled.");
                    return;
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
            output.HideCancel();
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
                await session.ConnectAsync(cmd.Url, cmd.AuthToken, ct);
                output.SetConnectionUrl(cmd.Url);
                output.WriteInfo($"Connected to {cmd.Url}");
                var serverVersion = session.ServerVersion ?? "Unknown";
                output.WriteInfo($"Server version: {serverVersion}");
                if (serverVersion != WcDefaults.Version)
                    output.WriteWarning($"Version mismatch — client: {WcDefaults.Version}, server: {serverVersion}");
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

            case ResolveCommand cmd:
                RequireApp();
                var resolveSelector = cmd.Selector.TrimStart();
                var resolveResult = session.HasSelectedElement && IsRelativeXPath(resolveSelector)
                    ? await session.ResolveValueFromElementAsync(cmd.Selector, ct)
                    : await session.ResolveValueAsync(cmd.Selector, ct);
                output.WriteInfo(WcValueYamlFormatter.Format(resolveResult));
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
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case SetAttributeCommand setAttr:
                RequireElement();
                await session.SetAttributeAsync(setAttr.AttributeName, setAttr.Value, ct);
                output.WriteInfo($"Set {setAttr.AttributeName} = {setAttr.Value}");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case ClickCommand { OcrText: not null } clickOcr
                when !string.IsNullOrWhiteSpace(clickOcr.OcrText):
                RequireElement();
                var clickMatch = await OcrMatchAsync(clickOcr.OcrText, clickOcr.MaxDistance, clickOcr.MatchIndex, ct);
                if (clickOcr.ClickAnchor is { } clickOcrAnchor)
                    await clickMatch.ClickAsync(clickOcrAnchor, new System.Drawing.Point(clickOcr.OffsetX, clickOcr.OffsetY), ct);
                else
                    await clickMatch.ClickAsync(ct);
                output.WriteInfo($"Clicked OCR match \"{clickOcr.OcrText}\" [\"{clickMatch.Text}\" ~ dist={clickMatch.Distance}].");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case ClickCommand { ClickAnchor: not null } clickAnch:
                RequireElement();
                await session.ClickAsync(clickAnch.ClickAnchor.Value, new System.Drawing.Point(clickAnch.OffsetX, clickAnch.OffsetY), ct);
                output.WriteInfo($"Clicked at {clickAnch.ClickAnchor.Value} ({clickAnch.OffsetX}, {clickAnch.OffsetY}).");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case ClickCommand:
                RequireElement();
                await session.ClickAsync(ct);
                output.WriteInfo("Clicked.");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case DoubleClickCommand { OcrText: not null } dblOcr
                when !string.IsNullOrWhiteSpace(dblOcr.OcrText):
                RequireElement();
                var dblMatch = await OcrMatchAsync(dblOcr.OcrText, dblOcr.MaxDistance, dblOcr.MatchIndex, ct);
                if (dblOcr.ClickAnchor is { } dblOcrAnchor)
                    await dblMatch.DoubleClickAsync(dblOcrAnchor, new System.Drawing.Point(dblOcr.OffsetX, dblOcr.OffsetY), ct);
                else
                    await dblMatch.DoubleClickAsync(ct);
                output.WriteInfo($"Double-clicked OCR match \"{dblOcr.OcrText}\" [\"{dblMatch.Text}\" ~ dist={dblMatch.Distance}].");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case DoubleClickCommand { ClickAnchor: not null } dblAnch:
                RequireElement();
                await session.DoubleClickAsync(dblAnch.ClickAnchor.Value, new System.Drawing.Point(dblAnch.OffsetX, dblAnch.OffsetY), ct);
                output.WriteInfo($"Double-clicked at {dblAnch.ClickAnchor.Value} ({dblAnch.OffsetX}, {dblAnch.OffsetY}).");
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

            case RightClickCommand { OcrText: not null } rclkOcr
                when !string.IsNullOrWhiteSpace(rclkOcr.OcrText):
                RequireElement();
                var rclkMatch = await OcrMatchAsync(rclkOcr.OcrText, rclkOcr.MaxDistance, rclkOcr.MatchIndex, ct);
                if (rclkOcr.ClickAnchor is { } rclkOcrAnchor)
                    await rclkMatch.RightClickAsync(rclkOcrAnchor, new System.Drawing.Point(rclkOcr.OffsetX, rclkOcr.OffsetY), ct);
                else
                    await rclkMatch.RightClickAsync(ct);
                output.WriteInfo($"Right-clicked OCR match \"{rclkOcr.OcrText}\" [\"{rclkMatch.Text}\" ~ dist={rclkMatch.Distance}].");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case RightClickCommand { ClickAnchor: not null } rclkAnch:
                RequireElement();
                await session.RightClickAsync(rclkAnch.ClickAnchor.Value, new System.Drawing.Point(rclkAnch.OffsetX, rclkAnch.OffsetY), ct);
                output.WriteInfo($"Right-clicked at {rclkAnch.ClickAnchor.Value} ({rclkAnch.OffsetX}, {rclkAnch.OffsetY}).");
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

            case HoverCommand { OcrText: not null } hvrOcr
                when !string.IsNullOrWhiteSpace(hvrOcr.OcrText):
                RequireElement();
                var hvrMatch = await OcrMatchAsync(hvrOcr.OcrText, hvrOcr.MaxDistance, hvrOcr.MatchIndex, ct);
                if (hvrOcr.ClickAnchor is { } hvrOcrAnchor)
                    await hvrMatch.HoverAsync(hvrOcrAnchor, new System.Drawing.Point(hvrOcr.OffsetX, hvrOcr.OffsetY), ct);
                else
                    await hvrMatch.HoverAsync(ct);
                output.WriteInfo($"Hovered over OCR match \"{hvrOcr.OcrText}\" [\"{hvrMatch.Text}\" ~ dist={hvrMatch.Distance}].");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case HoverCommand { ClickAnchor: not null } hvrAnch:
                RequireElement();
                await session.HoverAsync(hvrAnch.ClickAnchor.Value, new System.Drawing.Point(hvrAnch.OffsetX, hvrAnch.OffsetY), ct);
                output.WriteInfo($"Hovered at {hvrAnch.ClickAnchor.Value} ({hvrAnch.OffsetX}, {hvrAnch.OffsetY}).");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case HoverCommand:
                RequireElement();
                await session.HoverAsync(ct);
                output.WriteInfo("Hovered.");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case DragCommand drag:
                RequireElement();
                var dragSelectors = drag.TargetLocator.Split(">>", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                await session.DragToAsync(
                    dragSelectors,
                    drag.FromAnchor ?? Anchor.Center,
                    new System.Drawing.Point(drag.FromX, drag.FromY),
                    drag.ToAnchor ?? Anchor.Center,
                    new System.Drawing.Point(drag.ToX, drag.ToY),
                    ct);
                output.WriteInfo("Dragged.");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case ScrollCommand scrollCmd:
                RequireElement();
                await session.ScrollAsync(scrollCmd.Lines, scrollCmd.Horizontal, ct);
                output.WriteInfo(scrollCmd.Horizontal
                    ? $"Scrolled horizontally {scrollCmd.Lines} lines."
                    : $"Scrolled {scrollCmd.Lines} lines.");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case HitKeysCommand cmd:
                RequireElement();
                await session.HitKeysAsync(cmd.Keys, ct);
                output.WriteInfo($"Hit keys: {string.Join('+', cmd.Keys)}.");
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

            case GlobalHitKeysCommand gHitCmd:
                RequireConnected();
                await session.GlobalHitKeysAsync(gHitCmd.Keys, ct);
                output.WriteInfo($"Global hit keys: {string.Join('+', gHitCmd.Keys)}.");
                if (session.HasApp)
                {
                    await ShowWindowScreenshotWithHighlightAsync(ct);
                    await ShowAttributesAsync(ct);
                }
                break;

            case GlobalTypeCommand gTypeCmd:
                RequireConnected();
                await session.GlobalTypeAsync(gTypeCmd.Text, gTypeCmd.Modifiers, ct);
                output.WriteInfo(gTypeCmd.Modifiers != KeyModifiers.None
                    ? $"Global typed: {gTypeCmd.Text} (modifiers: {gTypeCmd.Modifiers})"
                    : $"Global typed: {gTypeCmd.Text}");
                if (session.HasApp)
                {
                    await ShowWindowScreenshotWithHighlightAsync(ct);
                    await ShowAttributesAsync(ct);
                }
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
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case ForegroundCommand:
                RequireElement();
                await session.SetForegroundAsync(ct);
                output.WriteInfo("Brought to foreground.");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case WindowStateCommand { State: null }:
                RequireElement();
                var currentState = await session.GetWindowStateAsync(ct);
                output.WriteInfo(currentState.ToString().ToLowerInvariant());
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case WindowStateCommand { State: { } newState }:
                RequireElement();
                await session.SetWindowStateAsync(newState, ct);
                output.WriteInfo($"Window state set to {newState.ToString().ToLowerInvariant()}.");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case TextCommand:
                RequireElement();
                var text = await session.GetTextAsync(ct);
                output.WriteInfo(text);
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case ScreenshotCommand:
                RequireElement();
                var imgData = await session.ScreenshotElementAsync(ct);
                output.ShowScreenshot(imgData);
                output.WriteInfo("Element screenshot captured.");
                break;

            case OcrCommand:
                RequireElement();
                var ocrResult = await session.GetOcrTextAsync(ct);
                output.WriteInfo(FormatOcrResult(ocrResult));
                await ShowWindowScreenshotWithHighlightAsync(ct);
                await ShowAttributesAsync(ct);
                break;

            case SnapshotCommand:
                RequireElement();
                await output.RunSnapshotAsync();
                break;

            case SleepCommand sleepCmd:
                var sleepCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                output.ShowSleepCancel(sleepCmd.Milliseconds, () => sleepCts.Cancel());
                try
                {
                    await Task.Delay(sleepCmd.Milliseconds, sleepCts.Token);
                    output.WriteInfo($"Slept {sleepCmd.Milliseconds}ms.");
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    output.WriteCancellation("Sleep skipped.");
                }
                finally
                {
                    await output.HideSleepCancelAsync();
                    sleepCts.Dispose();
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
        var (imgData, unionRect) = await CaptureUnionScreenshotAsync(ct);
        var mainRect = await session.GetWindowBoundingRectAsync(ct);
        var offsetX = mainRect.X - unionRect.X;
        var offsetY = mainRect.Y - unionRect.Y;
        output.ShowScreenshot(imgData, windowDimensions: new WindowDimensions(unionRect.X, unionRect.Y, unionRect.Width, unionRect.Height, offsetX, offsetY));
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
        output.UpdateMatchNavigation(_matchIndex, _matchCount);
        await ShowWindowScreenshotWithHighlightAsync(ct);
        await ShowAttributesAsync(ct);
    }

    private async Task ShowAttributesAsync(CancellationToken ct)
    {
        if (!session.HasSelectedElement) return;
        var chain = _currentSelectors is not null
            ? string.Join(" >> ", _currentSelectors)
            : "";
        if (_matchCount > 1 && _currentSelectors is { Length: > 0 })
        {
            var last = _currentSelectors[^1];
            var suffix = IsXPath(last)
                ? $"({last})[{_matchIndex + 1}]"
                : last + $"[{_matchIndex + 1}]";
            chain = _currentSelectors.Length > 1
                ? string.Join(" >> ", [.. _currentSelectors[..^1], suffix])
                : suffix;
        }
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

        var (imgData, unionRect) = await CaptureUnionScreenshotAsync(ct);

        HighlightInfo? highlight = null;
        double offsetX = 0, offsetY = 0;
        try
        {
            var elWinRect = await session.GetElementWindowBoundingRectAsync(ct);
            offsetX = elWinRect.X - unionRect.X;
            offsetY = elWinRect.Y - unionRect.Y;

            var elRect = await session.GetElementBoundingRectAsync(ct);
            highlight = new HighlightInfo(
                elRect.X - unionRect.X,
                elRect.Y - unionRect.Y,
                elRect.Width,
                elRect.Height,
                unionRect.Width,
                unionRect.Height);
        }
        catch
        {
            // Some elements (e.g. Desktop root) do not support bounding rect.
        }

        output.ShowScreenshot(imgData, highlight, new WindowDimensions(unionRect.X, unionRect.Y, unionRect.Width, unionRect.Height, offsetX, offsetY));
    }

    private async Task<(byte[] ImageData, BoundingRect UnionRect)> CaptureUnionScreenshotAsync(CancellationToken ct)
    {
        var allRects = await session.GetAllWindowBoundingRectsAsync(ct);
        if (allRects.Length == 0)
            return await FallbackWindowScreenshotAsync(ct);

        try
        {
            var unionRect = ComputeUnionRect(allRects);
            var desktopResult = await session.DesktopScreenshotWithOriginAsync(ct);
            var (cropped, effectiveRect) = CropScreenshot(desktopResult.Png, unionRect, desktopResult.OriginX, desktopResult.OriginY);
            return (cropped, effectiveRect);
        }
        catch
        {
            return await FallbackWindowScreenshotAsync(ct);
        }
    }

    private async Task<(byte[] ImageData, BoundingRect Rect)> FallbackWindowScreenshotAsync(CancellationToken ct)
    {
        var imgData = await session.WindowScreenshotAsync(ct);
        var winRect = await session.GetWindowBoundingRectAsync(ct);
        return (imgData, winRect);
    }

    private static BoundingRect ComputeUnionRect(BoundingRect[] rects)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var r in rects)
        {
            if (r.Width <= 0 || r.Height <= 0) continue;
            minX = Math.Min(minX, r.X);
            minY = Math.Min(minY, r.Y);
            maxX = Math.Max(maxX, r.X + r.Width);
            maxY = Math.Max(maxY, r.Y + r.Height);
        }
        return new BoundingRect(minX, minY, maxX - minX, maxY - minY);
    }

    internal static (byte[] ImageData, BoundingRect EffectiveRect) CropScreenshot(
        byte[] screenshotBytes, BoundingRect cropRect,
        double screenOriginX = 0, double screenOriginY = 0)
    {
        var fallbackRect = new BoundingRect(cropRect.X, cropRect.Y, cropRect.Width, cropRect.Height);

        SKBitmap? bitmap;
        try { bitmap = SKBitmap.Decode(screenshotBytes); }
        catch { return (screenshotBytes, fallbackRect); }
        if (bitmap is null) return (screenshotBytes, fallbackRect);
        using (bitmap)
        {
            var cropX = Math.Max(0, (int)(cropRect.X - screenOriginX));
            var cropY = Math.Max(0, (int)(cropRect.Y - screenOriginY));
            var cropW = Math.Min((int)cropRect.Width, bitmap.Width - cropX);
            var cropH = Math.Min((int)cropRect.Height, bitmap.Height - cropY);
            if (cropW <= 0 || cropH <= 0) return (screenshotBytes, fallbackRect);

            var effectiveRect = new BoundingRect(
                screenOriginX + cropX, screenOriginY + cropY, cropW, cropH);

            var subset = new SKBitmap(cropW, cropH);
            using var canvas = new SKCanvas(subset);
            canvas.DrawBitmap(bitmap, new SKRect(cropX, cropY, cropX + cropW, cropY + cropH),
                new SKRect(0, 0, cropW, cropH));
            using var image = SKImage.FromBitmap(subset);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return (data.ToArray(), effectiveRect);
        }
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

    private async Task<WcElementOcrMatch> OcrMatchAsync(string ocrText, int maxDistance, int? matchIndex, CancellationToken ct)
    {
        await session.SetForegroundAsync(ct);
        var ocrResult = await session.GetOcrTextAsync(ct);
        if (matchIndex is not null)
        {
            var allMatches = ocrResult.FindAllByEdits(ocrText, maxDistance);
            if (allMatches.Count == 0)
                throw new InvalidOperationException(
                    $"OCR match not found for \"{ocrText}\" (maxDistance={maxDistance}).");
            if (matchIndex.Value >= allMatches.Count)
                throw new InvalidOperationException(
                    $"Match index #{matchIndex.Value} out of range — only {allMatches.Count} match(es) found for \"{ocrText}\" (maxDistance={maxDistance}).");
            return allMatches[matchIndex.Value];
        }
        return ocrResult.FindBestByEdits(ocrText, maxDistance)
            ?? throw new InvalidOperationException(
                $"OCR match not found for \"{ocrText}\" (maxDistance={maxDistance}).");
    }

    private void BakeMatchIndex()
    {
        if (_matchCount > 1 && _currentSelectors is { Length: > 0 })
        {
            var last = _currentSelectors[^1];
            var baked = IsXPath(last)
                ? $"({last})[{_matchIndex + 1}]"
                : last + $"[{_matchIndex + 1}]";
            _currentSelectors = [.. _currentSelectors[..^1], baked];
        }
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
        return s.StartsWith('/') || s.StartsWith('.') || StartsWithAxis(s);
    }

    private static bool IsRelativeXPath(string selector)
    {
        var s = selector.TrimStart();
        return s.StartsWith('.') || s.StartsWith('(') || StartsWithAxis(s);
    }

    private static readonly HashSet<string> AxisNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ancestor", "ancestor-or-self", "attribute", "child", "descendant",
        "descendant-or-self", "following-sibling", "leafmost", "pruned-leafmost",
        "parent", "preceding-sibling", "self", "sibling"
    };

    private static bool StartsWithAxis(string s)
    {
        int sep = s.IndexOf("::", StringComparison.Ordinal);
        if (sep <= 0) return false;
        return AxisNames.Contains(s[..sep]);
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

    private static string FormatOcrRect(BoundingRect r, double? angle) =>
        string.Format(CultureInfo.InvariantCulture,
            "{{x:{0},y:{1},w:{2},h:{3},a:{4}}}",
            r.X, r.Y, r.Width, r.Height, angle?.ToString(CultureInfo.InvariantCulture) ?? "null");

    private static string FormatOcrResult(WcElementOcrResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("\"\"\"");
        foreach (var line in result.Lines)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"<<{line.Text}>>{FormatOcrRect(line.BoundingRect, line.Angle)}");
            var words = string.Join(", ", line.Words.Select(w =>
                $"\"{w.Text}\"{FormatOcrRect(w.BoundingRect, w.Angle)}"));
            sb.AppendLine(CultureInfo.InvariantCulture, $"  [ {words} ]");
        }

        sb.AppendLine("\"\"\"");
        sb.Append(FormatOcrRect(result.BoundingRect, result.Angle));
        return sb.ToString();
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

        if (path.EndsWith("/.", StringComparison.Ordinal))
            path = path[..^2];

        return path;
    }
}
