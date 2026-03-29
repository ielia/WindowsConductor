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

    [TestCase("//")]
    [TestCase("///")]
    public void ParseXPath_NoValidSteps_Throws(string xpath)
    {
        var ex = Assert.Throws<ArgumentException>(() => XPathEngine.Validate(xpath));
        Assert.That(ex!.Message, Does.Contain("no valid steps"));
    }

    // ── Root selector ─────────────────────────────────────────────────────────

    [Test]
    public void ParseXPath_BareSlash_ParsesAsSelfStep()
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate("/"));
        var steps = XPathEngine.ParseXPath("/");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Self));
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

    // ── Index predicate: zero or negative ────────────────────────────────────

    [TestCase("//Button[0]")]
    [TestCase("//Button[-1]")]
    public void ParseXPath_IndexLessThanOne_Throws(string xpath)
    {
        var ex = Assert.Throws<ArgumentException>(() => XPathEngine.Validate(xpath));
        Assert.That(ex!.Message, Does.Contain("Index predicate must be >= 1"));
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
    [TestCase("//Button[3]")]
    [TestCase("//Button[@Name='OK'][2]")]
    [TestCase("//Window[@Name='Calc']//Button[1]")]
    [TestCase("//Button[@Name='OK']/..")]
    [TestCase("//Button/..")]
    [TestCase("//Window/Button[@Name='OK']/..")]
    [TestCase("./Button")]
    [TestCase(".//Button")]
    [TestCase(".//Button[@Name='OK']")]
    [TestCase("./Button/Panel")]
    [TestCase(".//Panel/Button[@Name='OK']/..")]
    [TestCase("//Button[@AutomationId='num3Button']/./.././Button[@AutomationId='num3Button']")]
    [TestCase("../Button")]
    [TestCase("..//Button")]
    [TestCase("../../Button")]
    [TestCase("//Button[position()=5]")]
    [TestCase("//Button[3 < position()]")]
    [TestCase("//Button[position()-1 = 3]")]
    [TestCase("//Button[position() = last() - 1]")]
    [TestCase("//Button[position() != last()]")]
    [TestCase("//Button[last() / 2 = position()]")]
    [TestCase("//Button[position() >= 2]")]
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

    // ── Index predicate parsing ──────────────────────────────────────────────

    [Test]
    public void ParseXPath_IndexPredicate_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[3]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
        Assert.That(steps[0].Index, Is.EqualTo(3));
        Assert.That(steps[0].Predicates, Is.Empty);
    }

    [Test]
    public void ParseXPath_IndexWithAttributePredicate_ParsesBoth()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name='OK'][2]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Predicates, Has.Count.EqualTo(1));
        Assert.That(steps[0].Predicates[0].Attribute, Is.EqualTo("Name"));
        Assert.That(steps[0].Index, Is.EqualTo(2));
    }

    [Test]
    public void ParseXPath_IndexInMultiStep_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Window[@Name='Calc']//Button[1]");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Index, Is.Null);
        Assert.That(steps[1].Index, Is.EqualTo(1));
    }

    [Test]
    public void ParseXPath_NoIndex_IndexIsNull()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name='OK']");
        Assert.That(steps[0].Index, Is.Null);
    }

    // ── Parent axis (..) parsing ─────────────────────────────────────────────

    [Test]
    public void ParseXPath_ParentAxis_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button/..");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[1].Type, Is.EqualTo(".."));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(steps[1].Predicates, Is.Empty);
    }

    // ── Self axis (.) parsing ───────────────────────────────────────────────

    [Test]
    public void ParseXPath_SelfChildAxis_ParsesSelfThenChild()
    {
        var steps = XPathEngine.ParseXPath("./Button");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Self));
        Assert.That(steps[0].Type, Is.EqualTo("."));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_SelfDescendantAxis_ParsesSelfThenDescendant()
    {
        var steps = XPathEngine.ParseXPath(".//Button");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Self));
        Assert.That(steps[0].Type, Is.EqualTo("."));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_SelfWithPredicates_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath(".//Button[@Name='OK']");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Self));
        Assert.That(steps[1].Predicates, Has.Count.EqualTo(1));
        Assert.That(steps[1].Predicates[0].Attribute, Is.EqualTo("Name"));
        Assert.That(steps[1].Predicates[0].Values, Is.EqualTo(new[] { "OK" }));
    }

    [Test]
    public void ParseXPath_SelfMultiStep_ParsesAll()
    {
        var steps = XPathEngine.ParseXPath("./Panel/Button");
        Assert.That(steps, Has.Count.EqualTo(3));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Self));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[1].Type, Is.EqualTo("Panel"));
        Assert.That(steps[2].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[2].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_SelfInMiddleOfPath_ParsesCorrectly()
    {
        // //Button[..] / . / .. / . / Button[..]
        var steps = XPathEngine.ParseXPath("//Button[@AutomationId='num3Button']/./.././Button[@AutomationId='num3Button']");
        Assert.That(steps, Has.Count.EqualTo(5));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Self));
        Assert.That(steps[2].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(steps[3].Axis, Is.EqualTo(XPathAxis.Self));
        Assert.That(steps[4].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[4].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_ParentAfterPredicate_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name='OK']/..");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Predicates, Has.Count.EqualTo(1));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Parent));
    }

    [Test]
    public void ParseXPath_ParentInMiddleOfChain_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Panel/Button/../Edit");
        Assert.That(steps, Has.Count.EqualTo(4));
        Assert.That(steps[0].Type, Is.EqualTo("Panel"));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
        Assert.That(steps[2].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(steps[3].Type, Is.EqualTo("Edit"));
        Assert.That(steps[3].Axis, Is.EqualTo(XPathAxis.Child));
    }

    [Test]
    public void ParseXPath_ParentAtStart_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("../Button");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(steps[0].Type, Is.EqualTo(".."));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_DoubleParentAtStart_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("../../Button");
        Assert.That(steps, Has.Count.EqualTo(3));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(steps[2].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[2].Type, Is.EqualTo("Button"));
    }

    // ── Function predicates: position() and last() ─────────────────────────

    [Test]
    public void ParseXPath_PositionPredicate_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[position()=5]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
        Assert.That(steps[0].FunctionPredicates, Has.Count.EqualTo(1));
        Assert.That(steps[0].FunctionPredicates![0], Is.EqualTo("position()=5"));
        Assert.That(steps[0].Index, Is.Null);
        Assert.That(steps[0].Predicates, Is.Empty);
    }

    [Test]
    public void ParseXPath_PositionWithWhitespace_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[ position() = 3 ]");
        Assert.That(steps[0].FunctionPredicates, Has.Count.EqualTo(1));
        Assert.That(steps[0].FunctionPredicates![0], Is.EqualTo("position() = 3"));
    }

    [Test]
    public void ParseXPath_PositionWithAttributePredicate_ParsesBoth()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name='OK'][position()=2]");
        Assert.That(steps[0].Predicates, Has.Count.EqualTo(1));
        Assert.That(steps[0].Predicates[0].Attribute, Is.EqualTo("Name"));
        Assert.That(steps[0].FunctionPredicates, Has.Count.EqualTo(1));
    }

    [Test]
    public void ParseXPath_PositionAndIndex_ParsesBoth()
    {
        var steps = XPathEngine.ParseXPath("//Button[position()=2][3]");
        Assert.That(steps[0].FunctionPredicates, Has.Count.EqualTo(1));
        Assert.That(steps[0].Index, Is.EqualTo(3));
    }

    [Test]
    public void ParseXPath_ArithmeticExpression_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[position()-1=3]");
        Assert.That(steps[0].FunctionPredicates, Has.Count.EqualTo(1));
        Assert.That(steps[0].FunctionPredicates![0], Is.EqualTo("position()-1=3"));
    }

    [Test]
    public void ParseXPath_ComparisonWithPositionOnRight_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[3 < position()]");
        Assert.That(steps[0].FunctionPredicates, Has.Count.EqualTo(1));
    }

    [Test]
    public void ParseXPath_LastFunction_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[position() = last() - 1]");
        Assert.That(steps[0].FunctionPredicates, Has.Count.EqualTo(1));
    }

    [TestCase("//Button[position()=5]")]
    [TestCase("//Button[ position() = 3 ]")]
    [TestCase("//Button[@Name='OK'][position()=2]")]
    [TestCase("//Button[3 < position()]")]
    [TestCase("//Button[position()-1 = 3]")]
    [TestCase("//Button[position() = last() - 1]")]
    [TestCase("//Button[position() != last()]")]
    [TestCase("//Button[position() >= 2]")]
    [TestCase("//Button[position() <= last() - 2]")]
    [TestCase("//Button[last() * 2 > position()]")]
    [TestCase("//Button[last() / 2 = position()]")]
    [TestCase("//Button[position() >= 2]")]
    [TestCase("//Button[position() mod 2 = 1]")]
    [TestCase("//Button[position() div 3 > 1.5]")]
    [TestCase("//Button[position() > 2 and position() < last()]")]
    [TestCase("//Button[position() = 1 or position() = last()]")]
    [TestCase("//Button[string-length(@Name) > 5]")]
    public void ParseXPath_FunctionExpression_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── String comparison operators ──────────────────────────────────────────

    [TestCase("//Button[@Name^='Start']")]
    [TestCase("//Button[@Name*='thing']")]
    [TestCase("//Button[@Name$='End']")]
    public void ParseXPath_StringOperator_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    [Test]
    public void ParseXPath_StartsWithOperator_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name^='Start']");
        Assert.That(steps[0].Predicates, Has.Count.EqualTo(1));
        Assert.That(steps[0].Predicates[0].Attribute, Is.EqualTo("Name"));
        Assert.That(steps[0].Predicates[0].Values, Is.EqualTo(new[] { "Start" }));
        Assert.That(steps[0].Predicates[0].MatchMode, Is.EqualTo(AttributeMatchMode.StartsWith));
    }

    [Test]
    public void ParseXPath_ContainsOperator_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name*='thing']");
        Assert.That(steps[0].Predicates[0].MatchMode, Is.EqualTo(AttributeMatchMode.Contains));
    }

    [Test]
    public void ParseXPath_EndsWithOperator_ParsesCorrectly()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name$='End']");
        Assert.That(steps[0].Predicates[0].MatchMode, Is.EqualTo(AttributeMatchMode.EndsWith));
    }

    [Test]
    public void ParseXPath_ExactOperator_DefaultMatchMode()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name='OK']");
        Assert.That(steps[0].Predicates[0].MatchMode, Is.EqualTo(AttributeMatchMode.Exact));
    }

    // ── and/or in attribute predicates ───────────────────────────────────────

    [TestCase("//Button[@Name='foo' and @AutomationId='bar']")]
    [TestCase("//Button[@Name='foo' or @Name='bar']")]
    [TestCase("//Button[@Name^='Start' and @ClassName='Panel']")]
    public void ParseXPath_CompoundAttributePredicate_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    [Test]
    public void ParseXPath_AndAttributePredicate_AddsToBothPredicates()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name='foo' and @AutomationId='bar']");
        Assert.That(steps[0].Predicates, Has.Count.EqualTo(2));
        Assert.That(steps[0].Predicates[0].Attribute, Is.EqualTo("Name"));
        Assert.That(steps[0].Predicates[1].Attribute, Is.EqualTo("AutomationId"));
    }

    [Test]
    public void ParseXPath_OrAttributePredicate_CreatesOrGroup()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name='foo' or @Name='bar']");
        Assert.That(steps[0].Predicates, Is.Empty);
        Assert.That(steps[0].OrPredicateGroups, Has.Count.EqualTo(1));
        Assert.That(steps[0].OrPredicateGroups![0], Has.Count.EqualTo(2));
    }

    // ── concat() function ────────────────────────────────────────────────────

    [TestCase("//Button[@Name=concat('foo', 'bar')]")]
    [TestCase("//Button[@Name=concat('prefix-', @AutomationId)]")]
    [TestCase("//Button[@Name=concat('a', @Name, 'b')]")]
    public void ParseXPath_Concat_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    [Test]
    public void ParseXPath_Concat_ParsesStringArgs()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name=concat('foo', 'bar')]");
        var pred = steps[0].Predicates[0];
        Assert.That(pred.ConcatArgs, Has.Count.EqualTo(2));
        Assert.That(pred.ConcatArgs![0], Is.InstanceOf<StringConcatArg>());
        Assert.That(((StringConcatArg)pred.ConcatArgs[0]).Value, Is.EqualTo("foo"));
        Assert.That(((StringConcatArg)pred.ConcatArgs[1]).Value, Is.EqualTo("bar"));
    }

    [Test]
    public void ParseXPath_Concat_ParsesAttrArgs()
    {
        var steps = XPathEngine.ParseXPath("//Button[@Name=concat('prefix-', @AutomationId)]");
        var pred = steps[0].Predicates[0];
        Assert.That(pred.ConcatArgs, Has.Count.EqualTo(2));
        Assert.That(pred.ConcatArgs![0], Is.InstanceOf<StringConcatArg>());
        Assert.That(pred.ConcatArgs[1], Is.InstanceOf<AttrConcatArg>());
        Assert.That(((AttrConcatArg)pred.ConcatArgs[1]).Attribute, Is.EqualTo("AutomationId"));
    }

    // ── string-length() ──────────────────────────────────────────────────────

    [TestCase("//Button[string-length(@Name) > 5]")]
    [TestCase("//Button[string-length('hello') = 5]")]
    public void ParseXPath_StringLength_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── mod/div ──────────────────────────────────────────────────────────────

    [TestCase("//Button[position() mod 2 = 1]")]
    [TestCase("//Button[position() div 3 > 1.5]")]
    public void ParseXPath_ModDiv_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── Valid expressions list (extended) ─────────────────────────────────────

    [TestCase("//Button[@Name^='Start']")]
    [TestCase("//Button[@Name*='thing']")]
    [TestCase("//Button[@Name$='End']")]
    [TestCase("//Button[@Name='foo' and @AutomationId='bar']")]
    [TestCase("//Button[@Name='a' or @Name='b']")]
    [TestCase("//Button[@Name=concat('foo', 'bar')]")]
    [TestCase("//Button[string-length(@Name) > 5]")]
    [TestCase("//Button[position() mod 2 = 1]")]
    [TestCase("//Button[position() div 3 > 1.5]")]
    [TestCase("//Button[position() > 2 and position() < last()]")]
    [TestCase("//Button[position() = 1 or position() = last()]")]
    public void ParseXPath_ExtendedValidExpression_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }
}
