using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace WindowsConductor.DriverFlaUI;

// ── Data model ───────────────────────────────────────────────────────────────

public enum XPathAxis { Child, Descendant, Parent, Self }

/// <summary>A single <c>@Attr='value'</c> or <c>@Attr=('v1','v2')</c> predicate.</summary>
public sealed record XPathPredicate(string Attribute, IReadOnlyList<string> Values);

/// <summary>One step of an XPath expression: axis + element type + predicates + optional 1-based positional index.</summary>
public sealed record XPathStep(XPathAxis Axis, string Type, IReadOnlyList<XPathPredicate> Predicates, int? Index = null);

// ── Engine ───────────────────────────────────────────────────────────────────

/// <summary>
/// Evaluates a structural subset of XPath over the UIAutomation element tree.
///
/// Supported grammar
/// ─────────────────
///   xpath       ::= step+
///   step        ::= axis type predicate* | axis '..' | '.'
///   axis        ::= '//' (descendant) | '/' (child)
///   type        ::= '*' | '.' | '..' | ControlTypeName   e.g. Button, Edit, Window
///   predicate   ::= '[' '@' attr '=' value_expr ']' | '[' index ']'
///   value_expr  ::= quote value quote | '(' quote value quote (',' quote value quote)* ')'
///   index       ::= positive integer (1-based)
///   attr        ::= AutomationId | Name | ClassName | ControlType
///   quote       ::= ' | "
///
/// Examples
/// ────────
///   //Button[@AutomationId='num7Button']
///   //Window[@Name='Calculator']//Button[@Name='7']
///   //*[@Name='Cancel']
///   //Edit
///   //Button[3]
///   //Button[@Name='OK']/..
///   ./Button                                              (self / child)
///   .//Button                                             (self / descendant)
///   //Button/./.../Button                                 (self in mid-path)
/// </summary>
public sealed class XPathEngine
{
    // Matches:  @AutomationId='value'  or  @Name="value"  or  @Name=('v1','v2')
    private static readonly Regex PredicateRx = new(
        @"@(?<attr>\w+)=(?:['""](?<val>[^'""]*)['""]|\((?<list>[^)]+)\))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
                if (Matches(el, step))
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

    private static bool Matches(AutomationElement element, XPathStep step) =>
        MatchesStep(step, k => ElementProperties.Resolve(element, k));

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

        // Predicate checks (all must pass)
        foreach (var pred in step.Predicates)
        {
            string? actual = getProperty(pred.Attribute.ToLowerInvariant());
            if (!pred.Values.Any(v => string.Equals(actual, v, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

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
                // Bare '/' (the entire expression) selects the root.
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
                // Type is required — e.g. "//[attr=x]" is invalid (missing type before predicate)
                if (pos < len && xpath[pos] == '[')
                    throw new ArgumentException(
                        $"XPath is missing an element type before predicate at position {typeStart}: '{xpath}'",
                        nameof(xpath));
                continue;
            }

            // ── Self axis (.) ────────────────────────────────────────────────
            if (type == ".")
            {
                steps.Add(new XPathStep(XPathAxis.Self, ".", []));
                continue;
            }

            // ── Parent axis (..) ─────────────────────────────────────────────
            if (type == "..")
            {
                steps.Add(new XPathStep(XPathAxis.Parent, "..", []));
                continue;
            }

            // ── Predicates ───────────────────────────────────────────────────
            var predicates = new List<XPathPredicate>();
            int? index = null;

            while (pos < len && xpath[pos] == '[')
            {
                pos++; // consume '['
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

                // predStart..pos-1 is the predicate content (excludes surrounding brackets)
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

                var m = PredicateRx.Match(predContent);
                if (!m.Success)
                    throw new ArgumentException(
                        $"Invalid predicate syntax '{predContent}' in XPath expression: '{xpath}'. " +
                        "Expected format: @Attribute='value' or @Attribute=('v1','v2') or positional index",
                        nameof(xpath));

                var attr = m.Groups["attr"].Value;
                IReadOnlyList<string> values;

                if (m.Groups["list"].Success)
                {
                    values = ListItemRx.Matches(m.Groups["list"].Value)
                        .Select(li => li.Groups["item"].Value)
                        .ToList();
                }
                else
                {
                    values = new[] { m.Groups["val"].Value };
                }

                predicates.Add(new XPathPredicate(attr, values));
            }

            steps.Add(new XPathStep(
                isDescendant ? XPathAxis.Descendant : XPathAxis.Child,
                type,
                predicates,
                index));
        }

        if (steps.Count == 0)
            throw new ArgumentException(
                $"XPath expression produced no valid steps: '{xpath}'",
                nameof(xpath));

        return steps;
    }
}
