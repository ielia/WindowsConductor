using NUnit.Framework;

namespace PlaywrightWinApp.Client.Tests;

/// <summary>
/// Integration tests for the Windows 11 Notepad application.
/// Runs against the FlaUI driver on port 8765.
/// If the driver is not running its fixture is automatically skipped.
///
/// ── Starting the driver ──────────────────────────────────────────────────────
///   dotnet run --project PlaywrightWinApp.DriverFlaUI   # ws://localhost:8765/
///
/// ── AutomationId quick-reference (Win 11 Notepad) ────────────────────────────
///   Menu bar items exposed by Name: "File", "Edit", "View", "Help"
/// ── Name quick-reference (Win 11 Notepad) ────────────────────────────────────
///   Text editor    Main text area (reports ControlType = Document)
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
[TestFixtureSource(nameof(DriverUris))]
[Category("Integration")]
public sealed class NotepadTests : WinAppTest
{
    // ── Driver endpoints ──────────────────────────────────────────────────────

    public static IEnumerable<TestFixtureData> DriverUris()
    {
        yield return new TestFixtureData("ws://localhost:8765/").SetArgDisplayNames("DriverFlaUI");
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    private readonly string _driverUri;

    public NotepadTests(string driverUri) => _driverUri = driverUri;

    protected override string DriverUri => _driverUri;
    protected override string AppPath => "explorer.exe";
    protected override string[]? AppArgs => ["shell:appsfolder\\Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"];
    protected override string? DetachedTitleRegex => "^Untitled - Notepad$";
    protected override uint? MainWindowTimeout => 1000;

    [SetUp]
    public async Task FocusEditor()
    {
        await App.GetByName("Text editor").ClickAsync();
        await Task.Delay(100);
    }

    // ── Tests using AutomationId ──────────────────────────────────────────────

    [Test]
    public async Task TypeText_AndReadBack_ByName()
    {
        const string text = "Hello, WinApp Driver!";
        var editor = App.GetByName("Text editor");
        await editor.TypeAsync(text);

        string content = await editor.GetTextAsync();
        Assert.That(content, Does.Contain(text),
            $"Editor should contain typed text.  Got: '{content}'");
    }

    [Test]
    public async Task Editor_IsEnabled_ByName()
    {
        bool enabled = await App.GetByName("Text editor").IsEnabledAsync();
        Assert.That(enabled, Is.True, "Text editor should be enabled.");
    }

    [Test]
    public async Task Editor_IsVisible_ByName()
    {
        bool visible = await App.GetByName("Text editor").IsVisibleAsync();
        Assert.That(visible, Is.True, "Text editor should be visible.");
    }

    [Test]
    public async Task WindowTitle_ContainsNotepad()
    {
        string title = await App.GetTitleAsync();
        Assert.That(title, Does.Contain("Notepad"),
            $"Window title should contain 'Notepad'.  Got: '{title}'");
    }

    [Test]
    public async Task EditorBoundingRect_IsReasonable()
    {
        var rect = await App.GetByName("Text editor").GetBoundingRectAsync();
        Assert.Multiple(() =>
        {
            Assert.That(rect.Width,  Is.GreaterThan(200), "Editor width > 200 px.");
            Assert.That(rect.Height, Is.GreaterThan(100), "Editor height > 100 px.");
        });
    }

    [Test]
    public async Task GetAttribute_Name_RoundTrips()
    {
        string id = await App.GetByName("Text editor")
            .GetAttributeAsync("Name");
        Assert.That(id, Is.EqualTo("Text editor").IgnoreCase);
    }

    // ── Tests using Name / Text ───────────────────────────────────────────────

    [Test]
    public async Task FindFileMenu_ByText()
    {
        bool enabled = await App.GetByText("File").IsEnabledAsync();
        Assert.That(enabled, Is.True, "File menu item should be enabled.");
    }

    [Test]
    public async Task FindEditMenu_IsVisible_ByText()
    {
        bool visible = await App.GetByText("Edit").IsVisibleAsync();
        Assert.That(visible, Is.True, "Edit menu item should be visible.");
    }

    // ── Tests using XPath ─────────────────────────────────────────────────────

    [Test]
    public async Task TypeText_AndReadBack_ByXPath()
    {
        const string text = "XPath test — line one.";
        var editor = App.GetByXPath("//*[@Name='Text editor']");
        await editor.TypeAsync(text);

        string content = await App.GetByName("Text editor").GetTextAsync();
        Assert.That(content, Does.Contain(text),
            $"Editor should contain typed text.  Got: '{content}'");
    }

    [Test]
    public async Task FindEditorAsDocument_ByXPath()
    {
        bool visible = await App.GetByXPath("//Document").IsVisibleAsync();
        Assert.That(visible, Is.True,
            "A Document control (the editor) should be visible in Notepad.");
    }

    [Test]
    public async Task FindWindowByName_ByXPath()
    {
        bool visible = await App
            .GetByXPath("//Window[@Name='Notepad']")
            .IsVisibleAsync();
        Assert.That(visible, Is.True);
    }

    [Test]
    public async Task FindMenuItemsByXPath_DoesNotThrow()
    {
        var items = await App.GetByXPath("//MenuItem").GetAllElementsAsync();
        Assert.That(items, Is.Not.Null);
    }

    [Test]
    public async Task FindAllButtons_AtLeastWindowControls_ByXPath()
    {
        var buttons = await App.GetByXPath("//Button").GetAllElementsAsync();
        Assert.That(buttons.Count, Is.GreaterThanOrEqualTo(3),
            "Notepad should expose at least 3 buttons (Minimize, Maximize, Close).");
    }

    // ── Compound selector ─────────────────────────────────────────────────────

    [Test]
    public async Task CompoundSelector_AutomationIdAndControlType()
    {
        bool visible = await App
            .Locator("[name='Text editor']&&type=Document")
            .IsVisibleAsync();
        Assert.That(visible, Is.True,
            "Compound selector [name='Text editor']&&type=Document should resolve.");
    }
}