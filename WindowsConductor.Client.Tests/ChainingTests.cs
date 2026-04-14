using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

[TestFixture]
[Category("Unit")]
public class LocatorChainingTests
{
    private static WcLocator MakeLocator(string selector, WcLocator? parent = null) =>
        new("app1", selector, null!, parent);

    private static WcApp MakeApp() => new("app1", null!);

    // ── Each factory method sets parent correctly ────────────────────────────

    [Test]
    public void GetByAutomationId_ChainSetsParent()
    {
        var chain = MakeLocator("type=Window").GetByAutomationId("panel1");
        Assert.That(chain.ToString(),
            Is.EqualTo("WcLocator(type=Window) > WcLocator([automationid=panel1])"));
    }

    [Test]
    public void GetByName_ChainSetsParent()
    {
        var chain = MakeLocator("type=Window").GetByName("OK");
        Assert.That(chain.ToString(),
            Is.EqualTo("WcLocator(type=Window) > WcLocator([name=OK])"));
    }

    [Test]
    public void GetByText_ChainSetsParent()
    {
        var chain = MakeLocator("type=Window").GetByText("Cancel");
        Assert.That(chain.ToString(),
            Is.EqualTo("WcLocator(type=Window) > WcLocator(text=Cancel)"));
    }

    [Test]
    public void GetByControlType_ChainSetsParent()
    {
        var chain = MakeLocator("[name=root]").GetByControlType("Button");
        Assert.That(chain.ToString(),
            Is.EqualTo("WcLocator([name=root]) > WcLocator(type=Button)"));
    }

    [Test]
    public void GetByXPath_ChainSetsParent()
    {
        var chain = MakeLocator("type=Window").GetByXPath("//Button[@Name='OK']");
        Assert.That(chain.ToString(),
            Is.EqualTo("WcLocator(type=Window) > WcLocator(//Button[@Name='OK'])"));
    }

    [Test]
    public void Locator_ChainSetsParent()
    {
        var chain = MakeLocator("type=Window").Locator("[automationid=btn]");
        Assert.That(chain.ToString(),
            Is.EqualTo("WcLocator(type=Window) > WcLocator([automationid=btn])"));
    }

    // ── Multi-level chains ───────────────────────────────────────────────────

    [Test]
    public void ThreeLevelChain_AllFactoryMethods()
    {
        var chain = MakeLocator("type=Window")
            .GetByControlType("Panel")
            .GetByName("Submit");

        Assert.That(chain.ToString(), Is.EqualTo(
            "WcLocator(type=Window) > WcLocator(type=Panel) > WcLocator([name=Submit])"));
    }

    [Test]
    public void FourLevelChain_MixedFactoryMethods()
    {
        var chain = MakeLocator("type=Window")
            .GetByAutomationId("mainPanel")
            .GetByControlType("List")
            .GetByText("Item 1");

        Assert.That(chain.ToString(), Is.EqualTo(
            "WcLocator(type=Window) > WcLocator([automationid=mainPanel]) > " +
            "WcLocator(type=List) > WcLocator(text=Item 1)"));
    }

    [Test]
    public void FiveLevelChain()
    {
        var chain = MakeLocator("type=Window")
            .GetByAutomationId("nav")
            .GetByControlType("TreeView")
            .GetByName("Root")
            .GetByText("Leaf");

        var parts = chain.ToString().Split(" > ");
        Assert.That(parts, Has.Length.EqualTo(5));
    }

    // ── Chains starting from WcApp ───────────────────────────────────────────

    [Test]
    public void App_SingleLevel_NoParent()
    {
        var loc = MakeApp().GetByName("OK");
        Assert.That(loc.ToString(), Is.EqualTo("WcLocator([name=OK])"));
    }

    [Test]
    public void App_TwoLevelChain()
    {
        var chain = MakeApp()
            .GetByControlType("Panel")
            .GetByAutomationId("btn1");

        Assert.That(chain.ToString(), Is.EqualTo(
            "WcLocator(type=Panel) > WcLocator([automationid=btn1])"));
    }

    [Test]
    public void App_ThreeLevelChain_WithXPath()
    {
        var chain = MakeApp()
            .GetByControlType("Window")
            .GetByXPath("Panel[@Name='content']")
            .GetByAutomationId("submit");

        Assert.That(chain.ToString(), Is.EqualTo(
            "WcLocator(type=Window) > WcLocator(//Panel[@Name='content']) > " +
            "WcLocator([automationid=submit])"));
    }

    // ── Branching from same parent ───────────────────────────────────────────

    [Test]
    public void SameParent_DifferentChildren_AreIndependent()
    {
        var parent = MakeApp().GetByControlType("Panel");
        var child1 = parent.GetByName("OK");
        var child2 = parent.GetByName("Cancel");

        Assert.That(child1.ToString(), Does.Contain("[name=OK]"));
        Assert.That(child2.ToString(), Does.Contain("[name=Cancel]"));
        Assert.That(child1.ToString(), Does.Not.Contain("Cancel"));
        Assert.That(child2.ToString(), Does.Not.Contain("OK"));
    }

    [Test]
    public void BranchedChildren_ShareParentPrefix()
    {
        var parent = MakeApp().GetByControlType("Panel");
        var child1 = parent.GetByName("OK");
        var child2 = parent.GetByAutomationId("cancelBtn");

        var prefix = "WcLocator(type=Panel) > ";
        Assert.That(child1.ToString(), Does.StartWith(prefix));
        Assert.That(child2.ToString(), Does.StartWith(prefix));
    }

    // ── Validation at every level of the chain ───────────────────────────────

    [Test]
    public void Chain_CustomAttributeAtSecondLevel_DoesNotThrow()
    {
        var parent = MakeLocator("type=Window");
        Assert.DoesNotThrow(() => parent.Locator("[custom=foo]"));
    }

    [Test]
    public void Chain_EmptySelectorInMiddle_Throws()
    {
        var parent = MakeLocator("type=Window");
        Assert.Throws<ArgumentException>(() => parent.Locator(""));
    }

    // ── Compound selectors within a chain ────────────────────────────────────

    [Test]
    public void Chain_CompoundSelectorAsChild()
    {
        var chain = MakeLocator("type=Window")
            .Locator("[automationid=btn]&&type=Button");

        Assert.That(chain.ToString(), Is.EqualTo(
            "WcLocator(type=Window) > WcLocator([automationid=btn]&&type=Button)"));
    }

    [Test]
    public void Chain_CompoundSelectorAsParent()
    {
        var chain = MakeLocator("[automationid=panel]&&type=Panel")
            .GetByName("OK");

        Assert.That(chain.ToString(), Is.EqualTo(
            "WcLocator([automationid=panel]&&type=Panel) > WcLocator([name=OK])"));
    }

    // ── XPath within chains ──────────────────────────────────────────────────

    [Test]
    public void Chain_XPathAsParent_SimpleAsChild()
    {
        var chain = MakeApp()
            .GetByXPath("//Window[@Name='Main']")
            .GetByName("OK");

        Assert.That(chain.ToString(), Is.EqualTo(
            "WcLocator(//Window[@Name='Main']) > WcLocator([name=OK])"));
    }

    [Test]
    public void Chain_SimpleAsParent_XPathAsChild()
    {
        var chain = MakeApp()
            .GetByControlType("Window")
            .GetByXPath("//Button[@AutomationId='ok']");

        Assert.That(chain.ToString(), Is.EqualTo(
            "WcLocator(type=Window) > WcLocator(//Button[@AutomationId='ok'])"));
    }
}
