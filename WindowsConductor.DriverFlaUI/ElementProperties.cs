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
        return normalized == "text" || PropertyMap.ContainsKey(normalized);
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

        return result;
    }

    internal static string? Resolve(AutomationElement el, string key) =>
        ResolveRaw(el, key)?.ToString();

    internal static object? ResolveRaw(AutomationElement el, string key)
    {
        var normalized = Normalize(key);

        if (normalized == "text")
            return ResolveText(el);

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
