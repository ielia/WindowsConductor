using System.Collections.Concurrent;
using WindowsConductor.Client;

namespace WindowsConductor.MCP;

public sealed class ConductorState : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, WcApp> _apps = new();

    public string VideoDir { get; set; } =
        Path.Combine(Path.GetTempPath(), "WindowsConductor", "recordings");

    public string ScreenshotDir { get; set; } =
        Path.Combine(Path.GetTempPath(), "WindowsConductor", "screenshots");

    public WcSession? Session { get; private set; }

    internal IWcTransport? Transport => Session;

    public async Task<WcSession> ConnectAsync(
        string wsUri = WcDefaults.WebSocketUrl,
        string? authToken = null,
        CancellationToken ct = default)
    {
        if (Session is not null)
            throw new InvalidOperationException("Already connected. Disconnect first.");

        Session = await WcSession.ConnectAsync(wsUri, authToken, ct);
        return Session;
    }

    public WcSession RequireSession() =>
        Session ?? throw new InvalidOperationException(
            "Not connected to a WindowsConductor driver. Call the 'connect' tool first.");

    internal IWcTransport RequireTransport() =>
        Transport ?? throw new InvalidOperationException(
            "Not connected to a WindowsConductor driver. Call the 'connect' tool first.");

    public void TrackApp(string appId, WcApp app) => _apps[appId] = app;

    public WcApp GetApp(string appId) =>
        _apps.TryGetValue(appId, out var app)
            ? app
            : throw new InvalidOperationException(
                $"No tracked application with id '{appId}'. Launch or attach first.");

    public bool TryRemoveApp(string appId) => _apps.TryRemove(appId, out _);

    public IReadOnlyCollection<string> AppIds => _apps.Keys.ToArray();

    public string ResolveVideoPath(string? relativePath) =>
        ResolvePath(VideoDir, relativePath, "recording", ".mp4", "video");

    public string ResolveScreenshotPath(string? relativePath) =>
        ResolvePath(ScreenshotDir, relativePath, "screenshot", ".png", "screenshot");

    internal static string ResolvePath(
        string rootDir, string? relativePath, string defaultPrefix, string defaultExtension, string label)
    {
        string fullPath;
        if (relativePath is not null)
        {
            var combined = Path.Combine(rootDir, relativePath);
            fullPath = Path.GetFullPath(combined);
        }
        else
        {
            var fileName = $"{defaultPrefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}{defaultExtension}";
            fullPath = Path.GetFullPath(Path.Combine(rootDir, fileName));
        }

        var rootFull = Path.GetFullPath(rootDir + Path.DirectorySeparatorChar);
        if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Output path '{relativePath}' escapes the {label} root directory.");

        return fullPath;
    }

    internal void SetTransportForTesting(IWcTransport transport) =>
        _testTransport = transport;

    private IWcTransport? _testTransport;

    internal IWcTransport ResolveTransport() =>
        _testTransport ?? RequireTransport();

    public async ValueTask DisposeAsync()
    {
        foreach (var app in _apps.Values)
        {
            try { await app.DisposeAsync(); }
            catch { /* best-effort cleanup */ }
        }
        _apps.Clear();

        if (Session is not null)
        {
            await Session.DisposeAsync();
            Session = null;
        }
    }
}
