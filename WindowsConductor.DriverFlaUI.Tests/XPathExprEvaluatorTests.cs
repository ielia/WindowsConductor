using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class XPathExprEvaluatorTests
{
    // ── IsFunctionExpression ─────────────────────────────────────────────────

    [TestCase("position()=5", true)]
    [TestCase("last()-1", true)]
    [TestCase("3 < position()", true)]
    [TestCase("@Name='foo'", false)]
    [TestCase("42", false)]
    public void IsFunctionExpression_DetectsCorrectly(string expr, bool expected)
    {
        Assert.That(XPathExprEvaluator.IsFunctionExpression(expr), Is.EqualTo(expected));
    }

    // ── Simple position() equality ───────────────────────────────────────────

    [Test]
    public void Evaluate_PositionEquals_MatchesCorrectPosition()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position()=3", 3, 5), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position()=3", 2, 5), Is.False);
    }

    // ── Comparison operators ─────────────────────────────────────────────────

    [Test]
    public void Evaluate_LessThan_Works()
    {
        // [3 < position()] → true when position > 3
        Assert.That(XPathExprEvaluator.Evaluate("3 < position()", 4, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("3 < position()", 3, 10), Is.False);
        Assert.That(XPathExprEvaluator.Evaluate("3 < position()", 2, 10), Is.False);
    }

    [Test]
    public void Evaluate_GreaterThan_Works()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() > 3", 4, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() > 3", 3, 10), Is.False);
    }

    [Test]
    public void Evaluate_LessThanOrEqual_Works()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() <= 3", 3, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() <= 3", 4, 10), Is.False);
    }

    [Test]
    public void Evaluate_GreaterThanOrEqual_Works()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() >= 3", 3, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() >= 3", 2, 10), Is.False);
    }

    [Test]
    public void Evaluate_NotEqual_Works()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() != last()", 3, 5), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() != last()", 5, 5), Is.False);
    }

    // ── Arithmetic ───────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_PositionMinusOneEqualsThree_Works()
    {
        // [position()-1 = 3] → true when position = 4
        Assert.That(XPathExprEvaluator.Evaluate("position()-1 = 3", 4, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position()-1 = 3", 3, 10), Is.False);
    }

    [Test]
    public void Evaluate_PositionEqualsLastMinusOne_Works()
    {
        // [position() = last() - 1] → true when position = last - 1
        Assert.That(XPathExprEvaluator.Evaluate("position() = last() - 1", 4, 5), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() = last() - 1", 5, 5), Is.False);
    }

    [Test]
    public void Evaluate_Addition_Works()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() + 1 = 5", 4, 10), Is.True);
    }

    [Test]
    public void Evaluate_Multiplication_Works()
    {
        // [last() * 2 > position()] → true when position < last * 2
        Assert.That(XPathExprEvaluator.Evaluate("last() * 2 > position()", 3, 5), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("last() * 2 > position()", 10, 5), Is.False);
    }

    // ── Parenthesized expressions ────────────────────────────────────────────

    [Test]
    public void Evaluate_Parentheses_Work()
    {
        // (position() + 1) * 2 = 10  → position = 4: (4+1)*2 = 10
        Assert.That(XPathExprEvaluator.Evaluate("(position() + 1) * 2 = 10", 4, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("(position() + 1) * 2 = 10", 3, 10), Is.False);
    }

    // ── Unary minus ──────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_UnaryMinus_Works()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() = -(-3)", 3, 10), Is.True);
    }

    // ── last() ───────────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_Last_ReturnsLastValue()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() = last()", 5, 5), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() = last()", 4, 5), Is.False);
    }

    // ── Whitespace tolerance ─────────────────────────────────────────────────

    [Test]
    public void Evaluate_WithWhitespace_Works()
    {
        Assert.That(XPathExprEvaluator.Evaluate("  position()  =  last()  -  1  ", 4, 5), Is.True);
    }

    // ── Division ─────────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_Division_Works()
    {
        // last() / 2 = position()  → last=10, position=5: 10/2=5
        Assert.That(XPathExprEvaluator.Evaluate("last() / 2 = position()", 5, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("last() / 2 = position()", 4, 10), Is.False);
    }

    [Test]
    public void Evaluate_IntegerDivision_Truncates()
    {
        // 7 / 2 = 3 (integer division)
        Assert.That(XPathExprEvaluator.Evaluate("7 / 2 = position()", 3, 10), Is.True);
    }

    // ── => operator (alias for >=) ───────────────────────────────────────────

    [Test]
    public void Evaluate_FatArrowOperator_Works()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() => 3", 3, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() => 3", 4, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() => 3", 2, 10), Is.False);
    }

    // ── Validation errors ────────────────────────────────────────────────────

    [Test]
    public void Validate_InvalidExpression_Throws()
    {
        Assert.Throws<ArgumentException>(() => XPathExprEvaluator.Validate("position() = "));
    }

    [Test]
    public void Validate_UnclosedParen_Throws()
    {
        Assert.Throws<ArgumentException>(() => XPathExprEvaluator.Validate("(position() + 1"));
    }

    [Test]
    public void Validate_UnexpectedCharacter_Throws()
    {
        Assert.Throws<ArgumentException>(() => XPathExprEvaluator.Validate("position() & 3"));
    }
}
