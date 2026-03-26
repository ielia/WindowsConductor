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

    // ── Invalid selector rejects early ───────────────────────────────────────

    [Test]
    public void Locator_InvalidSelector_Throws()
    {
        var app = MakeApp();
        Assert.Throws<ArgumentException>(() => app.Locator("[invalid=foo]"));
    }

    [Test]
    public void GetByAutomationId_EscapesBrackets()
    {
        var app = MakeApp();
        var loc = app.GetByAutomationId("a]b");
        Assert.That(loc.ToString(), Does.Contain("a\\]b"));
    }

    // ── Internal properties ──────────────────────────────────────────────────

    [Test]
    public void AppId_ReturnsConstructorValue()
    {
        var app = new WcApp("my-app-id", null!);
        Assert.That(app.AppId, Is.EqualTo("my-app-id"));
    }
}
