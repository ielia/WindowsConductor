using WindowsConductor.Client;

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
            "clear" => new ClearCommand(),
            "close" => new CloseCommand(),
            "detach" => new DetachCommand(),
            "disconnect" => new DisconnectCommand(),
            "locate" => ParseLocate(parts),
            "matchindex" => ParseMatchIndex(parts),
            "nextmatch" => ParseNextMatch(parts),
            "prevmatch" => ParsePrevMatch(parts),
            "unselect" => new UnselectCommand(),
            "attribute" => ParseAttribute(parts),
            "click" => new ClickCommand(),
            "doubleclick" => new DoubleClickCommand(),
            "refresh" => new RefreshCommand(),
            "reset" => new ResetCommand(),
            "rightclick" => new RightClickCommand(),
            "type" => ParseType(parts),
            "focus" => new FocusCommand(),
            "foreground" => new ForegroundCommand(),
            "parent" => new ParentCommand(),
            "children" => new ChildrenCommand(),
            "sleep" => ParseSleep(parts),
            "text" => new TextCommand(),
            "screenshot" => new ScreenshotCommand(),
            "snapshot" => new SnapshotCommand(),
            "windowstate" => ParseWindowState(parts),
            "exit" or "quit" => new ExitCommand(),
            "help" => new HelpCommand(parts.Length >= 2 ? parts[1].ToLowerInvariant() : null),
            _ => throw new ArgumentException($"Unknown command: '{parts[0]}'.")
        };
    }

    private static ConnectCommand ParseConnect(string[] parts)
    {
        var url = parts.Length >= 2 ? parts[1] : WcDefaults.WebSocketUrl;
        var authToken = parts.Length >= 3 ? parts[2] : null;
        return new ConnectCommand(url, authToken);
    }

    private static LaunchCommand ParseLaunch(string[] parts)
    {
        if (parts.Length < 2)
            throw new ArgumentException("Usage: launch <path> [\"arg1\", ...] [detachedTitleRegex] [mainWindowTimeout]");

        var path = parts[1];
        string[] args = [];
        string? detachedTitleRegex = null;
        uint? mainWindowTimeout = null;

        int nextIdx = 2;

        if (nextIdx < parts.Length && parts[nextIdx].StartsWith('['))
        {
            args = ParseArgsArray(parts[nextIdx]);
            nextIdx++;
        }

        int endIdx = parts.Length;

        if (endIdx > nextIdx && uint.TryParse(parts[endIdx - 1], out var timeout))
        {
            mainWindowTimeout = timeout;
            endIdx--;
        }

        if (endIdx > nextIdx)
        {
            detachedTitleRegex = parts[endIdx - 1];
            endIdx--;
        }

        return new LaunchCommand(path, args, detachedTitleRegex, mainWindowTimeout);
    }

    private static string[] ParseArgsArray(string token)
    {
        if (token.StartsWith('[') && token.EndsWith(']'))
            token = token[1..^1];

        var args = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuote = false;
        char quoteChar = '\0';

        for (int i = 0; i < token.Length; i++)
        {
            char c = token[i];
            if (inQuote)
            {
                if (c == quoteChar) inQuote = false;
                else sb.Append(c);
            }
            else if (c is '"' or '\'')
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (c == ',')
            {
                var val = sb.ToString().Trim();
                if (val.Length > 0) args.Add(val);
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        var last = sb.ToString().Trim();
        if (last.Length > 0) args.Add(last);

        return args.ToArray();
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
            throw new ArgumentException("Usage: type <text> [ctrl alt shift meta]");

        var modifiers = KeyModifiers.None;
        var textParts = parts.Skip(1).ToArray();

        // If last token is a bracket group like "[ctrl alt]", parse modifiers from it
        if (textParts.Length > 1 && textParts[^1].StartsWith('[') && textParts[^1].EndsWith(']'))
        {
            modifiers = ParseModifiers(textParts[^1]);
            textParts = textParts[..^1];
        }

        var text = string.Join(' ', textParts);
        return new TypeCommand(text, modifiers);
    }

    private static KeyModifiers ParseModifiers(string token)
    {
        var inner = token[1..^1].Trim();
        if (string.IsNullOrEmpty(inner))
            throw new ArgumentException("Modifier list cannot be empty. Valid modifiers: ctrl, alt, shift, meta.");

        var modifiers = KeyModifiers.None;
        foreach (var part in inner.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" => KeyModifiers.Ctrl,
                "alt" => KeyModifiers.Alt,
                "shift" => KeyModifiers.Shift,
                "meta" => KeyModifiers.Meta,
                _ => throw new ArgumentException($"Unknown modifier: '{part}'. Valid modifiers: ctrl, alt, shift, meta.")
            };
        }
        return modifiers;
    }

    private static MatchIndexCommand ParseMatchIndex(string[] parts)
    {
        if (parts.Length < 2 || !int.TryParse(parts[1], out var index))
            throw new ArgumentException("Usage: matchindex <N>");
        return new MatchIndexCommand(index);
    }

    private static NextMatchCommand ParseNextMatch(string[] parts)
    {
        if (parts.Length >= 2)
        {
            if (!int.TryParse(parts[1], out var steps) || steps < 1)
                throw new ArgumentException("Usage: nextmatch [N] (N must be a positive integer)");
            return new NextMatchCommand(steps);
        }
        return new NextMatchCommand();
    }

    private static PrevMatchCommand ParsePrevMatch(string[] parts)
    {
        if (parts.Length >= 2)
        {
            if (!int.TryParse(parts[1], out var steps) || steps < 1)
                throw new ArgumentException("Usage: prevmatch [N] (N must be a positive integer)");
            return new PrevMatchCommand(steps);
        }
        return new PrevMatchCommand();
    }

    private static WindowStateCommand ParseWindowState(string[] parts)
    {
        if (parts.Length < 2)
            return new WindowStateCommand();
        if (Enum.TryParse<WcWindowState>(parts[1], ignoreCase: true, out var state))
            return new WindowStateCommand(state);
        var valid = string.Join(", ", Enum.GetValues<WcWindowState>().Select(s => s.ToString().ToLowerInvariant()));
        throw new ArgumentException($"Unknown window state: '{parts[1]}'. Valid states: {valid}.");
    }

    private static SleepCommand ParseSleep(string[] parts)
    {
        if (parts.Length < 2 || !int.TryParse(parts[1], out var ms) || ms <= 0)
            throw new ArgumentException("Usage: sleep <milliseconds>");
        return new SleepCommand(ms);
    }

    /// <summary>
    /// Splits input into individual commands separated by ';',
    /// respecting quoted strings and bracket groups.
    /// </summary>
    internal static string[] SplitCommands(string input)
    {
        var commands = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuote = false;
        char quoteChar = '\0';
        int bracketDepth = 0;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (inQuote)
            {
                current.Append(c);
                if (c == quoteChar) inQuote = false;
            }
            else if (c is '"' or '\'')
            {
                current.Append(c);
                inQuote = true;
                quoteChar = c;
            }
            else if (c == '[')
            {
                current.Append(c);
                bracketDepth++;
            }
            else if (c == ']' && bracketDepth > 0)
            {
                current.Append(c);
                bracketDepth--;
            }
            else if (c == ';' && bracketDepth == 0)
            {
                var cmd = current.ToString().Trim();
                if (cmd.Length > 0) commands.Add(cmd);
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        var last = current.ToString().Trim();
        if (last.Length > 0) commands.Add(last);

        return commands.ToArray();
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
            else if (c == '[')
            {
                current.Append(c);
                i++;
                bool inBracketQuote = false;
                char bracketQuoteChar = '\0';
                while (i < input.Length)
                {
                    char bc = input[i];
                    if (inBracketQuote)
                    {
                        current.Append(bc);
                        if (bc == bracketQuoteChar) inBracketQuote = false;
                    }
                    else if (bc is '"' or '\'')
                    {
                        current.Append(bc);
                        inBracketQuote = true;
                        bracketQuoteChar = bc;
                    }
                    else if (bc == ']')
                    {
                        current.Append(bc);
                        break;
                    }
                    else
                    {
                        current.Append(bc);
                    }
                    i++;
                }
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
