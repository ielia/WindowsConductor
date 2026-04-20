using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace WindowsConductor.DriverFlaUI;

public sealed record AttrMatch(AutomationElement Element, string Name, string? Value);

public abstract record XPathEvalResult;
public sealed record ElementsResult(IReadOnlyList<AutomationElement> Elements) : XPathEvalResult;
public sealed record AttrsResult(IReadOnlyList<AttrMatch> Attributes) : XPathEvalResult;
public sealed record ExpressionResult(XPathValue Value) : XPathEvalResult;

public sealed class XPathEngine
{
    public static IReadOnlyList<AutomationElement> Evaluate(
        AutomationElement root, string xpath) =>
        EvaluateFull(root, xpath) is ElementsResult er ? er.Elements : [];

    public static XPathEvalResult EvaluateFull(
        AutomationElement root, string xpath)
    {
        var steps = XPathSyntaxParser.Parse(xpath);
        IReadOnlyList<AutomationElement> currentElements = [root];
        List<AttrMatch>? currentAttrs = null;
        var subPathCache = new Dictionary<SubPathExpr, XPathValue>();

        foreach (var step in steps)
        {
            if (currentAttrs is not null && step.Axis == XPathAxis.Self)
            {
                // Self in attribute context — keep attrs
                continue;
            }

            if (currentAttrs is not null)
            {
                // Transition from attribute context back to elements.
                // In XPath the parent of an attribute node is its owner element,
                // so Parent returns the owners directly; Ancestor includes owners
                // and their ancestors; everything else starts from the owners.
                currentElements = currentAttrs.Select(a => a.Element).Distinct().ToList();
                currentAttrs = null;

                if (step.Axis == XPathAxis.Parent)
                {
                    currentElements = ApplyFilters(currentElements, step, subPathCache);
                    continue;
                }

                if (step.Axis == XPathAxis.Ancestor)
                {
                    var adjusted = step with { Axis = XPathAxis.AncestorOrSelf };
                    currentElements = ApplyStep(currentElements, adjusted, subPathCache);
                    continue;
                }
            }

            if (step.Axis == XPathAxis.Attribute)
            {
                currentAttrs = ResolveAttributes(currentElements, step);
                currentElements = [];
            }
            else
            {
                currentElements = ApplyStep(currentElements, step, subPathCache);
            }
        }

        return currentAttrs is not null
            ? new AttrsResult(currentAttrs)
            : new ElementsResult(currentElements);
    }

    /// <summary>
    /// Evaluates a standalone XPath expression (e.g. a function call) against a root element.
    /// </summary>
    public static ExpressionResult EvaluateExpression(
        AutomationElement root, string expression)
    {
        var expr = XPathSyntaxParser.ParseExpression(expression);
        var subPathCache = new Dictionary<SubPathExpr, XPathValue>();
        var ctx = new EvalContext(
            k => ElementProperties.Resolve(root, k),
            1, 1, root,
            sp => EvaluateSubPath(root, sp, subPathCache));
        var value = XPathFunctions.Evaluate(expr, ctx);
        return new ExpressionResult(value);
    }

    /// <summary>
    /// Validates an XPath expression without evaluating it.
    /// Throws <see cref="ArgumentException"/> if the expression is malformed.
    /// </summary>
    public static void Validate(string xpath) => XPathSyntaxParser.Parse(xpath);

    // ── Step evaluation ──────────────────────────────────────────────────────

    private static IReadOnlyList<AutomationElement> ApplyStep(
        IReadOnlyList<AutomationElement> roots, XPathStep step,
        Dictionary<SubPathExpr, XPathValue> subPathCache)
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

        if (step.Axis is XPathAxis.Ancestor or XPathAxis.AncestorOrSelf)
            return ApplyAncestorStep(roots, step, subPathCache);

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
                ExpressionFilter expr => ApplyExpressionFilter(results, expr, step.Type, subPathCache),
                _ => results
            };
        }

        return results;
    }

    private static IReadOnlyList<AutomationElement> ApplyFilters(
        IReadOnlyList<AutomationElement> elements, XPathStep step,
        Dictionary<SubPathExpr, XPathValue> subPathCache)
    {
        IReadOnlyList<AutomationElement> results = step.Type is "*" or ".."
            ? elements
            : elements.Where(e => MatchesType(step.Type, e)).ToList();

        foreach (var filter in step.Filters)
        {
            results = filter switch
            {
                IndexFilter idx => ApplyIndexFilter(results, idx.Index),
                ExpressionFilter expr => ApplyExpressionFilter(results, expr, step.Type, subPathCache),
                _ => results
            };
        }

        return results;
    }

    private static IReadOnlyList<AutomationElement> ApplyAncestorStep(
        IReadOnlyList<AutomationElement> roots, XPathStep step,
        Dictionary<SubPathExpr, XPathValue> subPathCache)
    {
        var candidates = new List<AutomationElement>();

        foreach (var root in roots)
        {
            var ancestors = new List<AutomationElement>();

            if (step.Axis == XPathAxis.AncestorOrSelf && MatchesType(step.Type, root))
                ancestors.Add(root);

            var current = SafeGetParent(root);
            while (current is not null)
            {
                if (MatchesType(step.Type, current))
                    ancestors.Add(current);
                current = SafeGetParent(current);
            }

            candidates.AddRange(ancestors);
        }

        IReadOnlyList<AutomationElement> results = candidates;
        foreach (var filter in step.Filters)
        {
            results = filter switch
            {
                IndexFilter idx => ApplyIndexFilter(results, idx.Index),
                ExpressionFilter expr => ApplyExpressionFilter(results, expr, step.Type, subPathCache),
                _ => results
            };
        }

        return results;
    }

    private static List<AttrMatch> ResolveAttributes(
        IReadOnlyList<AutomationElement> elements, XPathStep step)
    {
        var attrs = new List<AttrMatch>();
        foreach (var el in elements)
        {
            if (step.Type == "*")
            {
                foreach (var (name, value) in ElementProperties.ResolveAll(el))
                    attrs.Add(new AttrMatch(el, name, value?.ToString()));
            }
            else
            {
                var value = ElementProperties.Resolve(el, step.Type);
                if (value is not null)
                    attrs.Add(new AttrMatch(el, step.Type, value));
            }
        }

        foreach (var filter in step.Filters)
        {
            if (filter is ExpressionFilter expr)
                attrs = ApplyAttrExpressionFilter(attrs, expr);
            else if (filter is IndexFilter idx)
                attrs = idx.Index >= 1 && idx.Index <= attrs.Count ? [attrs[idx.Index - 1]] : [];
        }

        return attrs;
    }

    private static List<AttrMatch> ApplyAttrExpressionFilter(
        List<AttrMatch> attrs, ExpressionFilter filter)
    {
        var results = new List<AttrMatch>();
        for (int i = 0; i < attrs.Count; i++)
        {
            var attr = attrs[i];
            var ctx = new EvalContext(
                k => k == "." ? attr.Value : ElementProperties.Resolve(attr.Element, k),
                i + 1,
                attrs.Count,
                attr.Element);
            var value = XPathFunctions.Evaluate(filter.Expr, ctx);
            if (value.AsBool())
                results.Add(attr);
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

    private static List<AutomationElement> ApplyExpressionFilter(
        IReadOnlyList<AutomationElement> elements, ExpressionFilter filter, string stepType,
        Dictionary<SubPathExpr, XPathValue> subPathCache)
    {
        var results = new List<AutomationElement>();

        for (int i = 0; i < elements.Count; i++)
        {
            var el = elements[i];

            var (sibPos, sibLast) = GetSiblingPosition(el, stepType);

            var ctx = new EvalContext(
                k => ElementProperties.Resolve(el, k),
                sibPos,
                sibLast,
                el,
                sp => EvaluateSubPath(el, sp, subPathCache));

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

    private static XPathValue EvaluateSubPath(
        AutomationElement contextElement, SubPathExpr subPath,
        Dictionary<SubPathExpr, XPathValue> subPathCache)
    {
        if (subPath.IsAbsolute && subPathCache.TryGetValue(subPath, out var cached))
            return cached;

        var root = subPath.IsAbsolute ? GetDesktopRoot(contextElement) : contextElement;
        IReadOnlyList<AutomationElement> currentElements = [root];
        List<AttrMatch>? currentAttrs = null;
        foreach (var step in subPath.Steps)
        {
            if (currentAttrs is not null && step.Axis == XPathAxis.Self)
                continue;

            if (currentAttrs is not null)
            {
                currentElements = currentAttrs.Select(a => a.Element).Distinct().ToList();
                currentAttrs = null;

                if (step.Axis == XPathAxis.Parent)
                {
                    currentElements = ApplyFilters(currentElements, step, subPathCache);
                    continue;
                }

                if (step.Axis == XPathAxis.Ancestor)
                {
                    var adjusted = step with { Axis = XPathAxis.AncestorOrSelf };
                    currentElements = ApplyStep(currentElements, adjusted, subPathCache);
                    continue;
                }
            }

            if (step.Axis == XPathAxis.Attribute)
            {
                currentAttrs = ResolveAttributes(currentElements, step);
                currentElements = [];
            }
            else
            {
                currentElements = ApplyStep(currentElements, step, subPathCache);
            }
        }

        XPathValue result = currentAttrs is not null
            ? new XPathSequence(currentAttrs.Select(a => (XPathValue)new XPathString(a.Value ?? "")).ToList())
            : new XPathSequence(currentElements.Select(e => (XPathValue)new XPathString(ElementProperties.Resolve(e, "text") ?? "")).ToList());

        if (subPath.IsAbsolute)
            subPathCache[subPath] = result;

        return result;
    }

    private static AutomationElement GetDesktopRoot(AutomationElement element)
    {
        var current = element;
        while (true)
        {
            var parent = SafeGetParent(current);
            if (parent is null) return current;
            current = parent;
        }
    }

    private static AutomationElement? SafeGetParent(AutomationElement el)
    {
        try { return el.Parent; }
        catch { return null; }
    }
}
