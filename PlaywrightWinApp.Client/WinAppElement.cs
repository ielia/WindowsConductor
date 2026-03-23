using System.Text.Json;

namespace PlaywrightWinApp.Client;

/// <summary>
/// Handle to a specific UIAutomation element that was resolved and cached
/// on the Driver side.  Analogous to <c>IElementHandle</c> in Playwright.
///
/// Element handles can become stale if the UI changes; prefer using
/// <see cref="WinAppLocator"/> for most operations because it re-queries
/// the Driver on every call.
/// </summary>
public sealed class WinAppElement
{
    private readonly string _elementId;
    private readonly WinAppConnection _conn;

    internal WinAppElement(string elementId, WinAppConnection conn)
    {
        _elementId = elementId;
        _conn      = conn;
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    // Task<T> derives from Task, so these are valid Task-returning methods.
    public Task ClickAsync(CancellationToken ct = default) =>
        _conn.SendAsync("click", new { elementId = _elementId }, ct);

    public Task DoubleClickAsync(CancellationToken ct = default) =>
        _conn.SendAsync("doubleClick", new { elementId = _elementId }, ct);

    public Task TypeAsync(string text, CancellationToken ct = default) =>
        _conn.SendAsync("typeText", new { elementId = _elementId, text }, ct);

    public Task FocusAsync(CancellationToken ct = default) =>
        _conn.SendAsync("focus", new { elementId = _elementId }, ct);

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<string> GetTextAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getText", new { elementId = _elementId }, ct);
        return r.GetString() ?? "";
    }

    public async Task<string> GetAttributeAsync(string attribute, CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getAttribute",
            new { elementId = _elementId, attribute }, ct);
        return r.GetString() ?? "";
    }

    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("isEnabled", new { elementId = _elementId }, ct);
        return r.ValueKind == JsonValueKind.True;
    }

    public async Task<bool> IsVisibleAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("isVisible", new { elementId = _elementId }, ct);
        return r.ValueKind == JsonValueKind.True;
    }

    public async Task<BoundingRect> GetBoundingRectAsync(CancellationToken ct = default)
    {
        var r = await _conn.SendAsync("getBoundingRect", new { elementId = _elementId }, ct);
        return new BoundingRect(
            r.GetProperty("x").GetDouble(),
            r.GetProperty("y").GetDouble(),
            r.GetProperty("width").GetDouble(),
            r.GetProperty("height").GetDouble());
    }

    public override string ToString() => $"WinAppElement({_elementId})";
}

/// <summary>Screen coordinates and dimensions of a UIAutomation element.</summary>
/// <param name="X">Left edge in screen pixels.</param>
/// <param name="Y">Top edge in screen pixels.</param>
/// <param name="Width">Element width in pixels.</param>
/// <param name="Height">Element height in pixels.</param>
public sealed record BoundingRect(double X, double Y, double Width, double Height);
