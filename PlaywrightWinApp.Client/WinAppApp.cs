namespace PlaywrightWinApp.Client;

/// <summary>
/// Represents a running Windows application managed by the Driver.
/// Analogous to <c>IBrowser</c> in Playwright; windows/pages are accessed
/// through <see cref="WinAppLocator"/>s.
/// </summary>
public sealed class WinAppApp : IAsyncDisposable
{
    internal string AppId { get; }
    internal WinAppConnection Connection { get; }

    internal WinAppApp(string appId, WinAppConnection connection)
    {
        AppId = appId;
        Connection = connection;
    }

    // ── Factory methods ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns a lazy locator for elements within this application window.
    ///
    /// Selector syntax is described in <see cref="SelectorSyntax"/>.
    /// </summary>
    public WinAppLocator Locator(string selector) =>
        new(AppId, selector, Connection);

    /// <summary>Finds elements by their <c>AutomationId</c> property.</summary>
    public WinAppLocator GetByAutomationId(string automationId) =>
        Locator($"[automationid={automationId.Replace("]", "\\]")}]");

    /// <summary>Finds elements by their <c>Name</c> property.</summary>
    public WinAppLocator GetByName(string name) =>
        Locator($"[name={name.Replace("]", "\\]")}]");

    /// <summary>Finds elements whose <c>Name</c> property equals <paramref name="text"/>.</summary>
    public WinAppLocator GetByText(string text) =>
        Locator($"text={text.Replace("]", "\\]")}");

    /// <summary>
    /// Finds elements using an XPath expression evaluated over the UIAutomation tree.
    ///
    /// Attribute names in predicates: <c>AutomationId</c>, <c>Name</c>,
    /// <c>ClassName</c>, <c>ControlType</c>.
    /// </summary>
    public WinAppLocator GetByXPath(string xpath)
    {
        // Ensure the expression starts with a slash so the driver recognises it.
        string normalised = xpath.StartsWith('/') ? xpath : $"//{xpath}";
        return Locator(normalised);
    }

    /// <summary>Finds elements by UIAutomation <c>ControlType</c> name (e.g. "Button").</summary>
    public WinAppLocator GetByControlType(string controlType) =>
        Locator($"type={controlType}");

    // ── Window-level queries ─────────────────────────────────────────────────

    /// <summary>Returns the title of the application's main window.</summary>
    public async Task<string> GetTitleAsync(CancellationToken ct = default)
    {
        var result = await Connection.SendAsync("getWindowTitle", new { appId = AppId }, ct);
        return result.GetString() ?? "";
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>Closes the application.</summary>
    public async Task CloseAsync(CancellationToken ct = default) =>
        await Connection.SendAsync("close", new { appId = AppId }, ct);

    public async ValueTask DisposeAsync()
    {
        try { await CloseAsync(); }
        catch { /* ignore if already closed */ }
    }
}

/// <summary>Documents the selector syntax supported by <see cref="WinAppLocator"/>.</summary>
internal static class SelectorSyntax
{
    /*
     * Attribute selectors
     * ────────────────────
     *   [automationid=value]       → AutomationId equals value (case-insensitive)
     *   [name=value]               → Name equals value
     *   [classname=value]          → ClassName equals value
     *
     * Shorthand
     * ─────────
     *   text=value                 → same as [name=value]
     *   type=Button                → ControlType equals Button
     *
     * Compound (AND)
     * ──────────────
     *   [automationid=ok]&&type=Button
     *
     * XPath
     * ─────
     *   //Button[@AutomationId='num7Button']
     *   //Window[@Name='Calculator']//Button[@Name='7']
     *   //*[@AutomationId='CalculatorResults']
     */
}
