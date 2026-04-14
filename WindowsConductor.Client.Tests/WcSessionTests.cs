using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

// TODO: Fix race condition — WcSession.DisposeAsync performs a full WebSocket
// close handshake (sends close frame, waits for server response). When the
// in-process test server has stopped reading, this blocks until the CTS timeout
// fires, causing every test to take 10+ seconds. Needs either a non-blocking
// shutdown path in WcSession or a test harness that keeps the server-side read
// loop alive through disposal.
[TestFixture]
[Category("Unit")]
#pragma warning disable CA1001 // NUnit TearDown handles disposal
public class WcSessionTests
#pragma warning restore CA1001
{
    private HttpListener _listener = null!;
    private string _wsUrl = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        var tmp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tmp.Start();
        int port = ((IPEndPoint)tmp.LocalEndpoint).Port;
        tmp.Stop();

        var url = $"http://localhost:{port}/";
        _wsUrl = $"ws://localhost:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(url);
        _listener.Start();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    }

    [TearDown]
    public void TearDown()
    {
        _cts.Cancel();
        _cts.Dispose();
        _listener.Stop();
        _listener.Close();
    }

    private async Task<WebSocket> AcceptClientAsync()
    {
        var ctx = await _listener.GetContextAsync();
        var wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null);
        return wsCtx.WebSocket;
    }

    private static async Task<JsonDocument> ReceiveJsonAsync(WebSocket ws, CancellationToken ct)
    {
        var buf = new byte[64 * 1024];
        var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buf, ct);
            ms.Write(buf, 0, result.Count);
        } while (!result.EndOfMessage);
        return JsonDocument.Parse(Encoding.UTF8.GetString(ms.ToArray()));
    }

    private static async Task SendJsonAsync(WebSocket ws, object payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    /// <summary>
    /// Tears down both sides without blocking on close handshake.
    /// Abort kills the server socket immediately; the client receive loop
    /// catches the error and exits, so DisposeAsync completes fast.
    /// </summary>
    private static async Task CleanupAsync(WebSocket serverWs, WcSession session)
    {
        serverWs.Abort();
        await session.DisposeAsync();
    }

    // ── ConnectAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task ConnectAsync_EstablishesConnection()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        Assert.That(serverWs.State, Is.EqualTo(WebSocketState.Open));
        await CleanupAsync(serverWs, session);
    }

    // ── SendAsync round-trip ─────────────────────────────────────────────────

    [Test]
    public async Task SendAsync_SendsCommandAndReceivesResult()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var sendTask = session.SendAsync("getText", new { elementId = "el-1" }, _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        Assert.That(reqDoc.RootElement.GetProperty("command").GetString(), Is.EqualTo("getText"));

        await SendJsonAsync(serverWs, new { id, success = true, result = "Hello" }, _cts.Token);
        var result = await sendTask;
        Assert.That(result.GetString(), Is.EqualTo("Hello"));

        await CleanupAsync(serverWs, session);
    }

    [Test]
    public async Task SendAsync_BoolResult_RoundTrips()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var sendTask = session.SendAsync("isEnabled", new { elementId = "el-1" }, _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        await SendJsonAsync(serverWs, new { id, success = true, result = true }, _cts.Token);

        Assert.That((await sendTask).ValueKind, Is.EqualTo(JsonValueKind.True));
        await CleanupAsync(serverWs, session);
    }

    [Test]
    public async Task SendAsync_ArrayResult_RoundTrips()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var sendTask = session.SendAsync("findElements",
            new { appId = "a1", selector = "type=Button" }, _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        await SendJsonAsync(serverWs,
            new { id, success = true, result = new[] { "el-1", "el-2" } }, _cts.Token);

        var items = (await sendTask).EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.That(items, Is.EqualTo(new[] { "el-1", "el-2" }));
        await CleanupAsync(serverWs, session);
    }

    // ── Error handling ───────────────────────────────────────────────────────

    [Test]
    public async Task SendAsync_DriverError_ThrowsWcException()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var sendTask = session.SendAsync("click", new { elementId = "bad" }, _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        await SendJsonAsync(serverWs,
            new { id, success = false, error = "Element not found" }, _cts.Token);

        var ex = Assert.ThrowsAsync<WcException>(async () => await sendTask);
        Assert.That(ex!.Message, Is.EqualTo("Element not found"));
        await CleanupAsync(serverWs, session);
    }

    [Test]
    public async Task SendAsync_DriverErrorNoMessage_ThrowsGenericMessage()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var sendTask = session.SendAsync("click", new { elementId = "x" }, _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        await SendJsonAsync(serverWs,
            new { id, success = false, error = (string?)null }, _cts.Token);

        var ex = Assert.ThrowsAsync<WcException>(async () => await sendTask);
        Assert.That(ex!.Message, Does.Contain("unknown error"));
        await CleanupAsync(serverWs, session);
    }

    // ── Multiple concurrent requests ─────────────────────────────────────────

    [Test]
    public async Task SendAsync_MultipleConcurrent_CorrelatesById()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var task1 = session.SendAsync("getText", new { elementId = "e1" }, _cts.Token);
        var task2 = session.SendAsync("getText", new { elementId = "e2" }, _cts.Token);

        using var req1 = await ReceiveJsonAsync(serverWs, _cts.Token);
        using var req2 = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id1 = req1.RootElement.GetProperty("id").GetString()!;
        var id2 = req2.RootElement.GetProperty("id").GetString()!;

        // Respond in reverse order
        await SendJsonAsync(serverWs, new { id = id2, success = true, result = "second" }, _cts.Token);
        await SendJsonAsync(serverWs, new { id = id1, success = true, result = "first" }, _cts.Token);

        Assert.That((await task1).GetString(), Is.EqualTo("first"));
        Assert.That((await task2).GetString(), Is.EqualTo("second"));
        await CleanupAsync(serverWs, session);
    }

    // ── LaunchAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task LaunchAsync_ReturnsWcApp()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var launchTask = session.LaunchAsync("calc.exe", ct: _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        Assert.That(reqDoc.RootElement.GetProperty("command").GetString(), Is.EqualTo("launch"));

        await SendJsonAsync(serverWs, new { id, success = true, result = "app-99" }, _cts.Token);
        Assert.That((await launchTask).AppId, Is.EqualTo("app-99"));
        await CleanupAsync(serverWs, session);
    }

    [Test]
    public async Task LaunchAsync_NullResult_Throws()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var launchTask = session.LaunchAsync("bad.exe", ct: _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        await SendJsonAsync(serverWs, new { id, success = true, result = (string?)null }, _cts.Token);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await launchTask);
        await CleanupAsync(serverWs, session);
    }

    [Test]
    public async Task LaunchAsync_SendsAllParameters()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var launchTask = session.LaunchAsync(
            "app.exe",
            args: new[] { "--flag" },
            detachedTitleRegex: "MyApp.*",
            mainWindowTimeout: 5000,
            ct: _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        var p = reqDoc.RootElement.GetProperty("params");

        Assert.That(p.GetProperty("path").GetString(), Is.EqualTo("app.exe"));
        Assert.That(p.GetProperty("args").EnumerateArray().First().GetString(), Is.EqualTo("--flag"));
        Assert.That(p.GetProperty("detachedTitleRegex").GetString(), Is.EqualTo("MyApp.*"));
        Assert.That(p.GetProperty("mainWindowTimeout").GetUInt32(), Is.EqualTo(5000));

        await SendJsonAsync(serverWs, new { id, success = true, result = "app-1" }, _cts.Token);
        await launchTask;
        await CleanupAsync(serverWs, session);
    }

    // ── AttachAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task AttachAsync_ReturnsWcApp_WithOwnsAppFalse()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var attachTask = session.AttachAsync("Calc.*", ct: _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        Assert.That(reqDoc.RootElement.GetProperty("command").GetString(), Is.EqualTo("attach"));

        await SendJsonAsync(serverWs, new { id, success = true, result = "app-55" }, _cts.Token);
        var app = await attachTask;
        Assert.That(app.AppId, Is.EqualTo("app-55"));
        Assert.That(app.OwnsApp, Is.False);
        await CleanupAsync(serverWs, session);
    }

    [Test]
    public async Task AttachAsync_SendsAllParameters()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var attachTask = session.AttachAsync("MyApp.*", mainWindowTimeout: 3000, ct: _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        var p = reqDoc.RootElement.GetProperty("params");

        Assert.That(p.GetProperty("mainWindowTitleRegex").GetString(), Is.EqualTo("MyApp.*"));
        Assert.That(p.GetProperty("mainWindowTimeout").GetUInt32(), Is.EqualTo(3000));

        await SendJsonAsync(serverWs, new { id, success = true, result = "app-1" }, _cts.Token);
        await attachTask;
        await CleanupAsync(serverWs, session);
    }

    [Test]
    public async Task AttachAsync_NullResult_Throws()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var attachTask = session.AttachAsync("Bad.*", ct: _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        await SendJsonAsync(serverWs, new { id, success = true, result = (string?)null }, _cts.Token);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await attachTask);
        await CleanupAsync(serverWs, session);
    }

    [Test]
    public async Task LaunchAsync_ReturnsWcApp_WithOwnsAppTrue()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        var launchTask = session.LaunchAsync("calc.exe", ct: _cts.Token);

        using var reqDoc = await ReceiveJsonAsync(serverWs, _cts.Token);
        var id = reqDoc.RootElement.GetProperty("id").GetString()!;
        await SendJsonAsync(serverWs, new { id, success = true, result = "app-1" }, _cts.Token);

        var app = await launchTask;
        Assert.That(app.OwnsApp, Is.True);
        await CleanupAsync(serverWs, session);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Test]
    public async Task SendAsync_Cancelled_ThrowsOperationCancelled()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        using var localCts = new CancellationTokenSource();
        var sendTask = session.SendAsync("click", new { elementId = "e1" }, localCts.Token);

        localCts.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(async () => await sendTask);
        await CleanupAsync(serverWs, session);
    }

    // ── DisposeAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task DisposeAsync_ClosesWebSocket()
    {
        var acceptTask = AcceptClientAsync();
        var session = await WcSession.ConnectAsync(_wsUrl, _cts.Token);
        var serverWs = await acceptTask;

        // Start reading on server side before client disposes, so the close frame is received
        var receiveTask = serverWs.ReceiveAsync(new byte[1024], _cts.Token);

        await session.DisposeAsync();

        var result = await receiveTask;
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
    }
}
