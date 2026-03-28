using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

[TestFixture]
[Category("Unit")]
public class WcLocatorAsyncTests
{
    private FakeTransport _transport = null!;

    [SetUp]
    public void SetUp()
    {
        _transport = new FakeTransport();
    }

    private WcLocator MakeLocator(string selector, WcLocator? parent = null) =>
        new("app1", selector, _transport, parent);

    // ── GetElementAsync ──────────────────────────────────────────────────────

    [Test]
    public async Task GetElementAsync_ReturnsElement()
    {
        _transport.Enqueue("elem-id-1");
        var el = await MakeLocator("[name=OK]").GetElementAsync();
        Assert.That(el.ElementId, Is.EqualTo("elem-id-1"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("findElement"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"selector\":\"[name=OK]\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"appId\":\"app1\""));
    }

    [Test]
    public void GetElementAsync_NullResult_ThrowsWcException()
    {
        _transport.Enqueue(null);
        Assert.ThrowsAsync<WcException>(async () =>
            await MakeLocator("[name=OK]").GetElementAsync());
    }

    [Test]
    public async Task GetElementAsync_NoParent_SendsNullRootElementId()
    {
        _transport.Enqueue("elem-1");
        await MakeLocator("[name=OK]").GetElementAsync();
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"rootElementId\":null"));
    }

    // ── GetElementAsync with parent chaining ─────────────────────────────────

    [Test]
    public async Task GetElementAsync_WithParent_ResolvesParentFirst()
    {
        _transport.Enqueue("parent-el");   // parent's findElement
        _transport.Enqueue("child-el");    // child's findElement
        var parent = MakeLocator("type=Window");
        var child = MakeLocator("[name=OK]", parent);
        var el = await child.GetElementAsync();

        Assert.That(_transport.Calls, Has.Count.EqualTo(2));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("findElement"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"selector\":\"type=Window\""));
        Assert.That(_transport.Calls[1].Command, Is.EqualTo("findElement"));
        Assert.That(_transport.Calls[1].ParamsJson, Does.Contain("\"rootElementId\":\"parent-el\""));
        Assert.That(el.ElementId, Is.EqualTo("child-el"));
    }

    [Test]
    public async Task GetElementAsync_ThreeLevelChain_ResolvesInOrder()
    {
        _transport.Enqueue("gp-el");
        _transport.Enqueue("p-el");
        _transport.Enqueue("c-el");

        var chain = MakeLocator("type=Window")
            .GetByControlType("Panel")
            .GetByName("OK");

        var el = await chain.GetElementAsync();
        Assert.That(_transport.Calls, Has.Count.EqualTo(3));
        Assert.That(el.ElementId, Is.EqualTo("c-el"));
    }

    // ── Parent chaining ──────────────────────────────────────────────────────

    [Test]
    public async Task Parent_ResolvesElementThenNavigatesUp()
    {
        _transport.Enqueue("btn-el");    // findElement for Button
        _transport.Enqueue("parent-el"); // findElement for /..

        var locator = MakeLocator("type=Button").Parent();
        var el = await locator.GetElementAsync();

        Assert.That(_transport.Calls, Has.Count.EqualTo(2));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"selector\":\"type=Button\""));
        Assert.That(_transport.Calls[1].ParamsJson, Does.Contain("\"selector\":\"/..\""));
        Assert.That(_transport.Calls[1].ParamsJson, Does.Contain("\"rootElementId\":\"btn-el\""));
        Assert.That(el.ElementId, Is.EqualTo("parent-el"));
    }

    // ── GetAllElementsAsync ──────────────────────────────────────────────────

    [Test]
    public async Task GetAllElementsAsync_ReturnsMultipleElements()
    {
        _transport.Enqueue(new[] { "el-1", "el-2", "el-3" });
        var elements = await MakeLocator("type=Button").GetAllElementsAsync();
        Assert.That(elements, Has.Count.EqualTo(3));
        Assert.That(elements[0].ElementId, Is.EqualTo("el-1"));
        Assert.That(elements[2].ElementId, Is.EqualTo("el-3"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("findElements"));
    }

    [Test]
    public async Task GetAllElementsAsync_EmptyResult_ReturnsEmptyList()
    {
        _transport.Enqueue(Array.Empty<string>());
        var elements = await MakeLocator("type=Button").GetAllElementsAsync();
        Assert.That(elements, Is.Empty);
    }

    [Test]
    public async Task GetAllElementsAsync_WithParent_ResolvesParentFirst()
    {
        _transport.Enqueue("parent-el");
        _transport.Enqueue(new[] { "c1", "c2" });

        var chain = MakeLocator("type=Window").GetByControlType("Button");
        var elements = await chain.GetAllElementsAsync();

        Assert.That(_transport.Calls, Has.Count.EqualTo(2));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("findElement"));
        Assert.That(_transport.Calls[1].Command, Is.EqualTo("findElements"));
        Assert.That(_transport.Calls[1].ParamsJson, Does.Contain("\"rootElementId\":\"parent-el\""));
        Assert.That(elements, Has.Count.EqualTo(2));
    }

    // ── Delegated actions ────────────────────────────────────────────────────

    [Test]
    public async Task ClickAsync_ResolvesAndClicks()
    {
        _transport.Enqueue("el-1");  // findElement
        // click returns default
        await MakeLocator("[name=OK]").ClickAsync();
        Assert.That(_transport.Calls, Has.Count.EqualTo(2));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("findElement"));
        Assert.That(_transport.Calls[1].Command, Is.EqualTo("click"));
    }

    [Test]
    public async Task DoubleClickAsync_ResolvesAndDoubleClicks()
    {
        _transport.Enqueue("el-1");
        await MakeLocator("[name=OK]").DoubleClickAsync();
        Assert.That(_transport.Calls[1].Command, Is.EqualTo("doubleClick"));
    }

    [Test]
    public async Task RightClickAsync_ResolvesAndRightClicks()
    {
        _transport.Enqueue("el-1");
        await MakeLocator("[name=OK]").RightClickAsync();
        Assert.That(_transport.Calls[1].Command, Is.EqualTo("rightClick"));
    }

    [Test]
    public async Task TypeAsync_ResolvesAndTypes()
    {
        _transport.Enqueue("el-1");
        await MakeLocator("[name=OK]").TypeAsync("hello");
        Assert.That(_transport.Calls[1].Command, Is.EqualTo("typeText"));
        Assert.That(_transport.Calls[1].ParamsJson, Does.Contain("\"text\":\"hello\""));
    }

    [Test]
    public async Task FocusAsync_ResolvesAndFocuses()
    {
        _transport.Enqueue("el-1");
        await MakeLocator("[name=OK]").FocusAsync();
        Assert.That(_transport.Calls[1].Command, Is.EqualTo("focus"));
    }

    // ── Delegated queries ────────────────────────────────────────────────────

    [Test]
    public async Task GetTextAsync_ResolvesAndReturnsText()
    {
        _transport.Enqueue("el-1");
        _transport.Enqueue("Some text");
        var text = await MakeLocator("[name=OK]").GetTextAsync();
        Assert.That(text, Is.EqualTo("Some text"));
        Assert.That(_transport.Calls[1].Command, Is.EqualTo("getText"));
    }

    [Test]
    public async Task GetAttributeAsync_ResolvesAndReturnsAttribute()
    {
        _transport.Enqueue("el-1");
        _transport.Enqueue("MyClass");
        var val = await MakeLocator("[name=OK]").GetAttributeAsync("classname");
        Assert.That(val, Is.EqualTo("MyClass"));
    }

    [Test]
    public async Task GetAttributesAsync_ResolvesAndReturnsDictionary()
    {
        _transport.Enqueue("el-1");
        _transport.Enqueue(new { name = "OK", controltype = "Button" });
        var attrs = await MakeLocator("[name=OK]").GetAttributesAsync();
        Assert.That(attrs["name"], Is.EqualTo("OK"));
        Assert.That(_transport.Calls[1].Command, Is.EqualTo("getAttributes"));
    }

    [Test]
    public async Task IsEnabledAsync_ResolvesAndReturnsValue()
    {
        _transport.Enqueue("el-1");
        _transport.Enqueue(true);
        Assert.That(await MakeLocator("[name=OK]").IsEnabledAsync(), Is.True);
    }

    [Test]
    public async Task IsVisibleAsync_ResolvesAndReturnsValue()
    {
        _transport.Enqueue("el-1");
        _transport.Enqueue(false);
        Assert.That(await MakeLocator("[name=OK]").IsVisibleAsync(), Is.False);
    }

    [Test]
    public async Task GetBoundingRectAsync_ResolvesAndReturnsRect()
    {
        _transport.Enqueue("el-1");
        _transport.Enqueue(new { x = 5.0, y = 10.0, width = 50.0, height = 25.0 });
        var rect = await MakeLocator("[name=OK]").GetBoundingRectAsync();
        Assert.That(rect, Is.EqualTo(new BoundingRect(5, 10, 50, 25)));
    }

    [Test]
    public async Task ScreenshotAsync_ResolvesAndReturnsPath()
    {
        _transport.Enqueue("el-1");
        _transport.Enqueue("/tmp/shot.png");
        var path = await MakeLocator("[name=OK]").ScreenshotAsync("/tmp/shot.png");
        Assert.That(path, Is.EqualTo("/tmp/shot.png"));
    }

    // ── Chained actions resolve full hierarchy ───────────────────────────────

    [Test]
    public async Task ChainedClick_ResolvesEntireHierarchyThenClicks()
    {
        _transport.Enqueue("window-el");
        _transport.Enqueue("panel-el");
        _transport.Enqueue("btn-el");
        // click

        await MakeLocator("type=Window")
            .GetByControlType("Panel")
            .GetByName("OK")
            .ClickAsync();

        Assert.That(_transport.Calls, Has.Count.EqualTo(4));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"selector\":\"type=Window\""));
        Assert.That(_transport.Calls[1].ParamsJson, Does.Contain("\"rootElementId\":\"window-el\""));
        Assert.That(_transport.Calls[2].ParamsJson, Does.Contain("\"rootElementId\":\"panel-el\""));
        Assert.That(_transport.Calls[3].Command, Is.EqualTo("click"));
    }
}
