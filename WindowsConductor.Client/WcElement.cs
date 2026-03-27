using System.Text.Json;

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
    private readonly IWcTransport _conn;

    internal string ElementId { get; }

    internal WcElement(string elementId, IWcTransport conn)
    {
        ElementId = elementId;
        _conn = conn;
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    // Task<T> derives from Task, so these are valid Task-returning methods.
    public Task ClickAsync(CancellationToken ct = default) =>
        _conn.SendAsync("click", new { elementId = ElementId }, ct);

    public Task DoubleClickAsync(CancellationToken ct = default) =>
        _conn.SendAsync("doubleClick", new { elementId = ElementId }, ct);

    public Task RightClickAsync(CancellationToken ct = default) =>
        _conn.SendAsync("rightClick", new { elementId = ElementId }, ct);

    public Task TypeAsync(string text, CancellationToken ct = default) =>
        _conn.SendAsync("typeText", new { elementId = ElementId, text }, ct);

    public Task FocusAsync(CancellationToken ct = default) =>
        _conn.SendAsync("focus", new { elementId = ElementId }, ct);

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
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
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

    // ── Screenshots ────────────────────────────────────────────────────────

    /// <summary>Captures a screenshot of this element. Returns the saved file path.</summary>
    public async Task<string> ScreenshotAsync(string? path = null, CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("screenshot",
            new { elementId = ElementId, path = path ?? "" }, ct);
        return r.GetString() ?? "";
    }

    public override string ToString() => $"WcElement({ElementId})";
}

/// <summary>Screen coordinates and dimensions of a UIAutomation element.</summary>
/// <param name="X">Left edge in screen pixels.</param>
/// <param name="Y">Top edge in screen pixels.</param>
/// <param name="Width">Element width in pixels.</param>
/// <param name="Height">Element height in pixels.</param>
public sealed record BoundingRect(double X, double Y, double Width, double Height);
