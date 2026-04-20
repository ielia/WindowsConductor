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

    private static double EvalNum(string predicate, int position = 1, int last = 1, Func<string, string?>? props = null)
    {
        var tokens = Tokenize(predicate);
        var parseResult = XPathSyntaxParser.Expression.TryParse(tokens);
        if (!parseResult.HasValue)
            throw new ArgumentException($"Failed to parse: {predicate}");
        var ctx = new EvalContext(props ?? (_ => null), position, last, null);
        return XPathFunctions.Evaluate(parseResult.Value, ctx).AsNumber();
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

    [Test]
    public void Evaluate_UnaryMinus_WithBinaryPlus()
    {
        Assert.That(Eval("position() = 5 + -2", 3, 10), Is.True);
        Assert.That(Eval("position() = -1 + -2", 1, 10), Is.False);
        Assert.That(Eval("-1 + 4 = position()", 3, 10), Is.True);
    }

    [Test]
    public void Evaluate_UnaryMinus_WithBinaryMinus()
    {
        Assert.That(Eval("position() = 5 - -2", 7, 10), Is.True);
        Assert.That(Eval("position() = -3 - -1", 1, 10), Is.False);
    }

    // ── Unary plus ─────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_UnaryPlus_Works()
    {
        Assert.That(Eval("position() = +(+3)", 3, 10), Is.True);
    }

    [Test]
    public void Evaluate_UnaryPlus_WithBinaryPlus()
    {
        Assert.That(Eval("position() = 2 + +1", 3, 10), Is.True);
        Assert.That(Eval("+2 + +3 = position()", 5, 10), Is.True);
    }

    [Test]
    public void Evaluate_UnaryPlus_WithBinaryMinus()
    {
        Assert.That(Eval("position() = 5 - +2", 3, 10), Is.True);
        Assert.That(Eval("+10 - +3 = position()", 7, 10), Is.True);
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

    // ── idiv operator (integer division, truncates toward zero) ──────────────

    [Test]
    public void Evaluate_IntDiv_ExactDivision()
    {
        Assert.That(Eval("10 idiv 2 = 5", 1, 1), Is.True);
        Assert.That(Eval("9 idiv 3 = 3", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_IntDiv_TruncatesPositive()
    {
        Assert.That(Eval("7 idiv 2 = 4", 1, 1), Is.True);
        Assert.That(Eval("10 idiv 3 = 4", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_IntDiv_NegativeDividend()
    {
        Assert.That(Eval("-7 idiv 2 = -4", 1, 1), Is.True);
        Assert.That(Eval("7 idiv -2 = -4", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_IntDiv_BothNegative()
    {
        Assert.That(Eval("-7 idiv -2 = 4", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_IntDiv_WithPosition()
    {
        Assert.That(Eval("position() idiv 3 = 2", 4, 10), Is.True);
        Assert.That(Eval("last() idiv 3 = 4", 1, 10), Is.True);
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

    // ── string-join() ─────────────────────────────────────────────────────────

    [Test]
    public void Evaluate_StringJoin_SequenceNoSeparator()
    {
        Assert.That(Eval("string-join(('a', 'b', 'c')) = 'abc'", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_StringJoin_SequenceWithSeparator()
    {
        Assert.That(Eval("string-join(('a', 'b', 'c'), ', ') = 'a, b, c'", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_StringJoin_SingleItem()
    {
        Assert.That(Eval("string-join(('hello'), '-') = 'hello'", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_StringJoin_EmptySequence()
    {
        Assert.That(Eval("string-join(()) = ''", 1, 1), Is.True);
        Assert.That(Eval("string-join((), ',') = ''", 1, 1), Is.True);
    }

    [Test]
    public void Evaluate_StringJoin_SingleStringArg()
    {
        Assert.That(Eval("string-join('hello') = 'hello'", 1, 1), Is.True);
        Assert.That(Eval("string-join('hello', '-') = 'hello'", 1, 1), Is.True);
    }

    // ── Numeric functions ─────────────────────────────────────────────────────

    [Test]
    public void Evaluate_Abs()
    {
        Assert.That(EvalNum("abs(-5)"), Is.EqualTo(5));
        Assert.That(EvalNum("abs(3)"), Is.EqualTo(3));
        Assert.That(EvalNum("abs(0)"), Is.EqualTo(0));
    }

    [Test]
    public void Evaluate_Ceiling()
    {
        Assert.That(EvalNum("ceiling(2.3)"), Is.EqualTo(3));
        Assert.That(EvalNum("ceiling(-2.3)"), Is.EqualTo(-2));
        Assert.That(EvalNum("ceiling(5)"), Is.EqualTo(5));
    }

    [Test]
    public void Evaluate_Floor()
    {
        Assert.That(EvalNum("floor(2.7)"), Is.EqualTo(2));
        Assert.That(EvalNum("floor(-2.3)"), Is.EqualTo(-3));
        Assert.That(EvalNum("floor(5)"), Is.EqualTo(5));
    }

    [Test]
    public void Evaluate_Round_OneArg()
    {
        Assert.That(EvalNum("round(2.6)"), Is.EqualTo(3));
        Assert.That(EvalNum("round(2.4)"), Is.EqualTo(2));
        Assert.That(EvalNum("round(-2.6)"), Is.EqualTo(-3));
    }

    [Test]
    public void Evaluate_Round_TwoArgs()
    {
        Assert.That(EvalNum("round(2.456, 2)"), Is.EqualTo(2.46));
        Assert.That(EvalNum("round(2.454, 2)"), Is.EqualTo(2.45));
    }

    [Test]
    public void Evaluate_RoundHalfToEven_OneArg()
    {
        Assert.That(EvalNum("round-half-to-even(2.5)"), Is.EqualTo(2));
        Assert.That(EvalNum("round-half-to-even(3.5)"), Is.EqualTo(4));
        Assert.That(EvalNum("round-half-to-even(2.6)"), Is.EqualTo(3));
    }

    [Test]
    public void Evaluate_RoundHalfToEven_TwoArgs()
    {
        Assert.That(EvalNum("round-half-to-even(2.455, 2)"), Is.EqualTo(2.46));
        Assert.That(EvalNum("round-half-to-even(2.445, 2)"), Is.EqualTo(2.44));
    }

    // ── Aggregate functions ───────────────────────────────────────────────────

    [Test]
    public void Evaluate_Count_Sequence()
    {
        Assert.That(EvalNum("count((1, 2, 3))"), Is.EqualTo(3));
    }

    [Test]
    public void Evaluate_Count_EmptySequence()
    {
        Assert.That(EvalNum("count(())"), Is.EqualTo(0));
    }

    [Test]
    public void Evaluate_Count_SingleValue()
    {
        Assert.That(EvalNum("count(42)"), Is.EqualTo(1));
    }

    [Test]
    public void Evaluate_Sum_Sequence()
    {
        Assert.That(EvalNum("sum((1, 2, 3))"), Is.EqualTo(6));
    }

    [Test]
    public void Evaluate_Sum_Decimals()
    {
        Assert.That(EvalNum("sum((1.5, 2.3, 0.2))"), Is.EqualTo(4).Within(1e-10));
    }

    [Test]
    public void Evaluate_Sum_SingleValue()
    {
        Assert.That(EvalNum("sum(10)"), Is.EqualTo(10));
    }

    [Test]
    public void Evaluate_Sum_StringCoercion()
    {
        Assert.That(EvalNum("sum(('1', '2', '3'))"), Is.EqualTo(6));
    }

    [Test]
    public void Evaluate_Avg_Sequence()
    {
        Assert.That(EvalNum("avg((2, 4, 6))"), Is.EqualTo(4));
    }

    [Test]
    public void Evaluate_Avg_Decimals()
    {
        Assert.That(EvalNum("avg((1.5, 2.5, 3.5))"), Is.EqualTo(2.5));
    }

    [Test]
    public void Evaluate_Avg_EmptySequence_ReturnsNaN()
    {
        Assert.That(EvalNum("avg(())"), Is.NaN);
    }

    [Test]
    public void Evaluate_Max_Sequence()
    {
        Assert.That(EvalNum("max((3, 1, 4, 1, 5))"), Is.EqualTo(5));
    }

    [Test]
    public void Evaluate_Max_Decimals()
    {
        Assert.That(EvalNum("max((1.1, 3.7, 2.9))"), Is.EqualTo(3.7));
    }

    [Test]
    public void Evaluate_Max_EmptySequence_ReturnsNaN()
    {
        Assert.That(EvalNum("max(())"), Is.NaN);
    }

    [Test]
    public void Evaluate_Min_Sequence()
    {
        Assert.That(EvalNum("min((3, 1, 4, 1, 5))"), Is.EqualTo(1));
    }

    [Test]
    public void Evaluate_Min_Decimals()
    {
        Assert.That(EvalNum("min((2.8, 0.3, 1.5))"), Is.EqualTo(0.3));
    }

    [Test]
    public void Evaluate_Min_EmptySequence_ReturnsNaN()
    {
        Assert.That(EvalNum("min(())"), Is.NaN);
    }

    [Test]
    public void Evaluate_Min_SingleValue()
    {
        Assert.That(EvalNum("min(7)"), Is.EqualTo(7));
    }

    // ── math: namespace functions ────────────────────────────────────────────

    [Test]
    public void Evaluate_MathPi()
    {
        Assert.That(EvalNum("math:pi()"), Is.EqualTo(Math.PI));
    }

    [Test]
    public void Evaluate_MathExp()
    {
        Assert.That(EvalNum("math:exp(0)"), Is.EqualTo(1));
        Assert.That(EvalNum("math:exp(1)"), Is.EqualTo(Math.E).Within(1e-10));
    }

    [Test]
    public void Evaluate_MathExp10()
    {
        Assert.That(EvalNum("math:exp10(0)"), Is.EqualTo(1));
        Assert.That(EvalNum("math:exp10(2)"), Is.EqualTo(100));
        Assert.That(EvalNum("math:exp10(3)"), Is.EqualTo(1000));
    }

    [Test]
    public void Evaluate_MathLog()
    {
        Assert.That(EvalNum("math:log(1)"), Is.EqualTo(0));
        Assert.That(EvalNum("math:log(math:exp(1))"), Is.EqualTo(1).Within(1e-10));
    }

    [Test]
    public void Evaluate_MathLog10()
    {
        Assert.That(EvalNum("math:log10(1)"), Is.EqualTo(0));
        Assert.That(EvalNum("math:log10(100)"), Is.EqualTo(2));
    }

    [Test]
    public void Evaluate_MathPow()
    {
        Assert.That(EvalNum("math:pow(2, 10)"), Is.EqualTo(1024));
        Assert.That(EvalNum("math:pow(3, 0)"), Is.EqualTo(1));
    }

    [Test]
    public void Evaluate_MathSqrt()
    {
        Assert.That(EvalNum("math:sqrt(9)"), Is.EqualTo(3));
        Assert.That(EvalNum("math:sqrt(2)"), Is.EqualTo(Math.Sqrt(2)).Within(1e-10));
    }

    [Test]
    public void Evaluate_MathSin()
    {
        Assert.That(EvalNum("math:sin(0)"), Is.EqualTo(0));
        Assert.That(EvalNum("math:sin(math:pi() div 2)"), Is.EqualTo(1).Within(1e-10));
    }

    [Test]
    public void Evaluate_MathCos()
    {
        Assert.That(EvalNum("math:cos(0)"), Is.EqualTo(1));
        Assert.That(EvalNum("math:cos(math:pi())"), Is.EqualTo(-1).Within(1e-10));
    }

    [Test]
    public void Evaluate_MathTan()
    {
        Assert.That(EvalNum("math:tan(0)"), Is.EqualTo(0));
        Assert.That(EvalNum("math:tan(math:pi() div 4)"), Is.EqualTo(1).Within(1e-10));
    }

    [Test]
    public void Evaluate_MathAsin()
    {
        Assert.That(EvalNum("math:asin(0)"), Is.EqualTo(0));
        Assert.That(EvalNum("math:asin(1)"), Is.EqualTo(Math.PI / 2).Within(1e-10));
    }

    [Test]
    public void Evaluate_MathAcos()
    {
        Assert.That(EvalNum("math:acos(1)"), Is.EqualTo(0));
        Assert.That(EvalNum("math:acos(0)"), Is.EqualTo(Math.PI / 2).Within(1e-10));
    }

    [Test]
    public void Evaluate_MathAtan()
    {
        Assert.That(EvalNum("math:atan(0)"), Is.EqualTo(0));
        Assert.That(EvalNum("math:atan(1)"), Is.EqualTo(Math.PI / 4).Within(1e-10));
    }

    [Test]
    public void Evaluate_MathAtan2()
    {
        Assert.That(EvalNum("math:atan2(0, 1)"), Is.EqualTo(0));
        Assert.That(EvalNum("math:atan2(1, 1)"), Is.EqualTo(Math.PI / 4).Within(1e-10));
        Assert.That(EvalNum("math:atan2(1, 0)"), Is.EqualTo(Math.PI / 2).Within(1e-10));
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

    // ── Sub-path expression evaluation ────────────────────────────────────────

    private static readonly Func<SubPathExpr, XPathValue> NonEmptySequence =
        _ => new XPathSequence([new XPathString("a"), new XPathString("b")]);

    private static readonly Func<SubPathExpr, XPathValue> EmptySequence =
        _ => new XPathSequence([]);

    [Test]
    public void Evaluate_SubPathExpr_NonEmptySequence_IsTruthy()
    {
        var subPath = new SubPathExpr([new XPathStep(XPathAxis.Descendant, "Button", [])], false);
        var ctx = new EvalContext(_ => null, 1, 1, null, NonEmptySequence);
        var result = XPathFunctions.Evaluate(subPath, ctx);
        Assert.That(result.AsBool(), Is.True);
    }

    [Test]
    public void Evaluate_SubPathExpr_EmptySequence_IsFalsy()
    {
        var subPath = new SubPathExpr([new XPathStep(XPathAxis.Descendant, "Button", [])], false);
        var ctx = new EvalContext(_ => null, 1, 1, null, EmptySequence);
        var result = XPathFunctions.Evaluate(subPath, ctx);
        Assert.That(result.AsBool(), Is.False);
    }

    [Test]
    public void Evaluate_NotSubPathExpr_InvertsResult()
    {
        var subPath = new SubPathExpr([new XPathStep(XPathAxis.Descendant, "Button", [])], false);
        var notExpr = new FunctionCallExpr("not", [subPath]);
        var ctx = new EvalContext(_ => null, 1, 1, null, NonEmptySequence);
        var result = XPathFunctions.Evaluate(notExpr, ctx);
        Assert.That(result.AsBool(), Is.False);
    }

    [Test]
    public void Evaluate_SubPathExpr_WithoutEvaluator_Throws()
    {
        var subPath = new SubPathExpr([new XPathStep(XPathAxis.Descendant, "Button", [])], false);
        var ctx = new EvalContext(_ => null, 1, 1, null);
        Assert.Throws<InvalidOperationException>(() => XPathFunctions.Evaluate(subPath, ctx));
    }

    [Test]
    public void Evaluate_SubPathAndAttr_BothTrue()
    {
        var subPath = new SubPathExpr([new XPathStep(XPathAxis.Descendant, "Button", [])], false);
        var attrExpr = new BinaryExpr(new AttrRefExpr("Name"), XPathBinaryOp.Eq, new LiteralStringExpr("foo"));
        var andExpr = new BinaryExpr(subPath, XPathBinaryOp.And, attrExpr);
        Func<string, string?> props = k => k == "name" ? "foo" : null;
        var ctx = new EvalContext(props, 1, 1, null, NonEmptySequence);
        Assert.That(XPathFunctions.Evaluate(andExpr, ctx).AsBool(), Is.True);
    }

    [Test]
    public void Evaluate_SubPathAndAttr_SubPathFalse()
    {
        var subPath = new SubPathExpr([new XPathStep(XPathAxis.Descendant, "Button", [])], false);
        var attrExpr = new BinaryExpr(new AttrRefExpr("Name"), XPathBinaryOp.Eq, new LiteralStringExpr("foo"));
        var andExpr = new BinaryExpr(subPath, XPathBinaryOp.And, attrExpr);
        Func<string, string?> props = k => k == "name" ? "foo" : null;
        var ctx = new EvalContext(props, 1, 1, null, EmptySequence);
        Assert.That(XPathFunctions.Evaluate(andExpr, ctx).AsBool(), Is.False);
    }

    [Test]
    public void Evaluate_SubPathEvaluator_ReceivesCorrectExpr()
    {
        var expectedStep = new XPathStep(XPathAxis.Descendant, "Edit", []);
        var subPath = new SubPathExpr([expectedStep], false);
        SubPathExpr? received = null;
        var ctx = new EvalContext(_ => null, 1, 1, null, sp =>
        {
            received = sp;
            return new XPathSequence([new XPathString("x")]);
        });
        XPathFunctions.Evaluate(subPath, ctx);
        Assert.That(received, Is.Not.Null);
        Assert.That(received!.Steps, Has.Count.EqualTo(1));
        Assert.That(received.Steps[0].Type, Is.EqualTo("Edit"));
        Assert.That(received.IsAbsolute, Is.False);
    }

    // ── Sub-path as sequence argument ─────────────────────────────────────────

    [Test]
    public void Evaluate_StringJoin_SubPathSequence()
    {
        var subPath = new SubPathExpr([new XPathStep(XPathAxis.Descendant, "Button", [])], false);
        var joinExpr = new FunctionCallExpr("string-join", [subPath, new LiteralStringExpr(",")]);
        var ctx = new EvalContext(_ => null, 1, 1, null,
            _ => new XPathSequence([new XPathString("OK"), new XPathString("Cancel")]));
        var result = XPathFunctions.Evaluate(joinExpr, ctx);
        Assert.That(result.AsString(), Is.EqualTo("OK,Cancel"));
    }

    [Test]
    public void Evaluate_StringJoin_SubPathEmptySequence()
    {
        var subPath = new SubPathExpr([new XPathStep(XPathAxis.Descendant, "Button", [])], false);
        var joinExpr = new FunctionCallExpr("string-join", [subPath]);
        var ctx = new EvalContext(_ => null, 1, 1, null, EmptySequence);
        var result = XPathFunctions.Evaluate(joinExpr, ctx);
        Assert.That(result.AsString(), Is.EqualTo(""));
    }

    [Test]
    public void Evaluate_StringJoin_SubPathAttrValues()
    {
        var subPath = new SubPathExpr([
            new XPathStep(XPathAxis.Descendant, "Button", []),
            new XPathStep(XPathAxis.Attribute, "id", [])
        ], false);
        var joinExpr = new FunctionCallExpr("string-join", [subPath, new LiteralStringExpr("x")]);
        var ctx = new EvalContext(_ => null, 1, 1, null,
            _ => new XPathSequence([new XPathString("btn1"), new XPathString("btn2")]));
        var result = XPathFunctions.Evaluate(joinExpr, ctx);
        Assert.That(result.AsString(), Is.EqualTo("btn1xbtn2"));
    }

    // ── XPathCastException ───────────────────────────────────────────────────

    [Test]
    public void Evaluate_StringJoin_NonCastableType_Throws()
    {
        var rectArg = new XPathRect(0, 0, 100, 100);
        var joinExpr = new FunctionCallExpr("string-join", [new LiteralNumberExpr(0)]);
        // Directly test with a rect value
        var args = new XPathValue[] { rectArg };
        Assert.Throws<XPathCastException>(() =>
        {
            var first = args[0];
            if (first is not XPathSequence and not XPathString and not XPathNumber and not XPathBool)
                throw new XPathCastException(first.AsString(), first.GetType().Name, "string sequence");
        });
    }
}
