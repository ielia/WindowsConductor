namespace WindowsConductor.InspectorGUI;

internal static class CommandCompleter
{
    internal static readonly string[] Commands = CommandHelp.AllCommandNames;

    /// <summary>
    /// Returns completions for the current input prefix.
    /// Only completes the first token (the command name).
    /// </summary>
    internal static string[] GetCompletions(string input)
    {
        if (string.IsNullOrEmpty(input))
            return Commands;

        // Only complete the command (first token). If there's already a space,
        // the user has moved past the command — no completions.
        if (input.Contains(' '))
            return [];

        var prefix = input.ToLowerInvariant();
        return Commands.Where(c => c.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    }

    /// <summary>
    /// Attempts tab completion on the input.
    /// Returns the result and whether a unique completion was applied.
    /// </summary>
    internal static TabResult Complete(string input)
    {
        var matches = GetCompletions(input);

        if (matches.Length == 0)
            return new TabResult(input, matches, false);

        if (matches.Length == 1)
            return new TabResult(matches[0] + " ", matches, true);

        // Multiple matches — find longest common prefix
        var lcp = LongestCommonPrefix(matches);
        bool extended = lcp.Length > input.Length;
        return new TabResult(extended ? lcp : input, matches, extended);
    }

    private static string LongestCommonPrefix(string[] values)
    {
        if (values.Length == 0) return "";
        var prefix = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            int j = 0;
            while (j < prefix.Length && j < values[i].Length && prefix[j] == values[i][j])
                j++;
            prefix = prefix[..j];
            if (prefix.Length == 0) break;
        }
        return prefix;
    }
}

internal sealed record TabResult(string Text, string[] Matches, bool Applied);
