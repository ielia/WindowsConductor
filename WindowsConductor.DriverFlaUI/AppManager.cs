using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Manages launched applications and caches element references for a single client session.
/// Not thread-safe; each connected WebSocket client owns one instance.
/// </summary>
public sealed class AppManager : IAppOperations, IDisposable
{
    private const int DEFAULT_MAIN_WINDOW_TIMEOUT = 1500;

    private readonly UIA3Automation _automation = new();
    private readonly Dictionary<string, Application> _apps = new();
    private readonly HashSet<string> _attachedApps = new();
    private readonly Dictionary<string, int> _appProcessIds = new();
    private readonly Dictionary<string, AutomationElement> _elements = new();

    private readonly bool _confineToApp;
    private readonly string? _ffmpegPath;
    private bool _disposed;

    public AppManager(bool confineToApp = false, string? ffmpegPath = null)
    {
        _confineToApp = confineToApp;
        _ffmpegPath = ffmpegPath;

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
        if (_confineToApp) _appProcessIds[id] = app.ProcessId;
        return id;
    }

    /// <summary>
    /// Attaches to an already-running application by matching its main window title.
    /// Returns a session ID for the attached application.
    /// </summary>
    public string AttachApp(string mainWindowTitleRegex, int? mainWindowTimeout)
    {
        if (mainWindowTimeout is > 0)
            Thread.Sleep(mainWindowTimeout.Value);

        var automation = new UIA3Automation();
        var window = FindWindow(automation, mainWindowTitleRegex);
        automation.Dispose();

        var app = Application.Attach(window.Properties.ProcessId.Value);
        var id = NewId();
        _apps[id] = app;
        _attachedApps.Add(id);
        if (_confineToApp) _appProcessIds[id] = app.ProcessId;
        return id;
    }

    /// <summary>Closes the application and removes it from the session.</summary>
    public void CloseApp(string appId)
    {
        if (!_apps.TryGetValue(appId, out var app)) return;
        if (!_attachedApps.Contains(appId))
            try { app.Close(); } catch { /* already closed */ }
        _apps.Remove(appId);
        _attachedApps.Remove(appId);
        _appProcessIds.Remove(appId);
    }

    // ── Element discovery ───────────────────────────────────────────────────

    /// <summary>Returns the element ID of the first element matching <paramref name="selector"/>.</summary>
    public string FindElement(string appId, string selector, string? rootElementId = null)
    {
        var root = rootElementId != null ? GetElement(rootElementId) : GetAppRoot(appId);
        var element = SelectorEngine.FindElement(root, selector, GetDesktopRoot(), GetConfineProcessId(appId))
            ?? throw new InvalidOperationException(
                $"No element found for selector '{selector}'.");

        return CacheElement(element);
    }

    /// <summary>Returns element IDs for all elements matching <paramref name="selector"/>.</summary>
    public string[] FindElements(string appId, string selector, string? rootElementId = null)
    {
        var root = rootElementId != null ? GetElement(rootElementId) : GetAppRoot(appId);
        return SelectorEngine.FindElements(root, selector, GetDesktopRoot(), GetConfineProcessId(appId))
            .Select(CacheElement)
            .ToArray();
    }

    public object[] ResolveAttrs(string appId, string selector, string? rootElementId = null)
    {
        var root = rootElementId != null ? GetElement(rootElementId) : GetAppRoot(appId);
        var result = SelectorEngine.FindFull(root, selector, GetDesktopRoot(), GetConfineProcessId(appId));
        if (result is not AttrsResult ar)
            return [];
        return ar.Attributes.Select(a => (object)new
        {
            elementId = CacheElement(a.Element),
            name = a.Name,
            value = a.Value
        }).ToArray();
    }

    public string[] FindElementsAtPoint(string appId, double x, double y, string? rootElementId = null)
    {
        var root = rootElementId != null ? GetElement(rootElementId) : GetAppRoot(appId);
        var point = new System.Drawing.Point((int)x, (int)y);
        return root.FindAllDescendants()
            .Where(el => el.BoundingRectangle.Contains(point))
            .Select(CacheElement)
            .ToArray();
    }

    public string FindFrontElementAtPoint(string appId, double x, double y, string? rootElementId = null)
    {
        var root = rootElementId != null ? GetElement(rootElementId) : GetAppRoot(appId);
        var point = new System.Drawing.Point((int)x, (int)y);
        var candidates = root.FindAllDescendants()
            .Where(el => el.BoundingRectangle.Contains(point))
            .ToList();
        var element = ElementFilter.Frontmost(candidates).FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No element found at point ({x}, {y}).");
        return CacheElement(element);
    }

    // ── Wait operations ──────────────────────────────────────────────────────

    public string WaitForElement(string appId, string selector, string? rootElementId, uint timeout)
    {
        var deadline = Environment.TickCount64 + timeout;
        var desktopRoot = GetDesktopRoot();
        var processId = GetConfineProcessId(appId);
        while (true)
        {
            var root = rootElementId != null ? GetElement(rootElementId) : GetAppRoot(appId);
            var element = SelectorEngine.FindElement(root, selector, desktopRoot, processId);
            if (element is not null)
                return CacheElement(element);
            if (Environment.TickCount64 >= deadline)
                throw new NoMatchException(
                    $"No element found for selector '{selector}' within {timeout}ms.");
            Thread.Sleep(100);
        }
    }

    public string[] WaitForElements(string appId, string selector, string? rootElementId, uint timeout)
    {
        var deadline = Environment.TickCount64 + timeout;
        var desktopRoot = GetDesktopRoot();
        var processId = GetConfineProcessId(appId);
        while (true)
        {
            var root = rootElementId != null ? GetElement(rootElementId) : GetAppRoot(appId);
            var results = SelectorEngine.FindElements(root, selector, desktopRoot, processId);
            if (results.Length > 0)
                return results.Select(CacheElement).ToArray();
            if (Environment.TickCount64 >= deadline)
                throw new NoMatchException(
                    $"No elements found for selector '{selector}' within {timeout}ms.");
            Thread.Sleep(100);
        }
    }

    public object[] WaitForResolvedAttrs(string appId, string selector, string? rootElementId, uint timeout)
    {
        var deadline = Environment.TickCount64 + timeout;
        while (true)
        {
            var results = ResolveAttrs(appId, selector, rootElementId);
            if (results.Length > 0)
                return results;
            if (Environment.TickCount64 >= deadline)
                throw new NoMatchException(
                    $"No attribute results found for selector '{selector}' within {timeout}ms.");
            Thread.Sleep(100);
        }
    }

    public void WaitForVanish(string appId, string selector, string? rootElementId, uint timeout)
    {
        var deadline = Environment.TickCount64 + timeout;
        var desktopRoot = GetDesktopRoot();
        var processId = GetConfineProcessId(appId);
        while (true)
        {
            var root = rootElementId != null ? GetElement(rootElementId) : GetAppRoot(appId);
            var result = SelectorEngine.FindFull(root, selector, desktopRoot, processId);
            var hasMatches = result is ElementsResult er ? er.Elements.Count > 0
                : result is AttrsResult ar && ar.Attributes.Count > 0;
            if (!hasMatches)
                return;
            if (Environment.TickCount64 >= deadline)
                throw new UnwantedMatchException(
                    $"Selector '{selector}' still has matches after {timeout}ms.");
            Thread.Sleep(100);
        }
    }

    // ── Element actions ─────────────────────────────────────────────────────

    public void Click(string elementId) =>
        GetElement(elementId).Click();

    public void DoubleClick(string elementId) =>
        GetElement(elementId).DoubleClick();

    public void RightClick(string elementId) =>
        GetElement(elementId).RightClick();

    /// <summary>
    /// Focuses the element and types <paramref name="text"/> using keyboard simulation.
    /// For editable controls this appends to existing content; call SelectAll first
    /// or clear the field if you need to replace text.
    /// </summary>
    public void TypeText(string elementId, string text, int modifiers = 0)
    {
        var el = GetElement(elementId);
        el.Focus();

        var keys = (KeyModifiers)modifiers;
        var held = new List<VirtualKeyShort>();
        if (keys.HasFlag(KeyModifiers.Ctrl)) held.Add(VirtualKeyShort.CONTROL);
        if (keys.HasFlag(KeyModifiers.Alt)) held.Add(VirtualKeyShort.ALT);
        if (keys.HasFlag(KeyModifiers.Shift)) held.Add(VirtualKeyShort.SHIFT);
        if (keys.HasFlag(KeyModifiers.Meta)) held.Add(VirtualKeyShort.LWIN);

        foreach (var k in held) Keyboard.Press(k);
        Keyboard.Type(text);
        foreach (var k in held) Keyboard.Release(k);
    }

    // ── Element queries ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns visible text for the element.
    /// Returns <c>TextBox.Text</c> for the element, or empty string if not a text box.
    /// </summary>
    public string GetText(string elementId)
    {
        var el = GetElement(elementId);
        try
        {
            var tb = el.AsTextBox();
            return tb?.Text ?? "";
        }
        catch
        {
            return "";
        }
    }

    public string GetAttribute(string elementId, string attribute)
    {
        var el = GetElement(elementId);
        return ElementProperties.Resolve(el, attribute)
            ?? throw new NotSupportedException($"Attribute not supported: '{attribute}'");
    }

    public Dictionary<string, object?> GetAttributes(string elementId) =>
        ElementProperties.ResolveAll(GetElement(elementId));

    public string? GetParent(string elementId)
    {
        var el = GetElement(elementId);
        var parent = el.Parent;
        if (parent is null)
            return null;

        if (_confineToApp)
        {
            var parentPid = parent.Properties.ProcessId.ValueOrDefault;
            if (!_appProcessIds.ContainsValue(parentPid))
                throw new AccessRestrictedException(
                    "Parent element belongs to a different process (--confine-to-app is active).");
        }

        return CacheElement(parent);
    }

    public string? GetTopLevelWindow(string elementId)
    {
        var el = GetElement(elementId);
        var current = el;
        var parent = SafeGetParent(current);
        while (parent is not null)
        {
            var grandparent = SafeGetParent(parent);
            if (grandparent is null)
                break;
            current = parent;
            parent = grandparent;
        }
        if (parent is null)
            return null;
        return CacheElement(current);
    }

    private static AutomationElement? SafeGetParent(AutomationElement el)
    {
        try { return el.Parent; }
        catch { return null; }
    }

    public bool IsEnabled(string elementId) =>
        GetElement(elementId).IsEnabled;

    public bool IsVisible(string elementId) =>
        !GetElement(elementId).IsOffscreen;

    public void Focus(string elementId) =>
        GetElement(elementId).Focus();

    public void SetForeground(string elementId) =>
        GetElement(elementId).AsWindow().SetForeground();

    public string GetWindowTitle(string appId) =>
        GetAppRoot(appId).Name ?? "";

    public object GetBoundingRect(string elementId)
    {
        var r = GetElement(elementId).BoundingRectangle;
        return new { x = r.X, y = r.Y, width = r.Width, height = r.Height };
    }

    public object GetWindowBoundingRect(string appId)
    {
        var r = GetAppRoot(appId).BoundingRectangle;
        return new { x = r.X, y = r.Y, width = r.Width, height = r.Height };
    }

    // ── Screenshots ──────────────────────────────────────────────────────────

    public byte[] ScreenshotElement(string elementId)
    {
        var el = GetElement(elementId);
        using var capture = FlaUI.Core.Capturing.Capture.Element(el);
        using var ms = new MemoryStream();
        capture.Bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    public byte[] ScreenshotApp(string appId)
    {
        var root = GetAppRoot(appId);
        using var capture = FlaUI.Core.Capturing.Capture.Element(root);
        using var ms = new MemoryStream();
        capture.Bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    // ── Tree navigation ────────────────────────────────────────────────────

    public string[] GetChildren(string elementId)
    {
        var el = GetElement(elementId);
        var children = el.FindAllChildren();
        return children.Select(CacheElement).ToArray();
    }

    public object GetDescendants(string elementId)
    {
        var el = GetElement(elementId);
        return BuildDescendantTree(el);
    }

    private object BuildDescendantTree(AutomationElement el)
    {
        var id = CacheElement(el);
        var children = el.FindAllChildren();
        return new
        {
            id,
            children = children.Select(BuildDescendantTree).ToArray()
        };
    }

    // ── Desktop screenshot ──────────────────────────────────────────────────

    public byte[] DesktopScreenshot()
    {
        using var capture = FlaUI.Core.Capturing.Capture.Screen();
        using var ms = new MemoryStream();
        capture.Bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    // ── Video recording ──────────────────────────────────────────────────────

    private readonly Dictionary<string, VideoRecorder> _recorders = new();
    private readonly Dictionary<string, string> _recordingPaths = new();

    public void StartRecording(string appId)
    {
        if (_recorders.ContainsKey(appId))
            throw new InvalidOperationException($"Recording is already in progress for app '{appId}'.");

        var root = GetAppRoot(appId);
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");

        var settings = new VideoRecorderSettings
        {
            TargetVideoPath = tempPath,
            FrameRate = 15,
            VideoFormat = VideoFormat.x264,
            VideoQuality = 23
        };
        if (!string.IsNullOrWhiteSpace(_ffmpegPath))
            settings.ffmpegPath = _ffmpegPath;

        var recorder = new VideoRecorder(settings, _ => FlaUI.Core.Capturing.Capture.Element(root));
        _recorders[appId] = recorder;
        _recordingPaths[appId] = tempPath;
    }

    public byte[] StopRecording(string appId)
    {
        if (!_recorders.TryGetValue(appId, out var recorder))
            throw new InvalidOperationException($"No recording in progress for app '{appId}'.");

        recorder.Stop();
        var path = _recordingPaths[appId];
        var bytes = File.ReadAllBytes(path);
        try { File.Delete(path); } catch { }
        _recorders.Remove(appId);
        _recordingPaths.Remove(appId);
        return bytes;
    }

    // ── Internal helpers ────────────────────────────────────────────────────

    private static Window FindWindow(UIA3Automation automation, string titleRegex, int retries = 20)
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
        throw new InvalidOperationException($"Could not find window '{titleRegex}'");
    }

    private static bool GetWindowTime(IntPtr hwnd, out DateTime created)
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

    private static int GetWindowProcessId(IntPtr hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out uint pid);
        return (int)pid;
    }

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private AutomationElement GetDesktopRoot() => _automation.GetDesktop();

    private int? GetConfineProcessId(string appId) =>
        _confineToApp && _appProcessIds.TryGetValue(appId, out var pid) ? pid : null;

    private Window GetAppRoot(string appId)
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

        foreach (var path in _recordingPaths.Values)
        {
            try { File.Delete(path); } catch { }
        }
        _recordingPaths.Clear();

        foreach (var (id, app) in _apps)
        {
            if (!_attachedApps.Contains(id))
                try { app.Close(); } catch { }
            try { app.Dispose(); } catch { }
        }

        _automation.Dispose();
    }
}
