using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using WindowsConductor.Client;

namespace WindowsConductor.InspectorGUI;

[ExcludeFromCodeCoverage]
internal sealed class WcInspectorSession : IInspectorSession, IAsyncDisposable
{
    private WcSession? _session;
    private WcApp? _app;
    private WcElement? _selectedElement;

    public bool IsConnected => _session is not null;
    public bool HasApp => _app is not null;
    public bool HasSelectedElement => _selectedElement is not null;

    public async Task ConnectAsync(string url, CancellationToken ct = default)
    {
        if (_session is not null)
            await DisconnectAsync();
        _session = await WcSession.ConnectAsync(url, ct);
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

    public async Task<byte[]> WindowScreenshotAsync(CancellationToken ct = default)
    {
        var path = await _app!.ScreenshotAsync(ct: ct);
        return await File.ReadAllBytesAsync(path, ct);
    }

    public async Task<BoundingRect> GetWindowBoundingRectAsync(CancellationToken ct = default)
    {
        var result = await _session!.SendAsync("getWindowBoundingRect", new { appId = _app!.AppId }, ct);
        return new BoundingRect(
            result.GetProperty("x").GetDouble(),
            result.GetProperty("y").GetDouble(),
            result.GetProperty("width").GetDouble(),
            result.GetProperty("height").GetDouble());
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

    public void Unselect() => _selectedElement = null;

    public async Task<string> GetAttributeAsync(string attributeName, CancellationToken ct = default) =>
        await _selectedElement!.GetAttributeAsync(attributeName, ct);

    public async Task ClickAsync(CancellationToken ct = default) =>
        await _selectedElement!.ClickAsync(ct);

    public async Task DoubleClickAsync(CancellationToken ct = default) =>
        await _selectedElement!.DoubleClickAsync(ct);

    public async Task RightClickAsync(CancellationToken ct = default) =>
        await _selectedElement!.RightClickAsync(ct);

    public async Task TypeAsync(string text, CancellationToken ct = default) =>
        await _selectedElement!.TypeAsync(text, ct);

    public async Task FocusAsync(CancellationToken ct = default) =>
        await _selectedElement!.FocusAsync(ct);

    public async Task<string> GetTextAsync(CancellationToken ct = default) =>
        await _selectedElement!.GetTextAsync(ct);

    public async Task<byte[]> ScreenshotElementAsync(CancellationToken ct = default)
    {
        var path = await _selectedElement!.ScreenshotAsync(ct: ct);
        return await File.ReadAllBytesAsync(path, ct);
    }

    public async Task<BoundingRect> GetElementBoundingRectAsync(CancellationToken ct = default) =>
        await _selectedElement!.GetBoundingRectAsync(ct);

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
