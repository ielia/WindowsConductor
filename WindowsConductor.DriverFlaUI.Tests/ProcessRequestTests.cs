using System.Text.Json;
using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

internal sealed class FakeAppOperations : IAppOperations
{
    public record Call(string Method, object?[] Args);
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
    public string ScreenshotElementResult { get; set; } = "/tmp/el.png";
    public string ScreenshotAppResult { get; set; } = "/tmp/app.png";
    public string StartRecordingResult { get; set; } = "/tmp/video.mp4";
    public string StopRecordingResult { get; set; } = "/tmp/video.mp4";
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

    public void Click(string elementId) => Record("Click", elementId);
    public void DoubleClick(string elementId) => Record("DoubleClick", elementId);
    public void RightClick(string elementId) => Record("RightClick", elementId);
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

    public bool IsEnabled(string elementId) { Record("IsEnabled", elementId); return IsEnabledResult; }
    public bool IsVisible(string elementId) { Record("IsVisible", elementId); return IsVisibleResult; }
    public void Focus(string elementId) => Record("Focus", elementId);

    public string GetWindowTitle(string appId) { Record("GetWindowTitle", appId); return GetWindowTitleResult; }
    public object GetBoundingRect(string elementId) { Record("GetBoundingRect", elementId); return GetBoundingRectResult; }
    public object GetWindowBoundingRect(string appId) { Record("GetWindowBoundingRect", appId); return GetWindowBoundingRectResult; }

    public string ScreenshotElement(string elementId, string? path)
    { Record("ScreenshotElement", elementId, path); return ScreenshotElementResult; }

    public string ScreenshotApp(string appId, string? path)
    { Record("ScreenshotApp", appId, path); return ScreenshotAppResult; }

    public string StartRecording(string appId, string? path, string? ffmpegPath)
    { Record("StartRecording", appId, path, ffmpegPath); return StartRecordingResult; }

    public string StopRecording(string appId) { Record("StopRecording", appId); return StopRecordingResult; }

    public string[] FindElementsAtPointResult { get; set; } = { "el-p1", "el-p2" };
    public string[] FindElementsAtPoint(string appId, double x, double y, string? rootElementId = null)
    { Record("FindElementsAtPoint", appId, x, y, rootElementId); return FindElementsAtPointResult; }

    public string WaitForElementResult { get; set; } = "el-wait-1";
    public string[] WaitForElementsResult { get; set; } = { "el-w1", "el-w2" };

    public string WaitForElement(string appId, string selector, string? rootElementId, uint timeout)
    { Record("WaitForElement", appId, selector, rootElementId, timeout); return WaitForElementResult; }

    public string[] WaitForElements(string appId, string selector, string? rootElementId, uint timeout)
    { Record("WaitForElements", appId, selector, rootElementId, timeout); return WaitForElementsResult; }

    public void WaitForVanish(string appId, string selector, string? rootElementId, uint timeout)
    { Record("WaitForVanish", appId, selector, rootElementId, timeout); }
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
    public void Screenshot_ReturnsPath()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("screenshot", new()
        {
            ["elementId"] = "e1",
            ["path"] = "/tmp/shot.png"
        }));
        Assert.That(resp.Result, Is.EqualTo("/tmp/el.png"));
        Assert.That(_fake.Calls[0].Args[1], Is.EqualTo("/tmp/shot.png"));
    }

    // ── screenshotApp ────────────────────────────────────────────────────────

    [Test]
    public void ScreenshotApp_ReturnsPath()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("screenshotApp", new()
        {
            ["appId"] = "a1",
            ["path"] = "/tmp/app.png"
        }));
        Assert.That(resp.Result, Is.EqualTo("/tmp/app.png"));
    }

    // ── startRecording ───────────────────────────────────────────────────────

    [Test]
    public void StartRecording_ReturnsPath()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("startRecording", new()
        {
            ["appId"] = "a1",
            ["path"] = "/tmp/v.mp4",
            ["ffmpegPath"] = "/usr/bin/ffmpeg"
        }));
        Assert.That(resp.Result, Is.EqualTo("/tmp/video.mp4"));
        Assert.That(_fake.Calls[0].Args[2], Is.EqualTo("/usr/bin/ffmpeg"));
    }

    // ── stopRecording ────────────────────────────────────────────────────────

    [Test]
    public void StopRecording_ReturnsPath()
    {
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("stopRecording", new() { ["appId"] = "a1" }));
        Assert.That(resp.Result, Is.EqualTo("/tmp/video.mp4"));
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
    public void ElementNotFoundException_SetsErrorType()
    {
        _fake.ThrowOnNext = new ElementNotFoundException("Not found within 5000ms.");
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("waitForElement", new()
        {
            ["appId"] = "a1",
            ["selector"] = "[name=X]",
            ["rootElementId"] = "",
            ["timeout"] = 5000
        }));
        Assert.That(resp.Success, Is.False);
        Assert.That(resp.ErrorType, Is.EqualTo("ElementNotFoundException"));
    }

    [Test]
    public void UnwantedElementFoundException_SetsErrorType()
    {
        _fake.ThrowOnNext = new UnwantedElementFoundException("Still present after 2000ms.");
        var resp = WsServer.ProcessRequest(_fake, MakeRequest("waitForVanish", new()
        {
            ["appId"] = "a1",
            ["selector"] = "[name=X]",
            ["rootElementId"] = "",
            ["timeout"] = 2000
        }));
        Assert.That(resp.Success, Is.False);
        Assert.That(resp.ErrorType, Is.EqualTo("UnwantedElementFoundException"));
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
        _fake.ThrowOnNext = new Exception("boom");
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
