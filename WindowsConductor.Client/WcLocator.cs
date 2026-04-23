using System.Drawing;
using System.Text.Json;
using SkiaSharp;

namespace WindowsConductor.Client;

/// <summary>
/// Lazy reference to one or more UIAutomation elements, inspired by
/// Playwright's <c>ILocator</c> interface.
///
/// The selector is not resolved until an action or query method is called.
/// Each call re-queries the Driver, so the locator always reflects the
/// current state of the UI.
///
/// Chaining narrows scope: <c>app.GetByControlType("Panel").GetByName("OK")</c>
/// first resolves the panel, then searches within it.
/// </summary>
public sealed class WcLocator
{
    private readonly string _appId;
    private readonly string _selector;
    private readonly IWcTransport _conn;
    private readonly WcLocator? _parent;
    private readonly string? _rootElementId;

    internal WcLocator(string appId, string selector, IWcTransport conn, WcLocator? parent = null, string? rootElementId = null)
    {
        SelectorValidator.Validate(selector);
        _appId = appId;
        _selector = selector;
        _conn = conn;
        _parent = parent;
        _rootElementId = rootElementId;
    }

    // ── Scoped factory methods ────────────────────────────────────────────────

    /// <summary>Returns a locator scoped within this locator's match.</summary>
    public WcLocator Locator(string selector) =>
        new(_appId, selector, _conn, this);

    /// <summary>Finds elements by <c>AutomationId</c> within this locator's match.</summary>
    public WcLocator GetByAutomationId(string automationId) =>
        Locator($"[automationid={automationId.Replace("]", "\\]")}]");

    /// <summary>Finds elements by <c>Name</c> within this locator's match.</summary>
    public WcLocator GetByName(string name) =>
        Locator($"[name={name.Replace("]", "\\]")}]");

    /// <summary>Finds elements whose <c>Name</c> equals <paramref name="text"/> within this locator's match.</summary>
    public WcLocator GetByText(string text) =>
        Locator($"text={text.Replace("]", "\\]")}");

    /// <summary>Finds elements using an XPath expression within this locator's match.</summary>
    public WcLocator GetByXPath(string xpath)
    {
        string normalised = xpath.StartsWith('/') || xpath.StartsWith('.') ? xpath : $"//{xpath}";
        return Locator(normalised);
    }

    /// <summary>Finds elements by <c>ControlType</c> within this locator's match.</summary>
    public WcLocator GetByControlType(string controlType) =>
        Locator($"type={controlType}");

    /// <summary>Returns a locator that resolves to the parent of this locator's match.</summary>
    public WcLocator Parent() => Locator("/..");

    /// <summary>Returns all elements whose bounding rectangles contain the given point, scoped within this locator's match.</summary>
    public async Task<IReadOnlyList<WcElement>> GetAtAsync(double x, double y, CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        var result = await _conn.SendAsync(
            "findElementsAtPoint",
            new { appId = _appId, x, y, rootElementId = el.ElementId },
            ct);

        return result.EnumerateArray()
            .Select(e => new WcElement(e.GetString()!, _conn, _appId))
            .ToList();
    }

    /// <summary>Returns the front-most (smallest) element at the given point, scoped within this locator's match.</summary>
    public async Task<WcElement> GetFrontAtAsync(double x, double y, CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        var result = await _conn.SendAsync(
            "findFrontElementAtPoint",
            new { appId = _appId, x, y, rootElementId = el.ElementId },
            ct);

        return new WcElement(result.GetString()!, _conn, _appId);
    }

    // ── Element resolution ───────────────────────────────────────────────────

    /// <summary>Resolves and returns the first matching element.</summary>
    public async Task<WcElement> GetElementAsync(CancellationToken ct = default)
    {
        string? rootElementId = _rootElementId;
        if (rootElementId is null && _parent != null)
        {
            var parentElement = await _parent.GetElementAsync(ct);
            rootElementId = parentElement.ElementId;
        }

        var result = await _conn.SendAsync(
            "findElement",
            new { appId = _appId, selector = _selector, rootElementId },
            ct);

        string? elementId = result.GetString();
        if (elementId is null)
            throw new WcException($"No element found for selector: '{_selector}'");

        return new WcElement(elementId, _conn, _appId);
    }

    /// <summary>Resolves and returns all matching elements.</summary>
    public async Task<IReadOnlyList<WcElement>> GetAllElementsAsync(CancellationToken ct = default)
    {
        string? rootElementId = _rootElementId;
        if (rootElementId is null && _parent != null)
        {
            var parentElement = await _parent.GetElementAsync(ct);
            rootElementId = parentElement.ElementId;
        }

        var result = await _conn.SendAsync(
            "findElements",
            new { appId = _appId, selector = _selector, rootElementId },
            ct);

        return result.EnumerateArray()
            .Select(e => new WcElement(e.GetString()!, _conn, _appId))
            .ToList();
    }

    /// <summary>
    /// Resolves the selector and returns the result as a <see cref="WcValue"/>.
    /// Element selectors return a <c>ListValue</c> of string values (element text);
    /// attribute selectors return a <c>ListValue</c> of <see cref="WcAttr"/> items.
    /// </summary>
    public async Task<WcValue> GetResolvedValueAsync(CancellationToken ct = default)
    {
        string? rootElementId = _rootElementId;
        if (rootElementId is null && _parent != null)
        {
            var parentElement = await _parent.GetElementAsync(ct);
            rootElementId = parentElement.ElementId;
        }

        var result = await _conn.SendAsync(
            "resolveValue",
            new { appId = _appId, selector = _selector, rootElementId },
            ct);

        return DeserializeValue(result);
    }

    // ── Wait operations ───────────────────────────────────────────────────────

    /// <summary>
    /// Waits up to <paramref name="timeout"/> milliseconds for a matching element to appear.
    /// Throws <see cref="NoMatchException"/> if the timeout elapses without a match.
    /// </summary>
    public async Task<WcElement> WaitForElementAsync(uint timeout, CancellationToken ct = default)
    {
        string? rootElementId = _rootElementId;
        if (rootElementId is null && _parent != null)
        {
            var parentElement = await _parent.GetElementAsync(ct);
            rootElementId = parentElement.ElementId;
        }

        var result = await _conn.SendAsync(
            "waitForElement",
            new { appId = _appId, selector = _selector, rootElementId, timeout },
            ct);

        string? elementId = result.GetString();
        if (elementId is null)
            throw new NoMatchException($"No element found for selector: '{_selector}'");

        return new WcElement(elementId, _conn, _appId);
    }

    /// <summary>
    /// Waits up to <paramref name="timeout"/> milliseconds for at least one matching element to appear.
    /// Returns the full list of matches as soon as one is found.
    /// Throws <see cref="NoMatchException"/> if the timeout elapses without a match.
    /// </summary>
    public async Task<IReadOnlyList<WcElement>> WaitForAllElementsAsync(uint timeout, CancellationToken ct = default)
    {
        string? rootElementId = _rootElementId;
        if (rootElementId is null && _parent != null)
        {
            var parentElement = await _parent.GetElementAsync(ct);
            rootElementId = parentElement.ElementId;
        }

        var result = await _conn.SendAsync(
            "waitForElements",
            new { appId = _appId, selector = _selector, rootElementId, timeout },
            ct);

        return result.EnumerateArray()
            .Select(e => new WcElement(e.GetString()!, _conn, _appId))
            .ToList();
    }

    /// <summary>
    /// Waits up to <paramref name="timeout"/> milliseconds for a non-empty result.
    /// Returns the resolved value as soon as one is found.
    /// Throws <see cref="NoMatchException"/> if the timeout elapses without a match.
    /// </summary>
    public async Task<WcValue> WaitForResolvedValueAsync(uint timeout, CancellationToken ct = default)
    {
        string? rootElementId = _rootElementId;
        if (rootElementId is null && _parent != null)
        {
            var parentElement = await _parent.GetElementAsync(ct);
            rootElementId = parentElement.ElementId;
        }

        var result = await _conn.SendAsync(
            "waitForResolvedValue",
            new { appId = _appId, selector = _selector, rootElementId, timeout },
            ct);

        return DeserializeValue(result);
    }

    /// <summary>
    /// Waits up to <paramref name="timeout"/> milliseconds for the locator to stop matching anything.
    /// Throws <see cref="UnwantedMatchException"/> if the timeout elapses and elements still match.
    /// </summary>
    public async Task WaitForVanishAsync(uint timeout, CancellationToken ct = default)
    {
        string? rootElementId = _rootElementId;
        if (rootElementId is null && _parent != null)
        {
            var parentElement = await _parent.GetElementAsync(ct);
            rootElementId = parentElement.ElementId;
        }

        await _conn.SendAsync(
            "waitForVanish",
            new { appId = _appId, selector = _selector, rootElementId, timeout },
            ct);
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    /// <summary>Clicks the first matching element.</summary>
    public async Task ClickAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.ClickAsync(ct);
    }

    /// <summary>Clicks the first matching element.</summary>
    /// <param name="anchor">Offset anchor</param>
    /// <param name="offset">offset</param>
    public async Task ClickAsync(Anchor anchor, Point offset, CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.ClickAsync(anchor, offset, ct);
    }

    /// <summary>Double-clicks the first matching element.</summary>
    public async Task DoubleClickAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.DoubleClickAsync(ct);
    }

    /// <summary>Double-clicks the first matching element.</summary>
    /// <param name="anchor">Offset anchor</param>
    /// <param name="offset">offset</param>
    public async Task DoubleClickAsync(Anchor anchor, Point offset, CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.DoubleClickAsync(anchor, offset, ct);
    }

    /// <summary>Right-clicks the first matching element.</summary>
    public async Task RightClickAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.RightClickAsync(ct);
    }

    /// <summary>Right-clicks the first matching element.</summary>
    /// <param name="anchor">Offset anchor</param>
    /// <param name="offset">offset</param>
    public async Task RightClickAsync(Anchor anchor, Point offset, CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.RightClickAsync(anchor, offset, ct);
    }

    /// <summary>
    /// Focuses the first matching element and types <paramref name="text"/>
    /// using keyboard simulation.
    /// </summary>
    public async Task TypeAsync(string text, KeyModifiers modifiers = KeyModifiers.None, CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.TypeAsync(text, modifiers, ct);
    }

    /// <summary>Sets keyboard focus on the first matching element.</summary>
    public async Task FocusAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.FocusAsync(ct);
    }

    /// <summary>Brings the first matching element's window to the foreground.</summary>
    public async Task SetForegroundAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.SetForegroundAsync(ct);
    }

    /// <summary>Returns the window state of the first matching element's top-level window.</summary>
    public async Task<WcWindowState> GetWindowStateAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        return await el.GetWindowStateAsync(ct);
    }

    /// <summary>Sets the window state of the first matching element's top-level window.</summary>
    public async Task SetWindowStateAsync(WcWindowState state, CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.SetWindowStateAsync(state, ct);
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the visible text of the first matching element
    /// (TextBox.Text if applicable, otherwise Name).
    /// </summary>
    public async Task<string> GetTextAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        return await el.GetTextAsync(ct);
    }

    /// <summary>Returns a named UIAutomation property of the first matching element.</summary>
    public async Task<string> GetAttributeAsync(string attribute, CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        return await el.GetAttributeAsync(attribute, ct);
    }

    /// <summary>Returns all UIAutomation properties of the first matching element.</summary>
    public async Task<Dictionary<string, object?>> GetAttributesAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        return await el.GetAttributesAsync(ct);
    }

    /// <summary>Returns <c>true</c> if the first matching element is enabled.</summary>
    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        return await el.IsEnabledAsync(ct);
    }

    /// <summary>Returns <c>true</c> if the first matching element is on-screen.</summary>
    public async Task<bool> IsVisibleAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        return await el.IsVisibleAsync(ct);
    }

    /// <summary>Returns the bounding rectangle of the first matching element.</summary>
    public async Task<BoundingRect> GetBoundingRectAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        return await el.GetBoundingRectAsync(ct);
    }

    // ── Screenshots ────────────────────────────────────────────────────────

    /// <summary>Captures a screenshot of the first matching element as an SKBitmap.</summary>
    public async Task<SKBitmap> ScreenshotAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        return await el.ScreenshotAsync(ct);
    }

    public override string ToString() => _parent != null
        ? $"{_parent} > WcLocator({_selector})"
        : $"WcLocator({_selector})";

    // ── Value deserialization ────────────────────────────────────────────────

    private WcValue DeserializeValue(JsonElement json)
    {
        var typeName = json.GetProperty("type").GetString()!;
        var type = Enum.Parse<WcAttrType>(typeName);

        if (type == WcAttrType.ListValue)
        {
            var items = new List<WcValue>();
            foreach (var item in json.GetProperty("items").EnumerateArray())
                items.Add(DeserializeItem(item));
            return new WcValue(WcAttrType.ListValue, items);
        }

        var value = DeserializePrimitive(json, type);
        return new WcValue(type, value);
    }

    private WcValue DeserializeItem(JsonElement item)
    {
        var typeName = item.GetProperty("type").GetString()!;
        var type = Enum.Parse<WcAttrType>(typeName);
        var value = DeserializePrimitive(item, type);

        if (item.TryGetProperty("name", out var nameProp))
        {
            var name = nameProp.GetString()!;
            var elementId = item.GetProperty("elementId").GetString()!;
            var element = new WcElement(elementId, _conn, _appId);
            return new WcAttr(element, name, type, value);
        }

        if (item.TryGetProperty("elementId", out var elIdProp))
        {
            var elementId = elIdProp.GetString()!;
            var element = new WcElement(elementId, _conn, _appId);
            return new WcAttr(element, "text", type, value);
        }

        return new WcValue(type, value);
    }

    private static object? DeserializePrimitive(JsonElement item, WcAttrType type) => type switch
    {
        WcAttrType.NullValue => null,
        WcAttrType.BoolValue => item.GetProperty("value").GetBoolean(),
        WcAttrType.IntValue => item.GetProperty("value").GetInt32(),
        WcAttrType.LongValue => item.GetProperty("value").GetInt64(),
        WcAttrType.DoubleValue => item.GetProperty("value").GetDouble(),
        WcAttrType.PointValue => DeserializePoint(item.GetProperty("value")),
        WcAttrType.RectangleValue => DeserializeRectangle(item.GetProperty("value")),
        _ => item.GetProperty("value").GetString()
    };

    private static System.Drawing.Point DeserializePoint(JsonElement v) =>
        new(v.GetProperty("x").GetInt32(), v.GetProperty("y").GetInt32());

    private static System.Drawing.Rectangle DeserializeRectangle(JsonElement v) =>
        new(v.GetProperty("x").GetInt32(), v.GetProperty("y").GetInt32(),
            v.GetProperty("width").GetInt32(), v.GetProperty("height").GetInt32());
}
