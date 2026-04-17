using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WindowsConductor.Client;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Kestrel-based WebSocket server that accepts connections from WindowsConductor Clients.
/// Each connected client gets its own <see cref="AppManager"/> so sessions
/// are isolated from one another.
/// </summary>
public sealed class WsServer
{
    private readonly bool _confineToApp;
    private readonly string? _ffmpegPath;
    private readonly AuthTokenValidator _authValidator;
    private readonly int? _httpPort;
    private readonly int? _httpsPort;
    private readonly X509Certificate2? _httpsCert;
    private readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public WsServer(
        int? httpPort = 8765,
        int? httpsPort = null,
        X509Certificate2? httpsCert = null,
        bool confineToApp = false,
        string? ffmpegPath = null,
        AuthTokenValidator? authValidator = null)
    {
        _httpPort = httpPort;
        _httpsPort = httpsPort;
        _httpsCert = httpsCert;
        _confineToApp = confineToApp;
        _ffmpegPath = ffmpegPath;
        _authValidator = authValidator ?? AuthTokenValidator.None();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            if (_httpPort is not null)
                options.ListenLocalhost(_httpPort.Value);
            if (_httpsPort is not null && _httpsCert is not null)
                options.ListenLocalhost(_httpsPort.Value, lo => lo.UseHttps(_httpsCert));
        });

        await using var app = builder.Build();
        app.UseWebSockets();

        app.Run(async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 426; // Upgrade Required
                return;
            }

            if (_authValidator.RequiresAuth)
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                var token = authHeader?.StartsWith("Bearer ", StringComparison.Ordinal) == true
                    ? authHeader["Bearer ".Length..]
                    : null;

                if (!_authValidator.Validate(token))
                {
                    Console.WriteLine("[!] Rejected client: invalid or missing auth token.");
                    context.Response.StatusCode = 401;
                    return;
                }
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            await HandleClientAsync(ws, ct);
        });

        var endpoints = new List<string>();
        if (_httpPort is not null) endpoints.Add($"http://localhost:{_httpPort}");
        if (_httpsPort is not null) endpoints.Add($"https://localhost:{_httpsPort}");
        Console.WriteLine($"Listening on {string.Join(", ", endpoints)}");
        Console.WriteLine("Press Ctrl+C to stop.");

        await ((IHost)app).RunAsync(ct);
    }

    private async Task HandleClientAsync(WebSocket ws, CancellationToken ct)
    {
        using var appManager = new AppManager(confineToApp: _confineToApp, ffmpegPath: _ffmpegPath);
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

                case "resolveAttrs":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var attrs = mgr.ResolveAttrs(
                            req.GetString("appId"),
                            req.GetString("selector"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId);
                        return WcResponse.Ok(req.Id, attrs);
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

                case "waitForResolvedAttrs":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var attrs = mgr.WaitForResolvedAttrs(
                            req.GetString("appId"),
                            req.GetString("selector"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            (uint)req.GetInt("timeout"));
                        return WcResponse.Ok(req.Id, attrs);
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

                case "setForeground":
                    mgr.SetForeground(req.GetString("elementId"));
                    return WcResponse.Ok(req.Id);

                case "getWindowState":
                    return WcResponse.Ok(req.Id, (int)mgr.GetWindowState(req.GetString("elementId")));

                case "setWindowState":
                    mgr.SetWindowState(req.GetString("elementId"), (WcWindowState)req.GetInt("state"));
                    return WcResponse.Ok(req.Id);

                case "getWindowTitle":
                    return WcResponse.Ok(req.Id, mgr.GetWindowTitle(req.GetString("appId")));

                case "getBoundingRect":
                    return WcResponse.Ok(req.Id, mgr.GetBoundingRect(req.GetString("elementId")));

                case "getWindowBoundingRect":
                    return WcResponse.Ok(req.Id, mgr.GetWindowBoundingRect(req.GetString("appId")));

                case "getChildren":
                    return WcResponse.Ok(req.Id,
                        mgr.GetChildren(req.GetString("elementId")));

                case "getDescendants":
                    return WcResponse.Ok(req.Id,
                        mgr.GetDescendants(req.GetString("elementId")));

                case "desktopScreenshot":
                    return WcResponse.Ok(req.Id, mgr.DesktopScreenshot());

                case "screenshot":
                    return WcResponse.Ok(req.Id,
                        mgr.ScreenshotElement(req.GetString("elementId")));

                case "screenshotApp":
                    return WcResponse.Ok(req.Id,
                        mgr.ScreenshotApp(req.GetString("appId")));

                case "startRecording":
                    mgr.StartRecording(req.GetString("appId"));
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
            var errorType = ex is NoMatchException or UnwantedMatchException or AccessRestrictedException
                ? ex.GetType().Name
                : null;
            return WcResponse.Fail(req.Id, ex.Message, errorType);
        }
    }
}
