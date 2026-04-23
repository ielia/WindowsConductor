using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

[TestFixture]
[Category("Unit")]
public class WcElementAsyncTests
{
    private FakeTransport _transport = null!;
    private WcElement _element = null!;

    [SetUp]
    public void SetUp()
    {
        _transport = new FakeTransport();
        _element = new WcElement("el-123", _transport);
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    [Test]
    public async Task ClickAsync_SendsCorrectCommand()
    {
        await _element.ClickAsync();
        Assert.That(_transport.Calls, Has.Count.EqualTo(1));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("click"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
    }

    [Test]
    public async Task DoubleClickAsync_SendsCorrectCommand()
    {
        await _element.DoubleClickAsync();
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("doubleClick"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
    }

    [Test]
    public async Task RightClickAsync_SendsCorrectCommand()
    {
        await _element.RightClickAsync();
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("rightClick"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
    }

    [Test]
    public async Task HoverAsync_SendsCorrectCommand()
    {
        await _element.HoverAsync();
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("hover"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
    }

    [Test]
    public async Task TypeAsync_SendsTextAndElementId()
    {
        await _element.TypeAsync("hello world");
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("typeText"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"text\":\"hello world\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"modifiers\":0"));
    }

    [Test]
    public async Task TypeAsync_WithModifiers_SendsModifiersBitmask()
    {
        await _element.TypeAsync("a", KeyModifiers.Ctrl | KeyModifiers.Shift);
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"text\":\"a\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"modifiers\":3"));
    }

    [Test]
    public async Task FocusAsync_SendsCorrectCommand()
    {
        await _element.FocusAsync();
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("focus"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
    }

    // ── Parent ───────────────────────────────────────────────────────────────

    [Test]
    public async Task ParentAsync_ReturnsParentElement()
    {
        _transport.Enqueue("parent-el-1");
        var parent = await _element.ParentAsync();
        Assert.That(parent, Is.Not.Null);
        Assert.That(parent!.ElementId, Is.EqualTo("parent-el-1"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getParent"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
    }

    [Test]
    public async Task ParentAsync_NullResult_ReturnsNull()
    {
        _transport.Enqueue(null);
        var parent = await _element.ParentAsync();
        Assert.That(parent, Is.Null);
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    [Test]
    public async Task GetTextAsync_ReturnsDriverValue()
    {
        _transport.Enqueue("Hello World");
        var text = await _element.GetTextAsync();
        Assert.That(text, Is.EqualTo("Hello World"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getText"));
    }

    [Test]
    public async Task GetTextAsync_NullResult_ReturnsEmpty()
    {
        _transport.Enqueue(null);
        var text = await _element.GetTextAsync();
        Assert.That(text, Is.EqualTo(""));
    }

    [Test]
    public async Task GetAttributeAsync_ReturnsDriverValue()
    {
        _transport.Enqueue("btn-class");
        var val = await _element.GetAttributeAsync("classname");
        Assert.That(val, Is.EqualTo("btn-class"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getAttribute"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"attribute\":\"classname\""));
    }

    [Test]
    public async Task GetAttributesAsync_ReturnsDictionary()
    {
        _transport.Enqueue(new { name = "OK", classname = "Button" });
        var attrs = await _element.GetAttributesAsync();
        Assert.That(attrs["name"], Is.EqualTo("OK"));
        Assert.That(attrs["classname"], Is.EqualTo("Button"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getAttributes"));
    }

    [Test]
    public async Task IsEnabledAsync_True()
    {
        _transport.Enqueue(true);
        Assert.That(await _element.IsEnabledAsync(), Is.True);
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("isEnabled"));
    }

    [Test]
    public async Task IsEnabledAsync_False()
    {
        _transport.Enqueue(false);
        Assert.That(await _element.IsEnabledAsync(), Is.False);
    }

    [Test]
    public async Task IsVisibleAsync_True()
    {
        _transport.Enqueue(true);
        Assert.That(await _element.IsVisibleAsync(), Is.True);
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("isVisible"));
    }

    [Test]
    public async Task IsVisibleAsync_False()
    {
        _transport.Enqueue(false);
        Assert.That(await _element.IsVisibleAsync(), Is.False);
    }

    [Test]
    public async Task GetBoundingRectAsync_ReturnsRect()
    {
        _transport.Enqueue(new { x = 10.0, y = 20.0, width = 300.0, height = 400.0 });
        var rect = await _element.GetBoundingRectAsync();
        Assert.That(rect, Is.EqualTo(new BoundingRect(10, 20, 300, 400)));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getBoundingRect"));
    }

    // ── Screenshot ───────────────────────────────────────────────────────────

    [Test]
    public async Task ScreenshotBytesAsync_ReturnsRawBytes()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        _transport.Enqueue(pngBytes);
        var result = await _element.ScreenshotBytesAsync();
        Assert.That(result, Is.EqualTo(pngBytes));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("screenshot"));
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
        using var result = await _element.ScreenshotAsync();
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Width, Is.EqualTo(1));
        Assert.That(result.Height, Is.EqualTo(1));
    }
}