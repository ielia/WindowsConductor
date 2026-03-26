using System.Collections.Frozen;
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
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = "name",
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

    internal static bool IsSupported(string key) =>
        PropertyMap.ContainsKey(Normalize(key));

    internal static string Normalize(string key)
    {
        key = key.ToLowerInvariant();
        return Aliases.TryGetValue(key, out var canonical) ? canonical : key;
    }

    internal static string? Resolve(AutomationElement el, string key)
    {
        var normalized = Normalize(key);
        if (!PropertyMap.TryGetValue(normalized, out var propInfo))
            return null;

        try
        {
            var automationProp = propInfo.GetValue(el.Properties);
            if (automationProp is null) return null;

            // AutomationProperty<T> exposes ValueOrDefault via its base class.
            var value = automationProp.GetType().GetProperty("ValueOrDefault")?.GetValue(automationProp);
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
