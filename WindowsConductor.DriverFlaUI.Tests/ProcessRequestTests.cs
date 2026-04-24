using System.Text.Json;
using WindowsConductor.Client;
using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

internal sealed class FakeAppOperations : IAppOperations
{
    public sealed record Call(string Method, object?[] Args);
    public List<Call> Calls { get; } = new();

    // Configurable return values
    public string LaunchResult { get; set; } = "app-1";
    public string AttachResult { get; set; } = "app-2";
    public string FindElementResult { get; set; } = "el-1";
    public string[] FindElementsResult { get; set; } = { "el-1", "el-2" };
    public string GetTextResult { get; set; } = "Hello";
    public string GetAttributeResult { get; set; } = "btn-class";
    public bool IsEnabledResult { get; set; } = true;
    public bool IsVisibleResult { get; set; } = true;
    public string GetWindowTitleResult { get; set; } = "My App";
    public object GetBoundingRectResult { get; set; } = new { x = 0, y = 0, width = 100, height = 50 };
    public object GetWindowBoundingRectResult { get; set; } = new { x = 10, y = 20, width = 800, height = 600 };
    public byte[] ScreenshotElementResult { get; set; } = [0x89, 0x50, 0x4E, 0x47];
    public byte[] ScreenshotAppResult { get; set; } = [0x89, 0x50, 0x4E, 0x47];
    public byte[] StopRecordingResult { get; set; } = [0x00, 0x00, 0x01, 0xBA];
    public Exception? ThrowOnNext { get; set; }

    private void Record(string method, params object?[] args)
    {
        if (ThrowOnNext is { } ex) { ThrowOnNext = null; throw ex; }
        Calls.Add(new Call(method, args));
    }

    public string LaunchApp(string path, string[] args, string? detachedTitleRegex, int? mainWindowTimeout)
    { Record("LaunchApp", path, args, detachedTitleRegex, mainWindowTimeout); return LaunchResult; }

    public string AttachApp(string mainWindowTitleRegex, int? mainWindowTimeout)
    { Record("AttachApp", mainWindowTitleRegex, mainWindowTimeout); return AttachResult; }

    public void CloseApp(string appId) => Record("CloseApp", appId);

    public string FindElement(string appId, string selector, string? rootElementId = null)
    { Record("FindElement", appId, selector, rootElementId); return FindElementResult; }

    public string[] FindElements(string appId, string selector, string? rootElementId = null)
    { Record("FindElements", appId, selector, rootElementId); return FindElementsResult; }

    public object ResolveValueResult { get; set; } = new { type = "ListValue", items = new object[] { new { type = "StringValue", value = "btn", elementId = "el-1", name = "class" } } };
    public object ResolveValue(string appId, string selector, string? rootElementId = null)
    { Record("ResolveValue", appId, selector, rootElementId); return ResolveValueResult; }

    public void Click(string elementId, string? anchor = null, int x = 0, int y = 0) => Record("Click", elementId, anchor, x, y);
    public void DoubleClick(string elementId, string? anchor = null, int x = 0, int y = 0) => Record("DoubleClick", elementId, anchor, x, y);
    public void RightClick(string elementId, string? anchor = null, int x = 0, int y = 0) => Record("RightClick", elementId, anchor, x, y);
    public void Hover(string elementId, string? anchor = null, int x = 0, int y = 0) => Record("Hover", elementId, anchor, x, y);
    public void HitKeys(string elementId, string[] keys) => Record("HitKeys", elementId, keys);
    public void TypeText(string elementId, string text, int modifiers = 0) => Record("TypeText", elementId, text, modifiers);
    public string GetText(string elementId) { Record("GetText", elementId); return GetTextResult; }

    public string GetAttribute(string elementId, string attribute)
    { Record("GetAttribute", elementId, attribute); return GetAttributeResult; }

    public Dictionary<string, object?> GetAttributesResult { get; set; } = new() { ["name"] = "OK" };
    public Dictionary<string, object?> GetAttributes(string elementId)
    { Record("GetAttributes", elementId); return GetAttributesResult; }

    public string? GetParentResult { get; set; } = "parent-el-1";
    public string? GetParent(string elementId)
    { Record("GetParent", elementId); return GetParentResult; }

    public string? GetTopLevelWindowResult { get; set; } = "win-el-1";
    public string? GetTopLevelWindow(string elementId)
    { Record("GetTopLevelWindow", elementId); return GetTopLevelWindowResult; }

    public bool IsEnabled(string elementId) { Record("IsEnabled", elementId); return IsEnabledResult; }
    public bool IsVisible(string elementId) { Record("IsVisible", elementId); return IsVisibleResult; }
    public void Focus(string elementId) => Record("Focus", elementId);
    public void SetForeground(string elementId) => Record("SetForeground", elementId);

    public WcWindowState GetWindowStateResult { get; set; } = WcWindowState.Normal;
    public WcWindowState GetWindowState(string elementId)
    { Record("GetWindowState", elementId); return GetWindowStateResult; }

    public void SetWindowState(string elementId, WcWindowState state) => Record("SetWindowState", elementId, state);

    public string GetWindowTitle(string appId) { Record("GetWindowTitle", appId); return GetWindowTitleResult; }
    public object GetBoundingRect(string elementId) { Record("GetBoundingRect", elementId); return GetBoundingRectResult; }
    public object GetWindowBoundingRect(string appId) { Record("GetWindowBoundingRect", appId); return GetWindowBoundingRectResult; }

    public object GetOcrTextResult { get; set; } = new { text = "", angle = (double?)null, boundingRect = new { x = 0.0, y = 0.0, width = 100.0, height = 50.0 }, lines = Array.Empty<object>() };
    public object GetOcrText(string elementId)
    { Record("GetOcrText", elementId); return GetOcrTextResult; }

    public byte[] ScreenshotElement(string elementId)
    { Record("ScreenshotElement", elementId); return ScreenshotElementResult; }

    public byte[] ScreenshotApp(string appId)
    { Record("ScreenshotApp", appId); return ScreenshotAppResult; }

    public void StartRecording(string appId)
    { Record("StartRecording", appId); }

    public byte[] StopRecording(string appId) { Record("StopRecording", appId); return StopRecordingResult; }

    public string[] FindElementsAtPointResult { get; set; } = { "el-p1", "el-p2" };
    public string[] FindElementsAtPoint(string appId, double x, double y, string? rootElementId = null)
    { Record("FindElementsAtPoint", appId, x, y, rootElementId); return FindElementsAtPointResult; }

    public string FindFrontElementAtPointResult { get; set; } = "el-front";
    public string FindFrontElementAtPoint(string appId, double x, double y, string? rootElementId = null)
    { Record("FindFrontElementAtPoint", appId, x, y, rootElementId); return FindFrontElementAtPointResult; }

    public string WaitForElementResult { get; set; } = "el-wait-1";
    public string[] WaitForElementsResult { get; set; } = { "el-w1", "el-w2" };

    public string WaitForElement(string appId, string selector, string? rootElementId, uint timeout)
    { Record("WaitForElement", appId, selector, rootElementId, timeout); return WaitForElementResult; }

    public string[] WaitForElements(string appId, string selector, string? rootElementId, uint timeout)
    { Record("WaitForElements", appId, selector, rootElementId, timeout); return WaitForElementsResult; }

    public object WaitForResolvedValue(string appId, string selector, string? rootElementId, uint timeout)
    { Record("WaitForResolvedValue", appId, selector, rootElementId, timeout); return ResolveValueResult; }

    public void WaitForVanish(string appId, string selector, string? rootElementId, uint timeout)
    { Record("WaitForVanish", appId, selector, rootElementId, timeout); }

    public string[] GetChildrenResult { get; set; } = ["child-1", "child-2"];
    public string[] GetChildren(string elementId)
    { Record("GetChildren", elementId); return GetChildrenResult; }

    public object GetDescendantsResult { get; set; } = new { id = "el-1", children = Array.Empty<object>() };
    public object GetDescendants(string elementId)
    { Record("GetDescendants", elementId); return GetDescendantsResult; }

    public byte[] DesktopScreenshotResult { get; set; } = [0x89, 0x50, 0x4E, 0x47];
    public byte[] DesktopScreenshot()
    { Record("DesktopScreenshot"); return DesktopScreenshotResult; }
}

[TestFixture]
[Category("Unit")]
public class ProcessRequestTests
{
    private FakeAppOperations _fake = null!;

    [SetUp]
    public void SetUp() => _fake = new FakeAppOperations();

    private static JsonElement ToJsonElement(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static WcRequest MakeRequest(string command, Dictionary<string, object>? p = null)
    {
        var req = new WcRequest { Id = "req-1", Command = command };
        if (p != null)
            foreach (var kv in p)
                req.Params[kv.Key] = ToJsonElement(kv.Value);
        return req;
    }

    // ── launch ───────────────────────────────────────────────────────────────

    [Test]
    public void Launch_CallsLaunchApp_ReturnsAppId()
    {
        var req = MakeRequest("launch", new()
        {
            ["path"] = "calc.exe",
            ["args"] = new[] { "--flag" },
            ["detachedTitleRegex"] = "Calc.*",
            ["mainWindowTimeout"] = 3000
        });

        var resp = WsServer.ProcessRequest(_fake, req);

        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo("app-1"));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("LaunchApp"));
    }

    [Test]
    public void Launch_ZeroTimeout_PassesNull()
    {
        var req = MakeRequest("launch", new()
        {
            ["path"] = "app.exe",
            ["args"] = Array.Empty<string>(),
            ["detachedTitleRegex"] = "",
            ["mainWindowTimeout"] = 0
        });

        WsServer.ProcessRequest(_fake, req);
        var args = _fake.Calls[0].Args;
        Assert.That(args[3], Is.Null); // mainWindowTimeout passed as null when 0
    }

    // ── attach ───────────────────────────────────────────────────────────────

    [Test]
    public void Attach_CallsAttachApp_ReturnsAppId()
    {
        var req = MakeRequest("attach", new()
        {
            ["mainWindowTitleRegex"] = "Calc.*",
            ["mainWindowTimeout"] = 2000
        });

        var resp = WsServer.ProcessRequest(_fake, req);

        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo("app-2"));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("AttachApp"));
        Assert.That(_fake.Calls[0].Args[0], Is.EqualTo("Calc.*"));
        Assert.That(_fake.Calls[0].Args[1], Is.EqualTo(2000));
    }

    [Test]
    public void Attach_ZeroTimeout_PassesNull()
    {
        var req = MakeRequest("attach", new()
        {
            ["mainWindowTitleRegex"] = "App.*",
            ["mainWindowTimeout"] = 0
        });

        WsServer.ProcessRequest(_fake, req);
        Assert.That(_fake.Calls[0].Args[1], Is.Null);
    }

    // ── close ────────────────────────────────────────────────────────────────

    [Test]
    public void Close_CallsCloseApp()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("close", new() { ["appId"] = "a1" }));
        Assert.That(resp.Success, Is.True);
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("CloseApp"));
        Assert.That(_fake.Calls[0].Args[0], Is.EqualTo("a1"));
    }

    // ── findElement ──────────────────────────────────────────────────────────

    [Test]
    public void FindElement_ReturnsElementId()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("findElement", new()
        {
            ["appId"] = "a1",
            ["selector"] = "[name=OK]",
            ["rootElementId"] = ""
        }));

        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo("el-1"));
        Assert.That(_fake.Calls[0].Args[2], Is.Null); // empty rootElementId → null
    }

    [Test]
    public void FindElement_WithRootElementId_PassesIt()
    {
        WsServer.ProcessRequest(_fake, MakeRequest("findElement", new()
        {
            ["appId"] = "a1",
            ["selector"] = "[name=OK]",
            ["rootElementId"] = "root-el"
        }));
        Assert.That(_fake.Calls[0].Args[2], Is.EqualTo("root-el"));
    }

    // ── findElements ─────────────────────────────────────────────────────────

    [Test]
    public void FindElements_ReturnsArray()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("findElements", new()
        {
            ["appId"] = "a1",
            ["selector"] = "type=Button",
            ["rootElementId"] = ""
        }));

        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo(new[] { "el-1", "el-2" }));
    }

    // ── resolveValue ─────────────────────────────────────────────────────────

    [Test]
    public void ResolveValue_ReturnsValue()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("resolveValue", new()
        {
            ["appId"] = "a1",
            ["selector"] = "//button/@class",
            ["rootElementId"] = ""
        }));

        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo(_fake.ResolveValueResult));
    }

    [Test]
    public void ResolveValue_PassesRootElementId()
    {
        WsServer.ProcessRequest(_fake, MakeRequest("resolveValue", new()
        {
            ["appId"] = "a1",
            ["selector"] = "//button/@class",
            ["rootElementId"] = "root-el"
        }));
        Assert.That(_fake.Calls[0].Args[2], Is.EqualTo("root-el"));
    }

    // ── findElementsAtPoint ──────────────────────────────────────────────────

    [Test]
    public void FindElementsAtPoint_ReturnsArray()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("findElementsAtPoint", new()
        {
            ["appId"] = "a1",
            ["x"] = 50.5,
            ["y"] = 100.0,
            ["rootElementId"] = ""
        }));

        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo(new[] { "el-p1", "el-p2" }));
        var call = _fake.Calls.Single(c => c.Method == "FindElementsAtPoint");
        Assert.That(call.Args[0], Is.EqualTo("a1"));
        Assert.That(call.Args[1], Is.EqualTo(50.5));
        Assert.That(call.Args[2], Is.EqualTo(100.0));
        Assert.That(call.Args[3], Is.Null);
    }

    [Test]
    public void FindElementsAtPoint_WithRootElementId_PassesIt()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("findElementsAtPoint", new()
        {
            ["appId"] = "a1",
            ["x"] = 10.0,
            ["y"] = 20.0,
            ["rootElementId"] = "root-el"
        }));

        Assert.That(resp.Success, Is.True);
        var call = _fake.Calls.Single(c => c.Method == "FindElementsAtPoint");
        Assert.That(call.Args[3], Is.EqualTo("root-el"));
    }

    // ── findFrontElementAtPoint ─────────────────────────────────────────────

    [Test]
    public void FindFrontElementAtPoint_ReturnsSingleElement()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("findFrontElementAtPoint", new()
        {
            ["appId"] = "a1",
            ["x"] = 50.5,
            ["y"] = 100.0,
            ["rootElementId"] = ""
        }));

        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo("el-front"));
        var call = _fake.Calls.Single(c => c.Method == "FindFrontElementAtPoint");
        Assert.That(call.Args[0], Is.EqualTo("a1"));
        Assert.That(call.Args[1], Is.EqualTo(50.5));
        Assert.That(call.Args[2], Is.EqualTo(100.0));
        Assert.That(call.Args[3], Is.Null);
    }

    // ── click ────────────────────────────────────────────────────────────────

    [Test]
    public void Click_CallsClick()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("click", new() { ["elementId"] = "e1" }));
        Assert.That(resp.Success, Is.True);
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("Click"));
    }

    // ── doubleClick ──────────────────────────────────────────────────────────

    [Test]
    public void DoubleClick_CallsDoubleClick()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("doubleClick", new() { ["elementId"] = "e1" }));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("DoubleClick"));
    }

    // ── rightClick ───────────────────────────────────────────────────────────

    [Test]
    public void RightClick_CallsRightClick()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("rightClick", new() { ["elementId"] = "e1" }));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("RightClick"));
    }

    // ── hover ────────────────────────────────────────────────────────────────

    [Test]
    public void Hover_CallsHover()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("hover", new() { ["elementId"] = "e1" }));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("Hover"));
    }

    // ── hitKeys ──────────────────────────────────────────────────────────────

    [Test]
    public void HitKeys_CallsHitKeys()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("hitKeys", new()
        {
            ["elementId"] = "e1",
            ["keys"] = new[] { "CONTROL", "KEY_A" }
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("HitKeys"));
        Assert.That(_fake.Calls[0].Args[0], Is.EqualTo("e1"));
        Assert.That(_fake.Calls[0].Args[1], Is.EqualTo(new[] { "CONTROL", "KEY_A" }));
    }

    [Test]
    public void HitKeys_SingleKey_CallsHitKeys()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("hitKeys", new()
        {
            ["elementId"] = "e1",
            ["keys"] = new[] { "ESCAPE" }
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(_fake.Calls[0].Args[1], Is.EqualTo(new[] { "ESCAPE" }));
    }

    [Test]
    public void HitKeys_EmptyKeys_CallsHitKeys()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("hitKeys", new()
        {
            ["elementId"] = "e1",
            ["keys"] = Array.Empty<string>()
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(_fake.Calls[0].Args[1], Is.EqualTo(Array.Empty<string>()));
    }

    // ── typeText ─────────────────────────────────────────────────────────────

    [Test]
    public void TypeText_CallsTypeText()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("typeText", new()
        {
            ["elementId"] = "e1",
            ["text"] = "hello"
        }));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("TypeText"));
        Assert.That(_fake.Calls[0].Args[1], Is.EqualTo("hello"));
    }

    [Test]
    public void TypeText_WithModifiers_PassesModifiers()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("typeText", new()
        {
            ["elementId"] = "e1",
            ["text"] = "a",
            ["modifiers"] = 3
        }));
        Assert.That(_fake.Calls[0].Args[2], Is.EqualTo(3));
    }

    // ── getText ──────────────────────────────────────────────────────────────

    [Test]
    public void GetText_ReturnsText()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("getText", new() { ["elementId"] = "e1" }));
        Assert.That(resp.Result, Is.EqualTo("Hello"));
    }

    // ── getAttribute ─────────────────────────────────────────────────────────

    [Test]
    public void GetAttribute_ReturnsValue()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("getAttribute", new()
        {
            ["elementId"] = "e1",
            ["attribute"] = "classname"
        }));
        Assert.That(resp.Result, Is.EqualTo("btn-class"));
        Assert.That(_fake.Calls[0].Args[1], Is.EqualTo("classname"));
    }

    // ── getAttributes ───────────────────────────────────────────────────────

    [Test]
    public void GetAttributes_ReturnsDictionary()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("getAttributes", new()
        {
            ["elementId"] = "e1"
        }));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("GetAttributes"));
    }

    // ── getParent ─────────────────────────────────────────────────────────────

    [Test]
    public void GetParent_ReturnsParentElementId()
    {
        _fake.GetParentResult = "parent-el-1";
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("getParent", new()
        {
            ["elementId"] = "e1"
        }));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("GetParent"));
        Assert.That(resp.Result, Is.EqualTo("parent-el-1"));
    }

    // ── getTopLevelWindow ──────────────────────────────────────────────────────

    [Test]
    public void GetTopLevelWindow_ReturnsWindowElementId()
    {
        _fake.GetTopLevelWindowResult = "win-el-1";
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("getTopLevelWindow", new()
        {
            ["elementId"] = "e1"
        }));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("GetTopLevelWindow"));
        Assert.That(resp.Result, Is.EqualTo("win-el-1"));
    }

    [Test]
    public void GetTopLevelWindow_ReturnsNull_WhenAlreadyTopLevel()
    {
        _fake.GetTopLevelWindowResult = null;
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("getTopLevelWindow", new()
        {
            ["elementId"] = "e1"
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.Null);
    }

    // ── isEnabled ────────────────────────────────────────────────────────────

    [Test]
    public void IsEnabled_ReturnsBool()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("isEnabled", new() { ["elementId"] = "e1" }));
        Assert.That(resp.Result, Is.EqualTo(true));
    }

    // ── isVisible ────────────────────────────────────────────────────────────

    [Test]
    public void IsVisible_ReturnsBool()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("isVisible", new() { ["elementId"] = "e1" }));
        Assert.That(resp.Result, Is.EqualTo(true));
    }

    // ── focus ────────────────────────────────────────────────────────────────

    [Test]
    public void Focus_CallsFocus()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("focus", new() { ["elementId"] = "e1" }));
        Assert.That(resp.Success, Is.True);
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("Focus"));
    }

    // ── setForeground ─────────────────────────────────────────────────────────

    [Test]
    public void SetForeground_CallsSetForeground()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("setForeground", new() { ["elementId"] = "e1" }));
        Assert.That(resp.Success, Is.True);
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("SetForeground"));
    }

    // ── getWindowState ────────────────────────────────────────────────────────

    [Test]
    public void GetWindowState_ReturnsState()
    {
        _fake.GetWindowStateResult = WcWindowState.Maximized;
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("getWindowState", new() { ["elementId"] = "e1" }));
        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo((int)WcWindowState.Maximized));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("GetWindowState"));
    }

    // ── setWindowState ────────────────────────────────────────────────────────

    [Test]
    public void SetWindowState_CallsSetWindowState()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("setWindowState", new()
        {
            ["elementId"] = "e1",
            ["state"] = (int)WcWindowState.Minimized
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("SetWindowState"));
        Assert.That(_fake.Calls[0].Args[1], Is.EqualTo(WcWindowState.Minimized));
    }

    // ── getWindowTitle ───────────────────────────────────────────────────────

    [Test]
    public void GetWindowTitle_ReturnsTitle()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("getWindowTitle", new() { ["appId"] = "a1" }));
        Assert.That(resp.Result, Is.EqualTo("My App"));
    }

    // ── getBoundingRect ──────────────────────────────────────────────────────

    [Test]
    public void GetBoundingRect_ReturnsRect()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("getBoundingRect", new() { ["elementId"] = "e1" }));
        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.Not.Null);
    }

    // ── getWindowBoundingRect ─────────────────────────────────────────────────

    [Test]
    public void GetWindowBoundingRect_ReturnsRect()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("getWindowBoundingRect", new() { ["appId"] = "a1" }));
        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.Not.Null);
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("GetWindowBoundingRect"));
        Assert.That(_fake.Calls[0].Args[0], Is.EqualTo("a1"));
    }

    // ── screenshot ───────────────────────────────────────────────────────────

    [Test]
    public void Screenshot_ReturnsBytes()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("screenshot", new()
        {
            ["elementId"] = "e1"
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("ScreenshotElement"));
        Assert.That(_fake.Calls[0].Args[0], Is.EqualTo("e1"));
    }

    // ── screenshotApp ────────────────────────────────────────────────────────

    [Test]
    public void ScreenshotApp_ReturnsBytes()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("screenshotApp", new()
        {
            ["appId"] = "a1"
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("ScreenshotApp"));
    }

    // ── startRecording ───────────────────────────────────────────────────────

    [Test]
    public void StartRecording_ReturnsOk()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("startRecording", new()
        {
            ["appId"] = "a1"
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("StartRecording"));
        Assert.That(_fake.Calls[0].Args[0], Is.EqualTo("a1"));
    }

    // ── stopRecording ────────────────────────────────────────────────────────

    [Test]
    public void StopRecording_ReturnsBytes()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("stopRecording", new() { ["appId"] = "a1" }));
        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo(new byte[] { 0x00, 0x00, 0x01, 0xBA }));
    }

    // ── waitForElement ────────────────────────────────────────────────────────

    [Test]
    public void WaitForElement_CallsWaitForElement_ReturnsElementId()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("waitForElement", new()
        {
            ["appId"] = "a1",
            ["selector"] = "[name=OK]",
            ["rootElementId"] = "",
            ["timeout"] = 5000
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo("el-wait-1"));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("WaitForElement"));
        Assert.That(_fake.Calls[0].Args[3], Is.EqualTo(5000u));
    }

    // ── waitForElements ──────────────────────────────────────────────────────

    [Test]
    public void WaitForElements_CallsWaitForElements_ReturnsIds()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("waitForElements", new()
        {
            ["appId"] = "a1",
            ["selector"] = "type=Button",
            ["rootElementId"] = "",
            ["timeout"] = 3000
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo(new[] { "el-w1", "el-w2" }));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("WaitForElements"));
    }

    // ── waitForResolvedValue ───────────────────────────────────────────────

    [Test]
    public void WaitForResolvedValue_ReturnsValue()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("waitForResolvedValue", new()
        {
            ["appId"] = "a1",
            ["selector"] = "//button/@class",
            ["rootElementId"] = "",
            ["timeout"] = 3000
        }));

        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Result, Is.EqualTo(_fake.ResolveValueResult));
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("WaitForResolvedValue"));
    }

    // ── waitForVanish ────────────────────────────────────────────────────────

    [Test]
    public void WaitForVanish_CallsWaitForVanish_ReturnsOk()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("waitForVanish", new()
        {
            ["appId"] = "a1",
            ["selector"] = "[name=Spinner]",
            ["rootElementId"] = "",
            ["timeout"] = 2000
        }));
        Assert.That(resp.Success, Is.True);
        Assert.That(_fake.Calls[0].Method, Is.EqualTo("WaitForVanish"));
    }

    // ── errorType propagation ────────────────────────────────────────────────

    [Test]
    public void NoMatchException_SetsErrorType()
    {
        _fake.ThrowOnNext = new NoMatchException("Not found within 5000ms.");
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("waitForElement", new()
        {
            ["appId"] = "a1",
            ["selector"] = "[name=X]",
            ["rootElementId"] = "",
            ["timeout"] = 5000
        }));
        Assert.That(resp.Success, Is.False);
        Assert.That(resp.ErrorType, Is.EqualTo("NoMatchException"));
    }

    [Test]
    public void UnwantedMatchException_SetsErrorType()
    {
        _fake.ThrowOnNext = new UnwantedMatchException("Still present after 2000ms.");
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("waitForVanish", new()
        {
            ["appId"] = "a1",
            ["selector"] = "[name=X]",
            ["rootElementId"] = "",
            ["timeout"] = 2000
        }));
        Assert.That(resp.Success, Is.False);
        Assert.That(resp.ErrorType, Is.EqualTo("UnwantedMatchException"));
    }

    [Test]
    public void AccessRestrictedException_SetsErrorType()
    {
        _fake.ThrowOnNext = new AccessRestrictedException("All matched elements belong to a different process.");
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("findElement", new()
        {
            ["appId"] = "a1",
            ["selector"] = "/Desktop",
            ["rootElementId"] = ""
        }));
        Assert.That(resp.Success, Is.False);
        Assert.That(resp.ErrorType, Is.EqualTo("AccessRestrictedException"));
    }

    [Test]
    public void GenericException_HasNullErrorType()
    {
        _fake.ThrowOnNext = new InvalidOperationException("boom");
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("click", new() { ["elementId"] = "e1" }));
        Assert.That(resp.Success, Is.False);
        Assert.That(resp.ErrorType, Is.Null);
    }

    // ── unknown command ──────────────────────────────────────────────────────

    [Test]
    public void UnknownCommand_ReturnsFail()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("bogus"));
        Assert.That(resp.Success, Is.False);
        Assert.That(resp.Error, Does.Contain("Unknown command"));
        Assert.That(resp.Error, Does.Contain("bogus"));
    }

    // ── exception handling ───────────────────────────────────────────────────

    [Test]
    public void Exception_InHandler_ReturnsFail()
    {
        _fake.ThrowOnNext = new InvalidOperationException("Element not found");
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("click", new() { ["elementId"] = "bad" }));
        Assert.That(resp.Success, Is.False);
        Assert.That(resp.Error, Is.EqualTo("Element not found"));
    }

    [Test]
    public void Exception_PreservesRequestId()
    {
        _fake.ThrowOnNext = new InvalidOperationException("boom");
        var req = MakeRequest("click", new() { ["elementId"] = "e1" });
        req.Id = "my-id";
        var resp = WsServer.ProcessRequest(_fake, req);
        Assert.That(resp.Id, Is.EqualTo("my-id"));
        Assert.That(resp.Success, Is.False);
    }

    // ── response IDs ─────────────────────────────────────────────────────────

    [Test]
    public void SuccessResponse_PreservesRequestId()
    {
        var req = MakeRequest("close", new() { ["appId"] = "a1" });
        req.Id = "abc-123";
        var resp = WsServer.ProcessRequest(_fake, req);
        Assert.That(resp.Id, Is.EqualTo("abc-123"));
    }
}
