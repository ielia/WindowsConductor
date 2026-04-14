using System.Globalization;

namespace WindowsConductor.DriverFlaUI;

// ── Enums ────────────────────────────────────────────────────────────────────

public enum XPathAxis { Child, Descendant, Parent, Self, Frontmost }

public enum XPathBinaryOp { Eq, NotEq, Lt, Gt, LtEq, GtEq, Add, Sub, Mul, Div, Mod, And, Or }

// ── Expression AST ──────────────────────────────────────────────────────────

public abstract record XPathExpr;

public sealed record LiteralStringExpr(string Value) : XPathExpr;
public sealed record LiteralNumberExpr(double Value) : XPathExpr;
public sealed record AttrRefExpr(string Name) : XPathExpr;
public sealed record FunctionCallExpr(string Name, IReadOnlyList<XPathExpr> Args) : XPathExpr;
public sealed record BinaryExpr(XPathExpr Left, XPathBinaryOp Op, XPathExpr Right) : XPathExpr;
public sealed record UnaryMinusExpr(XPathExpr Operand) : XPathExpr;
public sealed record SequenceExpr(IReadOnlyList<XPathExpr> Items) : XPathExpr;
public sealed record SubPathExpr(IReadOnlyList<XPathStep> Steps, bool IsAbsolute) : XPathExpr;

// ── Step filters ────────────────────────────────────────────────────────────

public abstract record XPathFilter;

/// <summary>Boolean predicate filter — keeps elements where the expression evaluates to true.</summary>
public sealed record ExpressionFilter(XPathExpr Expr) : XPathFilter;

/// <summary>Positional index filter — selects the Nth element (1-based) from the current result set.</summary>
public sealed record IndexFilter(int Index) : XPathFilter;

// ── Step ────────────────────────────────────────────────────────────────────

/// <summary>One step of an XPath expression: axis + element type + ordered list of filters.</summary>
public sealed record XPathStep(XPathAxis Axis, string Type, IReadOnlyList<XPathFilter> Filters);

// ── Runtime values ──────────────────────────────────────────────────────────

public abstract record XPathValue
{
    public abstract string AsString();
    public abstract double AsNumber();
    public abstract bool AsBool();
}

public sealed record XPathString(string Value) : XPathValue
{
    public override string AsString() => Value;

    public override double AsNumber() =>
        double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            ? n : double.NaN;

    public override bool AsBool() => !string.IsNullOrEmpty(Value);
}

public sealed record XPathNumber(double Value) : XPathValue
{
    public override string AsString() => Value.ToString(CultureInfo.InvariantCulture);
    public override double AsNumber() => Value;
    public override bool AsBool() => Value != 0 && !double.IsNaN(Value);
}

public sealed record XPathBool(bool Value) : XPathValue
{
    public override string AsString() => Value ? "true" : "false";
    public override double AsNumber() => Value ? 1 : 0;
    public override bool AsBool() => Value;
}

public sealed record XPathRect(int X, int Y, int Width, int Height) : XPathValue
{
    public bool ContainsPoint(int px, int py) =>
        px >= X && px < X + Width && py >= Y && py < Y + Height;

    public override string AsString() => $"({X},{Y},{Width},{Height})";
    public override double AsNumber() => double.NaN;
    public override bool AsBool() => true;
}

public sealed record XPathPoint(double X, double Y) : XPathValue
{
    public override string AsString() => $"({X.ToString(CultureInfo.InvariantCulture)},{Y.ToString(CultureInfo.InvariantCulture)})";
    public override double AsNumber() => double.NaN;
    public override bool AsBool() => true;
}

// ── Evaluation context ──────────────────────────────────────────────────────

/// <summary>Context provided to expression evaluation — property resolver, positional info, and element reference.</summary>
/// <param name="GetProperty">Resolves an attribute name to its string value.</param>
/// <param name="Position">1-based position of the current element in the filter's input set.</param>
/// <param name="Last">Size of the filter's input set.</param>
/// <param name="Element">The current element (as object to avoid FlaUI dependency). May be null in tests.</param>
public sealed record EvalContext(
    Func<string, string?> GetProperty,
    int Position,
    int Last,
    object? Element,
    Func<SubPathExpr, bool>? SubPathEvaluator = null);
