using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

[TestFixture]
[Category("Unit")]
public class CommandHistoryTests
{
    private CommandHistory _history = null!;

    [SetUp]
    public void SetUp() => _history = new CommandHistory();

    // ── Entries ────────────────────────────────────────────────────────────

    [Test]
    public void Entries_Empty_ReturnsEmptyList()
    {
        Assert.That(_history.Entries, Is.Empty);
    }

    [Test]
    public void Entries_ReflectsAdded()
    {
        _history.Add("alpha");
        _history.Add("beta");
        Assert.That(_history.Entries, Is.EqualTo(new[] { "alpha", "beta" }));
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    [Test]
    public void Load_ReplacesExistingEntries()
    {
        _history.Add("old");
        _history.Load(["one", "two", "three"]);
        Assert.That(_history.Entries, Is.EqualTo(new[] { "one", "two", "three" }));
    }

    [Test]
    public void Load_ResetsCursor_NavigateUpReturnsMostRecent()
    {
        _history.Load(["first", "second"]);
        Assert.That(_history.NavigateUp(""), Is.EqualTo("second"));
    }

    [Test]
    public void Load_EmptyList_ClearsHistory()
    {
        _history.Add("existing");
        _history.Load([]);
        Assert.Multiple(() =>
        {
            Assert.That(_history.Count, Is.EqualTo(0));
            Assert.That(_history.NavigateUp(""), Is.Null);
        });
    }

    // ── Add ─────────────────────────────────────────────────────────────────

    [Test]
    public void Add_IncrementsCount()
    {
        _history.Add("connect ws://localhost/");
        Assert.That(_history.Count, Is.EqualTo(1));
    }

    [Test]
    public void Add_IgnoresEmptyOrWhitespace()
    {
        _history.Add("");
        _history.Add("   ");
        Assert.That(_history.Count, Is.EqualTo(0));
    }

    [Test]
    public void Add_SkipsConsecutiveDuplicates()
    {
        _history.Add("click");
        _history.Add("click");
        Assert.That(_history.Count, Is.EqualTo(1));
    }

    [Test]
    public void Add_AllowsNonConsecutiveDuplicates()
    {
        _history.Add("click");
        _history.Add("text");
        _history.Add("click");
        Assert.That(_history.Count, Is.EqualTo(3));
    }

    // ── NavigateUp ──────────────────────────────────────────────────────────

    [Test]
    public void NavigateUp_EmptyHistory_ReturnsNull()
    {
        Assert.That(_history.NavigateUp("typed"), Is.Null);
    }

    [Test]
    public void NavigateUp_ReturnsMostRecent()
    {
        _history.Add("first");
        _history.Add("second");
        Assert.That(_history.NavigateUp(""), Is.EqualTo("second"));
    }

    [Test]
    public void NavigateUp_Twice_ReturnsOlder()
    {
        _history.Add("first");
        _history.Add("second");
        _history.NavigateUp("");
        Assert.That(_history.NavigateUp(""), Is.EqualTo("first"));
    }

    [Test]
    public void NavigateUp_PastOldest_ReturnsNull()
    {
        _history.Add("only");
        _history.NavigateUp("");
        Assert.That(_history.NavigateUp(""), Is.Null);
    }

    [Test]
    public void NavigateUp_SavesCurrentInput()
    {
        _history.Add("click");
        _history.NavigateUp("partial");
        var restored = _history.NavigateDown();
        Assert.That(restored, Is.EqualTo("partial"));
    }

    // ── NavigateDown ────────────────────────────────────────────────────────

    [Test]
    public void NavigateDown_AtBottom_ReturnsNull()
    {
        _history.Add("click");
        Assert.That(_history.NavigateDown(), Is.Null);
    }

    [Test]
    public void NavigateDown_AfterUp_ReturnsNewer()
    {
        _history.Add("first");
        _history.Add("second");
        _history.NavigateUp("");
        _history.NavigateUp("");
        Assert.That(_history.NavigateDown(), Is.EqualTo("second"));
    }

    [Test]
    public void NavigateDown_PastNewest_RestoresSavedInput()
    {
        _history.Add("first");
        _history.Add("second");
        _history.NavigateUp("typing");
        _history.NavigateUp("typing");
        _history.NavigateDown(); // "second"
        _history.NavigateDown(); // restored "typing"
        Assert.That(_history.NavigateDown(), Is.Null); // already at bottom
    }

    [Test]
    public void NavigateDown_RestoresEmptyStringWhenNoSavedInput()
    {
        _history.Add("click");
        _history.NavigateUp("");
        Assert.That(_history.NavigateDown(), Is.EqualTo(""));
    }

    // ── Full navigation cycle ───────────────────────────────────────────────

    [Test]
    public void FullCycle_UpUpDownDown_WorksCorrectly()
    {
        _history.Add("alpha");
        _history.Add("beta");
        _history.Add("gamma");

        Assert.That(_history.NavigateUp("current"), Is.EqualTo("gamma"));
        Assert.That(_history.NavigateUp("current"), Is.EqualTo("beta"));
        Assert.That(_history.NavigateUp("current"), Is.EqualTo("alpha"));
        Assert.That(_history.NavigateUp("current"), Is.Null); // at top

        Assert.That(_history.NavigateDown(), Is.EqualTo("beta"));
        Assert.That(_history.NavigateDown(), Is.EqualTo("gamma"));
        Assert.That(_history.NavigateDown(), Is.EqualTo("current")); // restored
        Assert.That(_history.NavigateDown(), Is.Null); // at bottom
    }

    // ── ResetCursor ─────────────────────────────────────────────────────────

    [Test]
    public void ResetCursor_AfterNavigate_NextUpReturnsMostRecent()
    {
        _history.Add("first");
        _history.Add("second");
        _history.NavigateUp("");
        _history.NavigateUp("");

        _history.ResetCursor();

        Assert.That(_history.NavigateUp(""), Is.EqualTo("second"));
    }

    [Test]
    public void Add_ResetsCursor()
    {
        _history.Add("first");
        _history.NavigateUp("");
        _history.Add("second");
        Assert.That(_history.NavigateUp(""), Is.EqualTo("second"));
    }

    [Test]
    public void Add_DuplicateAfterNavigate_CursorNotReset()
    {
        // Reproduces the skip bug: run "click", press Up (cursor=0),
        // run "click" again (duplicate skipped, Add doesn't call ResetCursor),
        // cursor stays at 0, next Up returns null — appears to skip.
        _history.Add("click");
        _history.NavigateUp("");           // cursor = 0
        _history.Add("click");             // duplicate → skipped, cursor still 0

        // Without explicit ResetCursor after Add, this would return null
        // because cursor is at 0 (top). The caller must ResetCursor separately.
        Assert.That(_history.Count, Is.EqualTo(1));
        // cursor is NOT at Count — it's stale at 0
    }

    [Test]
    public void ResetCursor_AfterDuplicateAdd_FixesCursor()
    {
        _history.Add("click");
        _history.NavigateUp("");           // cursor = 0
        _history.Add("click");             // duplicate skipped
        _history.ResetCursor();            // explicit reset

        Assert.That(_history.NavigateUp(""), Is.EqualTo("click"));
    }

    // ── GetEntry ──────────────────────────────────────────────────────────

    [Test]
    public void GetEntry_ValidIndex_ReturnsEntry()
    {
        _history.Add("alpha");
        _history.Add("beta");
        Assert.That(_history.GetEntry(0), Is.EqualTo("alpha"));
        Assert.That(_history.GetEntry(1), Is.EqualTo("beta"));
    }

    [Test]
    public void GetEntry_OutOfRange_ReturnsNull()
    {
        _history.Add("only");
        Assert.That(_history.GetEntry(-1), Is.Null);
        Assert.That(_history.GetEntry(1), Is.Null);
    }

    [Test]
    public void GetEntry_EmptyHistory_ReturnsNull()
    {
        Assert.That(_history.GetEntry(0), Is.Null);
    }

    // ── FindBackward ──────────────────────────────────────────────────────

    [Test]
    public void FindBackward_FindsMostRecentMatch()
    {
        _history.Load(["connect ws://a", "click", "connect ws://b"]);
        Assert.That(_history.FindBackward("connect", 3), Is.EqualTo(2));
    }

    [Test]
    public void FindBackward_FindsOlderMatch()
    {
        _history.Load(["connect ws://a", "click", "connect ws://b"]);
        Assert.That(_history.FindBackward("connect", 2), Is.EqualTo(0));
    }

    [Test]
    public void FindBackward_NoMatch_ReturnsNegativeOne()
    {
        _history.Load(["click", "text"]);
        Assert.That(_history.FindBackward("missing", 2), Is.EqualTo(-1));
    }

    [Test]
    public void FindBackward_CaseInsensitive()
    {
        _history.Load(["Connect ws://a"]);
        Assert.That(_history.FindBackward("connect", 1), Is.EqualTo(0));
    }

    [Test]
    public void FindBackward_EmptySubstring_MatchesAny()
    {
        _history.Load(["alpha", "beta"]);
        Assert.That(_history.FindBackward("", 2), Is.EqualTo(1));
    }

    [Test]
    public void FindBackward_EmptyHistory_ReturnsNegativeOne()
    {
        Assert.That(_history.FindBackward("test", 0), Is.EqualTo(-1));
    }

    // ── FindForward ───────────────────────────────────────────────────────

    [Test]
    public void FindForward_FindsNextMatch()
    {
        _history.Load(["connect ws://a", "click", "connect ws://b"]);
        Assert.That(_history.FindForward("connect", 0), Is.EqualTo(2));
    }

    [Test]
    public void FindForward_NoMatch_ReturnsNegativeOne()
    {
        _history.Load(["click", "text"]);
        Assert.That(_history.FindForward("missing", -1), Is.EqualTo(-1));
    }

    [Test]
    public void FindForward_CaseInsensitive()
    {
        _history.Load(["alpha", "BETA"]);
        Assert.That(_history.FindForward("beta", -1), Is.EqualTo(1));
    }

    [Test]
    public void FindForward_FromStart_FindsFirst()
    {
        _history.Load(["match", "other"]);
        Assert.That(_history.FindForward("match", -1), Is.EqualTo(0));
    }

    // ── SetCursor ─────────────────────────────────────────────────────────

    [Test]
    public void SetCursor_SetsPosition_NavigateUpReturnsEntry()
    {
        _history.Load(["alpha", "beta", "gamma"]);
        _history.SetCursor(2);
        Assert.That(_history.NavigateUp(""), Is.EqualTo("beta"));
    }

    [Test]
    public void SetCursor_ClampsToRange()
    {
        _history.Load(["alpha"]);
        _history.SetCursor(100);
        Assert.That(_history.NavigateUp(""), Is.EqualTo("alpha"));
        _history.SetCursor(-5);
        Assert.That(_history.NavigateUp(""), Is.Null); // at 0, can't go further up
    }
}
