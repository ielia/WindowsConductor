namespace WindowsConductor.Client;

/// <summary>
/// Represents a running Windows application managed by the Driver.
/// Analogous to <c>IBrowser</c> in Playwright; windows/pages are accessed
/// through <see cref="WcLocator"/>s.
/// </summary>
public sealed class WcApp : IAsyncDisposable
{
    internal string AppId { get; }
    internal WcSession Connection { get; }

    internal WcApp(string appId, WcSession connection)
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
    public WcLocator Locator(string selector) =>
        new(AppId, selector, Connection);

    /// <summary>Finds elements by their <c>AutomationId</c> property.</summary>
    public WcLocator GetByAutomationId(string automationId) =>
        Locator($"[automationid={automationId.Replace("]", "\\]")}]");

    /// <summary>Finds elements by their <c>Name</c> property.</summary>
    public WcLocator GetByName(string name) =>
        Locator($"[name={name.Replace("]", "\\]")}]");

    /// <summary>Finds elements whose <c>Name</c> property equals <paramref name="text"/>.</summary>
    public WcLocator GetByText(string text) =>
        Locator($"text={text.Replace("]", "\\]")}");

    /// <summary>
    /// Finds elements using an XPath expression evaluated over the UIAutomation tree.
    ///
    /// Attribute names in predicates: <c>AutomationId</c>, <c>Name</c>,
    /// <c>ClassName</c>, <c>ControlType</c>.
    /// </summary>
    public WcLocator GetByXPath(string xpath)
    {
        // Ensure the expression starts with a slash so the driver recognises it.
        string normalised = xpath.StartsWith('/') ? xpath : $"//{xpath}";
        return Locator(normalised);
    }

    /// <summary>Finds elements by UIAutomation <c>ControlType</c> name (e.g. "Button").</summary>
    public WcLocator GetByControlType(string controlType) =>
        Locator($"type={controlType}");

    // ── Window-level queries ─────────────────────────────────────────────────

    /// <summary>Returns the title of the application's main window.</summary>
    public async Task<string> GetTitleAsync(CancellationToken ct = default)
    {
        var result = await Connection.SendAsync("getWindowTitle", new { appId = AppId }, ct);
        return result.GetString() ?? "";
    }

    // ── Screenshots ────────────────────────────────────────────────────────

    /// <summary>Captures a screenshot of the app's main window. Returns the saved file path.</summary>
    public async Task<string> ScreenshotAsync(string? path = null, CancellationToken ct = default)
    {
        var r = await Connection.SendAsync("screenshotApp",
            new { appId = AppId, path = path ?? "" }, ct);
        return r.GetString() ?? "";
    }

    // ── Video recording ──────────────────────────────────────────────────────

    /// <summary>Starts video recording of the app window. Returns the video file path.</summary>
    public async Task<string> StartRecordingAsync(string? path = null, string? ffmpegPath = null, CancellationToken ct = default)
    {
        var r = await Connection.SendAsync("startRecording",
            new { appId = AppId, path = path ?? "", ffmpegPath = ffmpegPath ?? "" }, ct);
        return r.GetString() ?? "";
    }

    /// <summary>Stops video recording. Returns the video file path.</summary>
    public async Task<string> StopRecordingAsync(CancellationToken ct = default)
    {
        var r = await Connection.SendAsync("stopRecording", new { appId = AppId }, ct);
        return r.GetString() ?? "";
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

/// <summary>Documents the selector syntax supported by <see cref="WcLocator"/>.</summary>
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
