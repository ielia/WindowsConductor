namespace WindowsConductor.Client;

/// <summary>
/// Client-side validation of selector strings.
/// Full syntax validation is performed server-side by the XPath parser.
/// </summary>
public static class SelectorValidator
{
    public static void Validate(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            throw new ArgumentException("Selector must not be empty.", nameof(selector));
    }
}
