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
    public async Task TypeAsync_SendsTextAndElementId()
    {
        await _element.TypeAsync("hello world");
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("typeText"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"text\":\"hello world\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
    }

    [Test]
    public async Task FocusAsync_SendsCorrectCommand()
    {
        await _element.FocusAsync();
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("focus"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
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
    public async Task ScreenshotAsync_ReturnsPath()
    {
        _transport.Enqueue("/tmp/shot.png");
        var path = await _element.ScreenshotAsync("/tmp/shot.png");
        Assert.That(path, Is.EqualTo("/tmp/shot.png"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("screenshot"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"path\":\"/tmp/shot.png\""));
    }

    [Test]
    public async Task ScreenshotAsync_NullPath_SendsEmpty()
    {
        _transport.Enqueue("/tmp/auto.png");
        await _element.ScreenshotAsync();
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"path\":\"\""));
    }
}