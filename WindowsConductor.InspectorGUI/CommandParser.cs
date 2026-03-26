namespace WindowsConductor.InspectorGUI;

internal static class CommandParser
{
    internal static ParsedCommand Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Command cannot be empty.");

        var parts = Tokenize(input);
        var command = parts[0].ToLowerInvariant();

        return command switch
        {
            "connect" => ParseConnect(parts),
            "launch" => ParseLaunch(parts),
            "attach" => ParseAttach(parts),
            "close" => new CloseCommand(),
            "wscreenshot" => new WindowScreenshotCommand(),
            "locate" => ParseLocate(parts),
            "unselect" => new UnselectCommand(),
            "attribute" => ParseAttribute(parts),
            "click" => new ClickCommand(),
            "doubleclick" => new DoubleClickCommand(),
            "type" => ParseType(parts),
            "focus" => new FocusCommand(),
            "text" => new TextCommand(),
            "screenshot" => new ScreenshotCommand(),
            _ => throw new ArgumentException($"Unknown command: '{parts[0]}'.")
        };
    }

    private static ConnectCommand ParseConnect(string[] parts)
    {
        if (parts.Length < 2)
            throw new ArgumentException("Usage: connect <URL>");
        return new ConnectCommand(parts[1]);
    }

    private static LaunchCommand ParseLaunch(string[] parts)
    {
        if (parts.Length < 2)
            throw new ArgumentException("Usage: launch <path> [args...] [detachedTitleRegex] [mainWindowTimeout]");

        var path = parts[1];
        string? detachedTitleRegex = null;
        uint? mainWindowTimeout = null;
        var argsList = new List<string>();

        // Walk backwards from the end to find optional trailing params
        int endIdx = parts.Length;

        // Last part might be a timeout (unsigned integer)
        if (endIdx > 2 && uint.TryParse(parts[endIdx - 1], out var timeout))
        {
            mainWindowTimeout = timeout;
            endIdx--;
        }

        // Next-to-last (now) might be the detachedTitleRegex
        if (endIdx > 2)
        {
            detachedTitleRegex = parts[endIdx - 1];
            endIdx--;
        }

        for (int i = 2; i < endIdx; i++)
            argsList.Add(parts[i]);

        return new LaunchCommand(path, argsList.ToArray(), detachedTitleRegex, mainWindowTimeout);
    }

    private static AttachCommand ParseAttach(string[] parts)
    {
        if (parts.Length < 2)
            throw new ArgumentException("Usage: attach <mainWindowTitleRegex> [mainWindowTimeout]");

        var regex = parts[1];
        uint? timeout = null;
        if (parts.Length >= 3 && uint.TryParse(parts[2], out var t))
            timeout = t;

        return new AttachCommand(regex, timeout);
    }

    private static LocateCommand ParseLocate(string[] parts)
    {
        if (parts.Length < 2)
            throw new ArgumentException("Usage: locate <selector1> [>> <selector2> ...]");

        // Rejoin everything after "locate" and split by ">>"
        var rest = string.Join(' ', parts.Skip(1));
        var selectors = rest.Split(">>", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (selectors.Length == 0)
            throw new ArgumentException("Usage: locate <selector1> [>> <selector2> ...]");

        return new LocateCommand(selectors);
    }

    private static AttributeCommand ParseAttribute(string[] parts)
    {
        if (parts.Length < 2)
            throw new ArgumentException("Usage: attribute <attributeName>");
        return new AttributeCommand(parts[1]);
    }

    private static TypeCommand ParseType(string[] parts)
    {
        if (parts.Length < 2)
            throw new ArgumentException("Usage: type <text>");
        // Rejoin everything after "type" to preserve spaces
        var text = string.Join(' ', parts.Skip(1));
        return new TypeCommand(text);
    }

    /// <summary>
    /// Splits input respecting quoted strings (single or double quotes).
    /// </summary>
    internal static string[] Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuote = false;
        char quoteChar = '\0';

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (inQuote)
            {
                if (c == quoteChar)
                {
                    inQuote = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"' || c == '\'')
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens.ToArray();
    }
}
