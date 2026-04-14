using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

[TestFixture]
[Category("Unit")]
public class WcAppTests
{
    private static WcApp MakeApp() => new("app1", null!);

    // ── Factory methods produce correct selectors ────────────────────────────

    [Test]
    public void Locator_CreatesLocatorWithSelector()
    {
        var app = MakeApp();
        var loc = app.Locator("[name=OK]");
        Assert.That(loc.ToString(), Is.EqualTo("WcLocator([name=OK])"));
    }

    [Test]
    public void GetByAutomationId_ProducesCorrectSelector()
    {
        var app = MakeApp();
        var loc = app.GetByAutomationId("btn1");
        Assert.That(loc.ToString(), Does.Contain("[automationid=btn1]"));
    }

    [Test]
    public void GetByName_ProducesCorrectSelector()
    {
        var app = MakeApp();
        var loc = app.GetByName("Cancel");
        Assert.That(loc.ToString(), Does.Contain("[name=Cancel]"));
    }

    [Test]
    public void GetByText_ProducesCorrectSelector()
    {
        var app = MakeApp();
        var loc = app.GetByText("Submit");
        Assert.That(loc.ToString(), Does.Contain("text=Submit"));
    }

    [Test]
    public void GetByXPath_AbsolutePath_KeepsAsIs()
    {
        var app = MakeApp();
        var loc = app.GetByXPath("//Button[@Name='OK']");
        Assert.That(loc.ToString(), Does.Contain("//Button[@Name='OK']"));
    }

    [Test]
    public void GetByXPath_RelativePath_PrependDoubleSlash()
    {
        var app = MakeApp();
        var loc = app.GetByXPath("Button[@Name='OK']");
        Assert.That(loc.ToString(), Does.Contain("//Button[@Name='OK']"));
    }

    [Test]
    public void GetByControlType_ProducesCorrectSelector()
    {
        var app = MakeApp();
        var loc = app.GetByControlType("Edit");
        Assert.That(loc.ToString(), Does.Contain("type=Edit"));
    }

    // ── Locator has no parent ────────────────────────────────────────────────

    [Test]
    public void Locator_FromApp_HasNoParent()
    {
        var app = MakeApp();
        var loc = app.GetByName("OK");
        // No parent means ToString won't have " > " separator at root
        Assert.That(loc.ToString(), Is.EqualTo("WcLocator([name=OK])"));
    }

    // ── Custom attributes are now accepted ────────────────────────────────────

    [Test]
    public void Locator_CustomAttribute_DoesNotThrow()
    {
        var app = MakeApp();
        Assert.DoesNotThrow(() => app.Locator("[custom=foo]"));
    }

    [Test]
    public void GetByAutomationId_EscapesBrackets()
    {
        var app = MakeApp();
        var loc = app.GetByAutomationId("a]b");
        Assert.That(loc.ToString(), Does.Contain("a\\]b"));
    }

    // ── GetAtAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task GetAtAsync_SendsFindElementsAtPoint()
    {
        var transport = new FakeTransport();
        transport.Enqueue(new[] { "el-1", "el-2" });
        var app = new WcApp("app1", transport);

        var elements = await app.GetAtAsync(50.5, 100.0);

        Assert.That(elements, Has.Count.EqualTo(2));
        Assert.That(elements[0].ElementId, Is.EqualTo("el-1"));
        Assert.That(elements[1].ElementId, Is.EqualTo("el-2"));
        Assert.That(transport.Calls[0].Command, Is.EqualTo("findElementsAtPoint"));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"appId\":\"app1\""));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"x\":50.5"));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"y\":100"));
    }

    [Test]
    public async Task GetAtAsync_EmptyResult_ReturnsEmptyList()
    {
        var transport = new FakeTransport();
        transport.Enqueue(Array.Empty<string>());
        var app = new WcApp("app1", transport);

        var elements = await app.GetAtAsync(0, 0);

        Assert.That(elements, Is.Empty);
    }

    // ── GetFrontAtAsync ───────────────────────────────────────────────────────

    [Test]
    public async Task GetFrontAtAsync_SendsFindFrontElementAtPoint()
    {
        var transport = new FakeTransport();
        transport.Enqueue("el-front");
        var app = new WcApp("app1", transport);

        var element = await app.GetFrontAtAsync(50.5, 100.0);

        Assert.That(element.ElementId, Is.EqualTo("el-front"));
        Assert.That(transport.Calls[0].Command, Is.EqualTo("findFrontElementAtPoint"));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"x\":50.5"));
        Assert.That(transport.Calls[0].ParamsJson, Does.Contain("\"y\":100"));
    }

    // ── Internal properties ──────────────────────────────────────────────────

    [Test]
    public void AppId_ReturnsConstructorValue()
    {
        var app = new WcApp("my-app-id", null!);
        Assert.That(app.AppId, Is.EqualTo("my-app-id"));
    }
}
