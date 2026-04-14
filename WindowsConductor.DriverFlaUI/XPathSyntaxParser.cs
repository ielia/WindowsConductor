using System.Globalization;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using SP = Superpower.Parse;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Parses a tokenized XPath expression into a list of <see cref="XPathStep"/>.
/// Expression grammar follows XPath 3.1 structure with a function-call registry model.
/// </summary>
internal static class XPathSyntaxParser
{
    // ── Forward reference for recursive expression grammar ──────────────────

    private static readonly TokenListParser<XPathToken, XPathExpr> ExprRef =
        SP.Ref(() => Expression);

    // ── Primary expressions ─────────────────────────────────────────────────

    private static readonly TokenListParser<XPathToken, string> QuotedString =
        Token.EqualTo(XPathToken.SingleQuotedString)
            .Or(Token.EqualTo(XPathToken.DoubleQuotedString))
            .Select(XPathTokenizer.GetStringValue);

    private static readonly TokenListParser<XPathToken, string> IdentifierValue =
        Token.EqualTo(XPathToken.Identifier).Select(t => t.Span.ToStringValue());

    // Function call: name(arg1, arg2, ...)
    private static readonly TokenListParser<XPathToken, XPathExpr> FuncCall =
        (from name in IdentifierValue
         from lp in Token.EqualTo(XPathToken.LParen)
         from args in ExprRef
             .ManyDelimitedBy(Token.EqualTo(XPathToken.Comma))
             .OptionalOrDefault(Array.Empty<XPathExpr>())
         from rp in Token.EqualTo(XPathToken.RParen)
         select (XPathExpr)new FunctionCallExpr(name, args.ToList()))
        .Try();

    // Number literal
    private static readonly TokenListParser<XPathToken, XPathExpr> NumberExpr =
        Token.EqualTo(XPathToken.Number)
            .Select(t => (XPathExpr)new LiteralNumberExpr(
                double.Parse(t.Span.ToStringValue(), CultureInfo.InvariantCulture)));

    // String literal
    private static readonly TokenListParser<XPathToken, XPathExpr> StringExpr =
        QuotedString.Select(s => (XPathExpr)new LiteralStringExpr(s));

    // @attr reference
    private static readonly TokenListParser<XPathToken, XPathExpr> AttrRef =
        from at in Token.EqualTo(XPathToken.At)
        from name in IdentifierValue
        select (XPathExpr)new AttrRefExpr(name);

    // Parenthesized expression or sequence: (expr) or (expr, expr, ...)
    private static readonly TokenListParser<XPathToken, XPathExpr> ParenExpr =
        from lp in Token.EqualTo(XPathToken.LParen)
        from first in ExprRef
        from rest in Token.EqualTo(XPathToken.Comma).IgnoreThen(ExprRef).Many()
        from rp in Token.EqualTo(XPathToken.RParen)
        select rest.Length == 0
            ? first
            : (XPathExpr)new SequenceExpr(new[] { first }.Concat(rest).ToList());

    private static readonly TokenListParser<XPathToken, XPathExpr> Primary =
        FuncCall.Or(NumberExpr).Or(StringExpr).Or(AttrRef).Or(ParenExpr);

    // ── Unary minus ─────────────────────────────────────────────────────────

    private static readonly TokenListParser<XPathToken, XPathExpr> UnaryRef =
        SP.Ref(() => Unary);

    private static readonly TokenListParser<XPathToken, XPathExpr> Unary =
        Token.EqualTo(XPathToken.Minus)
            .IgnoreThen(UnaryRef)
            .Select(e => (XPathExpr)new UnaryMinusExpr(e))
        .Or(Primary);

    // ── Binary operators (precedence climbing via SP.Chain) ───────────────

    private static TokenListParser<XPathToken, XPathBinaryOp> IdentifierOp(string name, XPathBinaryOp op) =>
        Token.EqualTo(XPathToken.Identifier)
            .Where(t => string.Equals(t.Span.ToStringValue(), name, StringComparison.OrdinalIgnoreCase),
                $"'{name}'")
            .Value(op);

    // Multiplicative: * div mod
    private static readonly TokenListParser<XPathToken, XPathBinaryOp> MulOp =
        Token.EqualTo(XPathToken.Star).Value(XPathBinaryOp.Mul)
        .Or(IdentifierOp("div", XPathBinaryOp.Div))
        .Or(IdentifierOp("mod", XPathBinaryOp.Mod));

    private static readonly TokenListParser<XPathToken, XPathExpr> Multiplicative =
        SP.Chain(MulOp, Unary, (op, l, r) => new BinaryExpr(l, op, r));

    // Additive: + -
    private static readonly TokenListParser<XPathToken, XPathBinaryOp> AddOp =
        Token.EqualTo(XPathToken.Plus).Value(XPathBinaryOp.Add)
        .Or(Token.EqualTo(XPathToken.Minus).Value(XPathBinaryOp.Sub));

    private static readonly TokenListParser<XPathToken, XPathExpr> Additive =
        SP.Chain(AddOp, Multiplicative, (op, l, r) => new BinaryExpr(l, op, r));

    // Comparison: = != < > <= >=
    private static readonly TokenListParser<XPathToken, XPathBinaryOp> CompOp =
        Token.EqualTo(XPathToken.Equals).Value(XPathBinaryOp.Eq)
        .Or(Token.EqualTo(XPathToken.NotEquals).Value(XPathBinaryOp.NotEq))
        .Or(Token.EqualTo(XPathToken.LessThanOrEqual).Value(XPathBinaryOp.LtEq))
        .Or(Token.EqualTo(XPathToken.GreaterThanOrEqual).Value(XPathBinaryOp.GtEq))
        .Or(Token.EqualTo(XPathToken.LessThan).Value(XPathBinaryOp.Lt))
        .Or(Token.EqualTo(XPathToken.GreaterThan).Value(XPathBinaryOp.Gt));

    private static readonly TokenListParser<XPathToken, XPathExpr> Comparison =
        SP.Chain(CompOp, Additive, (op, l, r) => new BinaryExpr(l, op, r));

    // And
    private static readonly TokenListParser<XPathToken, XPathExpr> AndExpr =
        SP.Chain(IdentifierOp("and", XPathBinaryOp.And), Comparison, (op, l, r) => new BinaryExpr(l, op, r));

    // Or (lowest precedence)
    private static readonly TokenListParser<XPathToken, XPathExpr> OrExpr =
        SP.Chain(IdentifierOp("or", XPathBinaryOp.Or), AndExpr, (op, l, r) => new BinaryExpr(l, op, r));

    /// <summary>Full expression parser.</summary>
    internal static readonly TokenListParser<XPathToken, XPathExpr> Expression = OrExpr;

    // ── Step-level parsing ──────────────────────────────────────────────────

    internal static List<XPathStep> Parse(string xpath)
    {
        if (string.IsNullOrWhiteSpace(xpath))
            throw new ArgumentException("XPath expression must not be empty.", nameof(xpath));

        Token<XPathToken>[] tokens;
        try
        {
            tokens = XPathTokenizer.Instance.Tokenize(xpath).ToArray();
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid XPath expression: '{xpath}'. {ex.Message}", nameof(xpath));
        }

        if (tokens.Length == 0)
            throw new ArgumentException($"XPath expression produced no valid steps: '{xpath}'", nameof(xpath));

        // Special case: bare "/"
        if (tokens.Length == 1 && tokens[0].Kind == XPathToken.Slash)
            return [new XPathStep(XPathAxis.Self, ".", [])];

        var steps = new List<XPathStep>();
        int pos = 0;

        while (pos < tokens.Length)
        {
            var step = ParseStep(tokens, ref pos, xpath);
            if (step is not null)
                steps.Add(step);
        }

        if (steps.Count == 0)
            throw new ArgumentException($"XPath expression produced no valid steps: '{xpath}'", nameof(xpath));

        return steps;
    }

    private static XPathStep? ParseStep(Token<XPathToken>[] tokens, ref int pos, string xpath)
    {
        // Consume axis separators (/ or //)
        bool isDescendant = false;
        bool consumedAxis = false;

        while (pos < tokens.Length && tokens[pos].Kind is XPathToken.Slash or XPathToken.DoubleSlash)
        {
            isDescendant = tokens[pos].Kind == XPathToken.DoubleSlash;
            consumedAxis = true;
            pos++;
        }

        if (pos >= tokens.Length)
            return null;

        // frontmost:: axis
        bool isFrontmost = false;
        if (pos + 1 < tokens.Length
            && tokens[pos].Kind == XPathToken.Identifier
            && string.Equals(tokens[pos].Span.ToStringValue(), "frontmost", StringComparison.OrdinalIgnoreCase)
            && tokens[pos + 1].Kind == XPathToken.DoubleColon)
        {
            isFrontmost = true;
            pos += 2;
        }

        // Element type
        string type;
        if (pos < tokens.Length && tokens[pos].Kind == XPathToken.Star)
        {
            type = "*";
            pos++;
        }
        else if (pos < tokens.Length && tokens[pos].Kind == XPathToken.DoubleDot)
        {
            pos++;
            return new XPathStep(XPathAxis.Parent, "..", []);
        }
        else if (pos < tokens.Length && tokens[pos].Kind == XPathToken.Dot)
        {
            pos++;
            return new XPathStep(XPathAxis.Self, ".", []);
        }
        else if (pos < tokens.Length && tokens[pos].Kind == XPathToken.Identifier)
        {
            type = tokens[pos].Span.ToStringValue();
            pos++;
        }
        else if (pos < tokens.Length && tokens[pos].Kind == XPathToken.LBracket)
        {
            throw new ArgumentException(
                $"XPath is missing an element type before predicate at position {tokens[pos].Span.Position.Absolute}: '{xpath}'",
                nameof(xpath));
        }
        else
        {
            return null;
        }

        // Parse filters: [...]
        var filters = new List<XPathFilter>();

        while (pos < tokens.Length && tokens[pos].Kind == XPathToken.LBracket)
        {
            pos++; // skip [
            int contentStart = pos;
            int depth = 1;

            while (pos < tokens.Length && depth > 0)
            {
                if (tokens[pos].Kind == XPathToken.LBracket) depth++;
                else if (tokens[pos].Kind == XPathToken.RBracket) depth--;
                if (depth > 0) pos++;
            }

            if (depth != 0)
                throw new ArgumentException(
                    $"Unclosed predicate bracket in XPath expression: '{xpath}'", nameof(xpath));

            var predTokens = tokens[contentStart..pos];
            pos++; // skip ]

            if (predTokens.Length == 0)
                throw new ArgumentException(
                    $"Empty predicate '[]' in XPath expression: '{xpath}'", nameof(xpath));

            filters.Add(ParseFilter(predTokens, xpath));
        }

        var axis = isFrontmost ? XPathAxis.Frontmost
            : isDescendant ? XPathAxis.Descendant
            : consumedAxis ? XPathAxis.Child
            : XPathAxis.Child;

        return new XPathStep(axis, type, filters);
    }

    private static XPathFilter ParseFilter(Token<XPathToken>[] tokens, string xpath)
    {
        // Index filter: single literal number
        if (tokens.Length == 1 && tokens[0].Kind == XPathToken.Number)
        {
            int idx = (int)double.Parse(tokens[0].Span.ToStringValue(), CultureInfo.InvariantCulture);
            if (idx < 1)
                throw new ArgumentException(
                    $"Index predicate must be >= 1, got '{idx}' in XPath expression: '{xpath}'",
                    nameof(xpath));
            return new IndexFilter(idx);
        }

        // Negative index check: - Number
        if (tokens.Length == 2 && tokens[0].Kind == XPathToken.Minus && tokens[1].Kind == XPathToken.Number)
        {
            throw new ArgumentException(
                $"Index predicate must be >= 1 in XPath expression: '{xpath}'", nameof(xpath));
        }

        // Expression filter
        var tokenList = new TokenList<XPathToken>(tokens);
        var result = Expression.TryParse(tokenList);

        if (!result.HasValue)
        {
            string predText = ExtractSpanText(xpath, tokens);
            throw new ArgumentException(
                $"Invalid predicate syntax '{predText}' in XPath expression: '{xpath}'",
                nameof(xpath));
        }

        if (!result.Remainder.IsAtEnd)
        {
            string predText = ExtractSpanText(xpath, tokens);
            throw new ArgumentException(
                $"Unexpected tokens after predicate expression '{predText}' in XPath expression: '{xpath}'",
                nameof(xpath));
        }

        return new ExpressionFilter(result.Value);
    }

    private static string ExtractSpanText(string xpath, Token<XPathToken>[] tokens)
    {
        if (tokens.Length == 0) return "";
        int start = tokens[0].Span.Position.Absolute;
        var last = tokens[^1];
        int end = last.Span.Position.Absolute + last.Span.Length;
        return xpath[start..end];
    }
}
