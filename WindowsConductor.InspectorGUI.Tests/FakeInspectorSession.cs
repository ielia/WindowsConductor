using WindowsConductor.Client;
using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

internal sealed class FakeInspectorSession : IInspectorSession
{
    public record Call(string Method, object?[] Args);
    public List<Call> Calls { get; } = new();

    public bool IsConnected { get; set; }
    public bool HasApp { get; set; }
    public bool HasSelectedElement { get; set; }

    // Configurable return values
    public byte[] WindowScreenshotResult { get; set; } = [0x89, 0x50, 0x4E, 0x47];
    public BoundingRect WindowBoundingRectResult { get; set; } = new(0, 0, 800, 600);
    public string LocateResult { get; set; } = "el-1";
    public string GetAttributeResult { get; set; } = "value-1";
    public Dictionary<string, object?> GetAttributesResult { get; set; } = new() { ["name"] = "OK", ["controltype"] = "Button" };
    public string GetTextResult { get; set; } = "Hello";
    public byte[] ScreenshotElementResult { get; set; } = [0x89, 0x50, 0x4E, 0x47];
    public BoundingRect ElementBoundingRectResult { get; set; } = new(100, 200, 50, 30);

    public Exception? ThrowOnNext { get; set; }

    private void Record(string method, params object?[] args)
    {
        if (ThrowOnNext is { } ex) { ThrowOnNext = null; throw ex; }
        Calls.Add(new Call(method, args));
    }

    public Task ConnectAsync(string url, CancellationToken ct = default)
    {
        Record("Connect", url);
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task LaunchAsync(string path, string[] args, string? detachedTitleRegex, uint? mainWindowTimeout, CancellationToken ct = default)
    {
        Record("Launch", path, args, detachedTitleRegex, mainWindowTimeout);
        HasApp = true;
        return Task.CompletedTask;
    }

    public Task AttachAsync(string mainWindowTitleRegex, uint? mainWindowTimeout, CancellationToken ct = default)
    {
        Record("Attach", mainWindowTitleRegex, mainWindowTimeout);
        HasApp = true;
        return Task.CompletedTask;
    }

    public Task CloseAppAsync(CancellationToken ct = default)
    {
        Record("CloseApp");
        HasApp = false;
        HasSelectedElement = false;
        return Task.CompletedTask;
    }

    public Task DetachAppAsync()
    {
        Record("DetachApp");
        HasApp = false;
        HasSelectedElement = false;
        return Task.CompletedTask;
    }

    public Task<byte[]> WindowScreenshotAsync(CancellationToken ct = default)
    {
        Record("WindowScreenshot");
        return Task.FromResult(WindowScreenshotResult);
    }

    public Task<BoundingRect> GetWindowBoundingRectAsync(CancellationToken ct = default)
    {
        Record("GetWindowBoundingRect");
        return Task.FromResult(WindowBoundingRectResult);
    }

    public Task<string> LocateAsync(string[] selectors, CancellationToken ct = default)
    {
        Record("Locate", (object)selectors);
        HasSelectedElement = true;
        return Task.FromResult(LocateResult);
    }

    public void Unselect()
    {
        Record("Unselect");
        HasSelectedElement = false;
    }

    public Task<string> GetAttributeAsync(string attributeName, CancellationToken ct = default)
    {
        Record("GetAttribute", attributeName);
        return Task.FromResult(GetAttributeResult);
    }

    public Task<Dictionary<string, object?>> GetAttributesAsync(CancellationToken ct = default)
    {
        Record("GetAttributes");
        return Task.FromResult(GetAttributesResult);
    }

    public Task ClickAsync(CancellationToken ct = default)
    {
        Record("Click");
        return Task.CompletedTask;
    }

    public Task DoubleClickAsync(CancellationToken ct = default)
    {
        Record("DoubleClick");
        return Task.CompletedTask;
    }

    public Task RightClickAsync(CancellationToken ct = default)
    {
        Record("RightClick");
        return Task.CompletedTask;
    }

    public Task TypeAsync(string text, CancellationToken ct = default)
    {
        Record("Type", text);
        return Task.CompletedTask;
    }

    public Task FocusAsync(CancellationToken ct = default)
    {
        Record("Focus");
        return Task.CompletedTask;
    }

    public Task<string> GetTextAsync(CancellationToken ct = default)
    {
        Record("GetText");
        return Task.FromResult(GetTextResult);
    }

    public Task<byte[]> ScreenshotElementAsync(CancellationToken ct = default)
    {
        Record("ScreenshotElement");
        return Task.FromResult(ScreenshotElementResult);
    }

    public Task<BoundingRect> GetElementBoundingRectAsync(CancellationToken ct = default)
    {
        Record("GetElementBoundingRect");
        return Task.FromResult(ElementBoundingRectResult);
    }

    public Task DisconnectAsync()
    {
        Record("Disconnect");
        IsConnected = false;
        HasApp = false;
        HasSelectedElement = false;
        return Task.CompletedTask;
    }
}
