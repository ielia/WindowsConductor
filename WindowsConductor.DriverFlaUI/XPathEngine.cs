using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;

namespace WindowsConductor.DriverFlaUI;

public sealed record AttrMatch(AutomationElement Element, string Name, string? Value);

public abstract record XPathEvalResult;
public sealed record ElementsResult(IEnumerable<AutomationElement> Elements) : XPathEvalResult;
public sealed record AttrsResult(IReadOnlyList<AttrMatch> Attributes) : XPathEvalResult;
public sealed record ExpressionResult(XPathValue Value) : XPathEvalResult;

public sealed class XPathEngine
{
    public static IEnumerable<AutomationElement> Evaluate(
        AutomationElement root, string xpath, CancellationToken ct = default) =>
        EvaluateFull(root, xpath, ct) is ElementsResult er ? er.Elements : [];

    public static XPathEvalResult EvaluateFull(
        AutomationElement root, string xpath, CancellationToken ct = default)
    {
        var steps = XPathSyntaxParser.Parse(xpath);
        IEnumerable<AutomationElement> currentElements = [root];
        List<AttrMatch>? currentAttrs = null;
        var subPathCache = new Dictionary<SubPathExpr, XPathValue>();

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();

            if (currentAttrs is not null && step.Axis == XPathAxis.Self)
                continue;

            if (currentAttrs is not null)
            {
                currentElements = currentAttrs.Select(a => a.Element).Distinct();
                currentAttrs = null;

                if (step.Axis == XPathAxis.Parent)
                {
                    currentElements = ApplyFilters(Materialize(currentElements), step, subPathCache, ct);
                    continue;
                }

                if (step.Axis == XPathAxis.Ancestor)
                {
                    var adjusted = step with { Axis = XPathAxis.AncestorOrSelf };
                    currentElements = ApplyStep(currentElements, adjusted, root, subPathCache, ct);
                    continue;
                }
            }

            if (step.Axis == XPathAxis.Attribute)
            {
                currentAttrs = ResolveAttributes(Materialize(currentElements), step);
                currentElements = [];
            }
            else if (step.Axis == XPathAxis.SetFilter)
            {
                currentElements = ApplySetFilters(Materialize(currentElements), step, subPathCache, ct);
            }
            else
            {
                currentElements = ApplyStep(currentElements, step, root, subPathCache, ct);
            }
        }

        return currentAttrs is not null
            ? new AttrsResult(currentAttrs)
            : new ElementsResult(currentElements);
    }

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

    public static void Validate(string xpath) => XPathSyntaxParser.Parse(xpath);

    // ── Materialization helper ────────────────────────────────────────────────

    private static IReadOnlyList<AutomationElement> Materialize(IEnumerable<AutomationElement> elements) =>
        elements as IReadOnlyList<AutomationElement> ?? elements.ToList();

    // ── Step evaluation ──────────────────────────────────────────────────────

    private static IEnumerable<AutomationElement> ApplyStep(
        IEnumerable<AutomationElement> roots, XPathStep step,
        AutomationElement referenceElement,
        Dictionary<SubPathExpr, XPathValue> subPathCache, CancellationToken ct)
    {
        return Deduplicate(ApplyStepCore(roots, step, referenceElement, subPathCache, ct));
    }

    private static IEnumerable<AutomationElement> ApplyStepCore(
        IEnumerable<AutomationElement> roots, XPathStep step,
        AutomationElement referenceElement,
        Dictionary<SubPathExpr, XPathValue> subPathCache, CancellationToken ct)
    {
        var condition = ConditionTranslator.BuildStepCondition(step, referenceElement);

        if (step.Axis == XPathAxis.Self)
        {
            var selfCandidates = FilterByType(roots, step.Type, ct);
            return ApplyFiltersToResults(selfCandidates, step, subPathCache, ct);
        }

        if (step.Axis == XPathAxis.Parent)
        {
            var parents = EnumerateParents(roots, step.Type, ct);
            return ApplyFiltersToResults(parents, step, subPathCache, ct);
        }

        if (step.Axis is XPathAxis.Ancestor or XPathAxis.AncestorOrSelf)
            return ApplyAncestorStep(roots, step, subPathCache, ct);

        if (step.Axis is XPathAxis.Sibling or XPathAxis.PrecedingSibling or XPathAxis.FollowingSibling)
            return ApplySiblingStep(roots, step, condition, subPathCache, ct);

        if (step.Axis == XPathAxis.DescendantOrSelf)
        {
            var candidates = EnumerateDescendantOrSelf(roots, step.Type, condition, ct);
            return ApplyFiltersToResults(candidates, step, subPathCache, ct);
        }

        if (step.Axis == XPathAxis.Frontmost)
        {
            var descendants = EnumerateDescendants(roots, step.Type, condition, ct);
            var frontmost = ElementFilter.Frontmost(Materialize(descendants).ToList());
            return ApplyFiltersToResults(frontmost, step, subPathCache, ct);
        }

        var childCandidates = step.Axis == XPathAxis.Descendant
            ? EnumerateDescendants(roots, step.Type, condition, ct)
            : EnumerateChildren(roots, step.Type, condition, ct);

        return ApplyFiltersToResults(childCandidates, step, subPathCache, ct);
    }

    private static IEnumerable<AutomationElement> Deduplicate(IEnumerable<AutomationElement> elements)
    {
        var seen = new HashSet<string>();
        foreach (var el in elements)
        {
            var key = ElementFilter.RuntimeIdKey(el);
            if (key is null || seen.Add(key))
                yield return el;
        }
    }

    // ── Lazy enumeration helpers ─────────────────────────────────────────────

    private static IEnumerable<AutomationElement> FilterByType(
        IEnumerable<AutomationElement> elements, string type, CancellationToken ct)
    {
        foreach (var el in elements)
        {
            ct.ThrowIfCancellationRequested();
            if (MatchesType(type, el))
                yield return el;
        }
    }

    private static IEnumerable<AutomationElement> EnumerateParents(
        IEnumerable<AutomationElement> roots, string type, CancellationToken ct)
    {
        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();
            var parent = root.Parent;
            if (parent is not null && MatchesType(type, parent))
                yield return parent;
        }
    }

    private static IEnumerable<AutomationElement> EnumerateChildren(
        IEnumerable<AutomationElement> roots, string type,
        ConditionBase? condition, CancellationToken ct)
    {
        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();
            var children = condition is not null
                ? root.FindAllChildren(condition)
                : root.FindAllChildren();
            foreach (var child in children)
            {
                if (MatchesType(type, child))
                    yield return child;
            }
        }
    }

    private static IEnumerable<AutomationElement> EnumerateDescendants(
        IEnumerable<AutomationElement> roots, string type,
        ConditionBase? condition, CancellationToken ct)
    {
        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();
            var descendants = condition is not null
                ? root.FindAllDescendants(condition)
                : root.FindAllDescendants();
            foreach (var el in descendants)
            {
                ct.ThrowIfCancellationRequested();
                if (MatchesType(type, el))
                    yield return el;
            }
        }
    }

    private static IEnumerable<AutomationElement> EnumerateDescendantOrSelf(
        IEnumerable<AutomationElement> roots, string type,
        ConditionBase? condition, CancellationToken ct)
    {
        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();
            if (MatchesType(type, root))
                // This does not need to test the root. Filters will later be applied.
                yield return root;
            var descendants = condition is not null
                ? root.FindAllDescendants(condition)
                : root.FindAllDescendants();
            foreach (var el in descendants)
            {
                ct.ThrowIfCancellationRequested();
                if (MatchesType(type, el))
                    yield return el;
            }
        }
    }

    // ── Filter application ───────────────────────────────────────────────────

    private static IEnumerable<AutomationElement> ApplyFiltersToResults(
        IEnumerable<AutomationElement> candidates, XPathStep step,
        Dictionary<SubPathExpr, XPathValue> subPathCache, CancellationToken ct)
    {
        if (step.Filters.Count == 0)
            return candidates;

        IEnumerable<AutomationElement> results = candidates;
        foreach (var filter in step.Filters)
        {
            ct.ThrowIfCancellationRequested();
            results = filter switch
            {
                IndexFilter idx => ApplyExpressionFilter(results, ToPositionFilter(idx.Index), step.Type, subPathCache, ct),
                ExpressionFilter expr => ApplyExpressionFilter(results, expr, step.Type, subPathCache, ct),
                _ => results
            };
        }
        return results;
    }

    private static ExpressionFilter ToPositionFilter(int index) =>
        new(new BinaryExpr(
            new FunctionCallExpr("position", []),
            XPathBinaryOp.Eq,
            new LiteralNumberExpr(index)));

    private static IEnumerable<AutomationElement> ApplySiblingStep(
        IEnumerable<AutomationElement> roots, XPathStep step,
        ConditionBase? condition,
        Dictionary<SubPathExpr, XPathValue> subPathCache, CancellationToken ct)
    {
        var candidates = EnumerateSiblings(roots, step, condition, ct);
        return ApplyFiltersToResults(candidates, step, subPathCache, ct);
    }

    private static IEnumerable<AutomationElement> EnumerateSiblings(
        IEnumerable<AutomationElement> roots, XPathStep step,
        ConditionBase? condition, CancellationToken ct)
    {
        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();
            var parent = SafeGetParent(root);
            if (parent is null) continue;

            var siblingCondition = EnsureMatchesSelf(condition, root);
            var siblings = siblingCondition is not null
                ? parent.FindAllChildren(siblingCondition)
                : parent.FindAllChildren();
            bool foundSelf = false;

            foreach (var sibling in siblings)
            {
                if (sibling.Equals(root))
                {
                    foundSelf = true;
                    continue;
                }

                if (step.Axis == XPathAxis.PrecedingSibling && foundSelf)
                    break;
                if (step.Axis == XPathAxis.FollowingSibling && !foundSelf)
                    continue;

                if (!MatchesType(step.Type, sibling))
                    continue;

                yield return sibling;
            }
        }
    }

    private static IEnumerable<AutomationElement> ApplyFilters(
        IEnumerable<AutomationElement> elements, XPathStep step,
        Dictionary<SubPathExpr, XPathValue> subPathCache, CancellationToken ct)
    {
        IEnumerable<AutomationElement> results = step.Type is "*"
            ? elements
            : elements.Where(e => MatchesType(step.Type, e));

        foreach (var filter in step.Filters)
        {
            ct.ThrowIfCancellationRequested();
            results = filter switch
            {
                IndexFilter idx => ApplyExpressionFilter(results, ToPositionFilter(idx.Index), step.Type, subPathCache, ct),
                ExpressionFilter expr => ApplyExpressionFilter(results, expr, step.Type, subPathCache, ct),
                _ => results
            };
        }

        return results;
    }

    private static IEnumerable<AutomationElement> ApplyAncestorStep(
        IEnumerable<AutomationElement> roots, XPathStep step,
        Dictionary<SubPathExpr, XPathValue> subPathCache, CancellationToken ct)
    {
        var candidates = EnumerateAncestors(roots, step, ct);
        return ApplyFiltersToResults(candidates, step, subPathCache, ct);
    }

    private static IEnumerable<AutomationElement> EnumerateAncestors(
        IEnumerable<AutomationElement> roots, XPathStep step, CancellationToken ct)
    {
        var seen = new HashSet<string>();
        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();

            if (step.Axis == XPathAxis.AncestorOrSelf && MatchesType(step.Type, root))
                yield return root;

            var current = SafeGetParent(root);
            while (current is not null)
            {
                ct.ThrowIfCancellationRequested();
                var key = ElementFilter.RuntimeIdKey(current);
                if (key is not null && !seen.Add(key))
                    break;
                if (MatchesType(step.Type, current))
                    yield return current;
                current = SafeGetParent(current);
            }
        }
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

    private static List<AutomationElement> ApplySetFilters(
        IReadOnlyList<AutomationElement> elements, XPathStep step,
        Dictionary<SubPathExpr, XPathValue> subPathCache, CancellationToken ct)
    {
        int originalCount = elements.Count;
        var indexed = elements.Select((el, i) => (Element: el, Position: i + 1)).ToList();

        foreach (var filter in step.Filters)
        {
            ct.ThrowIfCancellationRequested();
            if (filter is IndexFilter idx)
            {
                indexed = idx.Index >= 1 && idx.Index <= indexed.Count
                    ? [indexed[idx.Index - 1]] : [];
            }
            else if (filter is ExpressionFilter expr)
            {
                indexed = indexed.Where(item =>
                {
                    ct.ThrowIfCancellationRequested();
                    var ctx = new EvalContext(
                        k => ElementProperties.Resolve(item.Element, k),
                        item.Position,
                        originalCount,
                        item.Element,
                        sp => EvaluateSubPath(item.Element, sp, subPathCache));
                    return XPathFunctions.Evaluate(expr.Expr, ctx).AsBool();
                }).ToList();
            }
        }

        return indexed.Select(item => item.Element).ToList();
    }

    private static IEnumerable<AutomationElement> ApplyExpressionFilter(
        IEnumerable<AutomationElement> elements, ExpressionFilter filter, string stepType,
        Dictionary<SubPathExpr, XPathValue> subPathCache, CancellationToken ct)
    {
        bool needsPosition = ReferencesPosition(filter.Expr);

        foreach (var el in elements)
        {
            ct.ThrowIfCancellationRequested();

            var (sibPos, sibLast) = needsPosition
                ? GetSiblingPosition(el, stepType)
                : (1, 1);

            var ctx = new EvalContext(
                k => ElementProperties.Resolve(el, k),
                sibPos,
                sibLast,
                el,
                sp => EvaluateSubPath(el, sp, subPathCache));

            var value = XPathFunctions.Evaluate(filter.Expr, ctx);
            if (value.AsBool())
                yield return el;
        }
    }

    private static bool ReferencesPosition(XPathExpr expr) => expr switch
    {
        FunctionCallExpr f when f.Name is "position" or "last" => true,
        FunctionCallExpr f => f.Args.Any(ReferencesPosition),
        BinaryExpr b => ReferencesPosition(b.Left) || ReferencesPosition(b.Right),
        UnaryMinusExpr u => ReferencesPosition(u.Operand),
        UnaryPlusExpr u => ReferencesPosition(u.Operand),
        SequenceExpr s => s.Items.Any(ReferencesPosition),
        _ => false
    };

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
        if (step.Type != "*")
        {
            if (!Enum.TryParse<ControlType>(step.Type, ignoreCase: true, out var ct))
                return false;
            string? controlType = getProperty("controltype");
            if (!string.Equals(controlType, ct.ToString(), StringComparison.InvariantCulture))
                return false;
        }

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
        IEnumerable<AutomationElement> currentElements = [root];
        List<AttrMatch>? currentAttrs = null;
        foreach (var step in subPath.Steps)
        {
            if (currentAttrs is not null && step.Axis == XPathAxis.Self)
                continue;

            if (currentAttrs is not null)
            {
                currentElements = currentAttrs.Select(a => a.Element).Distinct();
                currentAttrs = null;

                if (step.Axis == XPathAxis.Parent)
                {
                    currentElements = ApplyFilters(Materialize(currentElements), step, subPathCache, default);
                    continue;
                }

                if (step.Axis == XPathAxis.Ancestor)
                {
                    var adjusted = step with { Axis = XPathAxis.AncestorOrSelf };
                    currentElements = ApplyStep(currentElements, adjusted, root, subPathCache, default);
                    continue;
                }
            }

            if (step.Axis == XPathAxis.Attribute)
            {
                currentAttrs = ResolveAttributes(Materialize(currentElements), step);
                currentElements = [];
            }
            else if (step.Axis == XPathAxis.SetFilter)
            {
                currentElements = ApplySetFilters(Materialize(currentElements), step, subPathCache, default);
            }
            else
            {
                currentElements = ApplyStep(currentElements, step, root, subPathCache, default);
            }
        }

        var materializedElements = Materialize(currentElements);
        XPathValue result = currentAttrs is not null
            ? new XPathSequence(currentAttrs.Select(a => (XPathValue)new XPathString(a.Value ?? "")).ToList())
            : new XPathSequence(materializedElements.Select(e => (XPathValue)new XPathString(ElementProperties.Resolve(e, "text") ?? "")).ToList());

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

    private static ConditionBase? EnsureMatchesSelf(ConditionBase? condition, AutomationElement self)
    {
        if (condition is null) return null;
        if (self.FindFirst(TreeScope.Element, condition) is not null) return condition;
        var selfCondition = self.ConditionFactory.ByControlType(self.Properties.ControlType.ValueOrDefault);
        return condition.Or(selfCondition);
    }

    private static AutomationElement? SafeGetParent(AutomationElement el)
    {
        try { return el.Parent; }
        catch { return null; }
    }
}
