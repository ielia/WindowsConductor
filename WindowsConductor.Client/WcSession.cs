using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

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
public sealed class WcSession : IAsyncDisposable
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

    private WcSession(ClientWebSocket ws) => _ws = ws;

    /// <summary>Connects to a WcApp Driver and starts the receive loop.</summary>
    public static async Task<WcSession> ConnectAsync(
        string wsUri = "ws://localhost:8765/",
        CancellationToken ct = default)
    {
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(wsUri), ct);

        var conn = new WcSession(ws);
        _ = Task.Run(() => conn.ReceiveLoopAsync(ct), ct);
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

    // ── Internal transport ───────────────────────────────────────────────────

    /// <summary>
    /// Sends a command to the Driver and awaits the matching response.
    /// Returns the <c>result</c> field of the response.
    /// Throws <see cref="WcException"/> when the Driver reports an error.
    /// </summary>
    internal async Task<JsonElement> SendAsync(
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
            throw new WcException(response.Error ?? "Driver returned an unknown error.");

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
}

/// <summary>Thrown when the WcApp Driver returns an error response.</summary>
public sealed class WcException(string message) : Exception(message);
