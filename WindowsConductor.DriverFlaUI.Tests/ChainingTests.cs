using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class CompoundSelectorChainingTests
{
    // ── Compound selectors (&&) — validation ─────────────────────────────────

    [TestCase("[automationid=foo]&&[name=bar]")]
    [TestCase("[automationid=ok]&&type=Button")]
    [TestCase("type=Button&&[name=Submit]")]
    [TestCase("[name=Text editor]&&type=Document")]
    [TestCase("[classname=Panel]&&[automationid=main]&&type=Custom")]
    public void Validate_ValidCompoundSelector_DoesNotThrow(string selector)
    {
        Assert.DoesNotThrow(() => SelectorEngine.Validate(selector));
    }

    [TestCase("[automationid=foo]&&[invalid=bar]")]
    [TestCase("[automationid=foo]&&[=bar]")]
    [TestCase("[name=x]&&[href=y]")]
    public void Validate_InvalidCompoundSelector_Throws(string selector)
    {
        Assert.Throws<ArgumentException>(() => SelectorEngine.Validate(selector));
    }

    // ── Compound selectors — ParsePart per segment ───────────────────────────

    [Test]
    public void CompoundSelector_EachPartParsesIndependently()
    {
        var selector = "[automationid=ok]&&type=Button&&[name=Submit]";
        var parts = selector.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var (k1, v1) = SelectorEngine.ParsePart(parts[0]);
        Assert.That(k1, Is.EqualTo("automationid"));
        Assert.That(v1, Is.EqualTo("ok"));

        var (k2, v2) = SelectorEngine.ParsePart(parts[1]);
        Assert.That(k2, Is.EqualTo("controltype"));
        Assert.That(v2, Is.EqualTo("Button"));

        var (k3, v3) = SelectorEngine.ParsePart(parts[2]);
        Assert.That(k3, Is.EqualTo("name"));
        Assert.That(v3, Is.EqualTo("Submit"));
    }

    [Test]
    public void CompoundSelector_TwoParts_BothValid()
    {
        var parts = "[classname=Foo]&&[automationid=bar]"
            .Split("&&", StringSplitOptions.TrimEntries);

        var (k1, v1) = SelectorEngine.ParsePart(parts[0]);
        var (k2, v2) = SelectorEngine.ParsePart(parts[1]);

        Assert.That(k1, Is.EqualTo("classname"));
        Assert.That(v1, Is.EqualTo("Foo"));
        Assert.That(k2, Is.EqualTo("automationid"));
        Assert.That(v2, Is.EqualTo("bar"));
    }

    [Test]
    public void CompoundSelector_MixedBracketAndShorthand()
    {
        var parts = "[automationid=x]&&text=Hello"
            .Split("&&", StringSplitOptions.TrimEntries);

        var (k1, _) = SelectorEngine.ParsePart(parts[0]);
        var (k2, v2) = SelectorEngine.ParsePart(parts[1]);

        Assert.That(k1, Is.EqualTo("automationid"));
        Assert.That(k2, Is.EqualTo("text"));
        Assert.That(v2, Is.EqualTo("Hello"));
    }

    // ── Multi-step XPath (chained steps) ─────────────────────────────────────

    [Test]
    public void XPath_TwoSteps_BothParsed()
    {
        var steps = XPathEngine.ParseXPath("//Window[@Name='Main']//Button[@Name='OK']");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Type, Is.EqualTo("Window"));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[0].Predicates[0].Attribute, Is.EqualTo("Name"));
        Assert.That(steps[0].Predicates[0].Values, Is.EqualTo(new[] { "Main" }));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
        Assert.That(steps[1].Predicates[0].Values, Is.EqualTo(new[] { "OK" }));
    }

    [Test]
    public void XPath_ThreeSteps_AllParsed()
    {
        var steps = XPathEngine.ParseXPath("//Window//Panel//Button");
        Assert.That(steps, Has.Count.EqualTo(3));
        Assert.That(steps[0].Type, Is.EqualTo("Window"));
        Assert.That(steps[1].Type, Is.EqualTo("Panel"));
        Assert.That(steps[2].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void XPath_MixedAxes_ChildAndDescendant()
    {
        var steps = XPathEngine.ParseXPath("/Window//Panel/Button");
        Assert.That(steps, Has.Count.EqualTo(3));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[2].Axis, Is.EqualTo(XPathAxis.Child));
    }

    [Test]
    public void XPath_StepWithMultiplePredicates()
    {
        var steps = XPathEngine.ParseXPath("//Button[@AutomationId='ok'][@Name='OK']");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Predicates, Has.Count.EqualTo(2));
        Assert.That(steps[0].Predicates[0].Attribute, Is.EqualTo("AutomationId"));
        Assert.That(steps[0].Predicates[1].Attribute, Is.EqualTo("Name"));
    }

    [Test]
    public void XPath_WildcardStep_InChain()
    {
        var steps = XPathEngine.ParseXPath("//Window//*[@Name='OK']");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Type, Is.EqualTo("Window"));
        Assert.That(steps[1].Type, Is.EqualTo("*"));
        Assert.That(steps[1].Predicates[0].Values, Is.EqualTo(new[] { "OK" }));
    }

    [Test]
    public void XPath_MultiValuePredicate_InChain()
    {
        var steps = XPathEngine.ParseXPath(
            "//Window[@Name='Main']//Button[@AutomationId=('ok','apply')]");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[1].Predicates[0].Values, Is.EqualTo(new[] { "ok", "apply" }));
    }

    [Test]
    public void XPath_StepWithoutPredicate_InChain()
    {
        var steps = XPathEngine.ParseXPath("//Window//Button");
        Assert.That(steps[0].Predicates, Is.Empty);
        Assert.That(steps[1].Predicates, Is.Empty);
    }

    // ── Invalid chained XPath ────────────────────────────────────────────────

    [Test]
    public void XPath_SecondStepMissingType_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => XPathEngine.Validate("//Window//[@Name='foo']"));
    }

    [Test]
    public void XPath_SecondStepEmptyPredicate_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => XPathEngine.Validate("//Window//Button[]"));
    }

    [Test]
    public void XPath_SecondStepUnclosedBracket_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => XPathEngine.Validate("//Window//Button[@Name='foo'"));
    }

    // ── Additional SelectorEngine.ParsePart edge cases ─────────────────────

    [Test]
    public void ParsePart_BareValue_TreatsAsName()
    {
        var (key, value) = SelectorEngine.ParsePart("Submit");
        Assert.That(key, Is.EqualTo("name"));
        Assert.That(value, Is.EqualTo("Submit"));
    }

    [TestCase("class=MyPanel")]
    [TestCase("[class=MyPanel]")]
    public void ParsePart_ClassAlias_NormalizesToClassName(string part)
    {
        var (key, _) = SelectorEngine.ParsePart(part);
        Assert.That(key, Is.EqualTo("classname"));
    }

    [TestCase("controltype=Button")]
    [TestCase("[controltype=Button]")]
    public void ParsePart_ControlTypeAlias_Works(string part)
    {
        var (key, _) = SelectorEngine.ParsePart(part);
        Assert.That(key, Is.EqualTo("controltype"));
    }

    [Test]
    public void ParsePart_ClosingBracketWithoutOpening_Throws()
    {
        Assert.Throws<ArgumentException>(() => SelectorEngine.ParsePart("name=foo]"));
    }

    [Test]
    public void ParsePart_EmptyBrackets_Throws()
    {
        Assert.Throws<ArgumentException>(() => SelectorEngine.ParsePart("[]"));
    }

    [Test]
    public void ParsePart_BracketNoEquals_Throws()
    {
        Assert.Throws<ArgumentException>(() => SelectorEngine.ParsePart("[name]"));
    }

    [Test]
    public void ParsePart_UnclosedBracket_Throws()
    {
        Assert.Throws<ArgumentException>(() => SelectorEngine.ParsePart("[name=foo"));
    }

    // ── Additional SelectorEngine.Validate edge cases ──────────────────────

    [Test]
    public void Validate_EmptyString_Throws()
    {
        Assert.Throws<ArgumentException>(() => SelectorEngine.Validate(""));
    }

    [Test]
    public void Validate_WhitespaceOnly_Throws()
    {
        Assert.Throws<ArgumentException>(() => SelectorEngine.Validate("   "));
    }

    [Test]
    public void Validate_XPathSelector_DelegatesToXPathValidate()
    {
        Assert.DoesNotThrow(() => SelectorEngine.Validate("//Button[@Name='OK']"));
    }

    [Test]
    public void Validate_InvalidXPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => SelectorEngine.Validate("//[@Name='foo']"));
    }

    // ── Additional XPathEngine.ParseXPath edge cases ───────────────────────

    [Test]
    public void XPath_EmptyString_Throws()
    {
        Assert.Throws<ArgumentException>(() => XPathEngine.ParseXPath(""));
    }

    [Test]
    public void XPath_WhitespaceOnly_Throws()
    {
        Assert.Throws<ArgumentException>(() => XPathEngine.ParseXPath("   "));
    }

    [Test]
    public void XPath_OnlySlashes_Throws()
    {
        Assert.Throws<ArgumentException>(() => XPathEngine.ParseXPath("//"));
    }

    [Test]
    public void XPath_DoubleQuotedValue_Works()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name=\"OK\"]");
        Assert.That(steps[0].Predicates[0].Values[0], Is.EqualTo("OK"));
    }

    [Test]
    public void XPath_SingleStep_ChildAxis()
    {
        var steps = XPathEngine.ParseXPath("/Button");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void XPath_SingleStep_NoPredicate()
    {
        var steps = XPathEngine.ParseXPath("//Edit");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Type, Is.EqualTo("Edit"));
        Assert.That(steps[0].Predicates, Is.Empty);
    }

    [Test]
    public void XPath_ListPredicate_DoubleQuoted()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name=(\"ok\",\"apply\")]");
        Assert.That(steps[0].Predicates[0].Values, Is.EqualTo(new[] { "ok", "apply" }));
    }

    [Test]
    public void XPath_InvalidPredicateSyntax_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => XPathEngine.ParseXPath("//Button[Name='foo']")); // missing @
    }

    [Test]
    public void XPath_FourSteps_AllParsed()
    {
        var steps = XPathEngine.ParseXPath("//Window/Panel//Group/Button");
        Assert.That(steps, Has.Count.EqualTo(4));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[2].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[3].Axis, Is.EqualTo(XPathAxis.Child));
    }
}
