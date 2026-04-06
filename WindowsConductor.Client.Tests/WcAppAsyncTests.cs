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
    public async Task ScreenshotBytesAsync_ReturnsRawBytes()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        _transport.Enqueue(pngBytes);
        var result = await _app.ScreenshotBytesAsync();
        Assert.That(result, Is.EqualTo(pngBytes));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("screenshotApp"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Not.Contain("\"path\""));
    }

    [Test]
    public async Task ScreenshotAsync_ReturnsBitmap()
    {
        var bitmap = new SkiaSharp.SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SkiaSharp.SKColors.Red);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        var pngBytes = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).ToArray();
        bitmap.Dispose();

        _transport.Enqueue(pngBytes);
        using var result = await _app.ScreenshotAsync();
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Width, Is.EqualTo(1));
    }

    // ── StartRecordingAsync ──────────────────────────────────────────────────

    [Test]
    public async Task StartRecordingAsync_SendsCommand()
    {
        await _app.StartRecordingAsync();
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("startRecording"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"appId\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Not.Contain("\"ffmpegPath\""));
    }

    // ── StopRecordingAsync ───────────────────────────────────────────────────

    [Test]
    public async Task StopRecordingAsync_ReturnsBytes()
    {
        var videoBytes = new byte[] { 0x00, 0x00, 0x01, 0xBA };
        _transport.Enqueue(videoBytes);
        var result = await _app.StopRecordingAsync();
        Assert.That(result, Is.EqualTo(videoBytes));
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
    public async Task DisposeAsync_Launched_SendsClose()
    {
        await _app.DisposeAsync();
        Assert.That(_transport.Calls, Has.Count.EqualTo(1));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("close"));
    }

    [Test]
    public async Task DisposeAsync_Attached_DoesNotSendClose()
    {
        var attached = new WcApp("app-99", _transport, ownsApp: false);
        await attached.DisposeAsync();
        Assert.That(_transport.Calls, Is.Empty);
    }

    [Test]
    public async Task DisposeAsync_Launched_OwnsAppIsTrue()
    {
        var launched = new WcApp("app-1", _transport);
        Assert.That(launched.OwnsApp, Is.True);
        await launched.DisposeAsync();
        Assert.That(_transport.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DisposeAsync_Attached_OwnsAppIsFalse()
    {
        var attached = new WcApp("app-1", _transport, ownsApp: false);
        Assert.That(attached.OwnsApp, Is.False);
        await attached.DisposeAsync();
        Assert.That(_transport.Calls, Is.Empty);
    }

    [Test]
    public async Task CloseAsync_Attached_StillSendsClose()
    {
        var attached = new WcApp("app-99", _transport, ownsApp: false);
        await attached.CloseAsync();
        Assert.That(_transport.Calls, Has.Count.EqualTo(1));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("close"));
    }
}
