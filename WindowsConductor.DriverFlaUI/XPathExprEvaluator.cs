using System.Globalization;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Evaluates XPath predicate expressions containing <c>position()</c>, <c>last()</c>,
/// <c>string-length()</c>, integer/decimal literals, arithmetic (<c>+</c>, <c>-</c>,
/// <c>*</c>, <c>/</c>, <c>mod</c>, <c>div</c>), comparisons (<c>=</c>, <c>!=</c>,
/// <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&gt;=</c>),
/// and logical operators (<c>and</c>, <c>or</c>).
/// </summary>
public static class XPathExprEvaluator
{
    internal enum TokenKind
    {
        Number, Position, Last, StringLength,
        Plus, Minus, Star, Slash, Mod, Div,
        Eq, NotEq, Lt, Gt, LtEq, GtEq,
        And, Or,
        LParen, RParen, End
    }

    internal readonly record struct Token(TokenKind Kind, double Value = 0, string? StringArg = null);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="expr"/> contains a function call
    /// that requires the expression evaluator rather than simple attribute matching.
    /// </summary>
    public static bool IsFunctionExpression(string expr) =>
        expr.Contains("position(") || expr.Contains("last(") || expr.Contains("string-length(");

    /// <summary>
    /// Evaluates the expression. Returns <c>true</c> when the result is non-zero (truthy).
    /// </summary>
    public static bool Evaluate(string expr, int position, int last, Func<string, string?>? getProperty = null)
    {
        var tokens = Tokenize(expr);
        int pos = 0;
        double result = ParseOr(tokens, ref pos, position, last, getProperty);
        if (pos < tokens.Count && tokens[pos].Kind != TokenKind.End)
            throw new ArgumentException($"Unexpected token at end of expression: '{expr}'");
        return result != 0;
    }

    /// <summary>
    /// Validates expression syntax without evaluating. Throws <see cref="ArgumentException"/> on errors.
    /// </summary>
    public static void Validate(string expr)
    {
        var tokens = Tokenize(expr);
        int pos = 0;
        ParseOr(tokens, ref pos, 1, 1, null);
        if (pos < tokens.Count && tokens[pos].Kind != TokenKind.End)
            throw new ArgumentException($"Unexpected token at end of expression: '{expr}'");
    }

    // ── Tokenizer ────────────────────────────────────────────────────────────

    internal static List<Token> Tokenize(string expr)
    {
        var tokens = new List<Token>();
        int i = 0;
        int len = expr.Length;

        while (i < len)
        {
            char c = expr[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c))
            {
                int start = i;
                while (i < len && char.IsDigit(expr[i])) i++;
                if (i < len && expr[i] == '.' && i + 1 < len && char.IsDigit(expr[i + 1]))
                {
                    i++;
                    while (i < len && char.IsDigit(expr[i])) i++;
                }
                tokens.Add(new Token(TokenKind.Number, double.Parse(expr[start..i], CultureInfo.InvariantCulture)));
                continue;
            }

            if (c == 'p' && MatchKeyword(expr, i, "position"))
            {
                i += 8;
                i = SkipWhitespace(expr, i);
                if (i < len && expr[i] == '(') i++;
                i = SkipWhitespace(expr, i);
                if (i < len && expr[i] == ')') i++;
                tokens.Add(new Token(TokenKind.Position));
                continue;
            }

            if (c == 'l' && MatchKeyword(expr, i, "last"))
            {
                i += 4;
                i = SkipWhitespace(expr, i);
                if (i < len && expr[i] == '(') i++;
                i = SkipWhitespace(expr, i);
                if (i < len && expr[i] == ')') i++;
                tokens.Add(new Token(TokenKind.Last));
                continue;
            }

            if (c == 's' && Match(expr, i, "string-length"))
            {
                i += 13;
                i = SkipWhitespace(expr, i);
                if (i < len && expr[i] == '(')
                {
                    i++;
                    int argStart = i;
                    int depth = 1;
                    while (i < len && depth > 0)
                    {
                        if (expr[i] == '(') depth++;
                        else if (expr[i] == ')') depth--;
                        if (depth > 0) i++;
                    }
                    string arg = expr[argStart..i].Trim();
                    if (i < len && expr[i] == ')') i++;
                    tokens.Add(new Token(TokenKind.StringLength, StringArg: arg));
                }
                continue;
            }

            if (c == 'm' && MatchKeyword(expr, i, "mod"))
            {
                i += 3;
                tokens.Add(new Token(TokenKind.Mod));
                continue;
            }

            if (c == 'd' && MatchKeyword(expr, i, "div"))
            {
                i += 3;
                tokens.Add(new Token(TokenKind.Div));
                continue;
            }

            if (c == 'a' && MatchKeyword(expr, i, "and"))
            {
                i += 3;
                tokens.Add(new Token(TokenKind.And));
                continue;
            }

            if (c == 'o' && MatchKeyword(expr, i, "or"))
            {
                i += 2;
                tokens.Add(new Token(TokenKind.Or));
                continue;
            }

            switch (c)
            {
                case '+': tokens.Add(new Token(TokenKind.Plus)); i++; continue;
                case '-': tokens.Add(new Token(TokenKind.Minus)); i++; continue;
                case '*': tokens.Add(new Token(TokenKind.Star)); i++; continue;
                case '/': tokens.Add(new Token(TokenKind.Slash)); i++; continue;
                case '(': tokens.Add(new Token(TokenKind.LParen)); i++; continue;
                case ')': tokens.Add(new Token(TokenKind.RParen)); i++; continue;
                case '=': tokens.Add(new Token(TokenKind.Eq)); i++; continue;
                case '!' when i + 1 < len && expr[i + 1] == '=':
                    tokens.Add(new Token(TokenKind.NotEq)); i += 2; continue;
                case '<' when i + 1 < len && expr[i + 1] == '=':
                    tokens.Add(new Token(TokenKind.LtEq)); i += 2; continue;
                case '<': tokens.Add(new Token(TokenKind.Lt)); i++; continue;
                case '>' when i + 1 < len && expr[i + 1] == '=':
                    tokens.Add(new Token(TokenKind.GtEq)); i += 2; continue;
                case '>': tokens.Add(new Token(TokenKind.Gt)); i++; continue;
                default:
                    throw new ArgumentException($"Unexpected character '{c}' in expression");
            }
        }

        tokens.Add(new Token(TokenKind.End));
        return tokens;
    }

    private static bool Match(string expr, int i, string word) =>
        i + word.Length <= expr.Length && expr.AsSpan(i, word.Length).SequenceEqual(word);

    private static bool MatchKeyword(string expr, int i, string word) =>
        Match(expr, i, word) && (i + word.Length >= expr.Length || !char.IsLetterOrDigit(expr[i + word.Length]));

    private static int SkipWhitespace(string expr, int i)
    {
        while (i < expr.Length && char.IsWhiteSpace(expr[i])) i++;
        return i;
    }

    // ── Recursive-descent parser/evaluator ───────────────────────────────────
    // Precedence (low → high): or → and → comparison → additive → multiplicative → unary → primary

    private static double ParseOr(List<Token> tokens, ref int pos, int position, int last, Func<string, string?>? getProperty)
    {
        double left = ParseAnd(tokens, ref pos, position, last, getProperty);
        while (pos < tokens.Count && tokens[pos].Kind == TokenKind.Or)
        {
            pos++;
            double right = ParseAnd(tokens, ref pos, position, last, getProperty);
            left = (left != 0 || right != 0) ? 1 : 0;
        }
        return left;
    }

    private static double ParseAnd(List<Token> tokens, ref int pos, int position, int last, Func<string, string?>? getProperty)
    {
        double left = ParseComparison(tokens, ref pos, position, last, getProperty);
        while (pos < tokens.Count && tokens[pos].Kind == TokenKind.And)
        {
            pos++;
            double right = ParseComparison(tokens, ref pos, position, last, getProperty);
            left = (left != 0 && right != 0) ? 1 : 0;
        }
        return left;
    }

    private static double ParseComparison(List<Token> tokens, ref int pos, int position, int last, Func<string, string?>? getProperty)
    {
        double left = ParseAdditive(tokens, ref pos, position, last, getProperty);

        while (pos < tokens.Count)
        {
            var kind = tokens[pos].Kind;
            if (kind is not (TokenKind.Eq or TokenKind.NotEq or TokenKind.Lt or TokenKind.Gt or TokenKind.LtEq or TokenKind.GtEq))
                break;
            pos++;
            double right = ParseAdditive(tokens, ref pos, position, last, getProperty);
            left = kind switch
            {
                TokenKind.Eq => left == right ? 1 : 0,
                TokenKind.NotEq => left != right ? 1 : 0,
                TokenKind.Lt => left < right ? 1 : 0,
                TokenKind.Gt => left > right ? 1 : 0,
                TokenKind.LtEq => left <= right ? 1 : 0,
                TokenKind.GtEq => left >= right ? 1 : 0,
                _ => throw new InvalidOperationException()
            };
        }

        return left;
    }

    private static double ParseAdditive(List<Token> tokens, ref int pos, int position, int last, Func<string, string?>? getProperty)
    {
        double left = ParseMultiplicative(tokens, ref pos, position, last, getProperty);

        while (pos < tokens.Count)
        {
            var kind = tokens[pos].Kind;
            if (kind is not (TokenKind.Plus or TokenKind.Minus)) break;
            pos++;
            double right = ParseMultiplicative(tokens, ref pos, position, last, getProperty);
            left = kind == TokenKind.Plus ? left + right : left - right;
        }

        return left;
    }

    private static double ParseMultiplicative(List<Token> tokens, ref int pos, int position, int last, Func<string, string?>? getProperty)
    {
        double left = ParseUnary(tokens, ref pos, position, last, getProperty);

        while (pos < tokens.Count && tokens[pos].Kind is TokenKind.Star or TokenKind.Slash or TokenKind.Mod or TokenKind.Div)
        {
            var kind = tokens[pos].Kind;
            pos++;
            double right = ParseUnary(tokens, ref pos, position, last, getProperty);
            left = kind switch
            {
                TokenKind.Star => left * right,
                TokenKind.Slash => (int)left / (int)right,
                TokenKind.Div => left / right,
                TokenKind.Mod => (int)left % (int)right,
                _ => throw new InvalidOperationException()
            };
        }

        return left;
    }

    private static double ParseUnary(List<Token> tokens, ref int pos, int position, int last, Func<string, string?>? getProperty)
    {
        if (pos < tokens.Count && tokens[pos].Kind == TokenKind.Minus)
        {
            pos++;
            return -ParseUnary(tokens, ref pos, position, last, getProperty);
        }

        return ParsePrimary(tokens, ref pos, position, last, getProperty);
    }

    private static double ParsePrimary(List<Token> tokens, ref int pos, int position, int last, Func<string, string?>? getProperty)
    {
        if (pos >= tokens.Count || tokens[pos].Kind == TokenKind.End)
            throw new ArgumentException("Unexpected end of expression");

        var token = tokens[pos];

        switch (token.Kind)
        {
            case TokenKind.Number:
                pos++;
                return token.Value;

            case TokenKind.Position:
                pos++;
                return position;

            case TokenKind.Last:
                pos++;
                return last;

            case TokenKind.StringLength:
                pos++;
                return EvalStringLength(token.StringArg!, getProperty);

            case TokenKind.LParen:
                pos++;
                double result = ParseOr(tokens, ref pos, position, last, getProperty);
                if (pos >= tokens.Count || tokens[pos].Kind != TokenKind.RParen)
                    throw new ArgumentException("Missing closing parenthesis in expression");
                pos++;
                return result;

            default:
                throw new ArgumentException($"Unexpected token '{token.Kind}' in expression");
        }
    }

    private static double EvalStringLength(string arg, Func<string, string?>? getProperty)
    {
        if (arg.StartsWith('@'))
        {
            var value = getProperty?.Invoke(arg[1..].ToLowerInvariant()) ?? "";
            return value.Length;
        }
        if ((arg.StartsWith('\'') && arg.EndsWith('\'')) || (arg.StartsWith('"') && arg.EndsWith('"')))
            return arg[1..^1].Length;
        return arg.Length;
    }
}
