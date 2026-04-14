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
        var steps = XPathSyntaxParser.Parse("/");
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
    [TestCase("//Button[last() div 2 = position()]")]
    [TestCase("//Button[position() >= 2]")]
    [TestCase("//Pane[//Button]")]
    [TestCase("//Pane[./Button]")]
    [TestCase("//Pane[.//Button]")]
    [TestCase("//Pane[/Button]")]
    [TestCase("//Pane[..//Button]")]
    [TestCase("//Pane[../Button]")]
    [TestCase("//Pane[../../Button]")]
    [TestCase("//Pane[//Button[@AutomationId='num3Button']]")]
    [TestCase("//Pane[not(//Button)]")]
    [TestCase("//Pane[not(//Button[@AutomationId='num3Button'])]")]
    [TestCase("//Pane[//Button and @Name='foo']")]
    [TestCase("//Pane[//Group[//Button]]")]
    [TestCase("//Pane[//Button/Edit]")]
    [TestCase("//Pane[//*]")]
    [TestCase("//Group[./Button[contains(@AutomationId, 'num')][@Name='Three']]")]
    public void ParseXPath_ValidExpression_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── ParseXPath returns correct steps for valid expressions ───────────────

    [Test]
    public void ParseXPath_SimpleDescendant_ReturnsOneStep()
    {
        var steps = XPathSyntaxParser.Parse("//Button");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
        Assert.That(steps[0].Filters, Is.Empty);
    }

    [Test]
    public void ParseXPath_WithPredicate_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@AutomationId='num7']");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var (attr, value) = GetAttrEqLiteral(steps[0].Filters[0]);
        Assert.That(attr, Is.EqualTo("AutomationId"));
        Assert.That(value, Is.EqualTo("num7"));
    }

    [Test]
    public void ParseXPath_MultiStep_ParsesAll()
    {
        var steps = XPathSyntaxParser.Parse("//Window[@Name='Calc']//Button[@Name='7']");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Type, Is.EqualTo("Window"));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_Wildcard_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//*[@Name='Cancel']");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Type, Is.EqualTo("*"));
    }

    [Test]
    public void ParseXPath_ChildAxis_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("/Window/Button");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Child));
    }

    [Test]
    public void ParseXPath_MultiValuePredicate_ParsesAll()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@AutomationId=('a','b','c')]");
        Assert.That(steps, Has.Count.EqualTo(1));
        var values = GetSequenceValues(steps[0].Filters[0]);
        Assert.That(values, Is.EqualTo(new[] { "a", "b", "c" }));
    }

    // ── Index predicate parsing ──────────────────────────────────────────────

    [Test]
    public void ParseXPath_IndexPredicate_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Button[3]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        Assert.That(steps[0].Filters[0], Is.InstanceOf<IndexFilter>());
        Assert.That(((IndexFilter)steps[0].Filters[0]).Index, Is.EqualTo(3));
    }

    [Test]
    public void ParseXPath_IndexWithAttributePredicate_ParsesBoth()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@Name='OK'][2]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Filters, Has.Count.EqualTo(2));
        Assert.That(steps[0].Filters[0], Is.InstanceOf<ExpressionFilter>());
        var (attr, _) = GetAttrEqLiteral(steps[0].Filters[0]);
        Assert.That(attr, Is.EqualTo("Name"));
        Assert.That(steps[0].Filters[1], Is.InstanceOf<IndexFilter>());
        Assert.That(((IndexFilter)steps[0].Filters[1]).Index, Is.EqualTo(2));
    }

    [Test]
    public void ParseXPath_IndexInMultiStep_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Window[@Name='Calc']//Button[1]");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Filters[0], Is.InstanceOf<ExpressionFilter>());
        Assert.That(steps[1].Filters[0], Is.InstanceOf<IndexFilter>());
        Assert.That(((IndexFilter)steps[1].Filters[0]).Index, Is.EqualTo(1));
    }

    [Test]
    public void ParseXPath_NoIndex_NoIndexFilter()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@Name='OK']");
        Assert.That(steps[0].Filters.All(f => f is ExpressionFilter), Is.True);
    }

    // ── Parent axis (..) parsing ─────────────────────────────────────────────

    [Test]
    public void ParseXPath_ParentAxis_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Button/..");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[1].Type, Is.EqualTo(".."));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(steps[1].Filters, Is.Empty);
    }

    // ── Self axis (.) parsing ───────────────────────────────────────────────

    [Test]
    public void ParseXPath_SelfChildAxis_ParsesSelfThenChild()
    {
        var steps = XPathSyntaxParser.Parse("./Button");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Self));
        Assert.That(steps[0].Type, Is.EqualTo("."));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_SelfDescendantAxis_ParsesSelfThenDescendant()
    {
        var steps = XPathSyntaxParser.Parse(".//Button");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Self));
        Assert.That(steps[0].Type, Is.EqualTo("."));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_SelfWithPredicates_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse(".//Button[@Name='OK']");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Self));
        Assert.That(steps[1].Filters, Has.Count.EqualTo(1));
        var (attr, value) = GetAttrEqLiteral(steps[1].Filters[0]);
        Assert.That(attr, Is.EqualTo("Name"));
        Assert.That(value, Is.EqualTo("OK"));
    }

    [Test]
    public void ParseXPath_SelfMultiStep_ParsesAll()
    {
        var steps = XPathSyntaxParser.Parse("./Panel/Button");
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
        var steps = XPathSyntaxParser.Parse("//Button[@AutomationId='num3Button']/./.././Button[@AutomationId='num3Button']");
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
        var steps = XPathSyntaxParser.Parse("//Button[@Name='OK']/..");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Parent));
    }

    [Test]
    public void ParseXPath_ParentInMiddleOfChain_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Panel/Button/../Edit");
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
        var steps = XPathSyntaxParser.Parse("../Button");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(steps[0].Type, Is.EqualTo(".."));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_DoubleParentAtStart_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("../../Button");
        Assert.That(steps, Has.Count.EqualTo(3));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(steps[2].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(steps[2].Type, Is.EqualTo("Button"));
    }

    // ── Function predicates: position() and last() ─────────────────────────

    [Test]
    public void ParseXPath_PositionPredicate_ParsesAsExpressionFilter()
    {
        var steps = XPathSyntaxParser.Parse("//Button[position()=5]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        Assert.That(steps[0].Filters[0], Is.InstanceOf<ExpressionFilter>());
    }

    [Test]
    public void ParseXPath_PositionWithAttributePredicate_ParsesBoth()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@Name='OK'][position()=2]");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(2));
        Assert.That(steps[0].Filters[0], Is.InstanceOf<ExpressionFilter>());
        Assert.That(steps[0].Filters[1], Is.InstanceOf<ExpressionFilter>());
    }

    [Test]
    public void ParseXPath_PositionAndIndex_ParsesBoth()
    {
        var steps = XPathSyntaxParser.Parse("//Button[position()=2][3]");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(2));
        Assert.That(steps[0].Filters[0], Is.InstanceOf<ExpressionFilter>());
        Assert.That(steps[0].Filters[1], Is.InstanceOf<IndexFilter>());
        Assert.That(((IndexFilter)steps[0].Filters[1]).Index, Is.EqualTo(3));
    }

    [Test]
    public void ParseXPath_ArithmeticExpression_ParsesAsExpressionFilter()
    {
        var steps = XPathSyntaxParser.Parse("//Button[position()-1=3]");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        Assert.That(steps[0].Filters[0], Is.InstanceOf<ExpressionFilter>());
    }

    [Test]
    public void ParseXPath_ComparisonWithPositionOnRight_ParsesAsExpressionFilter()
    {
        var steps = XPathSyntaxParser.Parse("//Button[3 < position()]");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        Assert.That(steps[0].Filters[0], Is.InstanceOf<ExpressionFilter>());
    }

    [Test]
    public void ParseXPath_LastFunction_ParsesAsExpressionFilter()
    {
        var steps = XPathSyntaxParser.Parse("//Button[position() = last() - 1]");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        Assert.That(steps[0].Filters[0], Is.InstanceOf<ExpressionFilter>());
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
    [TestCase("//Button[last() div 2 = position()]")]
    [TestCase("//Button[position() >= 2]")]
    [TestCase("//Button[position() mod 2 = 1]")]
    [TestCase("//Button[position() div 3 > 1.5]")]
    [TestCase("//Button[position() > 2 and position() < last()]")]
    [TestCase("//Button[position() = 1 or position() = last()]")]
    [TestCase("//Button[string-length(@Name) > 5]")]
    [TestCase("//Window[text()='Calculator']")]
    [TestCase("//Window[text()='foo' and @ClassName='bar']")]
    public void ParseXPath_FunctionExpression_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── text() function ──────────────────────────────────────────────────────

    [Test]
    public void ParseXPath_TextFunction_ExactMatch_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Window[text()='Calculator']");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var bin = (BinaryExpr)filter.Expr;
        Assert.That(bin.Left, Is.InstanceOf<FunctionCallExpr>());
        Assert.That(((FunctionCallExpr)bin.Left).Name, Is.EqualTo("text"));
        Assert.That(bin.Op, Is.EqualTo(XPathBinaryOp.Eq));
        Assert.That(((LiteralStringExpr)bin.Right).Value, Is.EqualTo("Calculator"));
    }

    [Test]
    public void ParseXPath_TextFunction_WithAndAttribute_ParsesBothInOneFilter()
    {
        var steps = XPathSyntaxParser.Parse("//Window[text()='foo' and @ClassName='bar']");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        Assert.That(filter.Expr, Is.InstanceOf<BinaryExpr>());
        var bin = (BinaryExpr)filter.Expr;
        Assert.That(bin.Op, Is.EqualTo(XPathBinaryOp.And));
    }

    // ── Removed operators ^=, *=, $= now throw ─────────────────────────────

    [TestCase("//Button[@Name^='Start']")]
    [TestCase("//Button[@Name*='thing']")]
    [TestCase("//Button[@Name$='End']")]
    [TestCase("//Window[text()$='- Microsoft Edge']")]
    [TestCase("//Window[text()^='Calculator']")]
    [TestCase("//Window[text()*='Edge']")]
    public void ParseXPath_RemovedStringOperator_Throws(string xpath)
    {
        Assert.Throws<ArgumentException>(() => XPathEngine.Validate(xpath));
    }

    // ── and/or in attribute predicates ───────────────────────────────────────

    [TestCase("//Button[@Name='foo' and @AutomationId='bar']")]
    [TestCase("//Button[@Name='foo' or @Name='bar']")]
    [TestCase("//Button[starts-with(@Name, 'Start') and @ClassName='Panel']")]
    public void ParseXPath_CompoundAttributePredicate_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    [Test]
    public void ParseXPath_AndAttributePredicate_ParsesAsSingleExpressionFilter()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@Name='foo' and @AutomationId='bar']");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var bin = (BinaryExpr)filter.Expr;
        Assert.That(bin.Op, Is.EqualTo(XPathBinaryOp.And));
    }

    [Test]
    public void ParseXPath_OrAttributePredicate_ParsesAsSingleExpressionFilter()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@Name='foo' or @Name='bar']");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var bin = (BinaryExpr)filter.Expr;
        Assert.That(bin.Op, Is.EqualTo(XPathBinaryOp.Or));
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
    public void ParseXPath_Concat_ParsesAsFunctionCall()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@Name=concat('foo', 'bar')]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var bin = (BinaryExpr)filter.Expr;
        Assert.That(bin.Right, Is.InstanceOf<FunctionCallExpr>());
        var func = (FunctionCallExpr)bin.Right;
        Assert.That(func.Name, Is.EqualTo("concat"));
        Assert.That(func.Args, Has.Count.EqualTo(2));
        Assert.That(((LiteralStringExpr)func.Args[0]).Value, Is.EqualTo("foo"));
        Assert.That(((LiteralStringExpr)func.Args[1]).Value, Is.EqualTo("bar"));
    }

    [Test]
    public void ParseXPath_Concat_ParsesAttrRef()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@Name=concat('prefix-', @AutomationId)]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var bin = (BinaryExpr)filter.Expr;
        var func = (FunctionCallExpr)bin.Right;
        Assert.That(func.Args, Has.Count.EqualTo(2));
        Assert.That(func.Args[0], Is.InstanceOf<LiteralStringExpr>());
        Assert.That(func.Args[1], Is.InstanceOf<AttrRefExpr>());
        Assert.That(((AttrRefExpr)func.Args[1]).Name, Is.EqualTo("AutomationId"));
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

    [TestCase("//Button[@Name='foo' and @AutomationId='bar']")]
    [TestCase("//Button[@Name='a' or @Name='b']")]
    [TestCase("//Button[@Name=concat('foo', 'bar')]")]
    [TestCase("//Button[string-length(@Name) > 5]")]
    [TestCase("//Button[position() mod 2 = 1]")]
    [TestCase("//Button[position() div 3 > 1.5]")]
    [TestCase("//Button[position() > 2 and position() < last()]")]
    [TestCase("//Button[position() = 1 or position() = last()]")]
    [TestCase("//Window[text()='Calculator']")]
    public void ParseXPath_ExtendedValidExpression_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── contains-point(bounds(), point()) ────────────────────────────────────

    [TestCase("//Button[contains-point(bounds(), point(10, 50))]")]
    [TestCase("//*[contains-point(bounds(), point(0.5, 100.25))]")]
    [TestCase("//Button[contains-point(bounds(), point(10, 50)) and @Name='OK']")]
    public void ParseXPath_ContainsPointPredicate_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    [Test]
    public void ParseXPath_ContainsPointBounds_ParsesAsFunctionCall()
    {
        var steps = XPathSyntaxParser.Parse("//Button[contains-point(bounds(), point(10, 50))]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var func = (FunctionCallExpr)filter.Expr;
        Assert.That(func.Name, Is.EqualTo("contains-point"));
        Assert.That(func.Args, Has.Count.EqualTo(2));
    }

    [Test]
    public void ParseXPath_ContainsPointWithAnd_ParsesAsAndExpr()
    {
        var steps = XPathSyntaxParser.Parse("//Button[contains-point(bounds(), point(5, 10)) and @Name='OK']");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        Assert.That(filter.Expr, Is.InstanceOf<BinaryExpr>());
        Assert.That(((BinaryExpr)filter.Expr).Op, Is.EqualTo(XPathBinaryOp.And));
    }

    [TestCase("//Button[contains-point()]")]
    [TestCase("//Button[contains-point(bounds())]")]
    public void ParseXPath_MalformedContainsPoint_ParsesAsSyntacticallyValid(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── contains() / starts-with() / ends-with() ────────────────────────────

    [TestCase("//Button[contains(@Name, 'foo')]")]
    [TestCase("//Button[contains(text(), 'bar')]")]
    [TestCase("//Button[contains('hello world', 'world')]")]
    [TestCase("//Button[starts-with(@Name, 'Start')]")]
    [TestCase("//Button[starts-with(text(), 'Calc')]")]
    [TestCase("//Button[starts-with('hello', 'hel')]")]
    [TestCase("//Button[ends-with(@Name, 'End')]")]
    [TestCase("//Button[ends-with(text(), 'Edge')]")]
    [TestCase("//Button[ends-with('hello', 'llo')]")]
    public void ParseXPath_StringFunctionPredicate_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    [Test]
    public void ParseXPath_ContainsSubstring_ParsesAsFunctionCall()
    {
        var steps = XPathSyntaxParser.Parse("//Button[contains(@Name, 'foo')]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var func = (FunctionCallExpr)filter.Expr;
        Assert.That(func.Name, Is.EqualTo("contains"));
        Assert.That(func.Args, Has.Count.EqualTo(2));
        Assert.That(func.Args[0], Is.InstanceOf<AttrRefExpr>());
        Assert.That(((AttrRefExpr)func.Args[0]).Name, Is.EqualTo("Name"));
        Assert.That(func.Args[1], Is.InstanceOf<LiteralStringExpr>());
        Assert.That(((LiteralStringExpr)func.Args[1]).Value, Is.EqualTo("foo"));
    }

    [Test]
    public void ParseXPath_StartsWithFunction_ParsesAsFunctionCall()
    {
        var steps = XPathSyntaxParser.Parse("//Button[starts-with(@Name, 'Start')]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var func = (FunctionCallExpr)filter.Expr;
        Assert.That(func.Name, Is.EqualTo("starts-with"));
        Assert.That(func.Args, Has.Count.EqualTo(2));
    }

    [Test]
    public void ParseXPath_EndsWithFunction_ParsesAsFunctionCall()
    {
        var steps = XPathSyntaxParser.Parse("//Button[ends-with(@Name, 'End')]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var func = (FunctionCallExpr)filter.Expr;
        Assert.That(func.Name, Is.EqualTo("ends-with"));
        Assert.That(func.Args, Has.Count.EqualTo(2));
    }

    [Test]
    public void ParseXPath_ContainsWithTextFunction_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Button[contains(text(), 'bar')]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var func = (FunctionCallExpr)filter.Expr;
        Assert.That(func.Name, Is.EqualTo("contains"));
        Assert.That(func.Args[0], Is.InstanceOf<FunctionCallExpr>());
        Assert.That(((FunctionCallExpr)func.Args[0]).Name, Is.EqualTo("text"));
    }

    [TestCase("//Button[contains()]")]
    [TestCase("//Button[contains(@Name)]")]
    [TestCase("//Button[starts-with()]")]
    [TestCase("//Button[starts-with(@Name)]")]
    [TestCase("//Button[ends-with()]")]
    [TestCase("//Button[ends-with(@Name)]")]
    public void ParseXPath_MalformedStringFunction_ParsesAsSyntacticallyValid(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── frontmost:: axis ────────────────────────────────────────────────────

    [TestCase("//Window//frontmost::Button[contains-point(bounds(), point(10, 50))]")]
    [TestCase("//frontmost::Button")]
    [TestCase("//frontmost::Button[@Name='OK']")]
    [TestCase("/frontmost::Button")]
    [TestCase("//Window/frontmost::Button[contains-point(bounds(), point(5, 5))]")]
    public void ParseXPath_FrontmostAxis_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    [Test]
    public void ParseXPath_FrontmostAxis_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Window//frontmost::Button[@Name='OK']");
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(steps[0].Type, Is.EqualTo("Window"));
        Assert.That(steps[1].Axis, Is.EqualTo(XPathAxis.Frontmost));
        Assert.That(steps[1].Type, Is.EqualTo("Button"));
        Assert.That(steps[1].Filters, Has.Count.EqualTo(1));
        var (attr, _) = GetAttrEqLiteral(steps[1].Filters[0]);
        Assert.That(attr, Is.EqualTo("Name"));
    }

    [Test]
    public void ParseXPath_FrontmostWithContainsPoint_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//frontmost::Button[contains-point(bounds(), point(10, 50))]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Frontmost));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        Assert.That(filter.Expr, Is.InstanceOf<FunctionCallExpr>());
        Assert.That(((FunctionCallExpr)filter.Expr).Name, Is.EqualTo("contains-point"));
    }

    [Test]
    public void ParseXPath_FrontmostWithChildAxis_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("/frontmost::Button");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Axis, Is.EqualTo(XPathAxis.Frontmost));
        Assert.That(steps[0].Type, Is.EqualTo("Button"));
    }

    // ── at(x, y) shorthand ─────────────────────────────────────────────────

    [TestCase("//Button[at(10, 50)]")]
    [TestCase("//frontmost::Button[at(10, 50)]")]
    [TestCase("//Button[at(0.5, 100.25)]")]
    [TestCase("//Button[at(10, 50) and @Name='OK']")]
    public void ParseXPath_AtFunction_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    [Test]
    public void ParseXPath_AtFunction_ParsesAsFunctionCall()
    {
        var steps = XPathSyntaxParser.Parse("//Button[at(10, 50)]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        Assert.That(filter.Expr, Is.InstanceOf<FunctionCallExpr>());
        Assert.That(((FunctionCallExpr)filter.Expr).Name, Is.EqualTo("at"));
    }

    [Test]
    public void ParseXPath_AtFunctionWithAnd_ParsesAsAndExpr()
    {
        var steps = XPathSyntaxParser.Parse("//Button[at(5, 10) and @Name='OK']");
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        Assert.That(filter.Expr, Is.InstanceOf<BinaryExpr>());
        Assert.That(((BinaryExpr)filter.Expr).Op, Is.EqualTo(XPathBinaryOp.And));
    }

    // ── Previously-failing expressions (motivating the parser refactor) ──────

    [TestCase("//button[contains('Memory', 'Memory')]")]
    [TestCase("//button[contains(concat('Mem ', @name), 'Mem')]")]
    [TestCase("//button[contains(concat(@name, text()), 'Mem')]")]
    [TestCase("//button[contains(@name, concat('<', text(), '>'))]")]
    [TestCase("//button['num3Button'=@automationid]")]
    public void ParseXPath_PreviouslyFailingExpressions_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── XPath 2.0+ doubled-quote escaping ───────────────────────────────────

    [Test]
    public void ParseXPath_DoubledSingleQuote_UnescapesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@Name='it''s']");
        var (_, value) = GetAttrEqLiteral(steps[0].Filters[0]);
        Assert.That(value, Is.EqualTo("it's"));
    }

    [Test]
    public void ParseXPath_DoubledDoubleQuote_UnescapesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Button[@Name=\"say \"\"hello\"\"\"]");
        var (_, value) = GetAttrEqLiteral(steps[0].Filters[0]);
        Assert.That(value, Is.EqualTo("say \"hello\""));
    }

    // ── true() / false() functions ──────────────────────────────────────────

    [TestCase("//Button[@IsEnabled=true()]")]
    [TestCase("//Button[@IsEnabled=false()]")]
    public void ParseXPath_BooleanFunctions_DoesNotThrow(string xpath)
    {
        Assert.DoesNotThrow(() => XPathEngine.Validate(xpath));
    }

    // ── Sub-path filter predicates ──────────────────────────────────────────

    [Test]
    public void ParseXPath_SubPathPredicate_ParsesAsSubPathExpr()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[//Button]");
        Assert.That(steps, Has.Count.EqualTo(1));
        Assert.That(steps[0].Type, Is.EqualTo("Pane"));
        Assert.That(steps[0].Filters, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        Assert.That(filter.Expr, Is.InstanceOf<SubPathExpr>());
        var subPath = (SubPathExpr)filter.Expr;
        Assert.That(subPath.IsAbsolute, Is.False);
        Assert.That(subPath.Steps, Has.Count.EqualTo(1));
        Assert.That(subPath.Steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(subPath.Steps[0].Type, Is.EqualTo("Button"));
        Assert.That(subPath.Steps[0].Filters, Is.Empty);
    }

    [Test]
    public void ParseXPath_SubPathWithPredicate_ParsesNestedFilter()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[//Button[@AutomationId='num3Button']]");
        Assert.That(steps, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var subPath = (SubPathExpr)filter.Expr;
        Assert.That(subPath.Steps, Has.Count.EqualTo(1));
        Assert.That(subPath.Steps[0].Type, Is.EqualTo("Button"));
        Assert.That(subPath.Steps[0].Filters, Has.Count.EqualTo(1));
        var innerFilter = (ExpressionFilter)subPath.Steps[0].Filters[0];
        var bin = (BinaryExpr)innerFilter.Expr;
        Assert.That(((AttrRefExpr)bin.Left).Name, Is.EqualTo("AutomationId"));
        Assert.That(((LiteralStringExpr)bin.Right).Value, Is.EqualTo("num3Button"));
    }

    [Test]
    public void ParseXPath_NotSubPath_ParsesAsFunctionCallWrappingSubPath()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[not(//Button)]");
        Assert.That(steps, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var func = (FunctionCallExpr)filter.Expr;
        Assert.That(func.Name, Is.EqualTo("not"));
        Assert.That(func.Args, Has.Count.EqualTo(1));
        Assert.That(func.Args[0], Is.InstanceOf<SubPathExpr>());
        var subPath = (SubPathExpr)func.Args[0];
        Assert.That(subPath.Steps[0].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_NotSubPathWithPredicate_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[not(//Button[@AutomationId='num3Button'])]");
        Assert.That(steps, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var func = (FunctionCallExpr)filter.Expr;
        Assert.That(func.Name, Is.EqualTo("not"));
        var subPath = (SubPathExpr)func.Args[0];
        Assert.That(subPath.Steps[0].Type, Is.EqualTo("Button"));
        Assert.That(subPath.Steps[0].Filters, Has.Count.EqualTo(1));
    }

    [Test]
    public void ParseXPath_SubPathWithAndExpression_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[//Button and @Name='foo']");
        Assert.That(steps, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var bin = (BinaryExpr)filter.Expr;
        Assert.That(bin.Op, Is.EqualTo(XPathBinaryOp.And));
        Assert.That(bin.Left, Is.InstanceOf<SubPathExpr>());
        Assert.That(bin.Right, Is.InstanceOf<BinaryExpr>());
    }

    [Test]
    public void ParseXPath_MultiStepSubPath_ParsesAllSteps()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[//Group/Button]");
        Assert.That(steps, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var subPath = (SubPathExpr)filter.Expr;
        Assert.That(subPath.Steps, Has.Count.EqualTo(2));
        Assert.That(subPath.Steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(subPath.Steps[0].Type, Is.EqualTo("Group"));
        Assert.That(subPath.Steps[1].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(subPath.Steps[1].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_DotSlashSubPath_ParsesAsChild()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[./Button]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var subPath = (SubPathExpr)filter.Expr;
        Assert.That(subPath.IsAbsolute, Is.False);
        Assert.That(subPath.Steps[0].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(subPath.Steps[0].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_DotDoubleSlashSubPath_ParsesAsDescendant()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[.//Button]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var subPath = (SubPathExpr)filter.Expr;
        Assert.That(subPath.IsAbsolute, Is.False);
        Assert.That(subPath.Steps[0].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(subPath.Steps[0].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_AbsoluteSubPath_ParsesWithIsAbsoluteTrue()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[/Button]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var subPath = (SubPathExpr)filter.Expr;
        Assert.That(subPath.IsAbsolute, Is.True);
        Assert.That(subPath.Steps[0].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(subPath.Steps[0].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_ParentSubPath_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[..//Button]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var subPath = (SubPathExpr)filter.Expr;
        Assert.That(subPath.IsAbsolute, Is.False);
        Assert.That(subPath.Steps, Has.Count.EqualTo(2));
        Assert.That(subPath.Steps[0].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(subPath.Steps[0].Type, Is.EqualTo(".."));
        Assert.That(subPath.Steps[1].Axis, Is.EqualTo(XPathAxis.Descendant));
        Assert.That(subPath.Steps[1].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_DoubleParentSubPath_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[../../Button]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var subPath = (SubPathExpr)filter.Expr;
        Assert.That(subPath.Steps, Has.Count.EqualTo(3));
        Assert.That(subPath.Steps[0].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(subPath.Steps[1].Axis, Is.EqualTo(XPathAxis.Parent));
        Assert.That(subPath.Steps[2].Axis, Is.EqualTo(XPathAxis.Child));
        Assert.That(subPath.Steps[2].Type, Is.EqualTo("Button"));
    }

    [Test]
    public void ParseXPath_SubPathWithMultiplePredicates_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Group[./Button[contains(@AutomationId, 'num')][@Name='Three']]");
        Assert.That(steps, Has.Count.EqualTo(1));
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var subPath = (SubPathExpr)filter.Expr;
        Assert.That(subPath.Steps[0].Type, Is.EqualTo("Button"));
        Assert.That(subPath.Steps[0].Filters, Has.Count.EqualTo(2));
    }

    [Test]
    public void ParseXPath_NestedSubPaths_ParsesCorrectly()
    {
        var steps = XPathSyntaxParser.Parse("//Pane[//Group[//Button]]");
        var filter = (ExpressionFilter)steps[0].Filters[0];
        var subPath = (SubPathExpr)filter.Expr;
        Assert.That(subPath.Steps[0].Type, Is.EqualTo("Group"));
        var innerFilter = (ExpressionFilter)subPath.Steps[0].Filters[0];
        var innerSubPath = (SubPathExpr)innerFilter.Expr;
        Assert.That(innerSubPath.Steps[0].Type, Is.EqualTo("Button"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (string Attr, string Value) GetAttrEqLiteral(XPathFilter filter)
    {
        var ef = (ExpressionFilter)filter;
        var bin = (BinaryExpr)ef.Expr;
        var attr = (AttrRefExpr)bin.Left;
        var val = (LiteralStringExpr)bin.Right;
        return (attr.Name, val.Value);
    }

    private static string[] GetSequenceValues(XPathFilter filter)
    {
        var ef = (ExpressionFilter)filter;
        var bin = (BinaryExpr)ef.Expr;
        var seq = (SequenceExpr)bin.Right;
        return seq.Items.Select(i => ((LiteralStringExpr)i).Value).ToArray();
    }
}
