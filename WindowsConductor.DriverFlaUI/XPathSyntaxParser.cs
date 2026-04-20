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
        SP.Ref(() => Expression!);  // Expression is assigned before any parse runs

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

    // Empty sequence: ()
    private static readonly TokenListParser<XPathToken, XPathExpr> EmptySequence =
        (from lp in Token.EqualTo(XPathToken.LParen)
         from rp in Token.EqualTo(XPathToken.RParen)
         select (XPathExpr)new SequenceExpr([])).Try();

    // Parenthesized expression or sequence: (expr) or (expr, expr, ...)
    private static readonly TokenListParser<XPathToken, XPathExpr> ParenExpr =
        from lp in Token.EqualTo(XPathToken.LParen)
        from first in ExprRef
        from rest in Token.EqualTo(XPathToken.Comma).IgnoreThen(ExprRef).Many()
        from rp in Token.EqualTo(XPathToken.RParen)
        select rest.Length == 0
            ? first
            : (XPathExpr)new SequenceExpr(new[] { first }.Concat(rest).ToList());

    // Sub-path expression used inside predicates.
    // Supports all valid XPath path starts: /type (absolute), //type, ./type, .//type, ../type, etc.
    private static readonly TokenListParser<XPathToken, XPathExpr> SubPathPrimary =
        ParseSubPathExpression;

    private static TokenListParserResult<XPathToken, XPathExpr> ParseSubPathExpression(TokenList<XPathToken> input)
    {
        if (input.IsAtEnd) return TokenListParserResult.Empty<XPathToken, XPathExpr>(input);

        var allTokens = CollectRemainingTokens(input);
        if (allTokens.Length == 0) return TokenListParserResult.Empty<XPathToken, XPathExpr>(input);

        // Sub-path must start with a path-like token or a named axis (identifier::)
        var firstKind = allTokens[0].Kind;
        bool isNamedAxisStart = firstKind == XPathToken.Identifier
            && allTokens.Length >= 2 && allTokens[1].Kind == XPathToken.DoubleColon;
        if (!isNamedAxisStart
            && firstKind is not (XPathToken.DoubleSlash or XPathToken.Slash or XPathToken.Dot or XPathToken.DoubleDot))
            return TokenListParserResult.Empty<XPathToken, XPathExpr>(input);

        // Bare . is the context node reference (used in attribute predicates like [.='value'])
        if (firstKind == XPathToken.Dot
            && (allTokens.Length < 2 || allTokens[1].Kind is not (XPathToken.Slash or XPathToken.DoubleSlash)))
        {
            var afterDot = input.ConsumeToken().Remainder;
            return TokenListParserResult.Value<XPathToken, XPathExpr>(
                new ContextNodeExpr(), input, afterDot);
        }

        bool isAbsolute = firstKind is XPathToken.Slash or XPathToken.DoubleSlash;

        try
        {
            int pos = 0;
            var steps = new List<XPathStep>();

            // First step — ParseStep handles axis prefix (/, //, ., ..)
            int prevCount = steps.Count;
            ParseStep(allTokens, ref pos, "<sub-path>", steps);
            if (steps.Count == prevCount)
                return TokenListParserResult.Empty<XPathToken, XPathExpr>(input);

            // Additional steps only when preceded by / or //
            while (pos < allTokens.Length
                   && allTokens[pos].Kind is XPathToken.Slash or XPathToken.DoubleSlash)
            {
                prevCount = steps.Count;
                ParseStep(allTokens, ref pos, "<sub-path>", steps);
                if (steps.Count == prevCount) break;
            }

            // Remove no-op Self steps (produced by . prefix in ./type or .//type)
            // but only when they act as a prefix — keep if it's the sole step (e.g. self::*)
            if (steps.Count > 1)
                steps.RemoveAll(s => s.Axis == XPathAxis.Self && s.Type == "*" && s.Filters.Count == 0);

            if (steps.Count == 0)
                return TokenListParserResult.Empty<XPathToken, XPathExpr>(input);

            // Advance TokenList past consumed tokens
            var remaining = input;
            for (int i = 0; i < pos; i++)
                remaining = remaining.ConsumeToken().Remainder;

            return TokenListParserResult.Value<XPathToken, XPathExpr>(
                new SubPathExpr(steps, isAbsolute), input, remaining);
        }
        catch
        {
            return TokenListParserResult.Empty<XPathToken, XPathExpr>(input);
        }
    }

    private static Token<XPathToken>[] CollectRemainingTokens(TokenList<XPathToken> input)
    {
        var tokens = new List<Token<XPathToken>>();
        var current = input;
        while (!current.IsAtEnd)
        {
            var result = current.ConsumeToken();
            if (!result.HasValue) break;
            tokens.Add(result.Value);
            current = result.Remainder;
        }
        return tokens.ToArray();
    }

    private static readonly TokenListParser<XPathToken, XPathExpr> Primary =
        FuncCall.Or(NumberExpr).Or(StringExpr).Or(AttrRef).Or(SubPathPrimary).Or(EmptySequence).Or(ParenExpr);

    // ── Unary minus ─────────────────────────────────────────────────────────

    private static readonly TokenListParser<XPathToken, XPathExpr> UnaryRef =
        SP.Ref(() => Unary!);  // Unary is assigned before any parse runs

    private static readonly TokenListParser<XPathToken, XPathExpr> Unary =
        Token.EqualTo(XPathToken.Minus)
            .IgnoreThen(UnaryRef)
            .Select(e => (XPathExpr)new UnaryMinusExpr(e))
        .Or(Token.EqualTo(XPathToken.Plus)
            .IgnoreThen(UnaryRef)
            .Select(e => (XPathExpr)new UnaryPlusExpr(e)))
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
        .Or(IdentifierOp("idiv", XPathBinaryOp.IntDiv))
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

    /// <summary>Parses a standalone expression string (e.g. a function call).</summary>
    internal static XPathExpr ParseExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Expression must not be empty.", nameof(expression));

        Token<XPathToken>[] tokens;
        try
        {
            tokens = XPathTokenizer.Instance.Tokenize(expression).ToArray();
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid expression: '{expression}'. {ex.Message}", nameof(expression));
        }

        var tokenList = new TokenList<XPathToken>(tokens);
        var result = Expression.TryParse(tokenList);

        if (!result.HasValue)
            throw new ArgumentException($"Invalid expression syntax: '{expression}'", nameof(expression));

        if (!result.Remainder.IsAtEnd)
            throw new ArgumentException(
                $"Unexpected tokens after expression: '{expression}'", nameof(expression));

        return result.Value;
    }

    /// <summary>Validates a standalone expression without evaluating it.</summary>
    internal static void ValidateExpression(string expression) => ParseExpression(expression);

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
            ParseStep(tokens, ref pos, xpath, steps);
        }

        if (steps.Count == 0)
            throw new ArgumentException($"XPath expression produced no valid steps: '{xpath}'", nameof(xpath));

        return steps;
    }

    private static void ParseStep(Token<XPathToken>[] tokens, ref int pos, string xpath, List<XPathStep> steps)
    {
        // Consume axis separators (/ or //)
        bool isDescendant = false;
        int posBeforeSep = pos;

        while (pos < tokens.Length && tokens[pos].Kind is XPathToken.Slash or XPathToken.DoubleSlash)
        {
            isDescendant = tokens[pos].Kind == XPathToken.DoubleSlash;
            pos++;
        }

        bool hadSeparator = pos > posBeforeSep;

        if (pos >= tokens.Length)
            return;

        // Named axis: frontmost::, ancestor::, ancestor-or-self::, descendant::, etc.
        XPathAxis? namedAxis = null;
        if (pos + 1 < tokens.Length
            && tokens[pos].Kind == XPathToken.Identifier
            && tokens[pos + 1].Kind == XPathToken.DoubleColon)
        {
            var axisName = tokens[pos].Span.ToStringValue();
            namedAxis = axisName.ToLowerInvariant() switch
            {
                "frontmost" => XPathAxis.Frontmost,
                "ancestor" => XPathAxis.Ancestor,
                "ancestor-or-self" => XPathAxis.AncestorOrSelf,
                "child" => XPathAxis.Child,
                "descendant" => XPathAxis.Descendant,
                "descendant-or-self" => XPathAxis.DescendantOrSelf,
                "self" => XPathAxis.Self,
                "sibling" => XPathAxis.Sibling,
                "preceding-sibling" => XPathAxis.PrecedingSibling,
                "following-sibling" => XPathAxis.FollowingSibling,
                "attribute" => XPathAxis.Attribute,
                _ => null
            };

            if (namedAxis is not null)
                pos += 2;
        }

        // Attribute step: @name or @* or attribute::name
        bool isAttributeAxis = namedAxis == XPathAxis.Attribute;
        if (!isAttributeAxis && pos < tokens.Length && tokens[pos].Kind == XPathToken.At)
        {
            if (!hadSeparator && posBeforeSep > 0)
                throw new ArgumentException(
                    $"Missing '/' before '@' at position {tokens[pos].Span.Position.Absolute} in XPath expression: '{xpath}'",
                    nameof(xpath));
            isAttributeAxis = true;
            pos++;
        }

        // Element type (or attribute name for @-steps)
        string type;
        if (pos < tokens.Length && tokens[pos].Kind == XPathToken.Star)
        {
            type = "*";
            pos++;
        }
        else if (!isAttributeAxis && pos < tokens.Length && tokens[pos].Kind == XPathToken.DoubleDot)
        {
            pos++;
            steps.Add(new XPathStep(XPathAxis.Parent, "..", []));
            return;
        }
        else if (!isAttributeAxis && pos < tokens.Length && tokens[pos].Kind == XPathToken.Dot)
        {
            pos++;
            steps.Add(new XPathStep(XPathAxis.Self, "*", []));
            return;
        }
        else if (pos < tokens.Length && tokens[pos].Kind == XPathToken.Identifier)
        {
            type = tokens[pos].Span.ToStringValue();
            pos++;
        }
        else if (pos < tokens.Length && tokens[pos].Kind == XPathToken.LBracket)
        {
            throw new ArgumentException(
                isAttributeAxis
                    ? $"Expected attribute name or '*' after '@' in XPath expression: '{xpath}'"
                    : $"XPath is missing an element type before predicate at position {tokens[pos].Span.Position.Absolute}: '{xpath}'",
                nameof(xpath));
        }
        else
        {
            if (isAttributeAxis)
                throw new ArgumentException(
                    $"Expected attribute name or '*' after '@' in XPath expression: '{xpath}'",
                    nameof(xpath));
            return;
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

        var axis = isAttributeAxis ? XPathAxis.Attribute
            : namedAxis
            ?? (isDescendant ? XPathAxis.Descendant : XPathAxis.Child);

        // When // precedes a named axis that doesn't already traverse descendants,
        // expand // to descendant-or-self::* so the axis operates on each descendant node.
        if (isDescendant && namedAxis is not null
            && namedAxis is not (XPathAxis.Descendant or XPathAxis.DescendantOrSelf or XPathAxis.Frontmost))
            steps.Add(new XPathStep(XPathAxis.DescendantOrSelf, "*", []));

        steps.Add(new XPathStep(axis, type, filters));
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
        if (start >= 0 && end <= xpath.Length && start < end)
            return xpath[start..end];
        return string.Join("", tokens.Select(t => t.Span.ToStringValue()));
    }
}
