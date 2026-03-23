using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace PlaywrightWinApp.Client;

/// <summary>
/// Base class for WinApp integration tests. Manages the driver connection,
/// application lifecycle, and optional artifacts (failure screenshots, video recording).
///
/// Subclasses must implement <see cref="AppPath"/> and optionally override
/// configuration properties.  Use <see cref="App"/> to interact with the
/// launched application.
///
/// Provides:
/// <list type="bullet">
///   <item>Automatic driver connection &amp; app launch in <c>[OneTimeSetUp]</c></item>
///   <item>Graceful teardown in <c>[OneTimeTearDown]</c></item>
///   <item>Automatic screenshot on test failure</item>
///   <item>Optional per-test video recording (set <see cref="RecordVideo"/> to <c>true</c>)</item>
///   <item>NUnit <c>[Retry(N)]</c> and <c>[Parallelizable]</c> support out of the box</item>
/// </list>
/// </summary>
public abstract class WinAppTest
{
    // ── Configuration (override in subclasses) ───────────────────────────────

    /// <summary>Driver WebSocket URI.</summary>
    protected virtual string DriverUri => "ws://localhost:8765/";

    /// <summary>Path to the application executable.</summary>
    protected abstract string AppPath { get; }

    /// <summary>Optional command-line arguments.</summary>
    protected virtual string[]? AppArgs => null;

    /// <summary>
    /// If set, the driver will search for a top-level window whose title
    /// matches this regex instead of using the launched process's main window.
    /// Required for UWP/packaged apps.
    /// </summary>
    protected virtual string? DetachedTitleRegex => null;

    /// <summary>Milliseconds to wait for the main window to appear.</summary>
    protected virtual uint? MainWindowTimeout => null;

    /// <summary>
    /// When <c>true</c>, each test is video-recorded.
    /// Videos are kept only for failed tests; passing tests' videos are deleted.
    /// </summary>
    protected virtual bool RecordVideo => false;

    /// <summary>Optional path to ffmpeg. If null, ffmpeg must be in PATH.</summary>
    protected virtual string? FfmpegPath => null;

    // ── Test-accessible state ────────────────────────────────────────────────

    /// <summary>The active driver connection.</summary>
    protected WinAppConnection Connection { get; private set; } = null!;

    /// <summary>The launched application handle.</summary>
    protected WinAppApp App { get; private set; } = null!;

    /// <summary>
    /// Directory where screenshots and videos are saved.
    /// Created automatically in <c>[OneTimeSetUp]</c>.
    /// </summary>
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
            Connection = await WinAppConnection.ConnectAsync(DriverUri);
        }
        catch (Exception ex)
        {
            Assert.Ignore($"Driver at {DriverUri} is not available — skipping fixture. ({ex.Message})");
            return;
        }

        App = await Connection.LaunchAsync(AppPath, AppArgs, DetachedTitleRegex, MainWindowTimeout);
    }

    [OneTimeTearDown]
    public async Task WinAppOneTimeTearDown()
    {
        if (App is not null) await App.DisposeAsync();
        if (Connection is not null) await Connection.DisposeAsync();
    }

    [SetUp]
    public async Task WinAppSetUp()
    {
        if (RecordVideo && App is not null)
        {
            var testName = SanitizeFileName(TestContext.CurrentContext.Test.Name);
            var videoPath = Path.Combine(ArtifactsDir, $"{testName}.mp4");
            _currentVideoPath = await App.StartRecordingAsync(videoPath, FfmpegPath);
        }
    }

    [TearDown]
    public async Task WinAppTearDown()
    {
        var outcome = TestContext.CurrentContext.Result.Outcome.Status;

        // Stop video recording
        if (_currentVideoPath is not null && App is not null)
        {
            try
            {
                await App.StopRecordingAsync();
            }
            catch { /* recording may have already stopped */ }

            // Delete video for passing tests to save disk space
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

        // Auto-screenshot on failure
        if (outcome == TestStatus.Failed && App is not null)
        {
            try
            {
                var testName = SanitizeFileName(TestContext.CurrentContext.Test.Name);
                var screenshotPath = Path.Combine(ArtifactsDir, $"{testName}_FAILED.png");
                await App.ScreenshotAsync(screenshotPath);
                TestContext.AddTestAttachment(screenshotPath, "Failure screenshot");
            }
            catch { /* best effort */ }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string SanitizeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}
