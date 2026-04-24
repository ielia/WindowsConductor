using System.Net.WebSockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using SkiaSharp;

namespace WindowsConductor.Client;

/// <summary>
/// Manages the WebSocket connection to a running WindowsConductor Driver.
///
/// Usage
/// ─────
/// <code>
/// await using var conn  = await WcSession.ConnectAsync("ws://localhost:8765/");
/// await using var calc  = await conn.LaunchAsync("calc.exe");
/// await calc.GetByAutomationId("num7Button").ClickAsync();
/// </code>
/// </summary>
public sealed class WcSession : IWcTransport, IAsyncDisposable
{
    private readonly ClientWebSocket _ws;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Pending requests indexed by their correlation ID
    private readonly Dictionary<string, TaskCompletionSource<WcResponse>> _pending = new();
    private readonly object _pendingLock = new();

    private readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string? ServerVersion { get; private set; }

    private WcSession(ClientWebSocket ws) => _ws = ws;

    /// <summary>Connects to a WcApp Driver and starts the receive loop.</summary>
    public static Task<WcSession> ConnectAsync(
        string wsUri = WcDefaults.WebSocketUrl,
        CancellationToken ct = default)
        => ConnectAsync(wsUri, null, false, ct);

    /// <summary>Connects to a WcApp Driver with an optional bearer token for authentication.</summary>
    public static Task<WcSession> ConnectAsync(
        string wsUri,
        string? authToken,
        CancellationToken ct = default)
        => ConnectAsync(wsUri, authToken, false, ct);

    /// <summary>Connects to a WcApp Driver with optional auth token and self-signed certificate support.</summary>
    public static async Task<WcSession> ConnectAsync(
        string wsUri,
        string? authToken,
        bool allowSelfSignedCerts,
        CancellationToken ct = default)
    {
        var ws = new ClientWebSocket();
        if (authToken is not null)
            ws.Options.SetRequestHeader("Authorization", $"Bearer {authToken}");
        if (allowSelfSignedCerts)
#pragma warning disable CA5359 // Intentional: user explicitly opted into allowing self-signed certificates
            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359

        try
        {
            await ws.ConnectAsync(new Uri(wsUri), ct);
        }
        catch (WebSocketException ex) when (ContainsAuthenticationException(ex))
        {
            ws.Dispose();
            throw new WcException(
                "TLS connection failed — the server certificate is not trusted. "
                + "If the server uses a self-signed certificate, set allowSelfSignedCerts to true.");
        }

        var conn = new WcSession(ws);
        _ = Task.Run(() => conn.ReceiveLoopAsync(ct), ct);
        var versionResult = await conn.SendAsync("version", new { clientVersion = WcDefaults.Version }, ct);
        conn.ServerVersion = versionResult.GetString();
        return conn;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Launches a Windows application and returns a handle for automating it.
    /// </summary>
    /// <param name="path">Executable path or name (e.g. "calc.exe").</param>
    /// <param name="args">Optional command-line arguments.</param>
    /// <param name="detachedTitleRegex">Optional detached application title regex.</param>
    /// <param name="mainWindowTimeout">Optional main window display timeout in milliseconds.</param>
    public async Task<WcApp> LaunchAsync(
        string path,
        string[]? args = null,
        string? detachedTitleRegex = null,
        uint? mainWindowTimeout = 0,
        CancellationToken ct = default)
    {
        var result = await SendAsync("launch",
            new { path, args = args ?? Array.Empty<string>(), detachedTitleRegex, mainWindowTimeout },
            ct);

        string appId = result.GetString()
            ?? throw new WcException("Driver returned no appId for 'launch'.");

        return new WcApp(appId, this);
    }

    /// <summary>
    /// Attaches to an already-running Windows application by matching its main
    /// window title. The returned <see cref="WcApp"/> will <b>not</b> close
    /// the application on dispose.
    /// </summary>
    /// <param name="mainWindowTitleRegex">Regex pattern matched against window titles.</param>
    /// <param name="mainWindowTimeout">Time in ms to wait for the window to appear (0 = driver default).</param>
    public async Task<WcApp> AttachAsync(
        string mainWindowTitleRegex,
        uint? mainWindowTimeout = 0,
        CancellationToken ct = default)
    {
        var result = await SendAsync("attach",
            new { mainWindowTitleRegex, mainWindowTimeout },
            ct);

        string appId = result.GetString()
            ?? throw new WcException("Driver returned no appId for 'attach'.");

        return new WcApp(appId, this, ownsApp: false);
    }

    // ── Desktop screenshots ─────────────────────────────────────────────────

    public async Task<byte[]> DesktopScreenshotBytesAsync(CancellationToken ct = default)
    {
        var r = await SendAsync("desktopScreenshot", null, ct);
        return r.GetBytesFromBase64();
    }

    public async Task<SKBitmap> DesktopScreenshotAsync(CancellationToken ct = default)
    {
        var bytes = await DesktopScreenshotBytesAsync(ct);
        return SKBitmap.Decode(bytes);
    }

    // ── Internal transport ───────────────────────────────────────────────────

    /// <summary>
    /// Sends a command to the Driver and awaits the matching response.
    /// Returns the <c>result</c> field of the response.
    /// Throws <see cref="WcException"/> when the Driver reports an error.
    /// </summary>
    public async Task<JsonElement> SendAsync(
        string command,
        object? @params,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<WcResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_pendingLock)
            _pending[id] = tcs;

        var req = new WcRequest { Id = id, Command = command, Params = @params };
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req, _opts));

        await _writeLock.WaitAsync(ct);
        try
        {
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        finally
        {
            _writeLock.Release();
        }

        using var reg = ct.Register(() =>
        {
            lock (_pendingLock) _pending.Remove(id);
            tcs.TrySetCanceled(ct);
        });

        var response = await tcs.Task;

        if (!response.Success)
        {
            var msg = response.Error ?? "Driver returned an unknown error.";
            throw response.ErrorType switch
            {
                nameof(NoMatchException) => new NoMatchException(msg),
                nameof(UnwantedMatchException) => new UnwantedMatchException(msg),
                nameof(LocationOutOfRangeException) => new LocationOutOfRangeException(msg),
                _ => new WcException(msg)
            };
        }

        return response.Result ?? default;
    }

    // ── Receive loop ─────────────────────────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[256 * 1024];

        while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult part;

            try
            {
                do
                {
                    part = await _ws.ReceiveAsync(buffer, ct);
                    if (part.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buffer, 0, part.Count);
                }
                while (!part.EndOfMessage);
            }
            catch (OperationCanceledException) { return; }
            catch { return; }

            WcResponse? response;
            try
            {
                response = JsonSerializer.Deserialize<WcResponse>(
                    Encoding.UTF8.GetString(ms.ToArray()), _opts);
            }
            catch { continue; }

            if (response is null) continue;

            TaskCompletionSource<WcResponse>? tcs;
            lock (_pendingLock)
            {
                _pending.TryGetValue(response.Id, out tcs);
                if (tcs is not null) _pending.Remove(response.Id);
            }

            tcs?.TrySetResult(response);
        }
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_ws.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "client closing",
                    CancellationToken.None);
            }
            catch { /* ignore close errors */ }
        }
        _ws.Dispose();
        _writeLock.Dispose();
    }

    private static bool ContainsAuthenticationException(Exception ex)
    {
        for (var current = ex.InnerException; current is not null; current = current.InnerException)
        {
            if (current is AuthenticationException)
                return true;
        }
        return false;
    }
}

/// <summary>Thrown when a wait-for-* operation times out without finding a matching element.</summary>
public sealed class NoMatchException(string message) : WcException(message);

/// <summary>Thrown when a wait-for-* operation times out and the locator still matches.</summary>
public sealed class UnwantedMatchException(string message) : WcException(message);

/// <summary>Thrown when a click target point falls outside the element's bounding rectangle.</summary>
public sealed class LocationOutOfRangeException(string message) : WcException(message);
