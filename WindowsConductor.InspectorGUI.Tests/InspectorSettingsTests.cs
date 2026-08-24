using System.IO;
using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

[TestFixture]
[Category("Unit")]
public class InspectorSettingsTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wc-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── History ───────────────────────────────────────────────────────────────

    [Test]
    public void LoadHistory_MissingFile_ReturnsEmpty()
    {
        var entries = InspectorSettings.LoadHistory(_tempDir);
        Assert.That(entries, Is.Empty);
    }

    [Test]
    public void SaveAndLoadHistory_RoundTrips()
    {
        var original = new List<string> { "connect ws://localhost/", "locate //Button", "click" };
        InspectorSettings.SaveHistory(_tempDir, original);
        var loaded = InspectorSettings.LoadHistory(_tempDir);
        Assert.That(loaded, Is.EqualTo(original));
    }

    [Test]
    public void LoadHistory_SkipsBlankLines()
    {
        File.WriteAllLines(Path.Combine(_tempDir, "history"), ["click", "", "  ", "text"]);
        var loaded = InspectorSettings.LoadHistory(_tempDir);
        Assert.That(loaded, Is.EqualTo(new[] { "click", "text" }));
    }

    [Test]
    public void SaveHistory_CreatesDirectory()
    {
        var subDir = Path.Combine(_tempDir, "nested", "dir");
        InspectorSettings.SaveHistory(subDir, ["cmd1"]);
        Assert.That(File.Exists(Path.Combine(subDir, "history")), Is.True);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    [Test]
    public void LoadState_MissingFile_ReturnsDefaults()
    {
        var state = InspectorSettings.LoadState(_tempDir);
        Assert.Multiple(() =>
        {
            Assert.That(state.StopOnError, Is.False);
            Assert.That(state.AllowSelfSignedCerts, Is.True);
            Assert.That(state.PrunedLocate, Is.True);
            Assert.That(state.HighlightColor, Is.EqualTo(0));
            Assert.That(state.PinnedAttributes, Is.Empty);
            Assert.That(state.ClicklessMode, Is.False);
            Assert.That(state.WrapAttributes, Is.False);
            Assert.That(state.WindowLeft, Is.Null);
            Assert.That(state.WindowTop, Is.Null);
            Assert.That(state.WindowWidth, Is.EqualTo(1000));
            Assert.That(state.WindowHeight, Is.EqualTo(700));
            Assert.That(state.WindowState, Is.EqualTo("Normal"));
            Assert.That(state.OutputLogHeight, Is.EqualTo(120));
            Assert.That(state.AttributesPanelWidth, Is.EqualTo(320));
            Assert.That(state.SnapshotPanelWidth, Is.EqualTo(280));
        });
    }

    [Test]
    public void SaveAndLoadState_RoundTrips()
    {
        var original = new StateData
        {
            StopOnError = true,
            AllowSelfSignedCerts = false,
            PrunedLocate = false,
            HighlightColor = 2,
            PinnedAttributes = ["Name", "AutomationId"],
            ClicklessMode = true,
            WrapAttributes = true,
            WindowLeft = 100.5,
            WindowTop = 200.5,
            WindowWidth = 1200,
            WindowHeight = 800,
            WindowState = "Maximized",
            OutputLogHeight = 150,
            AttributesPanelWidth = 400,
            SnapshotPanelWidth = 350,
        };

        InspectorSettings.SaveState(_tempDir, original);
        var loaded = InspectorSettings.LoadState(_tempDir);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.StopOnError, Is.EqualTo(original.StopOnError));
            Assert.That(loaded.AllowSelfSignedCerts, Is.EqualTo(original.AllowSelfSignedCerts));
            Assert.That(loaded.PrunedLocate, Is.EqualTo(original.PrunedLocate));
            Assert.That(loaded.HighlightColor, Is.EqualTo(original.HighlightColor));
            Assert.That(loaded.PinnedAttributes, Is.EqualTo(original.PinnedAttributes));
            Assert.That(loaded.ClicklessMode, Is.EqualTo(original.ClicklessMode));
            Assert.That(loaded.WrapAttributes, Is.EqualTo(original.WrapAttributes));
            Assert.That(loaded.WindowLeft, Is.EqualTo(original.WindowLeft));
            Assert.That(loaded.WindowTop, Is.EqualTo(original.WindowTop));
            Assert.That(loaded.WindowWidth, Is.EqualTo(original.WindowWidth));
            Assert.That(loaded.WindowHeight, Is.EqualTo(original.WindowHeight));
            Assert.That(loaded.WindowState, Is.EqualTo(original.WindowState));
            Assert.That(loaded.OutputLogHeight, Is.EqualTo(original.OutputLogHeight));
            Assert.That(loaded.AttributesPanelWidth, Is.EqualTo(original.AttributesPanelWidth));
            Assert.That(loaded.SnapshotPanelWidth, Is.EqualTo(original.SnapshotPanelWidth));
        });
    }

    [Test]
    public void LoadState_CorruptJson_ReturnsDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "state"), "not json at all {{{");
        var state = InspectorSettings.LoadState(_tempDir);
        Assert.That(state.WindowWidth, Is.EqualTo(1000));
    }

    [Test]
    public void LoadState_PartialJson_FillsDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "state"), """{"stopOnError":true}""");
        var state = InspectorSettings.LoadState(_tempDir);
        Assert.Multiple(() =>
        {
            Assert.That(state.StopOnError, Is.True);
            Assert.That(state.AllowSelfSignedCerts, Is.True);
            Assert.That(state.WindowWidth, Is.EqualTo(1000));
        });
    }

    [Test]
    public void SaveState_CreatesDirectory()
    {
        var subDir = Path.Combine(_tempDir, "nested", "dir");
        InspectorSettings.SaveState(subDir, new StateData());
        Assert.That(File.Exists(Path.Combine(subDir, "state")), Is.True);
    }

    // ── ResetAll ──────────────────────────────────────────────────────────────

    [Test]
    public void ResetAll_DeletesBothFiles()
    {
        InspectorSettings.SaveHistory(_tempDir, ["cmd"]);
        InspectorSettings.SaveState(_tempDir, new StateData { StopOnError = true });

        InspectorSettings.ResetAll(_tempDir);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(_tempDir, "history")), Is.False);
            Assert.That(File.Exists(Path.Combine(_tempDir, "state")), Is.False);
        });
    }

    [Test]
    public void ResetAll_NoFiles_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => InspectorSettings.ResetAll(_tempDir));
    }

    [Test]
    public void ResetAll_OnlyHistoryExists_DeletesIt()
    {
        InspectorSettings.SaveHistory(_tempDir, ["cmd"]);
        InspectorSettings.ResetAll(_tempDir);
        Assert.That(File.Exists(Path.Combine(_tempDir, "history")), Is.False);
    }
}
