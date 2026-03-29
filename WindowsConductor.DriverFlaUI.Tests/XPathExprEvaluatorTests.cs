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
    public void Evaluate_GreaterThanOrEqual_DoubleForm_Works()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() >= 3", 3, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() >= 3", 4, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() >= 3", 2, 10), Is.False);
    }

    // ── mod operator ─────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_Mod_Works()
    {
        // position() mod 2 = 1 → odd positions
        Assert.That(XPathExprEvaluator.Evaluate("position() mod 2 = 1", 1, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() mod 2 = 1", 3, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() mod 2 = 1", 2, 10), Is.False);
    }

    [Test]
    public void Evaluate_Mod_ThreeMod2IsOne()
    {
        Assert.That(XPathExprEvaluator.Evaluate("3 mod 2 = 1", 1, 1), Is.True);
    }

    // ── div operator (IEEE 754 float division) ───────────────────────────────

    [Test]
    public void Evaluate_Div_FloatingPoint()
    {
        // 10 div 3 ≈ 3.333...  so > 3 is true
        Assert.That(XPathExprEvaluator.Evaluate("10 div 3 > 3", 1, 1), Is.True);
        // 10 div 3 < 4 is true
        Assert.That(XPathExprEvaluator.Evaluate("10 div 3 < 4", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_Div_ExactHalf()
    {
        // 7 div 2 = 3.5
        Assert.That(XPathExprEvaluator.Evaluate("7 div 2 > 3", 1, 1), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("7 div 2 < 4", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_DecimalLiteral()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() div 3 > 1.5", 5, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() div 3 > 1.5", 4, 10), Is.False);
    }

    // ── and operator ─────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_And_BothTrue()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() > 2 and position() < 5", 3, 10), Is.True);
    }

    [Test]
    public void Evaluate_And_OneFalse()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() > 2 and position() < 5", 6, 10), Is.False);
    }

    [Test]
    public void Evaluate_And_BothFalse()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() > 5 and position() < 3", 4, 10), Is.False);
    }

    // ── or operator ──────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_Or_FirstTrue()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() = 1 or position() = last()", 1, 10), Is.True);
    }

    [Test]
    public void Evaluate_Or_SecondTrue()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() = 1 or position() = last()", 10, 10), Is.True);
    }

    [Test]
    public void Evaluate_Or_NoneTrue()
    {
        Assert.That(XPathExprEvaluator.Evaluate("position() = 1 or position() = last()", 5, 10), Is.False);
    }

    // ── and/or precedence (and binds tighter) ────────────────────────────────

    [Test]
    public void Evaluate_AndOrPrecedence()
    {
        // position()=1 or position()>3 and position()<6 → 1 or (>3 and <6)
        Assert.That(XPathExprEvaluator.Evaluate("position() = 1 or position() > 3 and position() < 6", 1, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() = 1 or position() > 3 and position() < 6", 4, 10), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("position() = 1 or position() > 3 and position() < 6", 7, 10), Is.False);
    }

    // ── string-length() ──────────────────────────────────────────────────────

    [Test]
    public void Evaluate_StringLength_StringLiteral()
    {
        Assert.That(XPathExprEvaluator.Evaluate("string-length('hello') = 5", 1, 1), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("string-length('hello') = 4", 1, 1), Is.False);
    }

    [Test]
    public void Evaluate_StringLength_AttributeRef()
    {
        Func<string, string?> props = k => k == "name" ? "Calculator" : null;
        Assert.That(XPathExprEvaluator.Evaluate("string-length(@Name) > 5", 1, 1, props), Is.True);
        Assert.That(XPathExprEvaluator.Evaluate("string-length(@Name) = 10", 1, 1, props), Is.True);
    }

    [Test]
    public void Evaluate_StringLength_NullAttribute_ReturnsZero()
    {
        Func<string, string?> props = _ => null;
        Assert.That(XPathExprEvaluator.Evaluate("string-length(@Name) = 0", 1, 1, props), Is.True);
    }

    [Test]
    public void IsFunctionExpression_StringLength_Detected()
    {
        Assert.That(XPathExprEvaluator.IsFunctionExpression("string-length(@Name) > 5"), Is.True);
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
