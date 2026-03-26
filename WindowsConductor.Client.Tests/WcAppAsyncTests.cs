using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

[TestFixture]
[Category("Unit")]
public class WcAppAsyncTests
{
    private FakeTransport _transport = null!;
    private WcApp _app = null!;

    [SetUp]
    public void SetUp()
    {
        _transport = new FakeTransport();
        _app = new WcApp("app-42", _transport);
    }

    // ── GetTitleAsync ────────────────────────────────────────────────────────

    [Test]
    public async Task GetTitleAsync_ReturnsTitle()
    {
        _transport.Enqueue("Calculator");
        var title = await _app.GetTitleAsync();
        Assert.That(title, Is.EqualTo("Calculator"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getWindowTitle"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"appId\":\"app-42\""));
    }

    [Test]
    public async Task GetTitleAsync_NullResult_ReturnsEmpty()
    {
        _transport.Enqueue(null);
        var title = await _app.GetTitleAsync();
        Assert.That(title, Is.EqualTo(""));
    }

    // ── ScreenshotAsync ──────────────────────────────────────────────────────

    [Test]
    public async Task ScreenshotAsync_WithPath_ReturnsPath()
    {
        _transport.Enqueue("/tmp/app.png");
        var path = await _app.ScreenshotAsync("/tmp/app.png");
        Assert.That(path, Is.EqualTo("/tmp/app.png"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("screenshotApp"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"path\":\"/tmp/app.png\""));
    }

    [Test]
    public async Task ScreenshotAsync_NullPath_SendsEmpty()
    {
        _transport.Enqueue("/tmp/auto.png");
        await _app.ScreenshotAsync();
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"path\":\"\""));
    }

    // ── StartRecordingAsync ──────────────────────────────────────────────────

    [Test]
    public async Task StartRecordingAsync_ReturnsPath()
    {
        _transport.Enqueue("/tmp/video.mp4");
        var path = await _app.StartRecordingAsync("/tmp/video.mp4", "/usr/bin/ffmpeg");
        Assert.That(path, Is.EqualTo("/tmp/video.mp4"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("startRecording"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"path\":\"/tmp/video.mp4\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"ffmpegPath\":\"/usr/bin/ffmpeg\""));
    }

    [Test]
    public async Task StartRecordingAsync_NullArgs_SendsEmpty()
    {
        _transport.Enqueue("/tmp/auto.mp4");
        await _app.StartRecordingAsync();
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"path\":\"\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"ffmpegPath\":\"\""));
    }

    // ── StopRecordingAsync ───────────────────────────────────────────────────

    [Test]
    public async Task StopRecordingAsync_ReturnsPath()
    {
        _transport.Enqueue("/tmp/video.mp4");
        var path = await _app.StopRecordingAsync();
        Assert.That(path, Is.EqualTo("/tmp/video.mp4"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("stopRecording"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"appId\":\"app-42\""));
    }

    // ── CloseAsync ───────────────────────────────────────────────────────────

    [Test]
    public async Task CloseAsync_SendsCloseCommand()
    {
        await _app.CloseAsync();
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("close"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"appId\":\"app-42\""));
    }

    // ── DisposeAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task DisposeAsync_SendsClose()
    {
        await _app.DisposeAsync();
        Assert.That(_transport.Calls, Has.Count.EqualTo(1));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("close"));
    }
}
