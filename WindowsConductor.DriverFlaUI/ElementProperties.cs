using System.Collections.Frozen;
using System.Drawing;
using System.Reflection;
using FlaUI.Core.AutomationElements;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Resolves property keys to string values on an <see cref="AutomationElement"/>
/// using reflection over <c>el.Properties</c>.
/// Single source of truth for attribute resolution — used by <see cref="SelectorEngine"/>,
/// <see cref="XPathEngine"/>, and <see cref="AppManager.GetAttribute"/>.
/// </summary>
internal static class ElementProperties
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.InvariantCultureIgnoreCase)
    {
        ["class"] = "classname",
        ["type"] = "controltype",
    };

    // Map lower-case property name → PropertyInfo on the IProperties interface.
    // Built once via reflection; every property exposed by FlaUI is automatically supported.
    private static readonly FrozenDictionary<string, PropertyInfo> PropertyMap =
        BuildPropertyMap();

    private static FrozenDictionary<string, PropertyInfo> BuildPropertyMap()
    {
        var propsType = typeof(AutomationElement).GetProperty("Properties")!.PropertyType;
        return propsType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToFrozenDictionary(p => p.Name.ToLowerInvariant(), p => p);
    }

    // ── Pattern property map ────────────────────────────────────────────────

    private sealed record PatternPropAccessor(
        PropertyInfo ContainerProp,
        PropertyInfo PatternOrDefaultProp,
        PropertyInfo ValueProp,
        string Key);

    private static readonly PropertyInfo PatternsProperty =
        typeof(AutomationElement).GetProperty("Patterns")!;

    private static readonly FrozenDictionary<string, PatternPropAccessor> PatternPropertyMap =
        BuildPatternPropertyMap();

    private static FrozenDictionary<string, PatternPropAccessor> BuildPatternPropertyMap()
    {
        var patternsType = PatternsProperty.PropertyType;
        var result = new Dictionary<string, PatternPropAccessor>(StringComparer.InvariantCultureIgnoreCase);

        foreach (var containerProp in patternsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var patternOrDefaultProp = containerProp.PropertyType.GetProperty("PatternOrDefault");
            if (patternOrDefaultProp is null) continue;

            var patternType = patternOrDefaultProp.PropertyType;
            var patternName = containerProp.Name.ToLowerInvariant();

            foreach (var valueProp in patternType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (valueProp.Name is "PropertyIds" or "EventIds") continue;
                if (!valueProp.PropertyType.IsGenericType) continue;
                if (!valueProp.PropertyType.GetGenericTypeDefinition().Name.StartsWith("AutomationProperty", StringComparison.Ordinal)) continue;

                var innerType = valueProp.PropertyType.GetGenericArguments()[0];
                if (!IsSerializableType(innerType)) continue;

                var key = $"{patternName}_{valueProp.Name.ToLowerInvariant()}";
                result[key] = new PatternPropAccessor(containerProp, patternOrDefaultProp, valueProp, key);
            }
        }

        return result.ToFrozenDictionary();
    }

    private static bool IsSerializableType(Type t) =>
        t == typeof(string) || t == typeof(bool) || t == typeof(int)
        || t == typeof(long) || t == typeof(double) || t == typeof(float)
        || t.IsEnum
        || (t.IsArray && IsSerializableType(t.GetElementType()!));

    internal static bool IsSupported(string key)
    {
        var normalized = Normalize(key);
        return normalized == "text" || DerivedKeys.Contains(normalized)
            || PropertyMap.ContainsKey(normalized) || PatternPropertyMap.ContainsKey(normalized);
    }

    internal static string Normalize(string key)
    {
        key = key.ToLowerInvariant();
        return Aliases.TryGetValue(key, out var canonical) ? canonical : key;
    }

    internal static Dictionary<string, object?> ResolveAll(AutomationElement el)
    {
        var result = new Dictionary<string, object?>(PropertyMap.Count + 1, StringComparer.InvariantCultureIgnoreCase);
        foreach (var (name, propInfo) in PropertyMap)
        {
            try
            {
                var automationProp = propInfo.GetValue(el.Properties);
                if (automationProp is null) continue;
                var value = automationProp.GetType().GetProperty("ValueOrDefault")?.GetValue(automationProp);
                if (value is not null)
                    result[name] = ToSerializable(value);
            }
            catch { /* skip unsupported properties */ }
        }

        var text = ResolveText(el);
        if (text is not null)
            result["text"] = text;

        ResolveDerivedProperties(el, result);
        ResolvePatternProperties(el, result);

        return result;
    }

    internal static string? Resolve(AutomationElement el, string key)
    {
        var raw = ResolveRaw(el, key);
        if (raw is null) return null;
        if (raw is Array arr)
            return string.Join(",", arr.Cast<object>().Select(o => o.ToString()));
        return raw.ToString();
    }

    private static readonly HashSet<string> DerivedKeys = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "vscrollpercent", "hscrollpercent", "vviewpercent", "hviewpercent",
        "scrolltop", "scrollleft", "vscrolltotal", "hscrolltotal",
        "ischecked"
    };

    internal static object? ResolveRaw(AutomationElement el, string key)
    {
        var normalized = Normalize(key);

        if (normalized == "text")
            return ResolveText(el);

        if (DerivedKeys.Contains(normalized))
        {
            var dict = new Dictionary<string, object?>(StringComparer.InvariantCultureIgnoreCase);
            ResolveDerivedProperties(el, dict);
            return dict.GetValueOrDefault(normalized);
        }

        if (PatternPropertyMap.TryGetValue(normalized, out var accessor))
            return ResolvePatternProperty(el, accessor);

        if (!PropertyMap.TryGetValue(normalized, out var propInfo))
            return null;

        try
        {
            var automationProp = propInfo.GetValue(el.Properties);
            if (automationProp is null) return null;

            // AutomationProperty<T> exposes ValueOrDefault via its base class.
            return automationProp.GetType().GetProperty("ValueOrDefault")?.GetValue(automationProp);
        }
        catch
        {
            return null;
        }
    }

    private static object ToSerializable(object value) => value switch
    {
        bool or int or long or double or float or string => value,
        Point p => new { x = p.X, y = p.Y },
        Rectangle r => new { x = r.X, y = r.Y, width = r.Width, height = r.Height },
        Array arr => arr.Cast<object>().Select(ToSerializable).ToArray(),
        _ => value.ToString() ?? ""
    };

    private static void ResolveDerivedProperties(AutomationElement el, Dictionary<string, object?> result)
    {
        ResolveScrollProperties(el, result);
        ResolveIsChecked(el, result);
    }

    private static void ResolveIsChecked(AutomationElement el, Dictionary<string, object?> result)
    {
        try
        {
            var toggle = el.Patterns.Toggle.PatternOrDefault;
            if (toggle is null) return;
            var state = toggle.ToggleState.ValueOrDefault;
            result["ischecked"] = state == FlaUI.Core.Definitions.ToggleState.On;
        }
        catch { /* skip if toggle pattern not available */ }
    }

    private static void ResolveScrollProperties(AutomationElement el, Dictionary<string, object?> result)
    {
        try
        {
            var scroll = el.Patterns.Scroll.PatternOrDefault;
            if (scroll is null) return;

            var vPercent = scroll.VerticalScrollPercent.ValueOrDefault;
            var hPercent = scroll.HorizontalScrollPercent.ValueOrDefault;
            var vView = scroll.VerticalViewSize.ValueOrDefault;
            var hView = scroll.HorizontalViewSize.ValueOrDefault;

            if (vPercent >= 0) result["vscrollpercent"] = vPercent;
            if (hPercent >= 0) result["hscrollpercent"] = hPercent;
            if (vView > 0) result["vviewpercent"] = vView;
            if (hView > 0) result["hviewpercent"] = hView;

            var rect = el.BoundingRectangle;
            if (vView > 0 && vView < 100)
            {
                var totalHeight = rect.Height / (vView / 100.0);
                result["vscrolltotal"] = Math.Round(totalHeight, 1);
                if (vPercent >= 0)
                    result["scrolltop"] = Math.Round(totalHeight * (vPercent / 100.0), 1);
            }
            if (hView > 0 && hView < 100)
            {
                var totalWidth = rect.Width / (hView / 100.0);
                result["hscrolltotal"] = Math.Round(totalWidth, 1);
                if (hPercent >= 0)
                    result["scrollleft"] = Math.Round(totalWidth * (hPercent / 100.0), 1);
            }
        }
        catch { /* skip if scroll pattern not available */ }
    }

    private static void ResolvePatternProperties(AutomationElement el, Dictionary<string, object?> result)
    {
        var patternsContainer = PatternsProperty.GetValue(el);
        if (patternsContainer is null) return;

        foreach (var (key, accessor) in PatternPropertyMap)
        {
            try
            {
                var container = accessor.ContainerProp.GetValue(patternsContainer);
                if (container is null) continue;
                var pattern = accessor.PatternOrDefaultProp.GetValue(container);
                if (pattern is null) continue;
                var automationProp = accessor.ValueProp.GetValue(pattern);
                if (automationProp is null) continue;
                var value = automationProp.GetType().GetProperty("ValueOrDefault")?.GetValue(automationProp);
                if (value is not null)
                    result[key] = ToSerializable(value);
            }
            catch { /* skip if pattern not available */ }
        }
    }

    private static object? ResolvePatternProperty(AutomationElement el, PatternPropAccessor accessor)
    {
        try
        {
            var patternsContainer = PatternsProperty.GetValue(el);
            if (patternsContainer is null) return null;
            var container = accessor.ContainerProp.GetValue(patternsContainer);
            if (container is null) return null;
            var pattern = accessor.PatternOrDefaultProp.GetValue(container);
            if (pattern is null) return null;
            var automationProp = accessor.ValueProp.GetValue(pattern);
            if (automationProp is null) return null;
            return automationProp.GetType().GetProperty("ValueOrDefault")?.GetValue(automationProp);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveText(AutomationElement el)
    {
        try
        {
            var tb = el.AsTextBox();
            return tb?.Text;
        }
        catch
        {
            return null;
        }
    }
}
