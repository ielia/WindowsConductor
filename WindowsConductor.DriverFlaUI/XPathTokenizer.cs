using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace WindowsConductor.DriverFlaUI;

internal enum XPathToken
{
    DoubleSlash,        // //
    Slash,              // /
    DoubleColon,        // ::
    DoubleDot,          // ..
    Dot,                // .
    LBracket,           // [
    RBracket,           // ]
    LParen,             // (
    RParen,             // )
    At,                 // @
    Comma,              // ,
    NotEquals,          // !=
    LessThanOrEqual,    // <=
    GreaterThanOrEqual, // >=
    Equals,             // =
    LessThan,           // <
    GreaterThan,        // >
    Plus,               // +
    Minus,              // -
    Star,               // *
    SingleQuotedString, // 'text' with '' escape
    DoubleQuotedString, // "text" with "" escape
    Number,             // 123 or 1.5
    Identifier,         // Button, Name, frontmost, contains, concat, etc.
}

internal static class XPathTokenizer
{
    // XPath 2.0+ strings: escape a quote by doubling it (' → '', " → "")
    private static readonly TextParser<Unit> SingleQuotedString =
        from open in Character.EqualTo('\'')
        from _ in Character.EqualTo('\'').IgnoreThen(Character.EqualTo('\'')).Value(Unit.Value).Try()
            .Or(Character.Except('\'').Value(Unit.Value))
            .Many()
        from close in Character.EqualTo('\'')
        select Unit.Value;

    private static readonly TextParser<Unit> DoubleQuotedString =
        from open in Character.EqualTo('"')
        from _ in Character.EqualTo('"').IgnoreThen(Character.EqualTo('"')).Value(Unit.Value).Try()
            .Or(Character.Except('"').Value(Unit.Value))
            .Many()
        from close in Character.EqualTo('"')
        select Unit.Value;

    private static readonly TextParser<Unit> NumberLiteral =
        from whole in Character.Digit.AtLeastOnce()
        from frac in Character.EqualTo('.').IgnoreThen(Character.Digit.AtLeastOnce()).OptionalOrDefault()
        select Unit.Value;

    private static readonly TextParser<Unit> IdentifierText =
        from first in Character.Letter.Or(Character.EqualTo('_'))
        from rest in Character.LetterOrDigit.Or(Character.EqualTo('-')).Or(Character.EqualTo('_')).Many()
        select Unit.Value;

    internal static Tokenizer<XPathToken> Instance { get; } =
        new TokenizerBuilder<XPathToken>()
            .Ignore(Span.WhiteSpace)
            .Match(Span.EqualTo("//"), XPathToken.DoubleSlash)
            .Match(Span.EqualTo("::"), XPathToken.DoubleColon)
            .Match(Span.EqualTo(".."), XPathToken.DoubleDot)
            .Match(Span.EqualTo("!="), XPathToken.NotEquals)
            .Match(Span.EqualTo("<="), XPathToken.LessThanOrEqual)
            .Match(Span.EqualTo(">="), XPathToken.GreaterThanOrEqual)
            .Match(Character.EqualTo('/'), XPathToken.Slash)
            .Match(Character.EqualTo('.'), XPathToken.Dot)
            .Match(Character.EqualTo('['), XPathToken.LBracket)
            .Match(Character.EqualTo(']'), XPathToken.RBracket)
            .Match(Character.EqualTo('('), XPathToken.LParen)
            .Match(Character.EqualTo(')'), XPathToken.RParen)
            .Match(Character.EqualTo('@'), XPathToken.At)
            .Match(Character.EqualTo(','), XPathToken.Comma)
            .Match(Character.EqualTo('='), XPathToken.Equals)
            .Match(Character.EqualTo('<'), XPathToken.LessThan)
            .Match(Character.EqualTo('>'), XPathToken.GreaterThan)
            .Match(Character.EqualTo('+'), XPathToken.Plus)
            .Match(Character.EqualTo('-'), XPathToken.Minus)
            .Match(Character.EqualTo('*'), XPathToken.Star)
            .Match(SingleQuotedString, XPathToken.SingleQuotedString)
            .Match(DoubleQuotedString, XPathToken.DoubleQuotedString)
            .Match(NumberLiteral, XPathToken.Number)
            .Match(IdentifierText, XPathToken.Identifier)
            .Build();

    /// <summary>
    /// Extracts the string value from a quoted token, unescaping doubled quotes (XPath 2.0+).
    /// </summary>
    internal static string GetStringValue(Token<XPathToken> token)
    {
        var raw = token.Span.ToStringValue();
        var quote = raw[0];
        var inner = raw[1..^1];
        return inner.Replace(new string(quote, 2), new string(quote, 1));
    }
}
