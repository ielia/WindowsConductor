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
    internal IReadOnlyList<string> Entries => _entries;

    internal void Load(IEnumerable<string> entries)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        ResetCursor();
    }

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

    internal void SetCursor(int index)
    {
        _cursor = Math.Clamp(index, 0, _entries.Count);
        _savedInput = null;
    }

    internal void ResetCursor()
    {
        _cursor = _entries.Count;
        _savedInput = null;
    }

    internal string? GetEntry(int index) =>
        index >= 0 && index < _entries.Count ? _entries[index] : null;

    internal int FindBackward(string substring, int fromIndex)
    {
        for (var i = Math.Min(fromIndex - 1, _entries.Count - 1); i >= 0; i--)
            if (_entries[i].Contains(substring, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    internal int FindForward(string substring, int fromIndex)
    {
        for (var i = Math.Max(fromIndex + 1, 0); i < _entries.Count; i++)
            if (_entries[i].Contains(substring, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }
}
