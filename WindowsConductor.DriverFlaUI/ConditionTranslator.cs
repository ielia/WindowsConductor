using System.Collections.Frozen;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Identifiers;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Translates XPath step predicates into UIA <see cref="ConditionBase"/> instances
/// for use as pre-filters in <c>FindAll</c> / <c>FindAllChildren</c> /
/// <c>FindAllDescendants</c> calls, reducing cross-process marshalling.
/// All original C# filters are kept as post-filters for correctness.
/// </summary>
internal static class ConditionTranslator
{
    private enum PropType { String, Bool, Int }

    // Maps normalized attribute names to FlaUI property accessor name and value type.
    // Comment out any entry to disable pushing that property to UIA.
    private static readonly Dictionary<string, (string Accessor, PropType Type)> PushableProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = ("Name", PropType.String),
        ["automationid"] = ("AutomationId", PropType.String),
        ["classname"] = ("ClassName", PropType.String),
        ["helptext"] = ("HelpText", PropType.String),
        ["localizedcontroltype"] = ("LocalizedControlType", PropType.String),
        ["itemstatus"] = ("ItemStatus", PropType.String),
        ["itemtype"] = ("ItemType", PropType.String),
        ["frameworkid"] = ("FrameworkId", PropType.String),
        ["acceleratorkey"] = ("AcceleratorKey", PropType.String),
        ["accesskey"] = ("AccessKey", PropType.String),
        ["isenabled"] = ("IsEnabled", PropType.Bool),
        ["isoffscreen"] = ("IsOffscreen", PropType.Bool),
        ["ispassword"] = ("IsPassword", PropType.Bool),
        ["iskeyboardfocusable"] = ("IsKeyboardFocusable", PropType.Bool),
        ["haskeyboardfocus"] = ("HasKeyboardFocus", PropType.Bool),
        ["processid"] = ("ProcessId", PropType.Int),
    };

    private static FrozenDictionary<string, PropertyId>? _propertyIds;

    internal static ConditionBase? BuildStepCondition(XPathStep step, AutomationElement referenceElement)
    {
        ConditionBase? condition = BuildControlTypeCondition(referenceElement, step.Type);

        foreach (var filter in step.Filters)
        {
            if (filter is not ExpressionFilter ef) continue;
            var translated = TryTranslate(ef.Expr, referenceElement);
            if (translated is null) continue;
            condition = condition is null ? translated : condition.And(translated);
        }

        return condition;
    }

    private static PropertyCondition? BuildControlTypeCondition(AutomationElement el, string type)
    {
        if (type == "*") return null;
        if (!Enum.TryParse<ControlType>(type, ignoreCase: true, out var ct)) return null;
        return el.ConditionFactory.ByControlType(ct);
    }

    internal static ConditionBase? TryTranslate(XPathExpr expr, AutomationElement el) => expr switch
    {
        BinaryExpr b when b.Op is XPathBinaryOp.Eq or XPathBinaryOp.NotEq
            => TryTranslateComparison(b, el),
        BinaryExpr { Left: var left, Op: XPathBinaryOp.And, Right: var right }
            => CombineAnd(TryTranslate(left, el), TryTranslate(right, el)),
        BinaryExpr { Left: var left, Op: XPathBinaryOp.Or, Right: var right }
            => CombineOr(TryTranslate(left, el), TryTranslate(right, el)),
        FunctionCallExpr { Name: "not", Args: [var inner] }
            => TryTranslate(inner, el)?.Not(),
        _ => null
    };

    private static ConditionBase? TryTranslateComparison(BinaryExpr expr, AutomationElement el)
    {
        if (!TryExtractAttrLiteral(expr, out var attrName, out var value))
            return null;

        var condition = TryBuildPropertyCondition(el, attrName, value);
        return expr.Op == XPathBinaryOp.NotEq ? condition?.Not() : condition;
    }

    private static bool TryExtractAttrLiteral(BinaryExpr expr, out string attrName, out object value)
    {
        if (expr.Left is AttrRefExpr attr1 && TryExtractLiteral(expr.Right, out var v1))
        {
            attrName = attr1.Name;
            value = v1;
            return true;
        }

        if (expr.Right is AttrRefExpr attr2 && TryExtractLiteral(expr.Left, out var v2))
        {
            attrName = attr2.Name;
            value = v2;
            return true;
        }

        attrName = "";
        value = "";
        return false;
    }

    private static bool TryExtractLiteral(XPathExpr expr, out object value)
    {
        switch (expr)
        {
            case LiteralStringExpr lit:
                value = lit.Value;
                return true;
            case LiteralNumberExpr num:
                value = num.Value;
                return true;
            case FunctionCallExpr { Name: "true", Args: [] }:
                value = true;
                return true;
            case FunctionCallExpr { Name: "false", Args: [] }:
                value = false;
                return true;
            default:
                value = "";
                return false;
        }
    }

    private static PropertyCondition? TryBuildPropertyCondition(
        AutomationElement el, string attrName, object value)
    {
        var normalized = ElementProperties.Normalize(attrName);
        if (!PushableProperties.TryGetValue(normalized, out var entry)) return null;

        var propertyIds = EnsurePropertyIds(el);
        if (!propertyIds.TryGetValue(normalized, out var propertyId)) return null;

        return entry.Type switch
        {
            PropType.String when value is string s =>
                new PropertyCondition(propertyId, s, PropertyConditionFlags.IgnoreCase),
            PropType.Bool when value is bool b =>
                new PropertyCondition(propertyId, b),
            PropType.Bool when value is string s && bool.TryParse(s, out var b) =>
                new PropertyCondition(propertyId, b),
            PropType.Int when value is double d && d == (int)d =>
                new PropertyCondition(propertyId, (int)d),
            PropType.Int when value is string s && int.TryParse(s, out var i) =>
                new PropertyCondition(propertyId, i),
            _ => null
        };
    }

    // For And: either or both sides can translate (partial push is valid)
    private static ConditionBase? CombineAnd(ConditionBase? left, ConditionBase? right) =>
        (left, right) switch
        {
            (not null, not null) => left.And(right),
            (not null, null) => left,
            (null, not null) => right,
            _ => null
        };

    // For Or: both sides MUST translate (can't partially push)
    private static OrCondition? CombineOr(ConditionBase? left, ConditionBase? right) =>
        left is not null && right is not null ? left.Or(right) : null;

    private static FrozenDictionary<string, PropertyId> EnsurePropertyIds(AutomationElement el)
    {
        if (_propertyIds is not null) return _propertyIds;

        var dict = new Dictionary<string, PropertyId>(StringComparer.OrdinalIgnoreCase);
        var propsObj = el.Properties;
        var propsType = propsObj.GetType();

        foreach (var (attrName, entry) in PushableProperties)
        {
            try
            {
                var propInfo = propsType.GetProperty(entry.Accessor);
                if (propInfo is null) continue;
                var automationProp = propInfo.GetValue(propsObj);
                if (automationProp is null) continue;
                var idProp = automationProp.GetType().GetProperty("Id")
                    ?? automationProp.GetType().GetProperty("PropertyId");
                if (idProp?.GetValue(automationProp) is PropertyId id)
                    dict[attrName] = id;
            }
            catch { /* skip unsupported properties */ }
        }

        _propertyIds = dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        return _propertyIds;
    }
}
