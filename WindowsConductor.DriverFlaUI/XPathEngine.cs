using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace WindowsConductor.DriverFlaUI;

// ── Data model ───────────────────────────────────────────────────────────────

public enum XPathAxis { Child, Descendant, Parent, Self }

public enum AttributeMatchMode { Exact, StartsWith, Contains, EndsWith }

public abstract record ConcatArg;
public sealed record StringConcatArg(string Value) : ConcatArg;
public sealed record AttrConcatArg(string Attribute) : ConcatArg;

/// <summary>A single attribute predicate such as <c>@Attr='value'</c>.</summary>
public sealed record XPathPredicate(
    string Attribute,
    IReadOnlyList<string> Values,
    AttributeMatchMode MatchMode = AttributeMatchMode.Exact,
    IReadOnlyList<ConcatArg>? ConcatArgs = null);

/// <summary>A predicate using the <c>contains()</c> function.</summary>
public abstract record ContainsPredicate;

/// <summary><c>contains(bounds(), point(x, y))</c> — spatial containment check.</summary>
public sealed record ContainsBoundsPoint(double X, double Y) : ContainsPredicate;

/// <summary><c>contains(haystack, needle)</c> — substring check where each arg is an attribute reference or string literal.</summary>
public sealed record ContainsSubstring(string Haystack, string Needle, bool HaystackIsAttr, bool NeedleIsAttr) : ContainsPredicate;

/// <summary>One step of an XPath expression: axis + element type + predicates + optional 1-based positional index.</summary>
/// <param name="Index">Result-set index from <c>[N]</c> — selects the Nth overall match.</param>
/// <param name="FunctionPredicates">Expressions containing <c>position()</c>, <c>last()</c>, or <c>string-length()</c> that must all evaluate to truthy.</param>
/// <param name="OrPredicateGroups">Predicate groups joined by <c>or</c> — for each group, at least one predicate must match.</param>
/// <param name="ContainsPredicates"><c>contains()</c> function predicates — spatial or substring checks.</param>
public sealed record XPathStep(
    XPathAxis Axis,
    string Type,
    IReadOnlyList<XPathPredicate> Predicates,
    int? Index = null,
    IReadOnlyList<string>? FunctionPredicates = null,
    IReadOnlyList<IReadOnlyList<XPathPredicate>>? OrPredicateGroups = null,
    IReadOnlyList<ContainsPredicate>? ContainsPredicates = null);

// ── Engine ───────────────────────────────────────────────────────────────────

public sealed class XPathEngine
{
    // Extracts individual quoted values from a list like  'v1','v2','v3'
    private static readonly Regex ListItemRx = new(
        @"['""](?<item>[^'""]*)['""]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<AutomationElement> Evaluate(
        AutomationElement root, string xpath,
        Func<AutomationElement, bool>? isAtBoundary = null)
    {
        var steps = ParseXPath(xpath);
        IReadOnlyList<AutomationElement> current = new[] { root };

        foreach (var step in steps)
            current = ApplyStep(current, step, isAtBoundary);

        return current;
    }

    /// <summary>
    /// Validates an XPath expression without evaluating it.
    /// Throws <see cref="ArgumentException"/> if the expression is malformed.
    /// </summary>
    public static void Validate(string xpath) => ParseXPath(xpath);

    // ── Step evaluation ──────────────────────────────────────────────────────

    private static IReadOnlyList<AutomationElement> ApplyStep(
        IReadOnlyList<AutomationElement> roots, XPathStep step,
        Func<AutomationElement, bool>? isAtBoundary = null)
    {
        if (step.Axis == XPathAxis.Self)
            return roots;

        if (step.Axis == XPathAxis.Parent)
        {
            var parents = new List<AutomationElement>();
            foreach (var root in roots)
            {
                if (isAtBoundary is not null && isAtBoundary(root))
                    throw new InvalidOperationException(
                        "XPath '..' cannot navigate above the application root (--confine-to-app is active).");
                var parent = root.Parent;
                if (parent is not null)
                    parents.Add(parent);
            }
            return parents;
        }

        var results = new List<AutomationElement>();

        foreach (var root in roots)
        {
            var candidates = step.Axis == XPathAxis.Descendant
                ? root.FindAllDescendants()
                : root.FindAllChildren();

            foreach (var el in candidates)
            {
                Func<string, string?> getProp = k => ElementProperties.Resolve(el, k);
                if (MatchesStep(step, getProp) && MatchesFunctionPredicates(el, step) && MatchesContainsPredicates(el, step, getProp))
                    results.Add(el);
            }
        }

        if (step.Index is { } idx)
        {
            if (idx < 1 || idx > results.Count)
                return [];
            return [results[idx - 1]];
        }

        return results;
    }

    private static bool MatchesFunctionPredicates(AutomationElement element, XPathStep step)
    {
        if (step.FunctionPredicates is not { Count: > 0 }) return true;

        Func<string, string?> getProp = k => ElementProperties.Resolve(element, k);

        int position = 1;
        int lastCount = 1;

        var parent = element.Parent;
        if (parent is not null)
        {
            var siblings = parent.FindAllChildren();
            position = 0;
            lastCount = 0;
            foreach (var sibling in siblings)
            {
                if (step.Type == "*" || string.Equals(
                        sibling.Properties.ControlType.ValueOrDefault.ToString(),
                        element.Properties.ControlType.ValueOrDefault.ToString(),
                        StringComparison.Ordinal))
                {
                    lastCount++;
                    if (sibling.Equals(element))
                        position = lastCount;
                }
            }
            if (position == 0) return false;
        }

        foreach (var expr in step.FunctionPredicates)
        {
            if (!XPathExprEvaluator.Evaluate(expr, position, lastCount, getProp))
                return false;
        }

        return true;
    }

    private static bool MatchesContainsPredicates(
        AutomationElement element, XPathStep step, Func<string, string?> getProperty)
    {
        if (step.ContainsPredicates is not { Count: > 0 }) return true;

        foreach (var cp in step.ContainsPredicates)
        {
            switch (cp)
            {
                case ContainsBoundsPoint bp:
                    var rect = element.BoundingRectangle;
                    if (!rect.Contains(new System.Drawing.Point((int)bp.X, (int)bp.Y)))
                        return false;
                    break;
                case ContainsSubstring cs:
                    var haystack = cs.HaystackIsAttr ? getProperty(cs.Haystack.ToLowerInvariant()) ?? "" : cs.Haystack;
                    var needle = cs.NeedleIsAttr ? getProperty(cs.Needle.ToLowerInvariant()) ?? "" : cs.Needle;
                    if (!haystack.Contains(needle, StringComparison.InvariantCultureIgnoreCase))
                        return false;
                    break;
            }
        }

        return true;
    }

    internal static bool MatchesStep(XPathStep step, Func<string, string?> getProperty)
    {
        // Type check
        if (step.Type != "*")
        {
            if (!Enum.TryParse<ControlType>(step.Type, ignoreCase: true, out var ct))
                return false;
            string? controlType = getProperty("controltype");
            if (!string.Equals(controlType, ct.ToString(), StringComparison.Ordinal))
                return false;
        }

        // AND predicates (all must pass)
        foreach (var pred in step.Predicates)
        {
            if (!MatchesPredicate(pred, getProperty))
                return false;
        }

        // OR predicate groups (for each group, at least one must pass)
        if (step.OrPredicateGroups is { Count: > 0 })
        {
            foreach (var group in step.OrPredicateGroups)
            {
                if (!group.Any(pred => MatchesPredicate(pred, getProperty)))
                    return false;
            }
        }

        return true;
    }

    private static bool MatchesPredicate(XPathPredicate pred, Func<string, string?> getProperty)
    {
        string? actual = getProperty(pred.Attribute.ToLowerInvariant());

        if (pred.ConcatArgs is { Count: > 0 })
        {
            string concatValue = string.Concat(pred.ConcatArgs.Select(a => a switch
            {
                StringConcatArg s => s.Value,
                AttrConcatArg attr => getProperty(attr.Attribute.ToLowerInvariant()) ?? "",
                _ => ""
            }));
            return MatchesValue(actual, concatValue, pred.MatchMode);
        }

        return pred.Values.Any(v => MatchesValue(actual, v, pred.MatchMode));
    }

    private static bool MatchesValue(string? actual, string expected, AttributeMatchMode mode) =>
        mode switch
        {
            AttributeMatchMode.Exact => string.Equals(actual, expected, StringComparison.InvariantCultureIgnoreCase),
            AttributeMatchMode.StartsWith => actual?.StartsWith(expected, StringComparison.InvariantCultureIgnoreCase) == true,
            AttributeMatchMode.Contains => actual?.Contains(expected, StringComparison.InvariantCultureIgnoreCase) == true,
            AttributeMatchMode.EndsWith => actual?.EndsWith(expected, StringComparison.InvariantCultureIgnoreCase) == true,
            _ => false
        };

    // ── XPath parser ─────────────────────────────────────────────────────────

    internal static List<XPathStep> ParseXPath(string xpath)
    {
        if (string.IsNullOrWhiteSpace(xpath))
            throw new ArgumentException("XPath expression must not be empty.", nameof(xpath));

        var steps = new List<XPathStep>();
        int pos = 0;
        int len = xpath.Length;

        while (pos < len)
        {
            // ── Axis ─────────────────────────────────────────────────────────
            bool isDescendant = false;
            int axisStart = pos;

            if (pos < len && xpath[pos] == '/')
            {
                pos++;
                if (pos < len && xpath[pos] == '/')
                {
                    isDescendant = true;
                    pos++;
                }
            }

            if (pos >= len)
            {
                if (axisStart == 0 && pos == 1)
                    steps.Add(new XPathStep(XPathAxis.Self, ".", []));
                break;
            }

            // ── Element type ─────────────────────────────────────────────────
            int typeStart = pos;
            while (pos < len && xpath[pos] != '[' && xpath[pos] != '/')
                pos++;

            string type = xpath[typeStart..pos].Trim();

            if (string.IsNullOrEmpty(type))
            {
                if (pos < len && xpath[pos] == '[')
                    throw new ArgumentException(
                        $"XPath is missing an element type before predicate at position {typeStart}: '{xpath}'",
                        nameof(xpath));
                continue;
            }

            if (type == ".")
            {
                steps.Add(new XPathStep(XPathAxis.Self, ".", []));
                continue;
            }

            if (type == "..")
            {
                steps.Add(new XPathStep(XPathAxis.Parent, "..", []));
                continue;
            }

            // ── Predicates ───────────────────────────────────────────────────
            var predicates = new List<XPathPredicate>();
            int? index = null;
            List<string>? functionPredicates = null;
            List<IReadOnlyList<XPathPredicate>>? orGroups = null;
            List<ContainsPredicate>? containsPredicates = null;

            while (pos < len && xpath[pos] == '[')
            {
                pos++;
                int predStart = pos;
                int depth = 1;

                while (pos < len && depth > 0)
                {
                    if (xpath[pos] == '[') depth++;
                    else if (xpath[pos] == ']') depth--;
                    pos++;
                }

                if (depth != 0)
                    throw new ArgumentException(
                        $"Unclosed predicate bracket in XPath expression: '{xpath}'",
                        nameof(xpath));

                string predContent = xpath[predStart..(pos - 1)];

                if (string.IsNullOrWhiteSpace(predContent))
                    throw new ArgumentException(
                        $"Empty predicate '[]' in XPath expression: '{xpath}'",
                        nameof(xpath));

                // Positional index predicate: [3]
                if (int.TryParse(predContent.Trim(), out int parsedIndex))
                {
                    if (parsedIndex < 1)
                        throw new ArgumentException(
                            $"Index predicate must be >= 1, got '{predContent}' in XPath expression: '{xpath}'",
                            nameof(xpath));
                    index = parsedIndex;
                    continue;
                }

                // Try to split on 'and'/'or' for compound predicates
                var (parts, logicOp) = SplitOnLogicalOperator(predContent);

                if (logicOp is not null && parts.Count > 1)
                {
                    // Classify parts
                    var attrParts = new List<XPathPredicate>();
                    var funcParts = new List<string>();
                    var containsParts = new List<ContainsPredicate>();

                    foreach (var rawPart in parts)
                    {
                        var part = NormalizeTextFunction(rawPart);
                        if (part.TrimStart().StartsWith("contains(", StringComparison.Ordinal))
                            containsParts.Add(ParseContainsPredicate(part.Trim(), xpath));
                        else if (XPathExprEvaluator.IsFunctionExpression(part))
                            funcParts.Add(part);
                        else if (part.TrimStart().StartsWith('@'))
                            attrParts.Add(ParseAttributePredicate(part, xpath));
                        else
                            throw new ArgumentException(
                                $"Invalid predicate syntax '{rawPart}' in XPath expression: '{xpath}'",
                                nameof(xpath));
                    }

                    if (logicOp == "and")
                    {
                        predicates.AddRange(attrParts);
                        foreach (var fp in funcParts)
                        {
                            XPathExprEvaluator.Validate(fp);
                            functionPredicates ??= [];
                            functionPredicates.Add(fp.Trim());
                        }
                        if (containsParts.Count > 0)
                        {
                            containsPredicates ??= [];
                            containsPredicates.AddRange(containsParts);
                        }
                    }
                    else // "or"
                    {
                        if (funcParts.Count > 0 && attrParts.Count > 0)
                            throw new ArgumentException(
                                $"Mixed attribute and function predicates with 'or' is not supported: '{predContent}'",
                                nameof(xpath));

                        if (funcParts.Count > 0)
                        {
                            string joined = string.Join(" or ", funcParts);
                            XPathExprEvaluator.Validate(joined);
                            functionPredicates ??= [];
                            functionPredicates.Add(joined);
                        }
                        else
                        {
                            orGroups ??= [];
                            orGroups.Add(attrParts);
                        }
                    }
                    continue;
                }

                // Single predicate (no and/or split)
                string trimmed = NormalizeTextFunction(predContent.Trim());

                if (XPathExprEvaluator.IsFunctionExpression(trimmed))
                {
                    XPathExprEvaluator.Validate(trimmed);
                    functionPredicates ??= [];
                    functionPredicates.Add(trimmed);
                    continue;
                }

                if (trimmed.StartsWith("contains(", StringComparison.Ordinal))
                {
                    containsPredicates ??= [];
                    containsPredicates.Add(ParseContainsPredicate(trimmed, xpath));
                    continue;
                }

                if (trimmed.StartsWith('@'))
                {
                    predicates.Add(ParseAttributePredicate(trimmed, xpath));
                    continue;
                }

                throw new ArgumentException(
                    $"Invalid predicate syntax '{predContent}' in XPath expression: '{xpath}'. " +
                    "Expected format: @Attribute='value' or @Attribute=('v1','v2') or positional index",
                    nameof(xpath));
            }

            steps.Add(new XPathStep(
                isDescendant ? XPathAxis.Descendant : XPathAxis.Child,
                type,
                predicates,
                index,
                functionPredicates,
                orGroups,
                containsPredicates));
        }

        if (steps.Count == 0)
            throw new ArgumentException(
                $"XPath expression produced no valid steps: '{xpath}'",
                nameof(xpath));

        return steps;
    }

    // ── Attribute predicate parser ───────────────────────────────────────────

    private static XPathPredicate ParseAttributePredicate(string content, string xpath)
    {
        content = content.Trim();
        if (!content.StartsWith('@'))
            throw new ArgumentException(
                $"Invalid predicate syntax '{content}' in XPath expression: '{xpath}'. Predicates must start with '@'.",
                nameof(xpath));

        // Find the operator position
        int i = 1;
        while (i < content.Length && content[i] != '=' && content[i] != '^' && content[i] != '*' && content[i] != '$')
            i++;

        if (i >= content.Length)
            throw new ArgumentException(
                $"Invalid predicate syntax '{content}' in XPath expression: '{xpath}'.",
                nameof(xpath));

        string attr = content[1..i].Trim();
        AttributeMatchMode mode;
        int valStart;

        if (content[i] == '^' && i + 1 < content.Length && content[i + 1] == '=')
        { mode = AttributeMatchMode.StartsWith; valStart = i + 2; }
        else if (content[i] == '*' && i + 1 < content.Length && content[i + 1] == '=')
        { mode = AttributeMatchMode.Contains; valStart = i + 2; }
        else if (content[i] == '$' && i + 1 < content.Length && content[i + 1] == '=')
        { mode = AttributeMatchMode.EndsWith; valStart = i + 2; }
        else if (content[i] == '=')
        { mode = AttributeMatchMode.Exact; valStart = i + 1; }
        else
            throw new ArgumentException(
                $"Invalid operator in predicate '{content}' in XPath expression: '{xpath}'.",
                nameof(xpath));

        string valueStr = content[valStart..].Trim();

        // concat() function
        if (valueStr.StartsWith("concat(", StringComparison.Ordinal) && valueStr.EndsWith(')'))
        {
            string argsStr = valueStr[7..^1];
            var concatArgs = ParseConcatArgs(argsStr, xpath);
            return new XPathPredicate(attr, [], mode, concatArgs);
        }

        // Multi-value list: ('v1','v2')
        if (valueStr.StartsWith('(') && valueStr.EndsWith(')'))
        {
            var values = ListItemRx.Matches(valueStr)
                .Select(m => m.Groups["item"].Value)
                .ToList();
            return new XPathPredicate(attr, values, mode);
        }

        // Single quoted value: 'value' or "value"
        if ((valueStr.StartsWith('\'') && valueStr.EndsWith('\'')) ||
            (valueStr.StartsWith('"') && valueStr.EndsWith('"')))
        {
            return new XPathPredicate(attr, [valueStr[1..^1]], mode);
        }

        throw new ArgumentException(
            $"Invalid value syntax in predicate '{content}' in XPath expression: '{xpath}'.",
            nameof(xpath));
    }

    private static ContainsPredicate ParseContainsPredicate(string content, string xpath)
    {
        // content = "contains(bounds(), point(10, 50))" or "contains(@Name, 'foo')"
        if (!content.EndsWith(')'))
            throw new ArgumentException(
                $"Malformed contains() predicate in XPath expression: '{xpath}'", nameof(xpath));

        string inner = content[9..^1]; // strip "contains(" and ")"
        var args = SplitContainsArgs(inner);
        if (args.Count != 2)
            throw new ArgumentException(
                $"contains() requires exactly 2 arguments in XPath expression: '{xpath}'", nameof(xpath));

        string arg1 = args[0].Trim();
        string arg2 = args[1].Trim();

        // Spatial: contains(bounds(), point(x, y))
        if (string.Equals(arg1, "bounds()", StringComparison.OrdinalIgnoreCase)
            && arg2.StartsWith("point(", StringComparison.OrdinalIgnoreCase) && arg2.EndsWith(')'))
        {
            string pointArgs = arg2[6..^1];
            var coords = pointArgs.Split(',', StringSplitOptions.TrimEntries);
            if (coords.Length != 2
                || !double.TryParse(coords[0], System.Globalization.CultureInfo.InvariantCulture, out double x)
                || !double.TryParse(coords[1], System.Globalization.CultureInfo.InvariantCulture, out double y))
                throw new ArgumentException(
                    $"Invalid point() arguments in XPath expression: '{xpath}'", nameof(xpath));
            return new ContainsBoundsPoint(x, y);
        }

        // String: contains(@attr, 'value') / contains('literal', @attr) / etc.
        var (haystack, haystackIsAttr) = ParseContainsStringArg(arg1, xpath);
        var (needle, needleIsAttr) = ParseContainsStringArg(arg2, xpath);
        return new ContainsSubstring(haystack, needle, haystackIsAttr, needleIsAttr);
    }

    private static (string Value, bool IsAttr) ParseContainsStringArg(string arg, string xpath)
    {
        if (arg.StartsWith('@'))
            return (arg[1..], true);
        if (arg.StartsWith("text()", StringComparison.Ordinal))
            return ("name", true);
        if ((arg.StartsWith('\'') && arg.EndsWith('\'')) || (arg.StartsWith('"') && arg.EndsWith('"')))
            return (arg[1..^1], false);
        throw new ArgumentException(
            $"Invalid contains() argument '{arg}' in XPath expression: '{xpath}'", nameof(xpath));
    }

    private static List<string> SplitContainsArgs(string inner)
    {
        var args = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < inner.Length; i++)
        {
            char c = inner[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                args.Add(inner[start..i]);
                start = i + 1;
            }
        }
        args.Add(inner[start..]);
        return args;
    }

    private static List<ConcatArg> ParseConcatArgs(string argsStr, string xpath)
    {
        var args = new List<ConcatArg>();
        int i = 0;
        int len = argsStr.Length;

        while (i < len)
        {
            while (i < len && (argsStr[i] == ' ' || argsStr[i] == ',')) i++;
            if (i >= len) break;

            if (argsStr[i] == '\'' || argsStr[i] == '"')
            {
                char quote = argsStr[i];
                i++;
                int start = i;
                while (i < len && argsStr[i] != quote) i++;
                args.Add(new StringConcatArg(argsStr[start..i]));
                if (i < len) i++; // skip closing quote
            }
            else if (argsStr[i] == '@')
            {
                i++;
                int start = i;
                while (i < len && char.IsLetterOrDigit(argsStr[i])) i++;
                args.Add(new AttrConcatArg(argsStr[start..i]));
            }
            else
            {
                throw new ArgumentException(
                    $"Invalid concat argument at position {i} in XPath expression: '{xpath}'",
                    nameof(xpath));
            }
        }

        return args;
    }

    /// <summary>
    /// Rewrites <c>text()</c> at the start of a predicate part to <c>@name</c>
    /// so the existing attribute-predicate pipeline handles it transparently.
    /// </summary>
    private static string NormalizeTextFunction(string part)
    {
        var trimmed = part.TrimStart();
        return trimmed.StartsWith("text()", StringComparison.Ordinal)
            ? "@name" + trimmed[6..]
            : part;
    }

    // ── Logical operator splitter ────────────────────────────────────────────

    private static (List<string> parts, string? op) SplitOnLogicalOperator(string content)
    {
        var parts = new List<string>();
        string? foundOp = null;
        int start = 0;
        bool inSingleQuote = false, inDoubleQuote = false;
        int parenDepth = 0;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (c == '\'' && !inDoubleQuote) inSingleQuote = !inSingleQuote;
            else if (c == '"' && !inSingleQuote) inDoubleQuote = !inDoubleQuote;
            else if (c == '(' && !inSingleQuote && !inDoubleQuote) parenDepth++;
            else if (c == ')' && !inSingleQuote && !inDoubleQuote) parenDepth--;

            if (inSingleQuote || inDoubleQuote || parenDepth > 0) continue;

            if (i + 4 < content.Length && content[i] == ' '
                && content[i + 1] == 'a' && content[i + 2] == 'n' && content[i + 3] == 'd'
                && content[i + 4] == ' ')
            {
                parts.Add(content[start..i].Trim());
                foundOp = "and";
                start = i + 5;
                i += 4;
            }
            else if (i + 3 < content.Length && content[i] == ' '
                && content[i + 1] == 'o' && content[i + 2] == 'r'
                && content[i + 3] == ' ')
            {
                parts.Add(content[start..i].Trim());
                foundOp = "or";
                start = i + 4;
                i += 3;
            }
        }

        parts.Add(content[start..].Trim());

        return parts.Count > 1 ? (parts, foundOp) : (parts, null);
    }
}
