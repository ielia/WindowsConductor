using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WindowsConductor.Client;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// HTTP/WebSocket server that accepts connections from WindowsConductor Clients.
/// Each connected client gets its own <see cref="AppManager"/> so sessions
/// are isolated from one another.
/// </summary>
public sealed class WsServer
{
    private readonly HttpListener _listener = new();
    private readonly bool _confineToApp;
    private readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public WsServer(string prefix = WcDefaults.HttpPrefix, bool confineToApp = false)
    {
        _confineToApp = confineToApp;
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
        using var appManager = new AppManager(confineToApp: _confineToApp);
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
                WcResponse response;

                try
                {
                    var request = JsonSerializer.Deserialize<WcRequest>(rawJson, _jsonOpts)
                        ?? throw new InvalidOperationException("Received null request.");
                    response = ProcessRequest(appManager, request);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[!] Request error: {ex.Message}");
                    response = WcResponse.Fail("", ex.Message);
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

    internal static WcResponse ProcessRequest(IAppOperations mgr, WcRequest req)
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
                        return WcResponse.Ok(req.Id, appId);
                    }

                case "attach":
                    {
                        var mwt = req.GetInt("mainWindowTimeout");
                        var appId = mgr.AttachApp(
                            req.GetString("mainWindowTitleRegex"),
                            mwt > 0 ? mwt : null
                        );
                        return WcResponse.Ok(req.Id, appId);
                    }

                case "close":
                    mgr.CloseApp(req.GetString("appId"));
                    return WcResponse.Ok(req.Id);

                case "findElement":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var elementId = mgr.FindElement(
                            req.GetString("appId"),
                            req.GetString("selector"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId);
                        return WcResponse.Ok(req.Id, elementId);
                    }

                case "findElements":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var ids = mgr.FindElements(
                            req.GetString("appId"),
                            req.GetString("selector"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId);
                        return WcResponse.Ok(req.Id, ids);
                    }

                case "findElementsAtPoint":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var ids = mgr.FindElementsAtPoint(
                            req.GetString("appId"),
                            req.GetDouble("x"),
                            req.GetDouble("y"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId);
                        return WcResponse.Ok(req.Id, ids);
                    }

                case "findFrontElementAtPoint":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var elementId = mgr.FindFrontElementAtPoint(
                            req.GetString("appId"),
                            req.GetDouble("x"),
                            req.GetDouble("y"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId);
                        return WcResponse.Ok(req.Id, elementId);
                    }

                case "waitForElement":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var elementId = mgr.WaitForElement(
                            req.GetString("appId"),
                            req.GetString("selector"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            (uint)req.GetInt("timeout"));
                        return WcResponse.Ok(req.Id, elementId);
                    }

                case "waitForElements":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var ids = mgr.WaitForElements(
                            req.GetString("appId"),
                            req.GetString("selector"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            (uint)req.GetInt("timeout"));
                        return WcResponse.Ok(req.Id, ids);
                    }

                case "waitForVanish":
                    {
                        var rootElId = req.GetString("rootElementId");
                        mgr.WaitForVanish(
                            req.GetString("appId"),
                            req.GetString("selector"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            (uint)req.GetInt("timeout"));
                        return WcResponse.Ok(req.Id);
                    }

                case "click":
                    mgr.Click(req.GetString("elementId"));
                    return WcResponse.Ok(req.Id);

                case "doubleClick":
                    mgr.DoubleClick(req.GetString("elementId"));
                    return WcResponse.Ok(req.Id);

                case "rightClick":
                    mgr.RightClick(req.GetString("elementId"));
                    return WcResponse.Ok(req.Id);

                case "typeText":
                    mgr.TypeText(req.GetString("elementId"), req.GetString("text"), req.GetInt("modifiers"));
                    return WcResponse.Ok(req.Id);

                case "getText":
                    return WcResponse.Ok(req.Id, mgr.GetText(req.GetString("elementId")));

                case "getAttribute":
                    return WcResponse.Ok(req.Id,
                        mgr.GetAttribute(req.GetString("elementId"), req.GetString("attribute")));

                case "getAttributes":
                    return WcResponse.Ok(req.Id,
                        mgr.GetAttributes(req.GetString("elementId")));

                case "getParent":
                    return WcResponse.Ok(req.Id,
                        mgr.GetParent(req.GetString("elementId")));

                case "getTopLevelWindow":
                    return WcResponse.Ok(req.Id,
                        mgr.GetTopLevelWindow(req.GetString("elementId")));

                case "isEnabled":
                    return WcResponse.Ok(req.Id, mgr.IsEnabled(req.GetString("elementId")));

                case "isVisible":
                    return WcResponse.Ok(req.Id, mgr.IsVisible(req.GetString("elementId")));

                case "focus":
                    mgr.Focus(req.GetString("elementId"));
                    return WcResponse.Ok(req.Id);

                case "getWindowTitle":
                    return WcResponse.Ok(req.Id, mgr.GetWindowTitle(req.GetString("appId")));

                case "getBoundingRect":
                    return WcResponse.Ok(req.Id, mgr.GetBoundingRect(req.GetString("elementId")));

                case "getWindowBoundingRect":
                    return WcResponse.Ok(req.Id, mgr.GetWindowBoundingRect(req.GetString("appId")));

                case "screenshot":
                    return WcResponse.Ok(req.Id,
                        mgr.ScreenshotElement(req.GetString("elementId")));

                case "screenshotApp":
                    return WcResponse.Ok(req.Id,
                        mgr.ScreenshotApp(req.GetString("appId")));

                case "startRecording":
                    mgr.StartRecording(req.GetString("appId"), req.GetString("ffmpegPath"));
                    return WcResponse.Ok(req.Id);

                case "stopRecording":
                    return WcResponse.Ok(req.Id,
                        mgr.StopRecording(req.GetString("appId")));

                default:
                    return WcResponse.Fail(req.Id, $"Unknown command: '{req.Command}'");
            }
        }
        catch (Exception ex)
        {
            var errorType = ex is ElementNotFoundException or UnwantedElementFoundException or AccessRestrictedException
                ? ex.GetType().Name
                : null;
            return WcResponse.Fail(req.Id, ex.Message, errorType);
        }
    }
}
