using System.Text.Json;

namespace WindowsConductor.Client;

/// <summary>
/// Lazy reference to one or more UIAutomation elements, inspired by
/// Playwright's <c>ILocator</c> interface.
///
/// The selector is not resolved until an action or query method is called.
/// Each call re-queries the Driver, so the locator always reflects the
/// current state of the UI.
/// </summary>
public sealed class WinAppLocator
{
    private readonly string _appId;
    private readonly string _selector;
    private readonly WinAppConnection _conn;

    internal WinAppLocator(string appId, string selector, WinAppConnection conn)
    {
        _appId    = appId;
        _selector = selector;
        _conn     = conn;
    }

    // ── Chaining ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a new locator scoped to elements matching <paramref name="selector"/>
    /// that are descendants of the current locator's match.
    /// </summary>
    public WinAppLocator Locator(string selector) =>
        new(_appId, _selector + " >> " + selector, _conn);

    // ── Element resolution ───────────────────────────────────────────────────

    /// <summary>Resolves and returns the first matching element.</summary>
    public async Task<WinAppElement> GetElementAsync(CancellationToken ct = default)
    {
        var result = await _conn.SendAsync(
            "findElement", new { appId = _appId, selector = _selector }, ct);

        string? elementId = result.GetString();
        if (elementId is null)
            throw new WinAppException($"No element found for selector: '{_selector}'");

        return new WinAppElement(elementId, _conn);
    }

    /// <summary>Resolves and returns all matching elements.</summary>
    public async Task<IReadOnlyList<WinAppElement>> GetAllElementsAsync(CancellationToken ct = default)
    {
        var result = await _conn.SendAsync(
            "findElements", new { appId = _appId, selector = _selector }, ct);

        return result.EnumerateArray()
            .Select(e => new WinAppElement(e.GetString()!, _conn))
            .ToList();
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    /// <summary>Clicks the first matching element.</summary>
    public async Task ClickAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.ClickAsync(ct);
    }

    /// <summary>Double-clicks the first matching element.</summary>
    public async Task DoubleClickAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.DoubleClickAsync(ct);
    }

    /// <summary>
    /// Focuses the first matching element and types <paramref name="text"/>
    /// using keyboard simulation.
    /// </summary>
    public async Task TypeAsync(string text, CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.TypeAsync(text, ct);
    }

    /// <summary>Sets keyboard focus on the first matching element.</summary>
    public async Task FocusAsync(CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        await el.FocusAsync(ct);
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

    /// <summary>Captures a screenshot of the first matching element. Returns the saved file path.</summary>
    public async Task<string> ScreenshotAsync(string? path = null, CancellationToken ct = default)
    {
        var el = await GetElementAsync(ct);
        return await el.ScreenshotAsync(path, ct);
    }

    public override string ToString() => $"WinAppLocator({_selector})";
}
