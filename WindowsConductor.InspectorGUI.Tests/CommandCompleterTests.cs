using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

[TestFixture]
[Category("Unit")]
public class CommandCompleterTests
{
    // ── GetCompletions ──────────────────────────────────────────────────────

    [Test]
    public void GetCompletions_Empty_ReturnsAllCommands()
    {
        var matches = CommandCompleter.GetCompletions("");
        Assert.That(matches, Is.EqualTo(CommandCompleter.Commands));
    }

    [Test]
    public void GetCompletions_UniquePrefix_ReturnsSingleMatch()
    {
        var matches = CommandCompleter.GetCompletions("con");
        Assert.That(matches, Is.EqualTo(new[] { "connect" }));
    }

    [Test]
    public void GetCompletions_AmbiguousPrefix_ReturnsMultipleMatches()
    {
        var matches = CommandCompleter.GetCompletions("cl");
        Assert.That(matches, Is.EqualTo(new[] { "clear", "click", "close" }));
    }

    [Test]
    public void GetCompletions_NoMatch_ReturnsEmpty()
    {
        var matches = CommandCompleter.GetCompletions("xyz");
        Assert.That(matches, Is.Empty);
    }

    [Test]
    public void GetCompletions_CaseInsensitive()
    {
        var matches = CommandCompleter.GetCompletions("CON");
        Assert.That(matches, Is.EqualTo(new[] { "connect" }));
    }

    [Test]
    public void GetCompletions_InputWithSpace_ReturnsEmpty()
    {
        var matches = CommandCompleter.GetCompletions("connect ws://");
        Assert.That(matches, Is.Empty);
    }

    [Test]
    public void GetCompletions_FullCommand_ReturnsSelf()
    {
        var matches = CommandCompleter.GetCompletions("click");
        Assert.That(matches, Is.EqualTo(new[] { "click" }));
    }

    [Test]
    public void GetCompletions_AttPrefix_ReturnsBothAttachAndAttribute()
    {
        var matches = CommandCompleter.GetCompletions("att");
        Assert.That(matches, Is.EqualTo(new[] { "attach", "attribute" }));
    }

    // ── Complete ────────────────────────────────────────────────────────────

    [Test]
    public void Complete_UniqueMatch_AppendsSpace()
    {
        var result = CommandCompleter.Complete("con");
        Assert.That(result.Text, Is.EqualTo("connect "));
        Assert.That(result.Applied, Is.True);
        Assert.That(result.Matches, Is.EqualTo(new[] { "connect" }));
    }

    [Test]
    public void Complete_AmbiguousPrefix_ExtendsToLCP()
    {
        // "cl" -> "clear", "click", "close" -> LCP = "cl" (no extension)
        var result = CommandCompleter.Complete("cl");
        Assert.That(result.Text, Is.EqualTo("cl"));
        Assert.That(result.Applied, Is.False);
        Assert.That(result.Matches, Has.Length.EqualTo(3));
    }

    [Test]
    public void Complete_AmbiguousPrefix_ExtendsWhenPossible()
    {
        // "att" -> "attach", "attribute" -> LCP = "att" (no extension beyond input)
        var result = CommandCompleter.Complete("at");
        Assert.That(result.Text, Is.EqualTo("att"));
        Assert.That(result.Applied, Is.True);
        Assert.That(result.Matches, Has.Length.EqualTo(2));
    }

    [Test]
    public void Complete_NoMatch_ReturnsInputUnchanged()
    {
        var result = CommandCompleter.Complete("xyz");
        Assert.That(result.Text, Is.EqualTo("xyz"));
        Assert.That(result.Applied, Is.False);
        Assert.That(result.Matches, Is.Empty);
    }

    [Test]
    public void Complete_EmptyInput_ReturnsAllCommands_NotApplied()
    {
        // Many matches, LCP = "" which is not longer than input ""
        var result = CommandCompleter.Complete("");
        Assert.That(result.Applied, Is.False);
        Assert.That(result.Matches, Is.EqualTo(CommandCompleter.Commands));
    }

    [Test]
    public void Complete_InputWithSpace_ReturnsUnchanged()
    {
        var result = CommandCompleter.Complete("connect ws://");
        Assert.That(result.Text, Is.EqualTo("connect ws://"));
        Assert.That(result.Applied, Is.False);
        Assert.That(result.Matches, Is.Empty);
    }

    [Test]
    public void Complete_FullCommandTyped_AppendsSpace()
    {
        var result = CommandCompleter.Complete("click");
        Assert.That(result.Text, Is.EqualTo("click "));
        Assert.That(result.Applied, Is.True);
    }

    [Test]
    public void Complete_Screenshot_UniqueMatch()
    {
        var result = CommandCompleter.Complete("sc");
        Assert.That(result.Text, Is.EqualTo("screenshot "));
        Assert.That(result.Applied, Is.True);
    }

    // ── TabResult record ────────────────────────────────────────────────────

    [Test]
    public void TabResult_StoresValues()
    {
        var result = new TabResult("text ", new[] { "text" }, true);
        Assert.That(result.Text, Is.EqualTo("text "));
        Assert.That(result.Matches, Is.EqualTo(new[] { "text" }));
        Assert.That(result.Applied, Is.True);
    }

    // ── Commands list is sorted ─────────────────────────────────────────────

    [Test]
    public void Commands_AreSortedAlphabetically()
    {
        var sorted = CommandCompleter.Commands.OrderBy(c => c).ToArray();
        Assert.That(CommandCompleter.Commands, Is.EqualTo(sorted));
    }

    [Test]
    public void Commands_ContainsAll21Commands()
    {
        Assert.That(CommandCompleter.Commands, Has.Length.EqualTo(21));
    }
}
