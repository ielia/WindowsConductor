namespace WindowsConductor.InspectorGUI;

/// <summary>
/// Bash-like command history navigable with Up/Down arrows.
/// </summary>
internal sealed class CommandHistory
{
    private readonly List<string> _entries = [];
    private int _cursor;
    private string? _savedInput;

    internal int Count => _entries.Count;

    internal void Add(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        // Avoid consecutive duplicates
        if (_entries.Count > 0 && _entries[^1] == command) return;
        _entries.Add(command);
        ResetCursor();
    }

    /// <summary>
    /// Moves up (older). On first call, saves the current input so it can be
    /// restored when the user navigates back down past the newest entry.
    /// Returns the history entry, or null if already at the oldest.
    /// </summary>
    internal string? NavigateUp(string currentInput)
    {
        if (_entries.Count == 0) return null;

        // First time navigating: save what the user was typing
        if (_cursor == _entries.Count)
            _savedInput = currentInput;

        if (_cursor <= 0) return null;

        _cursor--;
        return _entries[_cursor];
    }

    /// <summary>
    /// Moves down (newer). If past the newest entry, restores the saved input.
    /// Returns the history entry or saved input, or null if already at the bottom.
    /// </summary>
    internal string? NavigateDown()
    {
        if (_cursor >= _entries.Count) return null;

        _cursor++;

        if (_cursor == _entries.Count)
            return _savedInput ?? "";

        return _entries[_cursor];
    }

    internal void ResetCursor()
    {
        _cursor = _entries.Count;
        _savedInput = null;
    }
}
