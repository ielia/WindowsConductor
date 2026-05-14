using System.Text.Json;
using NUnit.Framework;
using WindowsConductor.Client;
using WindowsConductor.MCP.Tools;

namespace WindowsConductor.MCP.Tests;

[TestFixture]
[Category("Unit")]
public class AppToolsTests
{
    private ConductorState _state = null!;
    private AppTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _state = new ConductorState();
        _tools = new AppTools(_state);
    }

    [TearDown]
    public async Task TearDown() => await _state.DisposeAsync();

    [Test]
    public void LaunchApp_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.LaunchApp("calc.exe"));
    }

    [Test]
    public void AttachApp_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.AttachApp("Calculator"));
    }

    [Test]
    public void CloseApp_UnknownId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.CloseApp("nonexistent"));
    }

    [Test]
    public void GetAppTitle_UnknownId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetAppTitle("nonexistent"));
    }

    [Test]
    public void ListApps_WhenEmpty_ReturnsMessage()
    {
        var result = _tools.ListApps();
        Assert.That(result, Is.EqualTo("No tracked applications."));
    }

    [Test]
    public void ListApps_WithApps_ReturnsIds()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));
        _state.TrackApp("app-2", new WcApp("app-2", transport));

        var result = _tools.ListApps();
        Assert.That(result, Does.Contain("app-1"));
        Assert.That(result, Does.Contain("app-2"));
    }

    [Test]
    public void ScreenshotApp_UnknownId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.ScreenshotApp("nonexistent"));
    }

    [Test]
    public void ScreenshotDesktop_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.ScreenshotDesktop());
    }

    [Test]
    public void FindElements_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.FindElements("app-1", "[name=OK]"));
    }

    [Test]
    public void FindElement_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.FindElement("app-1", "[name=OK]"));
    }

    [Test]
    public void FindElements_UnknownAppId_Throws()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.FindElements("nonexistent", "[name=OK]"));
    }

    [Test]
    public async Task FindElements_WithTrackedApp_ReturnsElementIds()
    {
        var transport = new FakeTransport();
        var app = new WcApp("app-1", transport);
        _state.TrackApp("app-1", app);

        transport.Enqueue(new[] { "el-1", "el-2" });

        var result = await _tools.FindElements("app-1", "[name=OK]");
        var ids = JsonSerializer.Deserialize<string[]>(result);

        Assert.That(ids, Is.EqualTo(new[] { "el-1", "el-2" }));
    }

    [Test]
    public async Task FindElement_WithTrackedApp_ReturnsElementId()
    {
        var transport = new FakeTransport();
        var app = new WcApp("app-1", transport);
        _state.TrackApp("app-1", app);

        transport.Enqueue("el-1");

        var result = await _tools.FindElement("app-1", "[name=OK]");
        Assert.That(result, Is.EqualTo("el-1"));
    }

    [Test]
    public async Task CloseApp_WithTrackedApp_RemovesIt()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport, ownsApp: false));

        var result = await _tools.CloseApp("app-1");

        Assert.That(result, Does.Contain("app-1"));
        Assert.That(_state.AppIds, Does.Not.Contain("app-1"));
    }

    [Test]
    public async Task GetAppTitle_WithTrackedApp_ReturnsTitle()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue("Calculator");

        var result = await _tools.GetAppTitle("app-1");
        Assert.That(result, Is.EqualTo("Calculator"));
    }

    // ── Scoped locators ──────────────────────────────────────────────────────

    [Test]
    public void FindElementsWithin_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.FindElementsWithin("nonexistent", "root-1", "[name=OK]"));
    }

    [Test]
    public void FindElementWithin_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.FindElementWithin("nonexistent", "root-1", "[name=OK]"));
    }

    [Test]
    public async Task FindElementsWithin_ReturnsElementIds()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue(new[] { "child-1", "child-2" });

        var result = await _tools.FindElementsWithin("app-1", "root-1", "[name=OK]");
        var ids = JsonSerializer.Deserialize<string[]>(result);

        Assert.That(ids, Is.EqualTo(new[] { "child-1", "child-2" }));
        Assert.That(transport.Calls[0].Command, Is.EqualTo("findElements"));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"rootElementId\":\"root-1\""));
    }

    [Test]
    public async Task FindElementWithin_ReturnsElementId()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue("child-1");

        var result = await _tools.FindElementWithin("app-1", "root-1", "[name=OK]");

        Assert.That(result, Is.EqualTo("child-1"));
        Assert.That(transport.Calls[0].Command, Is.EqualTo("findElement"));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"rootElementId\":\"root-1\""));
    }

    // ── Resolved value ───────────────────────────────────────────────────────

    [Test]
    public void ResolveValue_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.ResolveValue("nonexistent", "[name=OK]"));
    }

    [Test]
    public async Task ResolveValue_ReturnsStringValue()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue(new { type = "StringValue", value = "hello" });

        var result = await _tools.ResolveValue("app-1", "text=hello");
        var doc = JsonDocument.Parse(result);

        Assert.That(transport.Calls[0].Command, Is.EqualTo("resolveValue"));
        Assert.That(doc.RootElement.GetProperty("type").GetString(), Is.EqualTo("StringValue"));
        Assert.That(doc.RootElement.GetProperty("value").GetString(), Is.EqualTo("hello"));
    }

    [Test]
    public async Task ResolveValue_WithRootElement_PassesRootElementId()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue(new { type = "StringValue", value = "scoped" });

        await _tools.ResolveValue("app-1", "[name=OK]", "root-1");

        Assert.That(transport.Calls[0].Command, Is.EqualTo("resolveValue"));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"rootElementId\":\"root-1\""));
    }

    [Test]
    public async Task ResolveValue_ListValue_ReturnsItems()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue(new
        {
            type = "ListValue",
            items = new object[]
            {
                new { type = "StringValue", value = "item1", elementId = "el-1" },
                new { type = "StringValue", value = "item2", elementId = "el-2" }
            }
        });

        var result = await _tools.ResolveValue("app-1", "text=test");
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("type").GetString(), Is.EqualTo("list"));
        Assert.That(doc.RootElement.GetProperty("items").GetArrayLength(), Is.EqualTo(2));
    }

    // ── Hit-testing ─────────────────────────────────────────────────────────

    [Test]
    public void GetElementsAtPoint_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetElementsAtPoint("nonexistent", 100, 200));
    }

    [Test]
    public async Task GetElementsAtPoint_ReturnsElementIds()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue(new[] { "el-1", "el-2" });

        var result = await _tools.GetElementsAtPoint("app-1", 100, 200);
        var ids = JsonSerializer.Deserialize<string[]>(result);

        Assert.That(ids, Is.EqualTo(new[] { "el-1", "el-2" }));
        Assert.That(transport.Calls[0].Command, Is.EqualTo("findElementsAtPoint"));
    }

    [Test]
    public void GetFrontElementAtPoint_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetFrontElementAtPoint("nonexistent", 100, 200));
    }

    [Test]
    public async Task GetFrontElementAtPoint_ReturnsElementId()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue("el-front");

        var result = await _tools.GetFrontElementAtPoint("app-1", 150, 250);

        Assert.That(result, Is.EqualTo("el-front"));
        Assert.That(transport.Calls[0].Command, Is.EqualTo("findFrontElementAtPoint"));
    }

    // ── Video recording ─────────────────────────────────────────────────────

    [Test]
    public void StartRecording_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.StartRecording("nonexistent"));
    }

    [Test]
    public async Task StartRecording_SendsCommand()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        var result = await _tools.StartRecording("app-1");

        Assert.That(transport.Calls[0].Command, Is.EqualTo("startRecording"));
        Assert.That(result, Does.Contain("app-1"));
    }

    [Test]
    public void StopRecording_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.StopRecording("nonexistent"));
    }

    [Test]
    public async Task StopRecording_SavesFileAndReturnsPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wc-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            _state.VideoDir = tempDir;
            var transport = new FakeTransport();
            _state.TrackApp("app-1", new WcApp("app-1", transport));

            var videoBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            transport.Enqueue(Convert.ToBase64String(videoBytes));

            var result = await _tools.StopRecording("app-1");

            Assert.That(transport.Calls[0].Command, Is.EqualTo("stopRecording"));
            Assert.That(File.Exists(result), Is.True);
            Assert.That(File.ReadAllBytes(result), Is.EqualTo(videoBytes));
            Assert.That(result, Does.StartWith(Path.GetFullPath(tempDir)));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task StopRecording_WithRelativePath_CreatesSubdirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wc-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            _state.VideoDir = tempDir;
            var transport = new FakeTransport();
            _state.TrackApp("app-1", new WcApp("app-1", transport));

            transport.Enqueue(Convert.ToBase64String(new byte[] { 0xFF }));

            var result = await _tools.StopRecording("app-1", "session1/test.mp4");

            var expected = Path.GetFullPath(Path.Combine(tempDir, "session1", "test.mp4"));
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(File.Exists(result), Is.True);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void StopRecording_PathTraversal_Throws()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));
        transport.Enqueue(Convert.ToBase64String(new byte[] { 0xFF }));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.StopRecording("app-1", "../../../etc/evil.mp4"));
    }

    // ── ResolveVideoPath ────────────────────────────────────────────────────

    [Test]
    public void ResolveVideoPath_DefaultGeneratesTimestampedFile()
    {
        var path = _state.ResolveVideoPath(null);

        Assert.That(path, Does.StartWith(Path.GetFullPath(_state.VideoDir)));
        Assert.That(path, Does.EndWith(".mp4"));
        Assert.That(path, Does.Contain("recording-"));
    }

    [Test]
    public void ResolveVideoPath_TraversalThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => _state.ResolveVideoPath("../../escape.mp4"));
    }

    [Test]
    public void ResolveVideoPath_ValidRelative_ResolvesUnderRoot()
    {
        var path = _state.ResolveVideoPath("sub/dir/video.mp4");

        var expected = Path.GetFullPath(Path.Combine(_state.VideoDir, "sub", "dir", "video.mp4"));
        Assert.That(path, Is.EqualTo(expected));
    }

    // ── Screenshot file saving ──────────────────────────────────────────────

    [Test]
    public async Task ScreenshotApp_WithOutputPath_SavesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wc-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            _state.ScreenshotDir = tempDir;
            var transport = new FakeTransport();
            _state.TrackApp("app-1", new WcApp("app-1", transport));

            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
            transport.Enqueue(Convert.ToBase64String(pngBytes));

            var result = await _tools.ScreenshotApp("app-1", "test.png");

            Assert.That(File.Exists(result), Is.True);
            Assert.That(File.ReadAllBytes(result), Is.EqualTo(pngBytes));
            Assert.That(result, Does.StartWith(Path.GetFullPath(tempDir)));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ScreenshotApp_WithoutOutputPath_ReturnsBase64()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        transport.Enqueue(Convert.ToBase64String(pngBytes));

        var result = await _tools.ScreenshotApp("app-1");

        Assert.That(Convert.FromBase64String(result), Is.EqualTo(pngBytes));
    }

    [Test]
    public void ScreenshotApp_PathTraversal_Throws()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));
        transport.Enqueue(Convert.ToBase64String(new byte[] { 0xFF }));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.ScreenshotApp("app-1", "../../evil.png"));
    }

    // ── ResolveScreenshotPath ───────────────────────────────────────────────

    [Test]
    public void ResolveScreenshotPath_DefaultGeneratesTimestampedFile()
    {
        var path = _state.ResolveScreenshotPath(null);

        Assert.That(path, Does.StartWith(Path.GetFullPath(_state.ScreenshotDir)));
        Assert.That(path, Does.EndWith(".png"));
        Assert.That(path, Does.Contain("screenshot-"));
    }

    [Test]
    public void ResolveScreenshotPath_TraversalThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => _state.ResolveScreenshotPath("../../escape.png"));
    }

    [Test]
    public void ResolveScreenshotPath_ValidRelative_ResolvesUnderRoot()
    {
        var path = _state.ResolveScreenshotPath("sub/dir/shot.png");

        var expected = Path.GetFullPath(Path.Combine(_state.ScreenshotDir, "sub", "dir", "shot.png"));
        Assert.That(path, Is.EqualTo(expected));
    }

    // ── Wait operations ─────────────────────────────────────────────────────

    [Test]
    public void WaitForElement_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.WaitForElement("nonexistent", "[name=OK]", 5000));
    }

    [Test]
    public async Task WaitForElement_ReturnsElementId()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue("el-1");

        var result = await _tools.WaitForElement("app-1", "[name=OK]", 5000);

        Assert.That(result, Is.EqualTo("el-1"));
        Assert.That(transport.Calls[0].Command, Is.EqualTo("waitForElement"));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"timeout\":5000"));
    }

    [Test]
    public async Task WaitForElement_WithRoot_PassesRootElementId()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue("el-1");

        await _tools.WaitForElement("app-1", "[name=OK]", 5000, "root-1");

        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"rootElementId\":\"root-1\""));
    }

    [Test]
    public void WaitForElements_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.WaitForElements("nonexistent", "[name=OK]", 5000));
    }

    [Test]
    public async Task WaitForElements_ReturnsElementIds()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue(new[] { "el-1", "el-2" });

        var result = await _tools.WaitForElements("app-1", "type=Button", 3000);
        var ids = JsonSerializer.Deserialize<string[]>(result);

        Assert.That(ids, Is.EqualTo(new[] { "el-1", "el-2" }));
        Assert.That(transport.Calls[0].Command, Is.EqualTo("waitForElements"));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"timeout\":3000"));
    }

    [Test]
    public void WaitForResolvedValue_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.WaitForResolvedValue("nonexistent", "text=hello", 5000));
    }

    [Test]
    public async Task WaitForResolvedValue_ReturnsValue()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        transport.Enqueue(new { type = "StringValue", value = "found" });

        var result = await _tools.WaitForResolvedValue("app-1", "text=hello", 5000);
        var doc = JsonDocument.Parse(result);

        Assert.That(transport.Calls[0].Command, Is.EqualTo("waitForResolvedValue"));
        Assert.That(doc.RootElement.GetProperty("type").GetString(), Is.EqualTo("StringValue"));
        Assert.That(doc.RootElement.GetProperty("value").GetString(), Is.EqualTo("found"));
    }

    [Test]
    public void WaitForVanish_UnknownAppId_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.WaitForVanish("nonexistent", "[name=OK]", 5000));
    }

    [Test]
    public async Task WaitForVanish_ReturnsConfirmation()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));

        var result = await _tools.WaitForVanish("app-1", "[name=Loading]", 3000);

        Assert.That(result, Is.EqualTo("Element vanished."));
        Assert.That(transport.Calls[0].Command, Is.EqualTo("waitForVanish"));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"timeout\":3000"));
    }
}
