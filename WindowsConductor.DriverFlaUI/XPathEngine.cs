using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace WindowsConductor.DriverFlaUI;

public sealed class XPathEngine
{
    public IReadOnlyList<AutomationElement> Evaluate(
        AutomationElement root, string xpath)
    {
        var steps = XPathSyntaxParser.Parse(xpath);
        IReadOnlyList<AutomationElement> current = [root];

        foreach (var step in steps)
            current = ApplyStep(current, step);

        return current;
    }

    /// <summary>
    /// Validates an XPath expression without evaluating it.
    /// Throws <see cref="ArgumentException"/> if the expression is malformed.
    /// </summary>
    public static void Validate(string xpath) => XPathSyntaxParser.Parse(xpath);

    // ── Step evaluation ──────────────────────────────────────────────────────

    private static IReadOnlyList<AutomationElement> ApplyStep(
        IReadOnlyList<AutomationElement> roots, XPathStep step)
    {
        if (step.Axis == XPathAxis.Self)
            return roots;

        if (step.Axis == XPathAxis.Parent)
        {
            var parents = new List<AutomationElement>();
            foreach (var root in roots)
            {
                var parent = root.Parent;
                if (parent is not null)
                    parents.Add(parent);
            }
            return parents;
        }

        var candidates = new List<AutomationElement>();

        foreach (var root in roots)
        {
            var children = step.Axis is XPathAxis.Descendant or XPathAxis.Frontmost
                ? root.FindAllDescendants()
                : root.FindAllChildren();

            foreach (var el in children)
            {
                if (MatchesType(step.Type, el))
                    candidates.Add(el);
            }
        }

        if (step.Axis == XPathAxis.Frontmost)
            candidates = ElementFilter.Frontmost(candidates);

        // Apply filters sequentially — each filter narrows the result set
        IReadOnlyList<AutomationElement> results = candidates;
        foreach (var filter in step.Filters)
        {
            results = filter switch
            {
                IndexFilter idx => ApplyIndexFilter(results, idx.Index),
                ExpressionFilter expr => ApplyExpressionFilter(results, expr, step.Type),
                _ => results
            };
        }

        return results;
    }

    private static bool MatchesType(string type, AutomationElement el)
    {
        if (type == "*") return true;
        if (!Enum.TryParse<ControlType>(type, ignoreCase: true, out var ct))
            return false;
        string? controlType = ElementProperties.Resolve(el, "controltype");
        return string.Equals(controlType, ct.ToString(), StringComparison.InvariantCulture);
    }

    private static IReadOnlyList<AutomationElement> ApplyIndexFilter(
        IReadOnlyList<AutomationElement> elements, int index)
    {
        if (index < 1 || index > elements.Count)
            return [];
        return [elements[index - 1]];
    }

    private static IReadOnlyList<AutomationElement> ApplyExpressionFilter(
        IReadOnlyList<AutomationElement> elements, ExpressionFilter filter, string stepType)
    {
        var results = new List<AutomationElement>();
        int last = elements.Count;

        for (int i = 0; i < elements.Count; i++)
        {
            var el = elements[i];
            int position = i + 1;

            // Compute sibling-aware position/last if expression uses position()/last()
            var (sibPos, sibLast) = GetSiblingPosition(el, stepType);

            var ctx = new EvalContext(
                k => ElementProperties.Resolve(el, k),
                sibPos,
                sibLast,
                el);

            var value = XPathFunctions.Evaluate(filter.Expr, ctx);
            if (value.AsBool())
                results.Add(el);
        }

        return results;
    }

    private static (int Position, int Last) GetSiblingPosition(AutomationElement element, string stepType)
    {
        var parent = SafeGetParent(element);
        if (parent is null)
            return (1, 1);

        var siblings = parent.FindAllChildren();
        int position = 0;
        int lastCount = 0;
        foreach (var sibling in siblings)
        {
            if (stepType == "*" || string.Equals(
                    sibling.Properties.ControlType.ValueOrDefault.ToString(),
                    element.Properties.ControlType.ValueOrDefault.ToString(),
                    StringComparison.InvariantCulture))
            {
                lastCount++;
                if (sibling.Equals(element))
                    position = lastCount;
            }
        }
        return position == 0 ? (1, 1) : (position, lastCount);
    }

    internal static bool MatchesStep(XPathStep step, Func<string, string?> getProperty)
    {
        // Type check
        if (step.Type != "*")
        {
            if (!Enum.TryParse<ControlType>(step.Type, ignoreCase: true, out var ct))
                return false;
            string? controlType = getProperty("controltype");
            if (!string.Equals(controlType, ct.ToString(), StringComparison.InvariantCulture))
                return false;
        }

        // Evaluate all expression filters
        foreach (var filter in step.Filters)
        {
            if (filter is ExpressionFilter ef)
            {
                var ctx = new EvalContext(getProperty, 1, 1, null);
                var value = XPathFunctions.Evaluate(ef.Expr, ctx);
                if (!value.AsBool())
                    return false;
            }
        }

        return true;
    }

    private static AutomationElement? SafeGetParent(AutomationElement el)
    {
        try { return el.Parent; }
        catch { return null; }
    }
}
