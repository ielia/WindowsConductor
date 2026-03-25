using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Manages launched applications and caches element references for a single client session.
/// Not thread-safe; each connected WebSocket client owns one instance.
/// </summary>
public sealed class AppManager : IDisposable
{
    private static readonly int DEFAULT_MAIN_WINDOW_TIMEOUT = 1500;

    private readonly UIA3Automation _automation = new();
    private readonly Dictionary<string, Application> _apps = new();
    private readonly Dictionary<string, AutomationElement> _elements = new();
    private readonly SelectorEngine _selector;
    private bool _disposed;

    public AppManager()
    {
        _selector = new SelectorEngine(new XPathEngine());
    }

    // ── Application lifecycle ───────────────────────────────────────────────

    /// <summary>
    /// Launches a Windows application and returns a session ID.
    /// For UWP apps launched via a stub (e.g. calc.exe on Win11), the stub
    /// redirects to the real process; we then search top-level windows by title.
    /// </summary>
    public string LaunchApp(string path, string[] args, string? detachedTitleRegex, int? mainWindowTimeout)
    {
        Application app;

        var psi = new ProcessStartInfo
        {
            FileName = path,
            Arguments = string.Join(" ", args),
            UseShellExecute = true
        };

        if (string.IsNullOrWhiteSpace(detachedTitleRegex))
        {
            app = Application.Launch(psi);
        }
        else
        {
            Process.Start(psi);
            Thread.Sleep(mainWindowTimeout ?? DEFAULT_MAIN_WINDOW_TIMEOUT); // Let Windows create the new UI window
            var automation = new UIA3Automation();
            var window = FindWindow(automation, detachedTitleRegex);
            automation.Dispose();
            app = Application.Attach(window.Properties.ProcessId.Value);
        }

        var id = NewId();
        _apps[id] = app;
        return id;
    }

    /// <summary>Closes the application and removes it from the session.</summary>
    public void CloseApp(string appId)
    {
        if (!_apps.TryGetValue(appId, out var app)) return;
        try { app.Close(); } catch { /* already closed */ }
        _apps.Remove(appId);
    }

    // ── Element discovery ───────────────────────────────────────────────────

    /// <summary>Returns the element ID of the first element matching <paramref name="selector"/>.</summary>
    public string FindElement(string appId, string selector)
    {
        var root = GetAppRoot(appId);
        var element = _selector.FindElement(root, selector)
            ?? throw new InvalidOperationException(
                $"No element found for selector '{selector}'.");

        return CacheElement(element);
    }

    /// <summary>Returns element IDs for all elements matching <paramref name="selector"/>.</summary>
    public string[] FindElements(string appId, string selector)
    {
        var root = GetAppRoot(appId);
        return _selector.FindElements(root, selector)
            .Select(CacheElement)
            .ToArray();
    }

    // ── Element actions ─────────────────────────────────────────────────────

    public void Click(string elementId) =>
        GetElement(elementId).Click();

    public void DoubleClick(string elementId) =>
        GetElement(elementId).DoubleClick();

    /// <summary>
    /// Focuses the element and types <paramref name="text"/> using keyboard simulation.
    /// For editable controls this appends to existing content; call SelectAll first
    /// or clear the field if you need to replace text.
    /// </summary>
    public void TypeText(string elementId, string text)
    {
        var el = GetElement(elementId);
        el.Focus();
        Keyboard.Type(text);
    }

    // ── Element queries ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns visible text for the element.
    /// Tries <c>TextBox.Text</c> first, then falls back to <c>Name</c>.
    /// </summary>
    public string GetText(string elementId)
    {
        var el = GetElement(elementId);
        var tb = el.AsTextBox();
        if (tb != null)
        {
            try { return tb.Text ?? el.Name ?? ""; }
            catch { /* pattern not supported */ }
        }
        return el.Name ?? "";
    }

    public string GetAttribute(string elementId, string attribute)
    {
        var el = GetElement(elementId);
        return attribute.ToLowerInvariant() switch
        {
            "automationid" => el.AutomationId ?? "",
            "name" => el.Name ?? "",
            "classname" => el.ClassName ?? "",
            "controltype" => el.ControlType.ToString(),
            "isenabled" => el.IsEnabled.ToString().ToLowerInvariant(),
            "isoffscreen" => el.IsOffscreen.ToString().ToLowerInvariant(),
            _ => throw new NotSupportedException($"Attribute not supported: '{attribute}'")
        };
    }

    public bool IsEnabled(string elementId) =>
        GetElement(elementId).IsEnabled;

    public bool IsVisible(string elementId) =>
        !GetElement(elementId).IsOffscreen;

    public void Focus(string elementId) =>
        GetElement(elementId).Focus();

    public string GetWindowTitle(string appId) =>
        GetAppRoot(appId).Name ?? "";

    public object GetBoundingRect(string elementId)
    {
        var r = GetElement(elementId).BoundingRectangle;
        return new { x = r.X, y = r.Y, width = r.Width, height = r.Height };
    }

    // ── Screenshots ──────────────────────────────────────────────────────────

    /// <summary>Captures an element and saves it as a PNG file. Returns the file path.</summary>
    public string ScreenshotElement(string elementId, string? path)
    {
        var el = GetElement(elementId);
        path = ResolvePath(path, ".png");
        el.CaptureToFile(path);
        return path;
    }

    /// <summary>Captures the app's main window and saves it as a PNG file. Returns the file path.</summary>
    public string ScreenshotApp(string appId, string? path)
    {
        var root = GetAppRoot(appId);
        path = ResolvePath(path, ".png");
        root.CaptureToFile(path);
        return path;
    }

    // ── Video recording ──────────────────────────────────────────────────────

    private readonly Dictionary<string, VideoRecorder> _recorders = new();

    /// <summary>Starts video recording of the app's main window. Returns the video file path.</summary>
    public string StartRecording(string appId, string? path, string? ffmpegPath)
    {
        if (_recorders.ContainsKey(appId))
            throw new InvalidOperationException($"Recording is already in progress for app '{appId}'.");

        var root = GetAppRoot(appId);
        path = ResolvePath(path, ".mp4");

        var settings = new VideoRecorderSettings
        {
            TargetVideoPath = path,
            FrameRate = 15,
            VideoFormat = VideoFormat.x264,
            VideoQuality = 23
        };
        if (!string.IsNullOrWhiteSpace(ffmpegPath))
            settings.ffmpegPath = ffmpegPath;

        var recorder = new VideoRecorder(settings, _ => FlaUI.Core.Capturing.Capture.Element(root));
        _recorders[appId] = recorder;
        return path;
    }

    /// <summary>Stops video recording for the app. Returns the video file path.</summary>
    public string StopRecording(string appId)
    {
        if (!_recorders.TryGetValue(appId, out var recorder))
            throw new InvalidOperationException($"No recording in progress for app '{appId}'.");

        recorder.Stop();
        var path = recorder.TargetVideoPath;
        _recorders.Remove(appId);
        return path;
    }

    // ── Internal helpers ────────────────────────────────────────────────────

    private static string ResolvePath(string? path, string defaultExtension)
    {
        if (!string.IsNullOrWhiteSpace(path)) return path;
        return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{defaultExtension}");
    }

    private Window FindWindow(UIA3Automation automation, string titleRegex, int retries = 20)
    {
        Window? newest = null;
        DateTime newestTime = DateTime.MinValue;
        for (int i = 0; i < retries; ++i)
        {
            var desktop = automation.GetDesktop();
            var wins = desktop
                .FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .Where(w => new Regex(titleRegex).IsMatch(w.Name));
            foreach (var w in wins)
            {
                var win = w.AsWindow();
                IntPtr hwnd = new IntPtr(win.Properties.NativeWindowHandle.Value);
                if (GetWindowTime(hwnd, out DateTime created))
                {
                    if (created > newestTime)
                    {
                        newestTime = created;
                        newest = win;
                    }
                }
            }
            if (newest != null) return newest;
            Thread.Sleep(100);
        }
        throw new Exception($"Could not find window '{titleRegex}'");
    }

    private bool GetWindowTime(IntPtr hwnd, out DateTime created)
    {
        created = DateTime.MinValue;
        try
        {
            int pid = GetWindowProcessId(hwnd);
            var p = Process.GetProcessById(pid);
            created = p.StartTime;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private int GetWindowProcessId(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        return (int)pid;
    }

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private AutomationElement GetAppRoot(string appId)
    {
        if (!_apps.TryGetValue(appId, out var app))
            throw new KeyNotFoundException($"App session '{appId}' not found.");

        // First attempt: standard GetMainWindow (works for classic Win32 apps)
        var window = app.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
        if (window != null) return window;

        // Fallback: UWP/packaged apps spawn in a different process, so the stub's
        // main window is null. Search all top-level windows owned by related processes.
        var allWindows = app.GetAllTopLevelWindows(_automation);
        return allWindows.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Could not obtain a window for app session '{appId}'.");
    }

    private AutomationElement GetElement(string elementId)
    {
        if (!_elements.TryGetValue(elementId, out var el))
            throw new KeyNotFoundException($"Element '{elementId}' not found in session cache.");
        return el;
    }

    private string CacheElement(AutomationElement el)
    {
        var id = NewId();
        _elements[id] = el;
        return id;
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var recorder in _recorders.Values)
        {
            try { recorder.Stop(); } catch { }
        }
        _recorders.Clear();

        foreach (var app in _apps.Values)
        {
            try { app.Close(); } catch { }
            try { app.Dispose(); } catch { }
        }

        _automation.Dispose();
    }
}
