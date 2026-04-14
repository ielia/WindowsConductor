using Superpower;
using Superpower.Model;
using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class XPathExprEvaluatorTests
{
    private static bool Eval(string predicate, int position, int last, Func<string, string?>? props = null)
    {
        var tokens = Tokenize(predicate);
        var parseResult = XPathSyntaxParser.Expression.TryParse(tokens);
        if (!parseResult.HasValue)
            throw new ArgumentException($"Failed to parse: {predicate}");
        var ctx = new EvalContext(props ?? (_ => null), position, last, null);
        return XPathFunctions.Evaluate(parseResult.Value, ctx).AsBool();
    }

    private static TokenList<XPathToken> Tokenize(string input)
    {
        return new TokenList<XPathToken>(
            XPathTokenizer.Instance.Tokenize(input).ToArray());
    }

    // ── Simple position() equality ───────────────────────────────────────────

    [Test]
    public void Evaluate_PositionEquals_MatchesCorrectPosition()
    {
        Assert.That(Eval("position()=3", 3, 5), Is.True);
        Assert.That(Eval("position()=3", 2, 5), Is.False);
    }

    // ── Comparison operators ─────────────────────────────────────────────────

    [Test]
    public void Evaluate_LessThan_Works()
    {
        Assert.That(Eval("3 < position()", 4, 10), Is.True);
        Assert.That(Eval("3 < position()", 3, 10), Is.False);
        Assert.That(Eval("3 < position()", 2, 10), Is.False);
    }

    [Test]
    public void Evaluate_GreaterThan_Works()
    {
        Assert.That(Eval("position() > 3", 4, 10), Is.True);
        Assert.That(Eval("position() > 3", 3, 10), Is.False);
    }

    [Test]
    public void Evaluate_LessThanOrEqual_Works()
    {
        Assert.That(Eval("position() <= 3", 3, 10), Is.True);
        Assert.That(Eval("position() <= 3", 4, 10), Is.False);
    }

    [Test]
    public void Evaluate_GreaterThanOrEqual_Works()
    {
        Assert.That(Eval("position() >= 3", 3, 10), Is.True);
        Assert.That(Eval("position() >= 3", 2, 10), Is.False);
    }

    [Test]
    public void Evaluate_NotEqual_Works()
    {
        Assert.That(Eval("position() != last()", 3, 5), Is.True);
        Assert.That(Eval("position() != last()", 5, 5), Is.False);
    }

    // ── Arithmetic ───────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_PositionMinusOneEqualsThree_Works()
    {
        Assert.That(Eval("position()-1 = 3", 4, 10), Is.True);
        Assert.That(Eval("position()-1 = 3", 3, 10), Is.False);
    }

    [Test]
    public void Evaluate_PositionEqualsLastMinusOne_Works()
    {
        Assert.That(Eval("position() = last() - 1", 4, 5), Is.True);
        Assert.That(Eval("position() = last() - 1", 5, 5), Is.False);
    }

    [Test]
    public void Evaluate_Addition_Works()
    {
        Assert.That(Eval("position() + 1 = 5", 4, 10), Is.True);
    }

    [Test]
    public void Evaluate_Multiplication_Works()
    {
        Assert.That(Eval("last() * 2 > position()", 3, 5), Is.True);
        Assert.That(Eval("last() * 2 > position()", 10, 5), Is.False);
    }

    // ── Parenthesized expressions ────────────────────────────────────────────

    [Test]
    public void Evaluate_Parentheses_Work()
    {
        Assert.That(Eval("(position() + 1) * 2 = 10", 4, 10), Is.True);
        Assert.That(Eval("(position() + 1) * 2 = 10", 3, 10), Is.False);
    }

    // ── Unary minus ──────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_UnaryMinus_Works()
    {
        Assert.That(Eval("position() = -(-3)", 3, 10), Is.True);
    }

    // ── last() ───────────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_Last_ReturnsLastValue()
    {
        Assert.That(Eval("position() = last()", 5, 5), Is.True);
        Assert.That(Eval("position() = last()", 4, 5), Is.False);
    }

    // ── Whitespace tolerance ─────────────────────────────────────────────────

    [Test]
    public void Evaluate_WithWhitespace_Works()
    {
        Assert.That(Eval("  position()  =  last()  -  1  ", 4, 5), Is.True);
    }

    // ── Division ─────────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_Division_Works()
    {
        Assert.That(Eval("last() div 2 = position()", 5, 10), Is.True);
        Assert.That(Eval("last() div 2 = position()", 4, 10), Is.False);
    }

    // ── mod operator ─────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_Mod_Works()
    {
        Assert.That(Eval("position() mod 2 = 1", 1, 10), Is.True);
        Assert.That(Eval("position() mod 2 = 1", 3, 10), Is.True);
        Assert.That(Eval("position() mod 2 = 1", 2, 10), Is.False);
    }

    [Test]
    public void Evaluate_Mod_ThreeMod2IsOne()
    {
        Assert.That(Eval("3 mod 2 = 1", 1, 1), Is.True);
    }

    // ── div operator (IEEE 754 float division) ───────────────────────────────

    [Test]
    public void Evaluate_Div_FloatingPoint()
    {
        Assert.That(Eval("10 div 3 > 3", 1, 1), Is.True);
        Assert.That(Eval("10 div 3 < 4", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_Div_ExactHalf()
    {
        Assert.That(Eval("7 div 2 > 3", 1, 1), Is.True);
        Assert.That(Eval("7 div 2 < 4", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_DecimalLiteral()
    {
        Assert.That(Eval("position() div 3 > 1.5", 5, 10), Is.True);
        Assert.That(Eval("position() div 3 > 1.5", 4, 10), Is.False);
    }

    // ── and operator ─────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_And_BothTrue()
    {
        Assert.That(Eval("position() > 2 and position() < 5", 3, 10), Is.True);
    }

    [Test]
    public void Evaluate_And_OneFalse()
    {
        Assert.That(Eval("position() > 2 and position() < 5", 6, 10), Is.False);
    }

    [Test]
    public void Evaluate_And_BothFalse()
    {
        Assert.That(Eval("position() > 5 and position() < 3", 4, 10), Is.False);
    }

    // ── or operator ──────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_Or_FirstTrue()
    {
        Assert.That(Eval("position() = 1 or position() = last()", 1, 10), Is.True);
    }

    [Test]
    public void Evaluate_Or_SecondTrue()
    {
        Assert.That(Eval("position() = 1 or position() = last()", 10, 10), Is.True);
    }

    [Test]
    public void Evaluate_Or_NoneTrue()
    {
        Assert.That(Eval("position() = 1 or position() = last()", 5, 10), Is.False);
    }

    // ── and/or precedence (and binds tighter) ────────────────────────────────

    [Test]
    public void Evaluate_AndOrPrecedence()
    {
        Assert.That(Eval("position() = 1 or position() > 3 and position() < 6", 1, 10), Is.True);
        Assert.That(Eval("position() = 1 or position() > 3 and position() < 6", 4, 10), Is.True);
        Assert.That(Eval("position() = 1 or position() > 3 and position() < 6", 7, 10), Is.False);
    }

    // ── string-length() ──────────────────────────────────────────────────────

    [Test]
    public void Evaluate_StringLength_StringLiteral()
    {
        Assert.That(Eval("string-length('hello') = 5", 1, 1), Is.True);
        Assert.That(Eval("string-length('hello') = 4", 1, 1), Is.False);
    }

    [Test]
    public void Evaluate_StringLength_AttributeRef()
    {
        Func<string, string?> props = k => k == "name" ? "Calculator" : null;
        Assert.That(Eval("string-length(@Name) > 5", 1, 1, props), Is.True);
        Assert.That(Eval("string-length(@Name) = 10", 1, 1, props), Is.True);
    }

    [Test]
    public void Evaluate_StringLength_NullAttribute_ReturnsZero()
    {
        Func<string, string?> props = _ => null;
        Assert.That(Eval("string-length(@Name) = 0", 1, 1, props), Is.True);
    }

    // ── Validation errors ────────────────────────────────────────────────────

    [Test]
    public void Parse_InvalidExpression_FailsToParse()
    {
        var tokens = Tokenize("position() = ");
        var result = XPathSyntaxParser.Expression.TryParse(tokens);
        Assert.That(result.HasValue, Is.False);
    }

    [Test]
    public void Parse_UnclosedParen_FailsToParse()
    {
        var tokens = Tokenize("(position() + 1");
        var result = XPathSyntaxParser.Expression.TryParse(tokens);
        Assert.That(result.HasValue, Is.False);
    }

    [Test]
    public void Tokenize_UnexpectedCharacter_Throws()
    {
        Assert.Throws<Superpower.ParseException>(() => Tokenize("position() & 3"));
    }
}
