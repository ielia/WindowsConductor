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
        return parent.ElementId;
    }

    public async Task<bool> IsSelectedElementRootAsync(CancellationToken ct = default)
    {
        var parent = await _selectedElement!.ParentAsync(ct);
        return parent is null;
    }

    public async Task<string> GetAttributeAsync(string attributeName, CancellationToken ct = default) =>
        await _selectedElement!.GetAttributeAsync(attributeName, ct);

    public async Task<Dictionary<string, object?>> GetAttributesAsync(CancellationToken ct = default) =>
        await _selectedElement!.GetAttributesAsync(ct);

    public async Task ClickAsync(CancellationToken ct = default) =>
        await _selectedElement!.ClickAsync(ct);

    public async Task DoubleClickAsync(CancellationToken ct = default) =>
        await _selectedElement!.DoubleClickAsync(ct);

    public async Task RightClickAsync(CancellationToken ct = default) =>
        await _selectedElement!.RightClickAsync(ct);

    public async Task TypeAsync(string text, KeyModifiers modifiers = KeyModifiers.None, CancellationToken ct = default) =>
        await _selectedElement!.TypeAsync(text, modifiers, ct);

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

    internal const bool SnapshotGetDescendantsInBulk = false;

    public async Task<IReadOnlyList<WcElement>> GetChildrenAsync(CancellationToken ct = default) =>
        await _selectedElement!.ChildrenAsync(ct);

    public async Task<IReadOnlyTreeNode<WcElement>> GetDescendantsAsync(CancellationToken ct = default) =>
        await _selectedElement!.DescendantsAsync(ct);

    public async Task<byte[]> DesktopScreenshotAsync(CancellationToken ct = default) =>
        await _session!.DesktopScreenshotBytesAsync(ct);

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
