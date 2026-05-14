using System.ComponentModel;
using System.Drawing;
using System.Text.Json;
using ModelContextProtocol.Server;
using WindowsConductor.Client;

namespace WindowsConductor.MCP.Tools;

[McpServerToolType]
public sealed class ElementTools(ConductorState state)
{
    [McpServerTool, Description("Click a UI element.")]
    public async Task<string> ClickElement(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        await element.ClickAsync();
        return "Clicked.";
    }

    [McpServerTool, Description("Double-click a UI element.")]
    public async Task<string> DoubleClickElement(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        await element.DoubleClickAsync();
        return "Double-clicked.";
    }

    [McpServerTool, Description("Right-click a UI element.")]
    public async Task<string> RightClickElement(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        await element.RightClickAsync();
        return "Right-clicked.";
    }

    [McpServerTool, Description("Hover the mouse over a UI element.")]
    public async Task<string> HoverElement(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        await element.HoverAsync();
        return "Hovered.";
    }

    [McpServerTool, Description(
        "Click a UI element at a specific position relative to an anchor point. " +
        "Anchors: Center, North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest.")]
    public async Task<string> ClickElementAt(
        [Description("Element ID returned by FindElement/FindElements")] string elementId,
        [Description("Anchor point on the element (e.g. Center, NorthWest)")] string anchor,
        [Description("Horizontal offset in pixels from the anchor")] int offsetX,
        [Description("Vertical offset in pixels from the anchor")] int offsetY)
    {
        var element = ResolveElement(elementId);
        var parsedAnchor = Enum.Parse<Anchor>(anchor, ignoreCase: true);
        await element.ClickAsync(parsedAnchor, new Point(offsetX, offsetY));
        return $"Clicked at {anchor} + ({offsetX}, {offsetY}).";
    }

    [McpServerTool, Description(
        "Double-click a UI element at a specific position relative to an anchor point.")]
    public async Task<string> DoubleClickElementAt(
        [Description("Element ID returned by FindElement/FindElements")] string elementId,
        [Description("Anchor point on the element (e.g. Center, NorthWest)")] string anchor,
        [Description("Horizontal offset in pixels from the anchor")] int offsetX,
        [Description("Vertical offset in pixels from the anchor")] int offsetY)
    {
        var element = ResolveElement(elementId);
        var parsedAnchor = Enum.Parse<Anchor>(anchor, ignoreCase: true);
        await element.DoubleClickAsync(parsedAnchor, new Point(offsetX, offsetY));
        return $"Double-clicked at {anchor} + ({offsetX}, {offsetY}).";
    }

    [McpServerTool, Description(
        "Right-click a UI element at a specific position relative to an anchor point.")]
    public async Task<string> RightClickElementAt(
        [Description("Element ID returned by FindElement/FindElements")] string elementId,
        [Description("Anchor point on the element (e.g. Center, NorthWest)")] string anchor,
        [Description("Horizontal offset in pixels from the anchor")] int offsetX,
        [Description("Vertical offset in pixels from the anchor")] int offsetY)
    {
        var element = ResolveElement(elementId);
        var parsedAnchor = Enum.Parse<Anchor>(anchor, ignoreCase: true);
        await element.RightClickAsync(parsedAnchor, new Point(offsetX, offsetY));
        return $"Right-clicked at {anchor} + ({offsetX}, {offsetY}).";
    }

    [McpServerTool, Description(
        "Hover the mouse over a UI element at a specific position relative to an anchor point.")]
    public async Task<string> HoverElementAt(
        [Description("Element ID returned by FindElement/FindElements")] string elementId,
        [Description("Anchor point on the element (e.g. Center, NorthWest)")] string anchor,
        [Description("Horizontal offset in pixels from the anchor")] int offsetX,
        [Description("Vertical offset in pixels from the anchor")] int offsetY)
    {
        var element = ResolveElement(elementId);
        var parsedAnchor = Enum.Parse<Anchor>(anchor, ignoreCase: true);
        await element.HoverAsync(parsedAnchor, new Point(offsetX, offsetY));
        return $"Hovered at {anchor} + ({offsetX}, {offsetY}).";
    }

    [McpServerTool, Description("Type text into a UI element (focuses it first).")]
    public async Task<string> TypeText(
        [Description("Element ID returned by FindElement/FindElements")] string elementId,
        [Description("Text to type")] string text,
        [Description("Optional modifier keys to hold while typing (e.g. [\"Shift\", \"Ctrl\"]). " +
                     "Valid values: Shift, Ctrl, Alt, Meta")] string[]? modifiers = null)
    {
        var element = ResolveElement(elementId);
        var parsed = KeyModifiers.None;
        if (modifiers is not null)
            foreach (var m in modifiers)
                parsed |= Enum.Parse<KeyModifiers>(m, ignoreCase: true);
        await element.TypeAsync(text, parsed);
        return $"Typed '{text}'.";
    }

    [McpServerTool, Description(
        "Press keyboard keys on a UI element. " +
        "Key names match the Key enum: ENTER, TAB, ESCAPE, BACK, DELETE, " +
        "KEY_A through KEY_Z, F1-F12, CONTROL, SHIFT, ALT, etc.")]
    public async Task<string> HitKeys(
        [Description("Element ID returned by FindElement/FindElements")] string elementId,
        [Description("Array of key names to press (e.g. [\"CONTROL\", \"KEY_A\"])")] string[] keys)
    {
        var element = ResolveElement(elementId);
        var parsed = keys.Select(k => Enum.Parse<Key>(k, ignoreCase: true)).ToArray();
        await element.HitKeysAsync(parsed);
        return $"Pressed keys: {string.Join("+", keys)}.";
    }

    [McpServerTool, Description("Focus a UI element.")]
    public async Task<string> FocusElement(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        await element.FocusAsync();
        return "Focused.";
    }

    [McpServerTool, Description("Bring a UI element's window to the foreground.")]
    public async Task<string> SetForeground(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        await element.SetForegroundAsync();
        return "Brought to foreground.";
    }

    [McpServerTool, Description("Get the visible text of a UI element.")]
    public async Task<string> GetText(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        return await element.GetTextAsync();
    }

    [McpServerTool, Description("Get a specific UIAutomation attribute of a UI element.")]
    public async Task<string> GetAttribute(
        [Description("Element ID returned by FindElement/FindElements")] string elementId,
        [Description("Attribute name (e.g. AutomationId, Name, ClassName, ControlType)")] string attribute)
    {
        var element = ResolveElement(elementId);
        return await element.GetAttributeAsync(attribute);
    }

    [McpServerTool, Description("Get all UIAutomation attributes of a UI element. Returns JSON.")]
    public async Task<string> GetAttributes(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        var attrs = await element.GetAttributesAsync();
        return JsonSerializer.Serialize(attrs);
    }

    [McpServerTool, Description("Check if a UI element is enabled.")]
    public async Task<bool> IsEnabled(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        return await element.IsEnabledAsync();
    }

    [McpServerTool, Description("Check if a UI element is visible on screen.")]
    public async Task<bool> IsVisible(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        return await element.IsVisibleAsync();
    }

    [McpServerTool, Description(
        "Get the bounding rectangle of a UI element. " +
        "Returns JSON with x, y, width, height in screen pixels.")]
    public async Task<string> GetBoundingRect(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        var rect = await element.GetBoundingRectAsync();
        return JsonSerializer.Serialize(new { rect.X, rect.Y, rect.Width, rect.Height });
    }

    [McpServerTool, Description(
        "Take a screenshot of a specific UI element. " +
        "Returns base64-encoded PNG, or if outputPath is provided, saves to file and returns the full path.")]
    public async Task<string> ScreenshotElement(
        [Description("Element ID returned by FindElement/FindElements")] string elementId,
        [Description("Optional relative path (under the screenshot root directory) to save the PNG file. " +
                     "If omitted, returns base64-encoded PNG instead.")] string? outputPath = null)
    {
        var element = ResolveElement(elementId);
        var bytes = await element.ScreenshotBytesAsync();
        if (outputPath is null)
            return Convert.ToBase64String(bytes);

        var filePath = state.ResolveScreenshotPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllBytesAsync(filePath, bytes);
        return filePath;
    }

    [McpServerTool, Description(
        "Get the direct children of a UI element. " +
        "Returns a JSON array of element IDs.")]
    public async Task<string> GetChildren(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        var children = await element.ChildrenAsync();
        var ids = children.Select(c => c.ElementId).ToArray();
        return JsonSerializer.Serialize(ids);
    }

    [McpServerTool, Description(
        "Get the full descendant tree of a UI element. " +
        "Returns a JSON tree where each node has 'id' and 'children' properties.")]
    public async Task<string> GetDescendants(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        var tree = await element.DescendantsAsync();
        return JsonSerializer.Serialize(SerializeTreeNode(tree));
    }

    [McpServerTool, Description(
        "Get the parent of a UI element. Returns the parent's element ID, or null if none.")]
    public async Task<string?> GetParent(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        var parent = await element.ParentAsync();
        return parent?.ElementId;
    }

    [McpServerTool, Description(
        "Get the top-level window containing a UI element. Returns the window's element ID, or null if none.")]
    public async Task<string?> GetTopLevelWindow(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        var window = await element.TopLevelWindowAsync();
        return window?.ElementId;
    }

    [McpServerTool, Description(
        "Get the window state of a UI element's top-level window. " +
        "Returns one of: Normal, Maximized, Minimized, MinimizedMaximized, Hidden.")]
    public async Task<string> GetWindowState(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        var windowState = await element.GetWindowStateAsync();
        return windowState.ToString();
    }

    [McpServerTool, Description(
        "Set the window state of a UI element's top-level window. " +
        "Valid states: Normal, Maximized, Minimized, MinimizedMaximized. " +
        "Hidden is not allowed.")]
    public async Task<string> SetWindowState(
        [Description("Element ID returned by FindElement/FindElements")] string elementId,
        [Description("Window state: Normal, Maximized, Minimized, or MinimizedMaximized")] string windowState)
    {
        var element = ResolveElement(elementId);
        var parsed = Enum.Parse<WcWindowState>(windowState, ignoreCase: true);
        await element.SetWindowStateAsync(parsed);
        return $"Window state set to {parsed}.";
    }

    [McpServerTool, Description(
        "Read text from a UI element using OCR. " +
        "Returns JSON with the full recognized text, angle, bounding rectangle, " +
        "and a hierarchy of lines and words with their own bounding rectangles.")]
    public async Task<string> GetOcrText(
        [Description("Element ID returned by FindElement/FindElements")] string elementId)
    {
        var element = ResolveElement(elementId);
        var result = await element.GetOcrTextAsync();
        return JsonSerializer.Serialize(SerializeOcrResult(result));
    }

    [McpServerTool, Description(
        "Search for text within an element's OCR result using fuzzy matching. " +
        "Returns a JSON array of matches, each with: matchedText, boundingRect (relative to element, NorthWest-anchored), " +
        "editDistance, lineText (the full line containing the match for disambiguation), " +
        "and lineBoundingRect. " +
        "Use ClickElementAt with anchor=NorthWest and the match boundingRect center as offset to click a match.")]
    public async Task<string> FindOcrText(
        [Description("Element ID returned by FindElement/FindElements")] string elementId,
        [Description("Text to search for within the OCR result")] string searchText,
        [Description("Maximum edit distance for fuzzy matching (0 = exact, default 0)")] int maxEdits = 0)
    {
        var element = ResolveElement(elementId);
        var ocrResult = await element.GetOcrTextAsync();
        var matches = ocrResult.FindAllByEdits(searchText, maxEdits);
        var serialized = matches.Select(m => SerializeOcrMatch(m, ocrResult)).ToArray();
        return JsonSerializer.Serialize(serialized);
    }

    private WcElement ResolveElement(string elementId)
    {
        var transport = state.ResolveTransport();
        return new WcElement(elementId, transport);
    }

    private static object SerializeTreeNode(IReadOnlyTreeNode<WcElement> node) => new
    {
        id = node.Value.ElementId,
        children = node.Children.Select(SerializeTreeNode).ToArray()
    };

    private static object SerializeOcrResult(WcElementOcrResult result) => new
    {
        text = result.Text,
        angle = result.Angle,
        boundingRect = SerializeBoundingRect(result.BoundingRect),
        lines = result.Lines.Select(line => new
        {
            text = line.Text,
            boundingRect = SerializeBoundingRect(line.BoundingRect),
            words = line.Words.Select(word => new
            {
                text = word.Text,
                boundingRect = SerializeBoundingRect(word.BoundingRect)
            }).ToArray()
        }).ToArray()
    };

    private static object SerializeOcrMatch(WcElementOcrMatch match, WcElementOcrResult ocrResult)
    {
        var line = FindContainingLine(match, ocrResult);
        return new
        {
            matchedText = match.Text,
            boundingRect = SerializeBoundingRect(match.BoundingRect),
            editDistance = match.Distance,
            lineText = line?.Text,
            lineBoundingRect = line is not null ? SerializeBoundingRect(line.BoundingRect) : null
        };
    }

    private static WcElementOcrLine? FindContainingLine(WcElementOcrMatch match, WcElementOcrResult result)
    {
        int pos = 0;
        foreach (var line in result.Lines)
        {
            int lineStart = result.Text.IndexOf(line.Text, pos, StringComparison.Ordinal);
            if (lineStart < 0) lineStart = pos;
            int lineEnd = lineStart + line.Text.Length;
            if (match.FromIndex >= lineStart && match.FromIndex < lineEnd)
                return line;
            pos = lineEnd;
        }
        return null;
    }

    private static object SerializeBoundingRect(BoundingRect rect) => new
    {
        x = rect.X,
        y = rect.Y,
        width = rect.Width,
        height = rect.Height
    };
}
