using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

[TestFixture]
[Category("Unit")]
public class WcLocatorTests
{
    // We can construct WcLocator with a null session for pure unit tests
    // (factory methods and ToString don't use the session).
    private static WcLocator MakeLocator(string selector, WcLocator? parent = null) =>
        new("app1", selector, null!, parent);

    // ── Constructor validation ───────────────────────────────────────────────

    [Test]
    public void Constructor_InvalidSelector_Throws()
    {
        Assert.Throws<ArgumentException>(() => MakeLocator("[invalid=foo]"));
    }

    [Test]
    public void Constructor_EmptySelector_Throws()
    {
        Assert.Throws<ArgumentException>(() => MakeLocator(""));
    }

    [Test]
    public void Constructor_ValidSelector_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => MakeLocator("[name=OK]"));
    }

    // ── Factory methods produce correct selectors ────────────────────────────

    [Test]
    public void GetByAutomationId_ProducesCorrectSelector()
    {
        var locator = MakeLocator("[name=root]");
        var child = locator.GetByAutomationId("btn1");
        Assert.That(child.ToString(), Does.Contain("[automationid=btn1]"));
    }

    [Test]
    public void GetByName_ProducesCorrectSelector()
    {
        var locator = MakeLocator("[name=root]");
        var child = locator.GetByName("OK");
        Assert.That(child.ToString(), Does.Contain("[name=OK]"));
    }

    [Test]
    public void GetByText_ProducesCorrectSelector()
    {
        var locator = MakeLocator("[name=root]");
        var child = locator.GetByText("Cancel");
        Assert.That(child.ToString(), Does.Contain("text=Cancel"));
    }

    [Test]
    public void GetByControlType_ProducesCorrectSelector()
    {
        var locator = MakeLocator("[name=root]");
        var child = locator.GetByControlType("Button");
        Assert.That(child.ToString(), Does.Contain("type=Button"));
    }

    [Test]
    public void GetByXPath_WithSlash_KeepsAsIs()
    {
        var locator = MakeLocator("[name=root]");
        var child = locator.GetByXPath("//Button[@Name='OK']");
        Assert.That(child.ToString(), Does.Contain("//Button[@Name='OK']"));
    }

    [Test]
    public void GetByXPath_WithoutSlash_PrependDoubleSlash()
    {
        var locator = MakeLocator("[name=root]");
        var child = locator.GetByXPath("Button[@Name='OK']");
        Assert.That(child.ToString(), Does.Contain("//Button[@Name='OK']"));
    }

    [Test]
    public void Locator_WithRawSelector_CreatesChild()
    {
        var parent = MakeLocator("[name=panel]");
        var child = parent.Locator("[automationid=btn]");
        Assert.That(child.ToString(), Does.Contain("[automationid=btn]"));
        Assert.That(child.ToString(), Does.Contain("[name=panel]"));
    }

    // ── Escaping ─────────────────────────────────────────────────────────────

    [Test]
    public void GetByAutomationId_EscapesClosingBracket()
    {
        var locator = MakeLocator("[name=root]");
        var child = locator.GetByAutomationId("foo]bar");
        Assert.That(child.ToString(), Does.Contain("foo\\]bar"));
    }

    [Test]
    public void GetByName_EscapesClosingBracket()
    {
        var locator = MakeLocator("[name=root]");
        var child = locator.GetByName("foo]bar");
        Assert.That(child.ToString(), Does.Contain("foo\\]bar"));
    }

    [Test]
    public void GetByText_EscapesClosingBracket()
    {
        var locator = MakeLocator("[name=root]");
        var child = locator.GetByText("foo]bar");
        Assert.That(child.ToString(), Does.Contain("foo\\]bar"));
    }

    // ── ToString ─────────────────────────────────────────────────────────────

    [Test]
    public void ToString_NoParent_ShowsLocatorOnly()
    {
        var locator = MakeLocator("[name=OK]");
        Assert.That(locator.ToString(), Is.EqualTo("WcLocator([name=OK])"));
    }

    [Test]
    public void ToString_WithParent_ShowsChain()
    {
        var parent = MakeLocator("type=Window");
        var child = MakeLocator("[name=OK]", parent);
        Assert.That(child.ToString(), Is.EqualTo("WcLocator(type=Window) > WcLocator([name=OK])"));
    }

    [Test]
    public void ToString_DeepChain_ShowsFullHierarchy()
    {
        var grandparent = MakeLocator("type=Window");
        var parent = MakeLocator("[name=Panel]", grandparent);
        var child = MakeLocator("[automationid=btn]", parent);
        Assert.That(child.ToString(),
            Is.EqualTo("WcLocator(type=Window) > WcLocator([name=Panel]) > WcLocator([automationid=btn])"));
    }

    // ── Chaining via factory methods sets parent ─────────────────────────────

    [Test]
    public void GetByName_SetsParent_ShowsInToString()
    {
        var root = MakeLocator("type=Window");
        var btn = root.GetByName("OK");
        Assert.That(btn.ToString(), Does.StartWith("WcLocator(type=Window) >"));
    }

    [Test]
    public void MultiLevelChain_ViaFactoryMethods()
    {
        var locator = MakeLocator("type=Window")
            .GetByControlType("Panel")
            .GetByAutomationId("btn1");
        var str = locator.ToString();
        Assert.That(str, Does.Contain("type=Window"));
        Assert.That(str, Does.Contain("type=Panel"));
        Assert.That(str, Does.Contain("[automationid=btn1]"));
    }
}
