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

    [Test]
    public async Task GetElementAsync_ReturnsSelf()
    {
        var result = await _element.GetElementAsync();
        Assert.That(result, Is.SameAs(_element));
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
    public async Task ScrollAsync_SendsCorrectCommand()
    {
        await _element.ScrollAsync(3);
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("scroll"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"lines\":3"));
    }

    [Test]
    public async Task ScrollAsync_Horizontal_SendsCorrectCommand()
    {
        await _element.ScrollAsync(-2, horizontal: true);
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("scroll"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"horizontal\":true"));
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
    public async Task GetAutomationIdAsync_SendsGetAttributeWithAutomationId()
    {
        _transport.Enqueue("txtInput");
        var val = await _element.GetAutomationIdAsync();
        Assert.That(val, Is.EqualTo("txtInput"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getAttribute"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"attribute\":\"AutomationId\""));
    }

    [Test]
    public async Task GetClassNameAsync_SendsGetAttributeWithClassName()
    {
        _transport.Enqueue("TextBlock");
        var val = await _element.GetClassNameAsync();
        Assert.That(val, Is.EqualTo("TextBlock"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getAttribute"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"attribute\":\"ClassName\""));
    }

    [Test]
    public async Task GetControlTypeAsync_SendsGetAttributeWithControlType()
    {
        _transport.Enqueue("Button");
        var val = await _element.GetControlTypeAsync();
        Assert.That(val, Is.EqualTo("Button"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getAttribute"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"attribute\":\"ControlType\""));
    }

    [Test]
    public async Task GetNameAsync_SendsGetAttributeWithName()
    {
        _transport.Enqueue("OK");
        var val = await _element.GetNameAsync();
        Assert.That(val, Is.EqualTo("OK"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getAttribute"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"attribute\":\"Name\""));
    }

    [Test]
    public async Task GetProcessIdAsync_SendsGetAttributeWithProcessId()
    {
        _transport.Enqueue("1234");
        var val = await _element.GetProcessIdAsync();
        Assert.That(val, Is.EqualTo("1234"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getAttribute"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"attribute\":\"ProcessId\""));
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
    public async Task SetAttributeAsync_SendsCorrectCommand()
    {
        _transport.Enqueue((object?)null);
        await _element.SetAttributeAsync("toggle_togglestate", "On");
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("setAttribute"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"attribute\":\"toggle_togglestate\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"value\":\"On\""));
    }

    [Test]
    public async Task WaitForElementAsync_NotStale_ReturnsSelf()
    {
        _transport.Enqueue(false); // isStale returns false
        var result = await _element.WaitForElementAsync(5000);
        Assert.That(result, Is.SameAs(_element));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("isStale"));
    }

    [Test]
    public void WaitForElementAsync_Stale_Throws()
    {
        _transport.Enqueue(true); // isStale returns true
        Assert.ThrowsAsync<NoMatchException>(() => _element.WaitForElementAsync(5000));
    }

    [Test]
    public async Task IsStaleAsync_True()
    {
        _transport.Enqueue(true);
        Assert.That(await _element.IsStaleAsync(), Is.True);
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("isStale"));
    }

    [Test]
    public async Task IsStaleAsync_False()
    {
        _transport.Enqueue(false);
        Assert.That(await _element.IsStaleAsync(), Is.False);
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
    public async Task ExistsAsync_True()
    {
        _transport.Enqueue(true);
        Assert.That(await _element.ExistsAsync(), Is.True);
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("exists"));
    }

    [Test]
    public async Task ExistsAsync_False()
    {
        _transport.Enqueue(false);
        Assert.That(await _element.ExistsAsync(), Is.False);
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

    // ── WaitForVanish ───────────────────────────────────────────────────────

    [Test]
    public async Task WaitForVanishAsync_SendsCorrectCommand()
    {
        await _element.WaitForVanishAsync(3000);
        Assert.That(_transport.Calls, Has.Count.EqualTo(1));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("waitForElementVanish"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"timeout\":3000"));
    }

    // ── WaitForVisible ────────────────────────────────────────────────────────

    [Test]
    public async Task WaitForVisibleAsync_SendsCorrectCommand()
    {
        await _element.WaitForVisibleAsync(3000);
        Assert.That(_transport.Calls, Has.Count.EqualTo(1));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("waitForVisible"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"timeout\":3000"));
    }

    // ── WaitForHidden ────────────────────────────────────────────────────────

    [Test]
    public async Task WaitForHiddenAsync_SendsCorrectCommand()
    {
        await _element.WaitForHiddenAsync(3000);
        Assert.That(_transport.Calls, Has.Count.EqualTo(1));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("waitForHidden"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-123\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"timeout\":3000"));
    }

    // ── GetBy* shorthand factories ──────────────────────────────────────────

    [Test]
    public void GetByAutomationId_ReturnsLocator()
    {
        var elWithApp = new WcElement("el-1", _transport, "app-1");
        var locator = elWithApp.GetByAutomationId("myId");
        Assert.That(locator, Is.Not.Null);
        Assert.That(locator.ToString(), Does.Contain("[automationid=myId]"));
    }

    [Test]
    public void GetByName_ReturnsLocator()
    {
        var elWithApp = new WcElement("el-1", _transport, "app-1");
        var locator = elWithApp.GetByName("myName");
        Assert.That(locator.ToString(), Does.Contain("[name=myName]"));
    }

    [Test]
    public void GetByText_ReturnsLocator()
    {
        var elWithApp = new WcElement("el-1", _transport, "app-1");
        var locator = elWithApp.GetByText("hello");
        Assert.That(locator.ToString(), Does.Contain("text=hello"));
    }

    [Test]
    public void GetByXPath_ReturnsLocator()
    {
        var elWithApp = new WcElement("el-1", _transport, "app-1");
        var locator = elWithApp.GetByXPath("//Button");
        Assert.That(locator.ToString(), Does.Contain("//Button"));
    }

    [Test]
    public void GetByControlType_ReturnsLocator()
    {
        var elWithApp = new WcElement("el-1", _transport, "app-1");
        var locator = elWithApp.GetByControlType("Button");
        Assert.That(locator.ToString(), Does.Contain("type=Button"));
    }

    [Test]
    public void GetBy_WithoutAppId_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _element.GetByAutomationId("x"));
    }

    // ── GetAtAsync / GetFrontAtAsync ────────────────────────────────────────

    [Test]
    public async Task GetAtAsync_SendsCommand()
    {
        var elWithApp = new WcElement("el-1", _transport, "app-1");
        _transport.Enqueue(new[] { "hit-1", "hit-2" });
        var results = await elWithApp.GetAtAsync(100.0, 200.0);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("findElementsAtPoint"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"rootElementId\":\"el-1\""));
    }

    [Test]
    public async Task GetFrontAtAsync_SendsCommand()
    {
        var elWithApp = new WcElement("el-1", _transport, "app-1");
        _transport.Enqueue("front-1");
        var result = await elWithApp.GetFrontAtAsync(50.0, 75.0);
        Assert.That(result.ElementId, Is.EqualTo("front-1"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("findFrontElementAtPoint"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"rootElementId\":\"el-1\""));
    }
}