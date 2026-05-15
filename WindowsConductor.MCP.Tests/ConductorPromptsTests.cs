using NUnit.Framework;
using WindowsConductor.MCP.Prompts;

namespace WindowsConductor.MCP.Tests;

[TestFixture]
[Category("Unit")]
public class ConductorPromptsTests
{
    [Test]
    public void FormFill_ReturnsNonEmptyPrompt()
    {
        var result = ConductorPrompts.FormFill("calc.exe", "{\"[automationid=input]\": \"42\"}");
        Assert.That(result, Is.Not.Empty);
        Assert.That(result, Does.Contain("calc.exe"));
        Assert.That(result, Does.Contain("[automationid=input]"));
    }

    [Test]
    public void InspectElement_ReturnsNonEmptyPrompt()
    {
        var result = ConductorPrompts.InspectElement("app-1", "[name=OK]");
        Assert.That(result, Is.Not.Empty);
        Assert.That(result, Does.Contain("app-1"));
        Assert.That(result, Does.Contain("[name=OK]"));
    }

    [Test]
    public void ScreenshotComparison_ReturnsNonEmptyPrompt()
    {
        var result = ConductorPrompts.ScreenshotComparison("app-1", "click the Save button");
        Assert.That(result, Is.Not.Empty);
        Assert.That(result, Does.Contain("app-1"));
        Assert.That(result, Does.Contain("click the Save button"));
    }

    [Test]
    public void WaitAndInteract_ReturnsNonEmptyPrompt()
    {
        var result = ConductorPrompts.WaitAndInteract("app-1", "[name=Loading]", "click");
        Assert.That(result, Is.Not.Empty);
        Assert.That(result, Does.Contain("app-1"));
        Assert.That(result, Does.Contain("[name=Loading]"));
        Assert.That(result, Does.Contain("click"));
    }

    [Test]
    public void WaitAndInteract_DefaultTimeout_Is10000()
    {
        var result = ConductorPrompts.WaitAndInteract("app-1", "[name=OK]", "click");
        Assert.That(result, Does.Contain("10000"));
    }

    [Test]
    public void WaitAndInteract_CustomTimeout_IsUsed()
    {
        var result = ConductorPrompts.WaitAndInteract("app-1", "[name=OK]", "click", "5000");
        Assert.That(result, Does.Contain("5000"));
    }

    [Test]
    public void OcrRead_ReturnsNonEmptyPrompt()
    {
        var result = ConductorPrompts.OcrRead("app-1", "type=Text");
        Assert.That(result, Is.Not.Empty);
        Assert.That(result, Does.Contain("app-1"));
        Assert.That(result, Does.Contain("type=Text"));
    }
}
