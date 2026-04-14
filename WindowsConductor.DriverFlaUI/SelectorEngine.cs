using FlaUI.Core.AutomationElements;

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
///   [isenabled=true]              Any property from <see cref="ElementProperties"/>
///
/// Compound selectors (AND logic):
///   [automationid=okBtn]&&type=Button
///
/// XPath expressions (passed through to <see cref="XPathEngine"/>):
///   //Button[@AutomationId='okBtn']
///   //*[@Name='Cancel']
/// </summary>
public static class SelectorEngine
{
    public static AutomationElement? FindElement(
        AutomationElement root, string selector,
        AutomationElement? desktopRoot = null,
        int? confineToProcessId = null) =>
        FindElements(root, selector, desktopRoot, confineToProcessId).FirstOrDefault();

    public static AutomationElement[] FindElements(
        AutomationElement root, string selector,
        AutomationElement? desktopRoot = null,
        int? confineToProcessId = null)
    {
        Validate(selector);

        selector = selector.Trim();

        AutomationElement[] results;

        if (selector.StartsWith('/') || selector.StartsWith('.'))
        {
            var effectiveRoot = IsAbsoluteXPath(selector) && desktopRoot is not null
                ? desktopRoot
                : root;
            results = XPathEngine.Evaluate(effectiveRoot, selector).ToArray();
        }
        else
        {
            var parts = selector.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            AutomationElement[]? current = null;
            foreach (var part in parts)
            {
                var (key, value) = ParsePart(part);
                var pool = current as IEnumerable<AutomationElement> ?? root.FindAllDescendants();
                current = Filter(pool, key, value);
            }
            results = current ?? [];
        }

        return ApplyProcessFilter(results, confineToProcessId);
    }

    private static bool IsAbsoluteXPath(string selector) =>
        selector.StartsWith('/') && !selector.StartsWith("//", StringComparison.Ordinal);

    private static AutomationElement[] ApplyProcessFilter(AutomationElement[] results, int? confineToProcessId)
    {
        if (confineToProcessId is not { } pid || results.Length == 0)
            return results;

        var inProcess = results.Where(el => BelongsToProcess(el, pid)).ToArray();
        if (inProcess.Length == 0)
            throw new AccessRestrictedException(
                $"All matched elements belong to a different process (confined to PID {pid}).");
        return inProcess;
    }

    private static bool BelongsToProcess(AutomationElement el, int processId)
    {
        try { return el.Properties.ProcessId.ValueOrDefault == processId; }
        catch { return false; }
    }

    // ── Validation ──────────────────────────────────────────────────────────

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
        if (selector.StartsWith('/') || selector.StartsWith('.'))
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

            var key = inner[..eq].Trim();
            return (ElementProperties.Normalize(key), inner[(eq + 1)..].Trim());
        }

        // Unclosed/mismatched brackets
        if (part.EndsWith(']') && !part.StartsWith('['))
            throw new ArgumentException(
                $"Unexpected closing bracket in selector part: '{part}'", nameof(part));

        // key=value
        var sep = part.IndexOf('=');
        if (sep > 0)
        {
            var key2 = part[..sep].Trim();
            return (ElementProperties.Normalize(key2), part[(sep + 1)..].Trim());
        }

        // bare value → treat as name
        return ("name", part);
    }

    private static AutomationElement[] Filter(
        IEnumerable<AutomationElement> source, string key, string value)
    {
        return source.Where(el =>
            MatchesProperty(key, value, k => ElementProperties.Resolve(el, k))
        ).ToArray();
    }

    internal static bool MatchesProperty(string key, string value, Func<string, string?> getProperty)
    {
        string? actual = getProperty(key);
        return string.Equals(actual, value, StringComparison.InvariantCultureIgnoreCase);
    }
}
