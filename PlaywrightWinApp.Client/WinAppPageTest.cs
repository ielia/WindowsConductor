using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace PlaywrightWinApp.Client;

/// <summary>
/// Base class for tests that need both Playwright browser automation (<see cref="PageTest.Page"/>)
/// and WinApp desktop automation (<see cref="WinApp"/>).
///
/// Inherits from Playwright's <see cref="PageTest"/>, so you get a fresh <c>Page</c>
/// for each test. On top of that it manages a <see cref="WinAppConnection"/> and
/// <see cref="WinAppApp"/> with automatic teardown, failure screenshots, and
/// optional video recording — the same lifecycle as <see cref="WinAppTest"/>.
/// </summary>
public abstract class WinAppPageTest : PageTest
{
    // ── WinApp configuration (override in subclasses) ────────────────────────

    /// <summary>Driver WebSocket URI.</summary>
    protected virtual string WinAppDriverUri => "ws://localhost:8765/";

    /// <summary>Path to the desktop application executable.</summary>
    protected abstract string AppPath { get; }

    /// <summary>Optional command-line arguments for the desktop app.</summary>
    protected virtual string[]? AppArgs => null;

    /// <summary>Title regex for UWP/packaged app window discovery.</summary>
    protected virtual string? DetachedTitleRegex => null;

    /// <summary>Milliseconds to wait for the main window to appear.</summary>
    protected virtual uint? MainWindowTimeout => null;

    /// <summary>When <c>true</c>, each test is video-recorded (kept only on failure).</summary>
    protected virtual bool RecordVideo => false;

    /// <summary>Optional path to ffmpeg.</summary>
    protected virtual string? FfmpegPath => null;

    // ── Test-accessible state ────────────────────────────────────────────────

    /// <summary>The active WinApp driver connection.</summary>
    protected WinAppConnection WinAppConnection { get; private set; } = null!;

    /// <summary>The launched desktop application handle.</summary>
    protected WinAppApp WinApp { get; private set; } = null!;

    /// <summary>Directory for screenshots and videos.</summary>
    protected string ArtifactsDir { get; private set; } = "";

    // ── Private state ────────────────────────────────────────────────────────

    private string? _currentVideoPath;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [OneTimeSetUp]
    public async Task WinAppOneTimeSetUp()
    {
        ArtifactsDir = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "test-artifacts",
            GetType().Name);
        Directory.CreateDirectory(ArtifactsDir);

        try
        {
            WinAppConnection = await Client.WinAppConnection.ConnectAsync(WinAppDriverUri);
        }
        catch (Exception ex)
        {
            Assert.Ignore($"WinApp driver at {WinAppDriverUri} is not available — skipping. ({ex.Message})");
            return;
        }

        WinApp = await WinAppConnection.LaunchAsync(AppPath, AppArgs, DetachedTitleRegex, MainWindowTimeout);
    }

    [OneTimeTearDown]
    public async Task WinAppOneTimeTearDown()
    {
        if (WinApp is not null) await WinApp.DisposeAsync();
        if (WinAppConnection is not null) await WinAppConnection.DisposeAsync();
    }

    [SetUp]
    public async Task WinAppSetUp()
    {
        if (RecordVideo && WinApp is not null)
        {
            var testName = SanitizeFileName(TestContext.CurrentContext.Test.Name);
            var videoPath = Path.Combine(ArtifactsDir, $"{testName}.mp4");
            _currentVideoPath = await WinApp.StartRecordingAsync(videoPath, FfmpegPath);
        }
    }

    [TearDown]
    public async Task WinAppTearDown()
    {
        var outcome = TestContext.CurrentContext.Result.Outcome.Status;

        if (_currentVideoPath is not null && WinApp is not null)
        {
            try { await WinApp.StopRecordingAsync(); } catch { }

            if (outcome == TestStatus.Passed)
            {
                try { File.Delete(_currentVideoPath); } catch { }
            }
            else
            {
                TestContext.AddTestAttachment(_currentVideoPath, "Test video recording");
            }

            _currentVideoPath = null;
        }

        if (outcome == TestStatus.Failed && WinApp is not null)
        {
            try
            {
                var testName = SanitizeFileName(TestContext.CurrentContext.Test.Name);
                var screenshotPath = Path.Combine(ArtifactsDir, $"{testName}_FAILED.png");
                await WinApp.ScreenshotAsync(screenshotPath);
                TestContext.AddTestAttachment(screenshotPath, "Failure screenshot");
            }
            catch { }
        }
    }

    private static string SanitizeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}
