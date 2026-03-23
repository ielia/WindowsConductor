using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;

namespace PlaywrightWinApp.DriverFlaUI;

/// <summary>
/// Resolves a selector string to one or more UIAutomation elements.
///
/// Selector syntax
/// ───────────────
///   [automationid=value]          By AutomationId  (case-insensitive value)
///   [name=value]                  By Name / display text
///   text=value                    Same as [name=value] (shorthand)
///   type=Button                   By ControlType name  (Button, Edit, Window …)
///   classname=Foo                 By ClassName
///
/// Compound selectors (AND logic):
///   [automationid=okBtn]&&type=Button
///
/// XPath expressions (passed through to <see cref="XPathEngine"/>):
///   //Button[@AutomationId='okBtn']
///   //*[@Name='Cancel']
/// </summary>
public sealed class SelectorEngine
{
    private readonly XPathEngine _xpath;

    public SelectorEngine(XPathEngine xpath) => _xpath = xpath;

    public AutomationElement? FindElement(AutomationElement root, string selector) =>
        FindElements(root, selector).FirstOrDefault();

    public AutomationElement[] FindElements(AutomationElement root, string selector)
    {
        selector = selector.Trim();

        // Delegate XPath to the dedicated engine
        if (selector.StartsWith('/'))
            return _xpath.Evaluate(root, selector).ToArray();

        // Split compound conditions on &&
        var parts = selector.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // Start with all descendants; narrow down with each condition
        AutomationElement[]? current = null;

        foreach (var part in parts)
        {
            var (key, value) = ParsePart(part);
            var pool = current as IEnumerable<AutomationElement> ?? root.FindAllDescendants();
            current = Filter(pool, key, value);
        }

        return current ?? Array.Empty<AutomationElement>();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static (string Key, string Value) ParsePart(string part)
    {
        // [key=value]
        if (part.StartsWith('[') && part.EndsWith(']'))
        {
            var inner = part[1..^1];
            var eq = inner.IndexOf('=');
            if (eq > 0)
                return (inner[..eq].Trim().ToLowerInvariant(), inner[(eq + 1)..].Trim());
        }

        // key=value
        var sep = part.IndexOf('=');
        if (sep > 0)
            return (part[..sep].Trim().ToLowerInvariant(), part[(sep + 1)..].Trim());

        // bare value → treat as name
        return ("name", part);
    }

    private static AutomationElement[] Filter(
        IEnumerable<AutomationElement> source, string key, string value)
    {
        return source.Where(el =>
        {
            bool result;
            try
            {
                result = key switch
                {
                    "automationid" =>
                        string.Equals(el.AutomationId, value, StringComparison.OrdinalIgnoreCase),
                    "name" or "text" =>
                        string.Equals(el.Name, value, StringComparison.OrdinalIgnoreCase),
                    "classname" or "class" =>
                        string.Equals(el.ClassName, value, StringComparison.OrdinalIgnoreCase),
                    "type" or "controltype" =>
                        string.Equals(el.ControlType.ToString(), value, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }
            catch (PropertyNotSupportedException)
            {
                result = false;
            }
            return result;
        }).ToArray();
    }
}
