using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WindowsConductor.Client;

namespace WindowsConductor.MCP.Tools;

[McpServerToolType]
public sealed class AppTools(ConductorState state)
{
    [McpServerTool, Description(
        "Launch a Windows application and return its appId for subsequent operations.")]
    public async Task<string> LaunchApp(
        [Description("Executable path or name (e.g. 'calc.exe', 'notepad.exe')")] string path,
        [Description("Command-line arguments (optional)")] string[]? args = null,
        [Description("Regex for matching a detached window title (optional)")] string? detachedTitleRegex = null,
        [Description("Timeout in ms to wait for the main window to appear (optional)")] uint? mainWindowTimeout = null)
    {
        var session = state.RequireSession();
        var app = await session.LaunchAsync(path, args, detachedTitleRegex, mainWindowTimeout);
        state.TrackApp(app.AppId, app);
        return $"Launched '{path}'. appId: {app.AppId}";
    }

    [McpServerTool, Description(
        "Attach to an already-running application by matching its window title. " +
        "Returns the appId. The application will NOT be closed on disconnect.")]
    public async Task<string> AttachApp(
        [Description("Regex pattern to match against window titles")] string mainWindowTitleRegex,
        [Description("Timeout in ms to wait for the window to appear (optional)")] uint? mainWindowTimeout = null)
    {
        var session = state.RequireSession();
        var app = await session.AttachAsync(mainWindowTitleRegex, mainWindowTimeout);
        state.TrackApp(app.AppId, app);
        return $"Attached to window matching '{mainWindowTitleRegex}'. appId: {app.AppId}";
    }

    [McpServerTool, Description("Close a tracked application.")]
    public async Task<string> CloseApp(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId)
    {
        var app = state.GetApp(appId);
        await app.CloseAsync();
        state.TryRemoveApp(appId);
        return $"Closed app {appId}.";
    }

    [McpServerTool, Description("Get the title of an application's main window.")]
    public async Task<string> GetAppTitle(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId)
    {
        var app = state.GetApp(appId);
        return await app.GetTitleAsync();
    }

    [McpServerTool, Description("List all currently tracked application IDs.")]
    public string ListApps()
    {
        var ids = state.AppIds;
        return ids.Count == 0
            ? "No tracked applications."
            : string.Join("\n", ids);
    }

    [McpServerTool, Description(
        "Take a screenshot of an application's main window. " +
        "Returns base64-encoded PNG, or if outputPath is provided, saves to file and returns the full path.")]
    public async Task<string> ScreenshotApp(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Optional relative path (under the screenshot root directory) to save the PNG file. " +
                     "If omitted, returns base64-encoded PNG instead.")] string? outputPath = null)
    {
        var app = state.GetApp(appId);
        var bytes = await app.ScreenshotBytesAsync();
        return await SaveOrEncodeScreenshot(bytes, outputPath);
    }

    [McpServerTool, Description(
        "Take a screenshot of the entire desktop. " +
        "Returns base64-encoded PNG, or if outputPath is provided, saves to file and returns the full path.")]
    public async Task<string> ScreenshotDesktop(
        [Description("Optional relative path (under the screenshot root directory) to save the PNG file. " +
                     "If omitted, returns base64-encoded PNG instead.")] string? outputPath = null)
    {
        var session = state.RequireSession();
        var bytes = await session.DesktopScreenshotBytesAsync();
        return await SaveOrEncodeScreenshot(bytes, outputPath);
    }

    [McpServerTool, Description(
        "Find UI elements using a selector (CSS-like attributes, XPath, text, or control type). " +
        "Returns a JSON array of element IDs. " +
        "Selector examples: " +
        "[automationid=myId], [name=OK], text=Save, type=Button, " +
        "//Button[@Name='7'], //*[@AutomationId='result']")]
    public async Task<string> FindElements(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Element selector (attribute, text, type, or XPath)")] string selector)
    {
        var app = state.GetApp(appId);
        var locator = app.Locator(selector);
        var elements = await locator.GetAllElementsAsync();
        var ids = elements.Select(e => e.ElementId).ToArray();
        return JsonSerializer.Serialize(ids);
    }

    [McpServerTool, Description(
        "Find a single UI element using a selector. Returns its element ID.")]
    public async Task<string> FindElement(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Element selector (attribute, text, type, or XPath)")] string selector)
    {
        var app = state.GetApp(appId);
        var locator = app.Locator(selector);
        var element = await locator.GetElementAsync();
        return element.ElementId;
    }

    [McpServerTool, Description(
        "Find UI elements scoped within a specific parent element. " +
        "Returns a JSON array of element IDs. " +
        "Use this to narrow searches to a subtree of the UI.")]
    public async Task<string> FindElementsWithin(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Element ID of the root/parent to search within")] string rootElementId,
        [Description("Element selector (attribute, text, type, or XPath)")] string selector)
    {
        var app = state.GetApp(appId);
        var root = new WcElement(rootElementId, app.Connection, app.AppId);
        var locator = root.Locator(selector);
        var elements = await locator.GetAllElementsAsync();
        var ids = elements.Select(e => e.ElementId).ToArray();
        return JsonSerializer.Serialize(ids);
    }

    [McpServerTool, Description(
        "Find a single UI element scoped within a specific parent element. " +
        "Returns its element ID.")]
    public async Task<string> FindElementWithin(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Element ID of the root/parent to search within")] string rootElementId,
        [Description("Element selector (attribute, text, type, or XPath)")] string selector)
    {
        var app = state.GetApp(appId);
        var root = new WcElement(rootElementId, app.Connection, app.AppId);
        var locator = root.Locator(selector);
        var element = await locator.GetElementAsync();
        return element.ElementId;
    }

    [McpServerTool, Description(
        "Resolve a selector and return the typed result. " +
        "Element selectors return a list of element text values; " +
        "attribute selectors return a list of attribute values with element IDs. " +
        "Returns JSON with 'type' and 'value' (or 'items' for lists).")]
    public async Task<string> ResolveValue(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Element selector (attribute, text, type, or XPath)")] string selector,
        [Description("Optional element ID to scope the search within")] string? rootElementId = null)
    {
        var locator = BuildLocator(appId, selector, rootElementId);
        var value = await locator.GetResolvedValueAsync();
        return JsonSerializer.Serialize(SerializeWcValue(value));
    }

    // ── Wait operations ───────────────────────────────────────────────────

    [McpServerTool, Description(
        "Wait for a UI element matching the selector to appear. " +
        "Returns the element ID once found, or throws if the timeout elapses.")]
    public async Task<string> WaitForElement(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Element selector (attribute, text, type, or XPath)")] string selector,
        [Description("Timeout in milliseconds")] uint timeout,
        [Description("Optional element ID to scope the search within")] string? rootElementId = null)
    {
        var locator = BuildLocator(appId, selector, rootElementId);
        var element = await locator.WaitForElementAsync(timeout);
        return element.ElementId;
    }

    [McpServerTool, Description(
        "Wait for UI elements matching the selector to appear. " +
        "Returns a JSON array of element IDs once at least one is found, or throws if the timeout elapses.")]
    public async Task<string> WaitForElements(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Element selector (attribute, text, type, or XPath)")] string selector,
        [Description("Timeout in milliseconds")] uint timeout,
        [Description("Optional element ID to scope the search within")] string? rootElementId = null)
    {
        var locator = BuildLocator(appId, selector, rootElementId);
        var elements = await locator.WaitForAllElementsAsync(timeout);
        var ids = elements.Select(e => e.ElementId).ToArray();
        return JsonSerializer.Serialize(ids);
    }

    [McpServerTool, Description(
        "Wait for a selector to resolve to a non-empty value. " +
        "Returns JSON with 'type' and 'value' once found, or throws if the timeout elapses.")]
    public async Task<string> WaitForResolvedValue(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Element selector (attribute, text, type, or XPath)")] string selector,
        [Description("Timeout in milliseconds")] uint timeout,
        [Description("Optional element ID to scope the search within")] string? rootElementId = null)
    {
        var locator = BuildLocator(appId, selector, rootElementId);
        var value = await locator.WaitForResolvedValueAsync(timeout);
        return JsonSerializer.Serialize(SerializeWcValue(value));
    }

    [McpServerTool, Description(
        "Wait for UI elements matching the selector to disappear. " +
        "Returns a confirmation once no elements match, or throws if the timeout elapses.")]
    public async Task<string> WaitForVanish(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Element selector (attribute, text, type, or XPath)")] string selector,
        [Description("Timeout in milliseconds")] uint timeout,
        [Description("Optional element ID to scope the search within")] string? rootElementId = null)
    {
        var locator = BuildLocator(appId, selector, rootElementId);
        await locator.WaitForVanishAsync(timeout);
        return "Element vanished.";
    }

    // ── Hit-testing ─────────────────────────────────────────────────────────

    [McpServerTool, Description(
        "Find all UI elements whose bounding rectangles contain the given screen point. " +
        "Returns a JSON array of element IDs. " +
        "Note: hit-testing by coordinates is not fully reliable — prefer FindElement/FindElements with selectors when possible.")]
    public async Task<string> GetElementsAtPoint(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("X coordinate in screen pixels")] double x,
        [Description("Y coordinate in screen pixels")] double y)
    {
        var app = state.GetApp(appId);
        var elements = await app.GetAtAsync(x, y);
        var ids = elements.Select(e => e.ElementId).ToArray();
        return JsonSerializer.Serialize(ids);
    }

    [McpServerTool, Description(
        "Find the front-most (smallest) UI element at the given screen point. " +
        "Returns its element ID. " +
        "Note: hit-testing by coordinates is not fully reliable — prefer FindElement/FindElements with selectors when possible.")]
    public async Task<string> GetFrontElementAtPoint(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("X coordinate in screen pixels")] double x,
        [Description("Y coordinate in screen pixels")] double y)
    {
        var app = state.GetApp(appId);
        var element = await app.GetFrontAtAsync(x, y);
        return element.ElementId;
    }

    [McpServerTool, Description("Start video recording of an application's window.")]
    public async Task<string> StartRecording(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId)
    {
        var app = state.GetApp(appId);
        await app.StartRecordingAsync();
        return $"Recording started for app {appId}.";
    }

    [McpServerTool, Description(
        "Stop video recording of an application's window and save it to a file. " +
        "Returns the full path to the saved video file.")]
    public async Task<string> StopRecording(
        [Description("The appId returned by LaunchApp or AttachApp")] string appId,
        [Description("Optional relative path (under the video root directory) for the output file, e.g. 'session1/test.mp4'. " +
                     "Subdirectories are created automatically. If omitted, a timestamped filename is generated.")] string? outputPath = null)
    {
        var app = state.GetApp(appId);
        var bytes = await app.StopRecordingAsync();
        var filePath = state.ResolveVideoPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllBytesAsync(filePath, bytes);
        return filePath;
    }

    private WcLocator BuildLocator(string appId, string selector, string? rootElementId)
    {
        var app = state.GetApp(appId);
        if (rootElementId is not null)
        {
            var root = new WcElement(rootElementId, app.Connection, app.AppId);
            return root.Locator(selector);
        }
        return app.Locator(selector);
    }

    private async Task<string> SaveOrEncodeScreenshot(byte[] bytes, string? outputPath)
    {
        if (outputPath is null)
            return Convert.ToBase64String(bytes);

        var filePath = state.ResolveScreenshotPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllBytesAsync(filePath, bytes);
        return filePath;
    }

    private static object? SerializeWcValue(WcValue value)
    {
        if (value.Type == WcAttrType.NullValue)
            return new { type = "null", value = (object?)null };

        if (value.Type == WcAttrType.ListValue)
        {
            var items = value.GetAsList()!;
            return new { type = "list", items = items.Select(SerializeWcValue).ToArray() };
        }

        var result = new Dictionary<string, object?>
        {
            ["type"] = value.Type.ToString(),
            ["value"] = value.Value
        };

        if (value is WcAttr attr)
        {
            result["elementId"] = attr.Element.ElementId;
            result["name"] = attr.Name;
        }

        return result;
    }
}
