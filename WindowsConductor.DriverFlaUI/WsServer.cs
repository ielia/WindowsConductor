using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WindowsConductor.Client;

// ReSharper disable AccessToDisposedClosure

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Kestrel-based WebSocket server that accepts connections from WindowsConductor Clients.
/// Each connected client gets its own <see cref="AppManager"/> so sessions
/// are isolated from one another.
/// </summary>
public sealed class WsServer
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<WsServer>();

    private const int COM_RETRY_ATTEMPTS = 3;
    private const int CATASTROPHIC_FAILURE = unchecked((int)0x8000FFFF);

    private readonly bool _confineToApp;
    private readonly string? _ffmpegPath;
    private readonly AuthTokenValidator _authValidator;
    private readonly int? _httpPort;
    private readonly int? _httpsPort;
    private readonly X509Certificate2? _httpsCert;
    private readonly int _maxConcurrency;
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
        AuthTokenValidator? authValidator = null,
        int maxConcurrency = 4)
    {
        _httpPort = httpPort;
        _httpsPort = httpsPort;
        _httpsCert = httpsCert;
        _confineToApp = confineToApp;
        _ffmpegPath = ffmpegPath;
        _authValidator = authValidator ?? AuthTokenValidator.None();
        _maxConcurrency = maxConcurrency;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Host.UseSerilog();
        builder.WebHost.ConfigureKestrel(options =>
        {
            if (_httpPort is not null)
                options.ListenAnyIP(_httpPort.Value);
            if (_httpsPort is not null && _httpsCert is not null)
                options.ListenAnyIP(_httpsPort.Value, lo => lo.UseHttps(_httpsCert));
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
                    Logger.Warning("Rejected client: invalid or missing auth token");
                    context.Response.StatusCode = 401;
                    return;
                }
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            await HandleClientAsync(ws, ct);
        });

        var endpoints = new List<string>();
        if (_httpPort is not null) endpoints.Add($"http://0.0.0.0:{_httpPort}");
        if (_httpsPort is not null) endpoints.Add($"https://0.0.0.0:{_httpsPort}");
        Logger.Information("Listening on {Endpoints}", string.Join(", ", endpoints));
        Logger.Information("Press Ctrl+C to stop");

        await ((IHost)app).RunAsync(ct);
    }

    private async Task HandleClientAsync(WebSocket ws, CancellationToken ct)
    {
        using var appManager = new AppManager(confineToApp: _confineToApp, ffmpegPath: _ffmpegPath);
        using var writeLock = new SemaphoreSlim(1, 1);
        using var concurrencyLimiter = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        var outstanding = new List<Task>();
        var buffer = new byte[256 * 1024];
        var clientId = ws.GetHashCode();
        Logger.Information("Client connected ({ClientId})", clientId);

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
                        Logger.Information("Client disconnected ({ClientId})", clientId);
                        return;
                    }
                    ms.Write(buffer, 0, wsResult.Count);
                }
                while (!wsResult.EndOfMessage);

                string rawJson = Encoding.UTF8.GetString(ms.ToArray());

                WcRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<WcRequest>(rawJson, _jsonOpts)
                        ?? throw new InvalidOperationException("Received null request.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Request parse error");
                    await SendResponseAsync(ws, writeLock, WcResponse.Fail("", ex.Message), ct);
                    continue;
                }

                var task = Task.Run(async () =>
                {
                    await concurrencyLimiter.WaitAsync(ct);
                    try
                    {
                        var response = ProcessRequestWithRetry(appManager, request, ct);
                        await SendResponseAsync(ws, writeLock, response, ct);
                    }
                    finally
                    {
                        concurrencyLimiter.Release();
                    }
                }, ct);

                lock (outstanding)
                    outstanding.Add(task);

                _ = task.ContinueWith(_ =>
                {
                    lock (outstanding)
                        outstanding.Remove(task);
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.Error(ex, "Connection error ({ClientId})", clientId);
        }
        finally
        {
            Task[] pending;
            lock (outstanding)
                pending = outstanding.ToArray();
            if (pending.Length > 0)
                await Task.WhenAll(pending).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            Logger.Information("Client session ended ({ClientId})", clientId);
        }
    }

    private async Task SendResponseAsync(WebSocket ws, SemaphoreSlim writeLock, WcResponse response, CancellationToken ct)
    {
        var responseJson = JsonSerializer.Serialize(response, _jsonOpts);
        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
        await writeLock.WaitAsync(ct);
        try
        {
            await ws.SendAsync(responseBytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            writeLock.Release();
        }
    }

    internal static WcResponse ProcessRequestWithRetry(IAppOperations mgr, WcRequest req, CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= COM_RETRY_ATTEMPTS; attempt++)
        {
            var response = ProcessRequest(mgr, req, ct);
            if (response.Success || attempt == COM_RETRY_ATTEMPTS)
                return response;

            if (!IsCatastrophicComError(response))
                return response;

            Logger.Warning("COM catastrophic failure on attempt {Attempt}/{Max} for command '{Command}' (id={Id}), retrying",
                attempt, COM_RETRY_ATTEMPTS, req.Command, req.Id);
        }
        return WcResponse.Fail(req.Id, "Unexpected: retry loop exited without returning");
    }

    private static bool IsCatastrophicComError(WcResponse response) =>
        response.Error?.Contains("0x8000FFFF", StringComparison.OrdinalIgnoreCase) == true
        || response.Error?.Contains("Catastrophic", StringComparison.OrdinalIgnoreCase) == true;

    internal static WcResponse ProcessRequest(IAppOperations mgr, WcRequest req, CancellationToken ct = default)
    {
        try
        {
            switch (req.Command)
            {
                case "version":
                    {
                        var clientVersion = req.GetString("clientVersion", "Unknown");
                        var serverVersion = WcDefaults.Version;
                        if (serverVersion == clientVersion)
                            Logger.Information("Client version: {ClientVersion}", clientVersion);
                        else
                            Logger.Warning("Client version: {ClientVersion}  <<< VERSION MISMATCH >>>", clientVersion);
                        return WcResponse.Ok(req.Id, serverVersion);
                    }

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
                            req.GetStringArray("selectors"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            ct);
                        return WcResponse.Ok(req.Id, elementId);
                    }

                case "findElements":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var ids = mgr.FindElements(
                            req.GetString("appId"),
                            req.GetStringArray("selectors"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            ct);
                        return WcResponse.Ok(req.Id, ids);
                    }

                case "resolveValue":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var value = mgr.ResolveValue(
                            req.GetString("appId"),
                            req.GetStringArray("selectors"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            ct);
                        return WcResponse.Ok(req.Id, value);
                    }

                case "findElementsAtPoint":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var ids = mgr.FindElementsAtPoint(
                            req.GetString("appId"),
                            req.GetDouble("x"),
                            req.GetDouble("y"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            ct);
                        return WcResponse.Ok(req.Id, ids);
                    }

                case "findFrontElementAtPoint":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var elementId = mgr.FindFrontElementAtPoint(
                            req.GetString("appId"),
                            req.GetDouble("x"),
                            req.GetDouble("y"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            ct);
                        return WcResponse.Ok(req.Id, elementId);
                    }

                case "waitForElement":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var elementId = mgr.WaitForElement(
                            req.GetString("appId"),
                            req.GetStringArray("selectors"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            (uint)req.GetInt("timeout"),
                            ct);
                        return WcResponse.Ok(req.Id, elementId);
                    }

                case "waitForElements":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var ids = mgr.WaitForElements(
                            req.GetString("appId"),
                            req.GetStringArray("selectors"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            (uint)req.GetInt("timeout"),
                            ct);
                        return WcResponse.Ok(req.Id, ids);
                    }

                case "waitForResolvedValue":
                    {
                        var rootElId = req.GetString("rootElementId");
                        var value = mgr.WaitForResolvedValue(
                            req.GetString("appId"),
                            req.GetStringArray("selectors"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            (uint)req.GetInt("timeout"),
                            ct);
                        return WcResponse.Ok(req.Id, value);
                    }

                case "waitForVanish":
                    {
                        var rootElId = req.GetString("rootElementId");
                        mgr.WaitForVanish(
                            req.GetString("appId"),
                            req.GetStringArray("selectors"),
                            string.IsNullOrEmpty(rootElId) ? null : rootElId,
                            (uint)req.GetInt("timeout"),
                            ct);
                        return WcResponse.Ok(req.Id);
                    }

                case "waitForElementVanish":
                    {
                        mgr.WaitForElementVanish(
                            req.GetString("elementId"),
                            (uint)req.GetInt("timeout"),
                            ct);
                        return WcResponse.Ok(req.Id);
                    }

                case "waitForVisible":
                    {
                        var selectors = req.GetStringArray("selectors");
                        if (selectors.Length > 0)
                        {
                            var rootElId = req.GetString("rootElementId");
                            mgr.WaitForVisible(
                                req.GetString("appId"),
                                selectors,
                                string.IsNullOrEmpty(rootElId) ? null : rootElId,
                                (uint)req.GetInt("timeout"),
                                ct);
                        }
                        else
                        {
                            mgr.WaitForElementVisible(
                                req.GetString("elementId"),
                                (uint)req.GetInt("timeout"),
                                ct);
                        }
                        return WcResponse.Ok(req.Id);
                    }

                case "waitForHidden":
                    {
                        var selectors = req.GetStringArray("selectors");
                        if (selectors.Length > 0)
                        {
                            var rootElId = req.GetString("rootElementId");
                            mgr.WaitForHidden(
                                req.GetString("appId"),
                                selectors,
                                string.IsNullOrEmpty(rootElId) ? null : rootElId,
                                (uint)req.GetInt("timeout"),
                                ct);
                        }
                        else
                        {
                            mgr.WaitForElementHidden(
                                req.GetString("elementId"),
                                (uint)req.GetInt("timeout"),
                                ct);
                        }
                        return WcResponse.Ok(req.Id);
                    }

                case "click":
                    {
                        var a = req.GetString("anchor");
                        mgr.Click(req.GetString("elementId"), a.Length > 0 ? a : null, req.GetInt("x"), req.GetInt("y"));
                        return WcResponse.Ok(req.Id);
                    }

                case "doubleClick":
                    {
                        var a = req.GetString("anchor");
                        mgr.DoubleClick(req.GetString("elementId"), a.Length > 0 ? a : null, req.GetInt("x"), req.GetInt("y"));
                        return WcResponse.Ok(req.Id);
                    }

                case "rightClick":
                    {
                        var a = req.GetString("anchor");
                        mgr.RightClick(req.GetString("elementId"), a.Length > 0 ? a : null, req.GetInt("x"), req.GetInt("y"));
                        return WcResponse.Ok(req.Id);
                    }

                case "hover":
                    {
                        var a = req.GetString("anchor");
                        mgr.Hover(req.GetString("elementId"), a.Length > 0 ? a : null, req.GetInt("x"), req.GetInt("y"));
                        return WcResponse.Ok(req.Id);
                    }

                case "dragTo":
                    {
                        var fa = req.GetString("fromAnchor");
                        var ta = req.GetString("toAnchor");
                        mgr.DragTo(
                            req.GetString("sourceElementId"), fa.Length > 0 ? fa : null, req.GetInt("fromX"), req.GetInt("fromY"),
                            req.GetString("targetElementId"), ta.Length > 0 ? ta : null, req.GetInt("toX"), req.GetInt("toY"));
                        return WcResponse.Ok(req.Id);
                    }

                case "scroll":
                    mgr.Scroll(req.GetString("elementId"), req.GetDouble("lines"), req.GetBool("horizontal"));
                    return WcResponse.Ok(req.Id);

                case "hitKeys":
                    mgr.HitKeys(req.GetString("elementId"), req.GetStringArray("keys"));
                    return WcResponse.Ok(req.Id);

                case "typeText":
                    mgr.TypeText(req.GetString("elementId"), req.GetString("text"), req.GetInt("modifiers"));
                    return WcResponse.Ok(req.Id);

                case "globalHitKeys":
                    mgr.GlobalHitKeys(req.GetStringArray("keys"));
                    return WcResponse.Ok(req.Id);

                case "globalTypeText":
                    mgr.GlobalTypeText(req.GetString("text"), req.GetInt("modifiers"));
                    return WcResponse.Ok(req.Id);

                case "getText":
                    return WcResponse.Ok(req.Id, mgr.GetText(req.GetString("elementId")));

                case "getAttribute":
                    return WcResponse.Ok(req.Id,
                        mgr.GetAttribute(req.GetString("elementId"), req.GetString("attribute")));

                case "getAttributes":
                    return WcResponse.Ok(req.Id,
                        mgr.GetAttributes(req.GetString("elementId")));

                case "setAttribute":
                    mgr.SetAttribute(req.GetString("elementId"), req.GetString("attribute"), req.GetString("value"));
                    return WcResponse.Ok(req.Id);

                case "getParent":
                    return WcResponse.Ok(req.Id,
                        mgr.GetParent(req.GetString("elementId")));

                case "getTopLevelWindow":
                    return WcResponse.Ok(req.Id,
                        mgr.GetTopLevelWindow(req.GetString("elementId")));

                case "isStale":
                    return WcResponse.Ok(req.Id, mgr.IsStale(req.GetString("elementId")));

                case "exists":
                    {
                        var selectors = req.GetStringArray("selectors");
                        if (selectors.Length > 0)
                        {
                            var rootElId = req.GetString("rootElementId");
                            return WcResponse.Ok(req.Id,
                                mgr.Exists(
                                    req.GetString("appId"),
                                    selectors,
                                    string.IsNullOrEmpty(rootElId) ? null : rootElId,
                                    ct));
                        }
                        return WcResponse.Ok(req.Id, mgr.Exists(req.GetString("elementId")));
                    }

                case "isEnabled":
                    return WcResponse.Ok(req.Id, mgr.IsEnabled(req.GetString("elementId")));

                case "isVisible":
                    {
                        var selectors = req.GetStringArray("selectors");
                        if (selectors.Length > 0)
                        {
                            var rootElId = req.GetString("rootElementId");
                            return WcResponse.Ok(req.Id,
                                mgr.IsVisible(
                                    req.GetString("appId"),
                                    selectors,
                                    string.IsNullOrEmpty(rootElId) ? null : rootElId,
                                    ct));
                        }
                        return WcResponse.Ok(req.Id, mgr.IsVisible(req.GetString("elementId")));
                    }

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

                case "getOcrText":
                    return WcResponse.Ok(req.Id,
                        mgr.GetOcrText(req.GetString("elementId")));

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
            var errorType = ex is NoMatchException or UnwantedMatchException or VisibilityException or AccessRestrictedException or LocationOutOfRangeException
                ? ex.GetType().Name
                : null;
            return WcResponse.Fail(req.Id, ex.Message, errorType);
        }
    }
}
