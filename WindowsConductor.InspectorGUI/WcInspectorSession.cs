using System.Diagnostics.CodeAnalysis;
using WindowsConductor.Client;

namespace WindowsConductor.InspectorGUI;

[ExcludeFromCodeCoverage]
internal sealed class WcInspectorSession : IInspectorSession, IAsyncDisposable
{
    private WcSession? _session;
    private WcApp? _app;
    private WcElement? _selectedElement;
    private IReadOnlyList<WcElement>? _matchedElements;

    public bool IsConnected => _session is not null;
    public bool HasApp => _app is not null;
    public bool HasSelectedElement => _selectedElement is not null;
    public bool AllowSelfSignedCerts { get; set; } = true;
    public string? ServerVersion => _session?.ServerVersion;

    public async Task ConnectAsync(string url, string? authToken = null, CancellationToken ct = default)
    {
        if (_session is not null)
            await DisconnectAsync();
        _session = await WcSession.ConnectAsync(url, authToken, AllowSelfSignedCerts, ct);
    }

    public async Task LaunchAsync(string path, string[] args, string? detachedTitleRegex, uint? mainWindowTimeout, CancellationToken ct = default)
    {
        _selectedElement = null;
        if (_app is not null)
            await _app.DisposeAsync();
        _app = await _session!.LaunchAsync(path, args, detachedTitleRegex, mainWindowTimeout, ct);
    }

    public async Task AttachAsync(string mainWindowTitleRegex, uint? mainWindowTimeout, CancellationToken ct = default)
    {
        _selectedElement = null;
        if (_app is not null)
            await _app.DisposeAsync();
        _app = await _session!.AttachAsync(mainWindowTitleRegex, mainWindowTimeout, ct);
    }

    public async Task CloseAppAsync(CancellationToken ct = default)
    {
        if (_app is null) return;
        _selectedElement = null;
        await _app.CloseAsync(ct);
        _app = null;
    }

    public Task DetachAppAsync()
    {
        _selectedElement = null;
        _app = null;
        return Task.CompletedTask;
    }

    public async Task<byte[]> WindowScreenshotAsync(CancellationToken ct = default) =>
        await _app!.ScreenshotBytesAsync(ct);

    public async Task<BoundingRect> GetWindowBoundingRectAsync(CancellationToken ct = default)
    {
        var result = await _session!.SendAsync("getWindowBoundingRect", new { appId = _app!.AppId }, ct);
        return new BoundingRect(
            result.GetProperty("x").GetDouble(),
            result.GetProperty("y").GetDouble(),
            result.GetProperty("width").GetDouble(),
            result.GetProperty("height").GetDouble());
    }

    public async Task<byte[]> ElementWindowScreenshotAsync(CancellationToken ct = default)
    {
        var window = await _selectedElement!.TopLevelWindowAsync(ct) ?? _selectedElement;
        return await window.ScreenshotBytesAsync(ct);
    }

    public async Task<BoundingRect> GetElementWindowBoundingRectAsync(CancellationToken ct = default)
    {
        var window = await _selectedElement!.TopLevelWindowAsync(ct) ?? _selectedElement;
        return await window.GetBoundingRectAsync(ct);
    }

    public async Task<string> LocateAsync(string[] selectors, CancellationToken ct = default)
    {
        WcLocator locator = _app!.Locator(selectors[0]);
        for (int i = 1; i < selectors.Length; i++)
            locator = locator.Locator(selectors[i]);

        var element = await locator.GetElementAsync(ct);
        _selectedElement = element;
        return element.ElementId;
    }

    public async Task<string> LocateFromElementAsync(string[] selectors, CancellationToken ct = default)
    {
        WcLocator locator = _selectedElement!.Locator(selectors[0]);
        for (int i = 1; i < selectors.Length; i++)
            locator = locator.Locator(selectors[i]);

        var element = await locator.GetElementAsync(ct);
        _selectedElement = element;
        return element.ElementId;
    }

    public async Task<int> LocateAllAsync(string[] selectors, CancellationToken ct = default)
    {
        WcLocator locator = _app!.Locator(selectors[0]);
        for (int i = 1; i < selectors.Length; i++)
            locator = locator.Locator(selectors[i]);

        var elements = await locator.GetAllElementsAsync(ct);
        if (elements.Count > 0)
        {
            _matchedElements = elements;
            _selectedElement = elements[0];
        }
        return elements.Count;
    }

    public async Task<int> LocateAllFromElementAsync(string[] selectors, CancellationToken ct = default)
    {
        WcLocator locator = _selectedElement!.Locator(selectors[0]);
        for (int i = 1; i < selectors.Length; i++)
            locator = locator.Locator(selectors[i]);

        var elements = await locator.GetAllElementsAsync(ct);
        if (elements.Count > 0)
        {
            _matchedElements = elements;
            _selectedElement = elements[0];
        }
        return elements.Count;
    }

    public Task<string> SelectMatchAsync(int index, CancellationToken ct = default)
    {
        if (_matchedElements is null || index < 0 || index >= _matchedElements.Count)
            throw new InvalidOperationException("No matches to select from.");
        _selectedElement = _matchedElements[index];
        return Task.FromResult(_selectedElement.ElementId);
    }

    public IReadOnlyList<WcElement>? MatchedElements => _matchedElements;

    public void RestoreElements(IReadOnlyList<WcElement> elements, int index)
    {
        _matchedElements = elements;
        _selectedElement = elements[index];
    }

    public void Unselect()
    {
        _selectedElement = null;
        _matchedElements = null;
    }

    public async Task<string?> ParentAsync(CancellationToken ct = default)
    {
        var parent = await _selectedElement!.ParentAsync(ct);
        if (parent is null) return null;
        _selectedElement = parent;
        _matchedElements = [parent];
        return parent.ElementId;
    }

    public async Task<bool> IsSelectedElementRootAsync(CancellationToken ct = default)
    {
        var parent = await _selectedElement!.ParentAsync(ct);
        return parent is null;
    }

    public async Task<WcValue> ResolveValueAsync(string selector, CancellationToken ct = default)
    {
        var locator = _app!.Locator(selector);
        return await locator.GetResolvedValueAsync(ct);
    }

    public async Task<WcValue> ResolveValueFromElementAsync(string selector, CancellationToken ct = default)
    {
        var locator = _selectedElement!.Locator(selector);
        return await locator.GetResolvedValueAsync(ct);
    }

    public async Task<string> GetAttributeAsync(string attributeName, CancellationToken ct = default) =>
        await _selectedElement!.GetAttributeAsync(attributeName, ct);

    public async Task<Dictionary<string, object?>> GetAttributesAsync(CancellationToken ct = default) =>
        await _selectedElement!.GetAttributesAsync(ct);

    public async Task SetAttributeAsync(string attributeName, string value, CancellationToken ct = default) =>
        await _selectedElement!.SetAttributeAsync(attributeName, value, ct);

    public async Task ClickAsync(CancellationToken ct = default) =>
        await _selectedElement!.ClickAsync(ct);

    public async Task ClickAsync(Anchor anchor, System.Drawing.Point offset, CancellationToken ct = default) =>
        await _selectedElement!.ClickAsync(anchor, offset, ct);

    public async Task DoubleClickAsync(CancellationToken ct = default) =>
        await _selectedElement!.DoubleClickAsync(ct);

    public async Task DoubleClickAsync(Anchor anchor, System.Drawing.Point offset, CancellationToken ct = default) =>
        await _selectedElement!.DoubleClickAsync(anchor, offset, ct);

    public async Task RightClickAsync(CancellationToken ct = default) =>
        await _selectedElement!.RightClickAsync(ct);

    public async Task RightClickAsync(Anchor anchor, System.Drawing.Point offset, CancellationToken ct = default) =>
        await _selectedElement!.RightClickAsync(anchor, offset, ct);

    public async Task HoverAsync(CancellationToken ct = default) =>
        await _selectedElement!.HoverAsync(ct);

    public async Task HoverAsync(Anchor anchor, System.Drawing.Point offset, CancellationToken ct = default) =>
        await _selectedElement!.HoverAsync(anchor, offset, ct);

    public async Task DragToAsync(string[] targetSelectors, Anchor fromAnchor, System.Drawing.Point fromOffset, Anchor toAnchor, System.Drawing.Point toOffset, CancellationToken ct = default)
    {
        WcLocator locator = _app!.Locator(targetSelectors[0]);
        for (int i = 1; i < targetSelectors.Length; i++)
            locator = locator.Locator(targetSelectors[i]);
        var target = await locator.GetElementAsync(ct);
        await _selectedElement!.DragToAsync(fromAnchor, fromOffset, target, toAnchor, toOffset, ct);
    }

    public async Task ScrollAsync(double lines, bool horizontal = false, CancellationToken ct = default) =>
        await _selectedElement!.ScrollAsync(lines, horizontal, ct);

    public async Task HitKeysAsync(Key[] keys, CancellationToken ct = default) =>
        await _selectedElement!.HitKeysAsync(keys, ct);

    public async Task TypeAsync(string text, KeyModifiers modifiers = KeyModifiers.None, CancellationToken ct = default) =>
        await _selectedElement!.TypeAsync(text, modifiers, ct);

    public async Task GlobalHitKeysAsync(Key[] keys, CancellationToken ct = default) =>
        await _session!.GlobalHitKeysAsync(keys, ct);

    public async Task GlobalTypeAsync(string text, KeyModifiers modifiers = KeyModifiers.None, CancellationToken ct = default) =>
        await _session!.GlobalTypeAsync(text, modifiers, ct);

    public async Task FocusAsync(CancellationToken ct = default) =>
        await _selectedElement!.FocusAsync(ct);

    public async Task SetForegroundAsync(CancellationToken ct = default) =>
        await _selectedElement!.SetForegroundAsync(ct);

    public async Task<WcWindowState> GetWindowStateAsync(CancellationToken ct = default) =>
        await _selectedElement!.GetWindowStateAsync(ct);

    public async Task SetWindowStateAsync(WcWindowState state, CancellationToken ct = default) =>
        await _selectedElement!.SetWindowStateAsync(state, ct);

    public async Task<string> GetTextAsync(CancellationToken ct = default) =>
        await _selectedElement!.GetTextAsync(ct);

    public async Task<byte[]> ScreenshotElementAsync(CancellationToken ct = default) =>
        await _selectedElement!.ScreenshotBytesAsync(ct);

    public async Task<BoundingRect> GetElementBoundingRectAsync(CancellationToken ct = default) =>
        await _selectedElement!.GetBoundingRectAsync(ct);

    public async Task<WcElementOcrResult> GetOcrTextAsync(CancellationToken ct = default) =>
        await _selectedElement!.GetOcrTextAsync(ct);

    public async Task<IReadOnlyList<WcElement>> GetChildrenAsync(CancellationToken ct = default) =>
        await _selectedElement!.ChildrenAsync(ct);

    public async Task<IReadOnlyTreeNode<WcElement>> GetDescendantsAsync(CancellationToken ct = default) =>
        await _selectedElement!.DescendantsAsync(ct);

    public async Task<BoundingRect[]> GetAllWindowBoundingRectsAsync(CancellationToken ct = default)
    {
        var mainRectTask = SafeBoundingRectAsync(GetWindowBoundingRectAsync(ct));
        var subWindowsTask = SafeSubWindowRectsAsync(ct);
        var elWinRectTask = _selectedElement is not null
            ? SafeBoundingRectAsync(GetElementWindowBoundingRectAsync(ct))
            : Task.FromResult<BoundingRect?>(null);
        var elRectTask = _selectedElement is not null
            ? SafeBoundingRectAsync(_selectedElement.GetBoundingRectAsync(ct))
            : Task.FromResult<BoundingRect?>(null);

        await Task.WhenAll(mainRectTask, subWindowsTask, elWinRectTask, elRectTask);

        var rects = new List<BoundingRect>();
        if (mainRectTask.Result is { } mainRect) rects.Add(mainRect);
        rects.AddRange(subWindowsTask.Result);
        if (elWinRectTask.Result is { } elWinRect) rects.Add(elWinRect);
        if (elRectTask.Result is { } elRect) rects.Add(elRect);
        return rects.ToArray();
    }

    private static async Task<BoundingRect?> SafeBoundingRectAsync(Task<BoundingRect> task)
    {
        try
        {
            var r = await task;
            return r.Width > 0 && r.Height > 0 ? r : null;
        }
        catch { return null; }
    }

    private async Task<List<BoundingRect>> SafeSubWindowRectsAsync(CancellationToken ct)
    {
        var rects = new List<BoundingRect>();
        try
        {
            var resolved = await _app!.Locator(".//Window/@boundingrectangle").GetResolvedValueAsync(ct);
            foreach (var item in resolved.GetAsList())
            {
                var rect = item.GetAsRectangle();
                if (rect is { Width: > 0, Height: > 0 })
                    rects.Add(new BoundingRect(rect.Value.X, rect.Value.Y, rect.Value.Width, rect.Value.Height));
            }
        }
        catch { /* skip if //Window locator fails */ }
        return rects;
    }

    public async Task<DesktopScreenshotResult> DesktopScreenshotWithOriginAsync(CancellationToken ct = default) =>
        await _session!.DesktopScreenshotWithOriginAsync(ct);

    public async Task DisconnectAsync()
    {
        _selectedElement = null;
        if (_app is not null)
        {
            await _app.DisposeAsync();
            _app = null;
        }
        if (_session is not null)
        {
            await _session.DisposeAsync();
            _session = null;
        }
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
