using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace PlaywrightWinApp.DriverFlaUI;

/// <summary>
/// HTTP/WebSocket server that accepts connections from WinApp Clients.
/// Each connected client gets its own <see cref="AppManager"/> so sessions
/// are isolated from one another.
/// </summary>
public sealed class WsServer
{
    private readonly HttpListener _listener = new();
    private readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public WsServer(string prefix = "http://localhost:8765/")
    {
        _listener.Prefixes.Add(prefix);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _listener.Start();
        Console.WriteLine($"Listening on {string.Join(", ", _listener.Prefixes)}");
        Console.WriteLine("Press Ctrl+C to stop.");

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }

            if (ctx.Request.IsWebSocketRequest)
            {
                var wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null);
                _ = Task.Run(() => HandleClientAsync(wsCtx.WebSocket, ct), ct);
            }
            else
            {
                ctx.Response.StatusCode = 426; // Upgrade Required
                ctx.Response.Close();
            }
        }

        _listener.Stop();
    }

    private async Task HandleClientAsync(WebSocket ws, CancellationToken ct)
    {
        using var appManager = new AppManager();
        var buffer = new byte[256 * 1024];
        Console.WriteLine($"[+] Client connected ({ws.GetHashCode()})");

        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var ms = new MemoryStream();
                WebSocketReceiveResult wsResult;

                do
                {
                    wsResult = await ws.ReceiveAsync(buffer, ct);
                    if (wsResult.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                        Console.WriteLine($"[-] Client disconnected ({ws.GetHashCode()})");
                        return;
                    }
                    ms.Write(buffer, 0, wsResult.Count);
                }
                while (!wsResult.EndOfMessage);

                string rawJson = Encoding.UTF8.GetString(ms.ToArray());
                WinAppResponse response;

                try
                {
                    var request = JsonSerializer.Deserialize<WinAppRequest>(rawJson, _jsonOpts)
                        ?? throw new InvalidOperationException("Received null request.");
                    response = ProcessRequest(appManager, request);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[!] Request error: {ex.Message}");
                    response = WinAppResponse.Fail("", ex.Message);
                }

                string responseJson = JsonSerializer.Serialize(response, _jsonOpts);
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                await ws.SendAsync(responseBytes, WebSocketMessageType.Text, true, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[!] Connection error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine($"[-] Client session ended ({ws.GetHashCode()})");
        }
    }

    private static WinAppResponse ProcessRequest(AppManager mgr, WinAppRequest req)
    {
        try
        {
            switch (req.Command)
            {
                case "launch":
                {
                    var mwt = req.GetInt("mainWindowTimeout");
                    var appId = mgr.LaunchApp(
                        req.GetString("path"),
                        req.GetStringArray("args"),
                        req.GetString("detachedTitleRegex"),
                        mwt > 0 ? mwt : null
                    );
                    return WinAppResponse.Ok(req.Id, appId);
                }

                case "close":
                    mgr.CloseApp(req.GetString("appId"));
                    return WinAppResponse.Ok(req.Id);

                case "findElement":
                {
                    var elementId = mgr.FindElement(req.GetString("appId"), req.GetString("selector"));
                    return WinAppResponse.Ok(req.Id, elementId);
                }

                case "findElements":
                {
                    var ids = mgr.FindElements(req.GetString("appId"), req.GetString("selector"));
                    return WinAppResponse.Ok(req.Id, ids);
                }

                case "click":
                    mgr.Click(req.GetString("elementId"));
                    return WinAppResponse.Ok(req.Id);

                case "doubleClick":
                    mgr.DoubleClick(req.GetString("elementId"));
                    return WinAppResponse.Ok(req.Id);

                case "typeText":
                    mgr.TypeText(req.GetString("elementId"), req.GetString("text"));
                    return WinAppResponse.Ok(req.Id);

                case "getText":
                    return WinAppResponse.Ok(req.Id, mgr.GetText(req.GetString("elementId")));

                case "getAttribute":
                    return WinAppResponse.Ok(req.Id,
                        mgr.GetAttribute(req.GetString("elementId"), req.GetString("attribute")));

                case "isEnabled":
                    return WinAppResponse.Ok(req.Id, mgr.IsEnabled(req.GetString("elementId")));

                case "isVisible":
                    return WinAppResponse.Ok(req.Id, mgr.IsVisible(req.GetString("elementId")));

                case "focus":
                    mgr.Focus(req.GetString("elementId"));
                    return WinAppResponse.Ok(req.Id);

                case "getWindowTitle":
                    return WinAppResponse.Ok(req.Id, mgr.GetWindowTitle(req.GetString("appId")));

                case "getBoundingRect":
                    return WinAppResponse.Ok(req.Id, mgr.GetBoundingRect(req.GetString("elementId")));

                case "screenshot":
                    return WinAppResponse.Ok(req.Id,
                        mgr.ScreenshotElement(req.GetString("elementId"), req.GetString("path")));

                case "screenshotApp":
                    return WinAppResponse.Ok(req.Id,
                        mgr.ScreenshotApp(req.GetString("appId"), req.GetString("path")));

                case "startRecording":
                    return WinAppResponse.Ok(req.Id,
                        mgr.StartRecording(req.GetString("appId"), req.GetString("path"), req.GetString("ffmpegPath")));

                case "stopRecording":
                    return WinAppResponse.Ok(req.Id,
                        mgr.StopRecording(req.GetString("appId")));

                default:
                    return WinAppResponse.Fail(req.Id, $"Unknown command: '{req.Command}'");
            }
        }
        catch (Exception ex)
        {
            return WinAppResponse.Fail(req.Id, ex.Message);
        }
    }
}
