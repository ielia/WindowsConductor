using WindowsConductor.Client;

namespace WindowsConductor.InspectorGUI;

internal sealed class CommandExecutor(IInspectorSession session, ICommandOutput output)
{
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
                output.ClearHighlight();
                output.WriteInfo("Application closed.");
                break;

            case WindowScreenshotCommand:
                RequireApp();
                await ShowWindowScreenshotAsync(ct);
                break;

            case LocateCommand cmd:
                RequireApp();
                var elementId = await session.LocateAsync(cmd.Selectors, ct);
                output.WriteInfo($"Located element: {elementId}");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                break;

            case UnselectCommand:
                session.Unselect();
                output.ClearHighlight();
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
                break;

            case DoubleClickCommand:
                RequireElement();
                await session.DoubleClickAsync(ct);
                output.WriteInfo("Double-clicked.");
                await ShowWindowScreenshotWithHighlightAsync(ct);
                break;

            case TypeCommand cmd:
                RequireElement();
                await session.TypeAsync(cmd.Text, ct);
                output.WriteInfo($"Typed: {cmd.Text}");
                await ShowWindowScreenshotWithHighlightAsync(ct);
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
        }
    }

    private async Task ShowWindowScreenshotAsync(CancellationToken ct)
    {
        var imgData = await session.WindowScreenshotAsync(ct);
        output.ShowScreenshot(imgData);
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
}
