using FlaUI.Core.AutomationElements;

namespace WindowsConductor.DriverFlaUI;

internal static class ElementFilter
{
    /// <summary>
    /// Filters a list of elements to only the leaf-most ones: elements that have
    /// no descendant also present in the list. Uses RuntimeId for stable identity
    /// comparison across separate FlaUI queries.
    /// </summary>
    internal static List<AutomationElement> Leafmost(IReadOnlyList<AutomationElement> elements)
    {
        if (elements.Count <= 1) return elements.ToList();

        var ancestorKeys = new HashSet<string>();
        foreach (var el in elements)
        {
            var parent = el.Parent;
            while (parent is not null)
            {
                var key = RuntimeIdKey(parent);
                if (key is not null && !ancestorKeys.Add(key))
                    break;
                parent = parent.Parent;
            }
        }

        return elements
            .Where(el =>
            {
                var key = RuntimeIdKey(el);
                return key is null || !ancestorKeys.Contains(key);
            })
            .ToList();
    }

    internal static string? RuntimeIdKey(AutomationElement el)
    {
        var id = el.Properties.RuntimeId.ValueOrDefault;
        return id is { Length: > 0 } ? string.Join(".", id) : null;
    }
}
