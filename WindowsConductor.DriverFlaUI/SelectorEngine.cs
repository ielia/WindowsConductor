using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;

namespace WindowsConductor.DriverFlaUI;

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
        Validate(selector);

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

    // ── Validation ──────────────────────────────────────────────────────────

    private static readonly HashSet<string> ValidKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "automationid", "name", "text", "classname", "class", "type", "controltype"
    };

    /// <summary>
    /// Validates a selector string without resolving it.
    /// Throws <see cref="ArgumentException"/> if the selector is malformed.
    /// </summary>
    public static void Validate(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            throw new ArgumentException("Selector must not be empty.", nameof(selector));

        selector = selector.Trim();

        // XPath validation is handled by XPathEngine
        if (selector.StartsWith('/'))
        {
            XPathEngine.Validate(selector);
            return;
        }

        var parts = selector.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            throw new ArgumentException($"Selector contains no valid parts: '{selector}'", nameof(selector));

        foreach (var part in parts)
            ParsePart(part); // throws on invalid syntax
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    internal static (string Key, string Value) ParsePart(string part)
    {
        // [key=value]
        if (part.StartsWith('['))
        {
            if (!part.EndsWith(']'))
                throw new ArgumentException(
                    $"Unclosed bracket in selector part: '{part}'", nameof(part));

            var inner = part[1..^1];

            if (string.IsNullOrWhiteSpace(inner))
                throw new ArgumentException(
                    $"Empty bracket selector: '{part}'", nameof(part));

            var eq = inner.IndexOf('=');
            if (eq <= 0)
                throw new ArgumentException(
                    $"Bracket selector must have the format [key=value]: '{part}'", nameof(part));

            var key = inner[..eq].Trim().ToLowerInvariant();
            if (!ValidKeys.Contains(key))
                throw new ArgumentException(
                    $"Unknown selector attribute '{inner[..eq].Trim()}' in '{part}'. " +
                    $"Valid attributes: {string.Join(", ", ValidKeys)}",
                    nameof(part));

            return (key, inner[(eq + 1)..].Trim());
        }

        // Unclosed/mismatched brackets
        if (part.EndsWith(']') && !part.StartsWith('['))
            throw new ArgumentException(
                $"Unexpected closing bracket in selector part: '{part}'", nameof(part));

        // key=value
        var sep = part.IndexOf('=');
        if (sep > 0)
        {
            var key2 = part[..sep].Trim().ToLowerInvariant();
            if (!ValidKeys.Contains(key2))
                throw new ArgumentException(
                    $"Unknown selector attribute '{part[..sep].Trim()}' in '{part}'. " +
                    $"Valid attributes: {string.Join(", ", ValidKeys)}",
                    nameof(part));
            return (key2, part[(sep + 1)..].Trim());
        }

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
