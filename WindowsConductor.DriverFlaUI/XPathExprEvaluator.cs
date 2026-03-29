namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Evaluates XPath predicate expressions containing <c>position()</c>, <c>last()</c>,
/// integer literals, arithmetic (<c>+</c>, <c>-</c>, <c>*</c>), comparisons
/// (<c>=</c>, <c>!=</c>, <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&gt;=</c>),
/// and parenthesized sub-expressions.
/// </summary>
public static class XPathExprEvaluator
{
    internal enum TokenKind
    {
        Number, Position, Last,
        Plus, Minus, Star, Slash,
        Eq, NotEq, Lt, Gt, LtEq, GtEq,
        LParen, RParen, End
    }

    internal readonly record struct Token(TokenKind Kind, int Value = 0);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="expr"/> contains <c>position()</c> or <c>last()</c>.
    /// </summary>
    public static bool IsFunctionExpression(string expr) =>
        expr.Contains("position(") || expr.Contains("last(");

    /// <summary>
    /// Evaluates the expression with the given <paramref name="position"/> (1-based) and <paramref name="last"/> values.
    /// Returns <c>true</c> when the result is non-zero (truthy).
    /// </summary>
    public static bool Evaluate(string expr, int position, int last)
    {
        var tokens = Tokenize(expr);
        int pos = 0;
        int result = ParseComparison(tokens, ref pos, position, last);
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
        ParseComparison(tokens, ref pos, 1, 1);
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
                tokens.Add(new Token(TokenKind.Number, int.Parse(expr[start..i])));
                continue;
            }

            if (c == 'p' && Match(expr, i, "position"))
            {
                i += 8; // "position"
                i = SkipWhitespace(expr, i);
                if (i < len && expr[i] == '(') i++;
                i = SkipWhitespace(expr, i);
                if (i < len && expr[i] == ')') i++;
                tokens.Add(new Token(TokenKind.Position));
                continue;
            }

            if (c == 'l' && Match(expr, i, "last"))
            {
                i += 4; // "last"
                i = SkipWhitespace(expr, i);
                if (i < len && expr[i] == '(') i++;
                i = SkipWhitespace(expr, i);
                if (i < len && expr[i] == ')') i++;
                tokens.Add(new Token(TokenKind.Last));
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
                case '=' when i + 1 < len && expr[i + 1] == '>':
                    tokens.Add(new Token(TokenKind.GtEq)); i += 2; continue;
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

    private static int SkipWhitespace(string expr, int i)
    {
        while (i < expr.Length && char.IsWhiteSpace(expr[i])) i++;
        return i;
    }

    // ── Recursive-descent parser/evaluator ───────────────────────────────────
    // Precedence (low → high): comparison → additive → multiplicative → unary → primary

    private static int ParseComparison(List<Token> tokens, ref int pos, int position, int last)
    {
        int left = ParseAdditive(tokens, ref pos, position, last);

        while (pos < tokens.Count)
        {
            var kind = tokens[pos].Kind;
            if (kind is not (TokenKind.Eq or TokenKind.NotEq or TokenKind.Lt or TokenKind.Gt or TokenKind.LtEq or TokenKind.GtEq))
                break;
            pos++;
            int right = ParseAdditive(tokens, ref pos, position, last);
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

    private static int ParseAdditive(List<Token> tokens, ref int pos, int position, int last)
    {
        int left = ParseMultiplicative(tokens, ref pos, position, last);

        while (pos < tokens.Count)
        {
            var kind = tokens[pos].Kind;
            if (kind is not (TokenKind.Plus or TokenKind.Minus)) break;
            pos++;
            int right = ParseMultiplicative(tokens, ref pos, position, last);
            left = kind == TokenKind.Plus ? left + right : left - right;
        }

        return left;
    }

    private static int ParseMultiplicative(List<Token> tokens, ref int pos, int position, int last)
    {
        int left = ParseUnary(tokens, ref pos, position, last);

        while (pos < tokens.Count && tokens[pos].Kind is TokenKind.Star or TokenKind.Slash)
        {
            var kind = tokens[pos].Kind;
            pos++;
            int right = ParseUnary(tokens, ref pos, position, last);
            left = kind == TokenKind.Star ? left * right : left / right;
        }

        return left;
    }

    private static int ParseUnary(List<Token> tokens, ref int pos, int position, int last)
    {
        if (pos < tokens.Count && tokens[pos].Kind == TokenKind.Minus)
        {
            pos++;
            return -ParseUnary(tokens, ref pos, position, last);
        }

        return ParsePrimary(tokens, ref pos, position, last);
    }

    private static int ParsePrimary(List<Token> tokens, ref int pos, int position, int last)
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

            case TokenKind.LParen:
                pos++;
                int result = ParseComparison(tokens, ref pos, position, last);
                if (pos >= tokens.Count || tokens[pos].Kind != TokenKind.RParen)
                    throw new ArgumentException("Missing closing parenthesis in expression");
                pos++;
                return result;

            default:
                throw new ArgumentException($"Unexpected token '{token.Kind}' in expression");
        }
    }
}
