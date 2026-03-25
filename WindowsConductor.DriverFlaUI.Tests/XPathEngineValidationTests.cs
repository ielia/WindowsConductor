using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class XPathEngineValidationTests
{
    // ── Missing element type before predicate ────────────────────────────────

    [TestCase("//[@AutomationId='foo']")]
    [TestCase("//[Name='bar']")]
    [TestCase("/[@Name='x']")]
    [TestCase("//[@AutomationId='a']//Button[@Name='b']")]
    public void ParseXPath_MissingTypBeforePredicate_Throws(string xpath)
    {
        var ex = Assert.Throws<ArgumentException>(() => XPathEngine.Validate(xpath));
        Assert.That(ex!.Message, Does.Contain("missing an element type before predicate"));
    }

    // ── Empty / whitespace ───────────────────────────────────────────────────

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void ParseXPath_EmptyOrWhitespace_Throws(string? xpath)
    {
        Assert.Throws<ArgumentException>(() => XPathEngine.Validate(xpath!));
    }

    // ── No valid steps ───────────────────────────────────────────────────────

    [TestCase("/")]
    [TestCase("//")]
    [TestCase("///")]
    public void ParseXPath_NoValidSteps_Throws(string xpath)
    {
        var ex = Assert.Throws<ArgumentException>(() => XPathEngine.Validate(xpath));
        Assert.That(ex!.Message, Does.Contain("no valid steps"));
    }

    // ── Empty predicate [] ───────────────────────────────────────────────────

    [Test]
    public void ParseXPath_EmptyPredicate_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => XPathEngine.Validate("//Button[]"));
        Assert.That(ex!.Message, Does.Contain("Empty predicate"));
    }

    // ── Invalid predicate syntax ─────────────────────────────────────────────

    [TestCase("//Button[Name='foo']", Description = "Missing @ prefix")]
    [TestCase("//Button[invalid]", Description = "No @attr=value pattern")]
    [TestCase("//Button[123]", Description = "Numeric predicate")]
    public void ParseXPath_InvalidPredicateSyntax_Throws(string xpath)
    {
        var ex = Assert.Throws<ArgumentException>(() => XPathEngine.Validate(xpath));
        Assert.That(ex!.Message, Does.Contain("Invalid predicate syntax"));
    }

    // ── Unclosed predicate bracket ───────────────────────────────────────────

    [Test]
    public void ParseXPath_UnclosedBracket_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => XPathEngine.Validate("//Button[@Name='foo'"));
        Assert.That(ex!.Message, Does.Contain("Unclosed predicate bracket"));
    }

    // ── Valid expressions should NOT throw ───────────────────────────────────

    [TestCase("//Button")]
    [TestCase("//Button[@AutomationId='num7Button']")]
    [TestCase("//*[@Name='Cancel']")]
    [TestCase("//Window[@Name='Calculator']//Button[@Name='7']")]
    [TestCase("//Edit")]
    [TestCase("//Button[@AutomationId=('clearButton','clearEntryButton')]")]
    [TestCase("/Window/Button")]
    [TestCase("//*")]
    public void ParseXPath_ValidExpression_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── ParseXPath returns correct steps for valid expressions ───────────────

    [Test]
    public void ParseXPath_SimpleDescendant_ReturnsOneStep()
    {
        var steps = XPathEngine.ParseXPath("//Button");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
        Assert.That(steps[0].Predicates, Is.Empty);
    }

    [Test]
    public void ParseXPath_WithPredicate_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[@AutomationId='num7']");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Predicates, Has.Count.EqualTo(1));
        Assert.That(steps[0].Predicates[0].Attribute, Is.EqualTo("AutomationId"));
        Assert.That(steps[0].Predicates[0].Values, Is.EqualTo(new[] { "num7" }));
    }

    [Test]
    public void ParseXPath_MultiStep_ParsesAll()
    {
        var steps = XPathEngine.ParseXPath("//Window[@Name='Calc']//Button[@Name='7']");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Type, Is.EqualTo("Window"));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_Wildcard_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//*[@Name='Cancel']");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Type, Is.EqualTo("*"));
    }

    [Test]
    public void ParseXPath_ChildAxis_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("/Window/Button");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Child));
    }

    [Test]
    public void ParseXPath_MultiValuePredicate_ParsesAll()
    {
        var steps = XPathEngine.ParseXPath("//Button[@AutomationId=('a','b','c')]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Predicates[0].Values, Is.EqualTo(new[] { "a", "b", "c" }));
    }
}
