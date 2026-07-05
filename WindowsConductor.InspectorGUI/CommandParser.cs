using WindowsConductor.Client;

namespace WindowsConductor.InspectorGUI;

internal static class CommandParser
{
    internal sealed record Token(string Value, bool WasQuoted);

    internal static ParsedCommand Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Command cannot be empty.");

        var tokens = TokenizeRich(input);
        var command = tokens[0].Value.ToLowerInvariant();

        return command switch
        {
            "connect" => ParseConnect(tokens),
            "launch" => ParseLaunch(tokens),
            "attach" => ParseAttach(tokens),
            "clear" => new ClearCommand(),
            "close" => new CloseCommand(),
            "detach" => new DetachCommand(),
            "disconnect" => new DisconnectCommand(),
            "locate" => ParseLocate(input),
            "matchindex" => ParseMatchIndex(tokens),
            "nextmatch" => ParseNextMatch(tokens),
            "prevmatch" => ParsePrevMatch(tokens),
            "unselect" => new UnselectCommand(),
            "attribute" => ParseAttribute(tokens),
            "setattribute" => ParseSetAttribute(tokens),
            "click" => ParseClick(tokens),
            "drag" => ParseDrag(tokens),
            "doubleclick" => ParseDoubleClick(tokens),
            "resolve" => ParseResolve(input),
            "refresh" => new RefreshCommand(),
            "reset" => new ResetCommand(),
            "rightclick" => ParseRightClick(tokens),
            "hover" => ParseHover(tokens),
            "scroll" => ParseScroll(tokens),
            "hitkeys" => ParseHitKeys(tokens),
            "type" => ParseType(tokens),
            "ghitkeys" => ParseGlobalHitKeys(tokens),
            "gtype" => ParseGlobalType(tokens),
            "ocr" => new OcrCommand(),
            "focus" => new FocusCommand(),
            "foreground" => new ForegroundCommand(),
            "parent" => new ParentCommand(),
            "children" => new ChildrenCommand(),
            "sleep" => ParseSleep(tokens),
            "text" => new TextCommand(),
            "screenshot" => new ScreenshotCommand(),
            "snapshot" => new SnapshotCommand(),
            "windowstate" => ParseWindowState(tokens),
            "exit" or "quit" => new ExitCommand(),
            "help" => new HelpCommand(tokens.Length >= 2 ? tokens[1].Value.ToLowerInvariant() : null),
            _ => throw new ArgumentException($"Unknown command: '{tokens[0].Value}'.")
        };
    }

    private static ConnectCommand ParseConnect(Token[] tokens)
    {
        var url = tokens.Length >= 2 ? tokens[1].Value : WcDefaults.WebSocketUrl;
        var authToken = tokens.Length >= 3 ? tokens[2].Value : null;
        return new ConnectCommand(url, authToken);
    }

    private static LaunchCommand ParseLaunch(Token[] tokens)
    {
        if (tokens.Length < 2)
            throw new ArgumentException("Usage: launch <path> [\"arg1\", ...] [detachedTitleRegex] [mainWindowTimeout]");

        var path = tokens[1].Value;
        string[] args = [];
        string? detachedTitleRegex = null;
        uint? mainWindowTimeout = null;

        int nextIdx = 2;

        if (nextIdx < tokens.Length && tokens[nextIdx].Value.StartsWith('['))
        {
            args = ParseArgsArray(tokens[nextIdx].Value);
            nextIdx++;
        }

        int endIdx = tokens.Length;

        if (endIdx > nextIdx && uint.TryParse(tokens[endIdx - 1].Value, out var timeout))
        {
            mainWindowTimeout = timeout;
            endIdx--;
        }

        if (endIdx > nextIdx)
        {
            detachedTitleRegex = tokens[endIdx - 1].Value;
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
        char quoteChar = '\0';

        for (int i = 0; i < token.Length; i++)
        {
            char c = token[i];
            if (quoteChar != '\0')
            {
                if (c == quoteChar) quoteChar = '\0';
                else sb.Append(c);
            }
            else if (c is '"' or '\'')
            {
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

    private static AttachCommand ParseAttach(Token[] tokens)
    {
        if (tokens.Length < 2)
            throw new ArgumentException("Usage: attach <mainWindowTitleRegex> [mainWindowTimeout]");

        var regex = tokens[1].Value;
        uint? timeout = null;
        if (tokens.Length >= 3 && uint.TryParse(tokens[2].Value, out var t))
            timeout = t;

        return new AttachCommand(regex, timeout);
    }

    private static LocateCommand ParseLocate(string rawInput)
    {
        var trimmed = rawInput.Trim();
        var spaceIdx = trimmed.IndexOf(' ');
        if (spaceIdx < 0)
            throw new ArgumentException("Usage: locate <selector1> [>> <selector2> ...]");

        var rest = trimmed[(spaceIdx + 1)..].Trim();
        var selectors = rest.Split(">>", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (selectors.Length == 0)
            throw new ArgumentException("Usage: locate <selector1> [>> <selector2> ...]");

        return new LocateCommand(selectors);
    }

    private static ResolveCommand ParseResolve(string rawInput)
    {
        var trimmed = rawInput.Trim();
        var spaceIdx = trimmed.IndexOf(' ');
        if (spaceIdx < 0)
            throw new ArgumentException("Usage: resolve <xpath>");
        var selector = trimmed[(spaceIdx + 1)..].Trim();
        if (selector.Length == 0)
            throw new ArgumentException("Usage: resolve <xpath>");
        return new ResolveCommand(selector);
    }

    private static (string? OcrText, int MaxDistance, int? MatchIndex, Anchor? Anchor, int OffsetX, int OffsetY) ParseMouseActionArgs(string commandName, Token[] tokens)
    {
        Anchor? anchor = null;
        int offsetX = 0, offsetY = 0;

        // Check for trailing anchor [+ offset] at the end of the token list
        int end = tokens.Length;
        if (TryParseTrailingAnchorAndOffset(tokens, 1, end, out var a, out var ax, out var ay, out var anchorConsumed))
        {
            anchor = a;
            offsetX = ax;
            offsetY = ay;
            end -= anchorConsumed;
        }

        if (end < 2) return (null, 0, null, anchor, offsetX, offsetY);
        if (!tokens[1].WasQuoted)
        {
            if (anchor is not null && end == 1)
                return (null, 0, null, anchor, offsetX, offsetY);
            throw new ArgumentException($"OCR text must be quoted. Usage: {commandName} \"ocrText\" [maxDistance] [#matchIndex] [<anchor> (<x>, <y>)]");
        }
        var ocrText = tokens[1].Value;
        if (string.IsNullOrWhiteSpace(ocrText)) return (null, 0, null, anchor, offsetX, offsetY);
        int maxDist = 0;
        int? matchIndex = null;
        for (int i = 2; i < end; i++)
        {
            if (tokens[i].Value.StartsWith('#'))
            {
                if (!int.TryParse(tokens[i].Value[1..], out var idx) || idx < 0)
                    throw new ArgumentException("matchIndex must be a non-negative integer (e.g. #0, #1).");
                matchIndex = idx;
            }
            else
            {
                if (!int.TryParse(tokens[i].Value, out maxDist))
                    throw new ArgumentException("maxDistance must be an integer.");
            }
        }
        return (ocrText, maxDist, matchIndex, anchor, offsetX, offsetY);
    }

    private static bool TryParseTrailingAnchorAndOffset(Token[] tokens, int start, int end, out Anchor anchor, out int x, out int y, out int consumed)
    {
        anchor = Anchor.Center;
        x = 0;
        y = 0;
        consumed = 0;

        // Try: ... <anchor> (<x>, <y>) — offset as 2 tokens
        if (end - start >= 3
            && AnchorNames.Contains(tokens[end - 3].Value)
            && TryParseOffset(tokens, end - 2, out var tx3, out var ty3, out var oc3) && oc3 == 2)
        {
            anchor = Enum.Parse<Anchor>(tokens[end - 3].Value, ignoreCase: true);
            x = tx3;
            y = ty3;
            consumed = 3;
            return true;
        }

        // Try: ... <anchor> (<x>,<y>) — offset as 1 token
        if (end - start >= 2
            && AnchorNames.Contains(tokens[end - 2].Value)
            && TryParseOffset(tokens, end - 1, out var tx2, out var ty2, out var oc2) && oc2 == 1)
        {
            anchor = Enum.Parse<Anchor>(tokens[end - 2].Value, ignoreCase: true);
            x = tx2;
            y = ty2;
            consumed = 2;
            return true;
        }

        // Try: ... <anchor> (no offset)
        if (end - start >= 1 && AnchorNames.Contains(tokens[end - 1].Value))
        {
            anchor = Enum.Parse<Anchor>(tokens[end - 1].Value, ignoreCase: true);
            consumed = 1;
            return true;
        }

        return false;
    }

    private static readonly HashSet<string> AnchorNames = new(
        Enum.GetNames<Anchor>().Select(n => n.ToLowerInvariant()),
        StringComparer.OrdinalIgnoreCase);

    private static bool TryParseAnchorAndOffset(Token[] tokens, int start, out Anchor anchor, out int x, out int y, out int consumed)
    {
        anchor = Anchor.Center;
        x = 0;
        y = 0;
        consumed = 0;

        if (start >= tokens.Length || !AnchorNames.Contains(tokens[start].Value))
            return false;

        anchor = Enum.Parse<Anchor>(tokens[start].Value, ignoreCase: true);
        consumed = 1;

        if (TryParseOffset(tokens, start + 1, out x, out y, out var offsetConsumed))
            consumed += offsetConsumed;

        return true;
    }

    private static bool TryParseOffset(Token[] tokens, int start, out int x, out int y, out int consumed)
    {
        x = 0;
        y = 0;
        consumed = 0;
        if (start >= tokens.Length) return false;

        // Single token: (x,y)
        if (tokens[start].Value.StartsWith('(') && tokens[start].Value.EndsWith(')'))
        {
            var inner = tokens[start].Value[1..^1];
            var parts = inner.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y))
            {
                consumed = 1;
                return true;
            }
        }

        // Two tokens: "(x," and "y)"
        if (start + 1 < tokens.Length
            && tokens[start].Value.StartsWith('(') && tokens[start].Value.EndsWith(',')
            && tokens[start + 1].Value.EndsWith(')'))
        {
            var xStr = tokens[start].Value[1..^1].Trim();
            var yStr = tokens[start + 1].Value[..^1].Trim();
            if (int.TryParse(xStr, out x) && int.TryParse(yStr, out y))
            {
                consumed = 2;
                return true;
            }
        }

        return false;
    }

    private static DragCommand ParseDrag(Token[] tokens)
    {
        // tokens[0] is "drag"
        if (tokens.Length < 3)
            throw new ArgumentException("Usage: drag [<anchor> (<x>, <y>)] to <locator> [<anchor> (<x>, <y>)]");

        int idx = 1;

        // Parse optional from-anchor/offset before "to"
        Anchor? fromAnchor = null;
        int fromX = 0, fromY = 0;

        if (TryParseAnchorAndOffset(tokens, idx, out var fa, out var fx, out var fy, out var fromConsumed))
        {
            fromAnchor = fa;
            fromX = fx;
            fromY = fy;
            idx += fromConsumed;
        }

        // Expect "to"
        if (idx >= tokens.Length || !tokens[idx].Value.Equals("to", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Usage: drag [<anchor> (<x>, <y>)] to <locator> [<anchor> (<x>, <y>)]");
        idx++;

        if (idx >= tokens.Length)
            throw new ArgumentException("Usage: drag [<anchor> (<x>, <y>)] to <locator> [<anchor> (<x>, <y>)]");

        // Remaining tokens: <locator> [<anchor> [(<x>, <y>)]]
        // Parse from end: check if last tokens are anchor+offset or just anchor
        Anchor? toAnchor = null;
        int toX = 0, toY = 0;
        int locatorEnd = tokens.Length;

        // Try trailing: ... <anchor> (<x>, <y>) — offset may be 1 or 2 tokens
        // Check anchor at locatorEnd-3 with 2-token offset, or locatorEnd-2 with 1-token offset
        if (locatorEnd - idx >= 4
            && AnchorNames.Contains(tokens[locatorEnd - 3].Value)
            && TryParseOffset(tokens, locatorEnd - 2, out var tx3, out var ty3, out var oc3) && oc3 == 2)
        {
            toAnchor = Enum.Parse<Anchor>(tokens[locatorEnd - 3].Value, ignoreCase: true);
            toX = tx3;
            toY = ty3;
            locatorEnd -= 3;
        }
        else if (locatorEnd - idx >= 3
            && AnchorNames.Contains(tokens[locatorEnd - 2].Value)
            && TryParseOffset(tokens, locatorEnd - 1, out var tx2, out var ty2, out var oc2) && oc2 == 1)
        {
            toAnchor = Enum.Parse<Anchor>(tokens[locatorEnd - 2].Value, ignoreCase: true);
            toX = tx2;
            toY = ty2;
            locatorEnd -= 2;
        }
        // Check for trailing anchor without offset: ... <anchor>
        else if (locatorEnd - idx >= 2 && AnchorNames.Contains(tokens[locatorEnd - 1].Value))
        {
            toAnchor = Enum.Parse<Anchor>(tokens[locatorEnd - 1].Value, ignoreCase: true);
            locatorEnd -= 1;
        }

        if (locatorEnd <= idx)
            throw new ArgumentException("Usage: drag [<anchor> (<x>, <y>)] to <locator> [<anchor> (<x>, <y>)]");

        var locator = string.Join(' ', tokens[idx..locatorEnd].Select(t => t.Value));
        return new DragCommand(fromAnchor, fromX, fromY, locator, toAnchor, toX, toY);
    }

    private static ClickCommand ParseClick(Token[] tokens)
    {
        var (ocrText, maxDist, matchIndex, anchor, offsetX, offsetY) = ParseMouseActionArgs("click", tokens);
        return new ClickCommand(ocrText, maxDist, matchIndex, anchor, offsetX, offsetY);
    }

    private static DoubleClickCommand ParseDoubleClick(Token[] tokens)
    {
        var (ocrText, maxDist, matchIndex, anchor, offsetX, offsetY) = ParseMouseActionArgs("doubleclick", tokens);
        return new DoubleClickCommand(ocrText, maxDist, matchIndex, anchor, offsetX, offsetY);
    }

    private static RightClickCommand ParseRightClick(Token[] tokens)
    {
        var (ocrText, maxDist, matchIndex, anchor, offsetX, offsetY) = ParseMouseActionArgs("rightclick", tokens);
        return new RightClickCommand(ocrText, maxDist, matchIndex, anchor, offsetX, offsetY);
    }

    private static HoverCommand ParseHover(Token[] tokens)
    {
        var (ocrText, maxDist, matchIndex, anchor, offsetX, offsetY) = ParseMouseActionArgs("hover", tokens);
        return new HoverCommand(ocrText, maxDist, matchIndex, anchor, offsetX, offsetY);
    }

    private static ScrollCommand ParseScroll(Token[] tokens)
    {
        if (tokens.Length < 2)
            throw new ArgumentException("Usage: scroll <lines> [horizontal]");
        if (!double.TryParse(tokens[1].Value, out var lines))
            throw new ArgumentException("lines must be a number.");
        var horizontal = tokens.Length >= 3 && tokens[2].Value.Equals("horizontal", StringComparison.OrdinalIgnoreCase);
        return new ScrollCommand(lines, horizontal);
    }

    private static AttributeCommand ParseAttribute(Token[] tokens)
    {
        if (tokens.Length < 2)
            throw new ArgumentException("Usage: attribute <attributeName>");
        return new AttributeCommand(tokens[1].Value);
    }

    private static SetAttributeCommand ParseSetAttribute(Token[] tokens)
    {
        if (tokens.Length < 3)
            throw new ArgumentException("Usage: setattribute <name> <value>");
        var value = string.Join(" ", tokens.Skip(2).Select(t => t.Value));
        return new SetAttributeCommand(tokens[1].Value, value);
    }

    private static HitKeysCommand ParseHitKeys(Token[] tokens) =>
        new(ParseGenericHitKeys("hitkeys", tokens));

    private static GlobalHitKeysCommand ParseGlobalHitKeys(Token[] tokens) =>
        new(ParseGenericHitKeys("ghitkeys", tokens));

    private static Key[] ParseGenericHitKeys(string commandName, Token[] tokens)
    {
        if (tokens.Length < 2)
            throw new ArgumentException($"No keys passed. Usage: {commandName} <space-separated-keys> [{{{string.Join(", ", Enum.GetNames<Key>().Select(k => k.ToLowerInvariant()))}}}]");

        try
        {
            return [.. tokens.Skip(1).Select(t => Enum.Parse<Key>(t.Value.ToUpperInvariant()))];
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"{ex.Message} Verify all key names are valid before sending. Usage: {commandName} <space-separated-keys> [{{{string.Join(", ", Enum.GetNames<Key>().Select(k => k.ToLowerInvariant()))}}}]", ex);
        }
    }

    private static TypeCommand ParseType(Token[] tokens)
    {
        var (text, modifiers) = ParseGenericType("type", tokens);
        return new TypeCommand(text, modifiers);
    }

    private static GlobalTypeCommand ParseGlobalType(Token[] tokens)
    {
        var (text, modifiers) = ParseGenericType("gtype", tokens);
        return new GlobalTypeCommand(text, modifiers);
    }

    private static (string Text, KeyModifiers Modifiers) ParseGenericType(string commandName, Token[] tokens)
    {
        if (tokens.Length < 2)
            throw new ArgumentException($"Usage: {commandName} <text> [ctrl alt shift meta]");

        var modifiers = KeyModifiers.None;
        var textTokens = tokens.Skip(1).ToArray();

        // If last token is a bracket group like "[ctrl alt]", parse modifiers from it
        if (textTokens.Length > 1 && textTokens[^1].Value.StartsWith('[') && textTokens[^1].Value.EndsWith(']'))
        {
            modifiers = ParseModifiers(textTokens[^1].Value);
            textTokens = textTokens[..^1];
        }

        var text = string.Join(' ', textTokens.Select(t => t.Value));
        return (text, modifiers);
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

    private static MatchIndexCommand ParseMatchIndex(Token[] tokens)
    {
        if (tokens.Length < 2 || !int.TryParse(tokens[1].Value, out var index))
            throw new ArgumentException("Usage: matchindex <N>");
        return new MatchIndexCommand(index);
    }

    private static NextMatchCommand ParseNextMatch(Token[] tokens)
    {
        if (tokens.Length >= 2)
        {
            if (!int.TryParse(tokens[1].Value, out var steps) || steps < 1)
                throw new ArgumentException("Usage: nextmatch [N] (N must be a positive integer)");
            return new NextMatchCommand(steps);
        }
        return new NextMatchCommand();
    }

    private static PrevMatchCommand ParsePrevMatch(Token[] tokens)
    {
        if (tokens.Length >= 2)
        {
            if (!int.TryParse(tokens[1].Value, out var steps) || steps < 1)
                throw new ArgumentException("Usage: prevmatch [N] (N must be a positive integer)");
            return new PrevMatchCommand(steps);
        }
        return new PrevMatchCommand();
    }

    private static WindowStateCommand ParseWindowState(Token[] tokens)
    {
        if (tokens.Length < 2)
            return new WindowStateCommand();
        if (Enum.TryParse<WcWindowState>(tokens[1].Value, ignoreCase: true, out var state))
            return new WindowStateCommand(state);
        var valid = string.Join(", ", Enum.GetValues<WcWindowState>().Select(s => s.ToString().ToLowerInvariant()));
        throw new ArgumentException($"Unknown window state: '{tokens[1].Value}'. Valid states: {valid}.");
    }

    private static SleepCommand ParseSleep(Token[] tokens)
    {
        if (tokens.Length < 2 || !int.TryParse(tokens[1].Value, out var ms) || ms <= 0)
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
        char quoteChar = '\0';
        int bracketDepth = 0;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (quoteChar != '\0')
            {
                current.Append(c);
                if (c == quoteChar) quoteChar = '\0';
            }
            else if (c is '"' or '\'')
            {
                current.Append(c);
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
    internal static string[] Tokenize(string input) =>
        TokenizeRich(input).Select(t => t.Value).ToArray();

    internal static Token[] TokenizeRich(string input)
    {
        var tokens = new List<Token>();
        var current = new System.Text.StringBuilder();
        bool wasQuoted = false;
        char quoteChar = '\0';

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (quoteChar != '\0')
            {
                if (c == quoteChar)
                {
                    quoteChar = '\0';
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c is '"' or '\'')
            {
                wasQuoted = true;
                quoteChar = c;
            }
            else if (c == '[')
            {
                current.Append(c);
                i++;
                char bracketQuoteChar = '\0';
                while (i < input.Length)
                {
                    char bc = input[i];
                    if (bracketQuoteChar != '\0')
                    {
                        current.Append(bc);
                        if (bc == bracketQuoteChar) bracketQuoteChar = '\0';
                    }
                    else if (bc is '"' or '\'')
                    {
                        current.Append(bc);
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
                    tokens.Add(new Token(current.ToString(), wasQuoted));
                    current.Clear();
                    wasQuoted = false;
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            tokens.Add(new Token(current.ToString(), wasQuoted));

        return tokens.ToArray();
    }
}
