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

    internal static bool IsSupported(string key)
    {
        var normalized = Normalize(key);
        return normalized == "text" || ScrollKeys.Contains(normalized) || PropertyMap.ContainsKey(normalized);
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

        ResolveScrollProperties(el, result);

        return result;
    }

    internal static string? Resolve(AutomationElement el, string key) =>
        ResolveRaw(el, key)?.ToString();

    private static readonly HashSet<string> ScrollKeys = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "vscrollpercent", "hscrollpercent", "vviewpercent", "hviewpercent",
        "scrolltop", "scrollleft", "vscrolltotal", "hscrolltotal"
    };

    internal static object? ResolveRaw(AutomationElement el, string key)
    {
        var normalized = Normalize(key);

        if (normalized == "text")
            return ResolveText(el);

        if (ScrollKeys.Contains(normalized))
        {
            var dict = new Dictionary<string, object?>(StringComparer.InvariantCultureIgnoreCase);
            ResolveScrollProperties(el, dict);
            return dict.GetValueOrDefault(normalized);
        }

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
        _ => value.ToString() ?? ""
    };

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
