using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

[TestFixture]
[Category("Unit")]
public class CommandHistoryTests
{
    private CommandHistory _history = null!;

    [SetUp]
    public void SetUp() => _history = new CommandHistory();

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
}
