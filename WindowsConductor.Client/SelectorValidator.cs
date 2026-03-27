using System.Text.RegularExpressions;

namespace WindowsConductor.Client;

/// <summary>
/// Client-side validation of selector strings.
/// Catches obvious syntax errors early, before sending to the driver.
/// </summary>
public static class SelectorValidator
{
    private static readonly HashSet<string> ValidSimpleKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "automationid", "name", "text", "classname", "class", "type", "controltype"
    };

    // Matches //TypeName or /TypeName or //* — type must be present before any '['
    private static readonly Regex XPathStepRx = new(
        @"/{1,2}(\*|[A-Za-z]\w*)(\[|/|$)",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates a selector string.
    /// Throws <see cref="ArgumentException"/> if the selector is malformed.
    /// </summary>
    public static void Validate(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            throw new ArgumentException("Selector must not be empty.", nameof(selector));

        var s = selector.Trim();

        if (s.StartsWith('/'))
            ValidateXPath(s, selector);
        else
            ValidateSimpleSelector(s, selector);
    }

    private static void ValidateXPath(string xpath, string fullSelector)
    {
        // Check for missing type before predicate: //[, /[
        // This catches patterns like //[attr=value] or /[attr=value]
        if (Regex.IsMatch(xpath, @"/{1,2}\s*\["))
            throw new ArgumentException(
                $"XPath is missing an element type before predicate: '{fullSelector}'. " +
                "Expected format: //TypeName[@attr='value'] or //*[@attr='value']",
                nameof(fullSelector));

        // Check for unclosed brackets
        int depth = 0;
        foreach (char c in xpath)
        {
            if (c == '[') depth++;
            else if (c == ']') depth--;
            if (depth < 0)
                throw new ArgumentException(
                    $"Unexpected closing bracket in XPath expression: '{fullSelector}'",
                    nameof(fullSelector));
        }
        if (depth != 0)
            throw new ArgumentException(
                $"Unclosed bracket in XPath expression: '{fullSelector}'",
                nameof(fullSelector));

        // Check for empty predicates []
        if (xpath.Contains("[]"))
            throw new ArgumentException(
                $"Empty predicate '[]' in XPath expression: '{fullSelector}'",
                nameof(fullSelector));

        // Check that predicates have valid @attr='value' or numeric index format
        var predicateMatches = Regex.Matches(xpath, @"\[([^\]]*)\]");
        foreach (Match m in predicateMatches)
        {
            var content = m.Groups[1].Value.Trim();
            // Allow positional index predicates: [1], [3], etc.
            if (int.TryParse(content, out int idx))
            {
                if (idx < 1)
                    throw new ArgumentException(
                        $"Index predicate must be >= 1, got '{content}' in XPath expression: '{fullSelector}'",
                        nameof(fullSelector));
                continue;
            }
            if (!content.StartsWith('@'))
                throw new ArgumentException(
                    $"Invalid predicate syntax '{content}' in XPath expression: '{fullSelector}'. " +
                    "Predicates must start with '@'.",
                    nameof(fullSelector));
        }
    }

    private static void ValidateSimpleSelector(string selector, string fullSelector)
    {
        var parts = selector.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            throw new ArgumentException(
                $"Selector contains no valid parts: '{fullSelector}'", nameof(fullSelector));

        foreach (var part in parts)
        {
            if (part.StartsWith('['))
            {
                if (!part.EndsWith(']'))
                    throw new ArgumentException(
                        $"Unclosed bracket in selector: '{fullSelector}'", nameof(fullSelector));

                var inner = part[1..^1];
                if (string.IsNullOrWhiteSpace(inner))
                    throw new ArgumentException(
                        $"Empty bracket selector in: '{fullSelector}'", nameof(fullSelector));

                var eq = inner.IndexOf('=');
                if (eq <= 0)
                    throw new ArgumentException(
                        $"Bracket selector must have the format [key=value]: '{fullSelector}'",
                        nameof(fullSelector));

                var key = inner[..eq].Trim();
                if (!ValidSimpleKeys.Contains(key))
                    throw new ArgumentException(
                        $"Unknown selector attribute '{key}' in: '{fullSelector}'. " +
                        $"Valid attributes: {string.Join(", ", ValidSimpleKeys)}",
                        nameof(fullSelector));
            }
            else if (part.EndsWith(']') && !part.StartsWith('['))
            {
                throw new ArgumentException(
                    $"Unexpected closing bracket in selector: '{fullSelector}'", nameof(fullSelector));
            }
            else
            {
                // key=value or bare text
                var sep = part.IndexOf('=');
                if (sep > 0)
                {
                    var key = part[..sep].Trim();
                    if (!ValidSimpleKeys.Contains(key))
                        throw new ArgumentException(
                            $"Unknown selector attribute '{key}' in: '{fullSelector}'. " +
                            $"Valid attributes: {string.Join(", ", ValidSimpleKeys)}",
                            nameof(fullSelector));
                }
                // bare value (no '=') is valid — treated as name=value
            }
        }
    }
}
