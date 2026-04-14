using System.Text.Json;
using SkiaSharp;

namespace WindowsConductor.Client;

/// <summary>
/// Handle to a specific UIAutomation element that was resolved and cached
/// on the Driver side.  Analogous to <c>IElementHandle</c> in Playwright.
///
/// Element handles can become stale if the UI changes; prefer using
/// <see cref="WcLocator"/> for most operations because it re-queries
/// the Driver on every call.
/// </summary>
public sealed class WcElement
{
    private readonly string? _appId;
    private readonly IWcTransport _conn;

    internal string ElementId { get; }

    internal WcElement(string elementId, IWcTransport conn, string? appId = null)
    {
        ElementId = elementId;
        _conn = conn;
        _appId = appId;
    }

    /// <summary>Returns a locator scoped within this element.</summary>
    public WcLocator Locator(string selector)
    {
        if (_appId is null)
            throw new InvalidOperationException("Cannot create a locator from an element without an associated application.");
        return new WcLocator(_appId, selector, _conn, rootElementId: ElementId);
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    // Task<T> derives from Task, so these are valid Task-returning methods.
    public Task ClickAsync(CancellationToken ct = default) =>
        _conn.SendAsync("click", new { elementId = ElementId }, ct);

    public Task DoubleClickAsync(CancellationToken ct = default) =>
        _conn.SendAsync("doubleClick", new { elementId = ElementId }, ct);

    public Task RightClickAsync(CancellationToken ct = default) =>
        _conn.SendAsync("rightClick", new { elementId = ElementId }, ct);

    public Task TypeAsync(string text, KeyModifiers modifiers = KeyModifiers.None, CancellationToken ct = default) =>
        _conn.SendAsync("typeText", new { elementId = ElementId, text, modifiers = (int)modifiers }, ct);

    public Task FocusAsync(CancellationToken ct = default) =>
        _conn.SendAsync("focus", new { elementId = ElementId }, ct);

    public async Task<WcElement?> ParentAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getParent", new { elementId = ElementId }, ct);
        var parentId = r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        return parentId is null ? null : new WcElement(parentId, _conn, _appId);
    }

    public async Task<WcElement?> TopLevelWindowAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getTopLevelWindow", new { elementId = ElementId }, ct);
        var windowId = r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        return windowId is null ? null : new WcElement(windowId, _conn, _appId);
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<string> GetTextAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getText", new { elementId = ElementId }, ct);
        return r.GetString() ?? "";
    }

    public async Task<string> GetAttributeAsync(string attribute, CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getAttribute",
            new { elementId = ElementId, attribute }, ct);
        return r.GetString() ?? "";
    }

    public async Task<Dictionary<string, object?>> GetAttributesAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getAttributes",
            new { elementId = ElementId }, ct);
        var dict = new Dictionary<string, object?>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var prop in r.EnumerateObject())
            dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
        return dict;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("isEnabled", new { elementId = ElementId }, ct);
        return r.ValueKind == JsonValueKind.True;
    }

    public async Task<bool> IsVisibleAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("isVisible", new { elementId = ElementId }, ct);
        return r.ValueKind == JsonValueKind.True;
    }

    public async Task<BoundingRect> GetBoundingRectAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getBoundingRect", new { elementId = ElementId }, ct);
        return new BoundingRect(
            r.GetProperty("x").GetDouble(),
            r.GetProperty("y").GetDouble(),
            r.GetProperty("width").GetDouble(),
            r.GetProperty("height").GetDouble());
    }

    // ── Tree navigation ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WcElement>> ChildrenAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getChildren", new { elementId = ElementId }, ct);
        return r.EnumerateArray()
            .Select(e => new WcElement(e.GetString()!, _conn, _appId))
            .ToList();
    }

    public async Task<IReadOnlyTreeNode<WcElement>> DescendantsAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getDescendants", new { elementId = ElementId }, ct);
        return ParseTreeNode(r);
    }

    private TreeNode<WcElement> ParseTreeNode(JsonElement json)
    {
        var id = json.GetProperty("id").GetString()!;
        var node = new TreeNode<WcElement>(new WcElement(id, _conn, _appId));
        if (json.TryGetProperty("children", out var children))
            foreach (var child in children.EnumerateArray())
                node.AddChild(ParseTreeNode(child));
        return node;
    }

    // ── Screenshots ────────────────────────────────────────────────────────

    /// <summary>Captures a screenshot of this element as raw PNG bytes.</summary>
    public async Task<byte[]> ScreenshotBytesAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("screenshot",
            new { elementId = ElementId }, ct);
        return r.GetBytesFromBase64();
    }

    /// <summary>Captures a screenshot of this element as an SKBitmap.</summary>
    public async Task<SKBitmap> ScreenshotAsync(CancellationToken ct = default)
    {
        var bytes = await ScreenshotBytesAsync(ct);
        return SKBitmap.Decode(bytes);
    }

    public override string ToString() => $"WcElement({ElementId})";
}

/// <summary>Screen coordinates and dimensions of a UIAutomation element.</summary>
/// <param name="X">Left edge in screen pixels.</param>
/// <param name="Y">Top edge in screen pixels.</param>
/// <param name="Width">Element width in pixels.</param>
/// <param name="Height">Element height in pixels.</param>
public sealed record BoundingRect(double X, double Y, double Width, double Height)
{
    public bool Contains(double pointX, double pointY) =>
        pointX >= X && pointX <= X + Width && pointY >= Y && pointY <= Y + Height;
}
