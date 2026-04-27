using WindowsConductor.Client;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Abstraction over application management operations.
/// Implemented by <see cref="AppManager"/>; also used for testing.
/// </summary>
internal interface IAppOperations
{
    string LaunchApp(string path, string[] args, string? detachedTitleRegex, int? mainWindowTimeout);
    string AttachApp(string mainWindowTitleRegex, int? mainWindowTimeout);
    void CloseApp(string appId);
    string FindElement(string appId, string selector, string? rootElementId = null, CancellationToken ct = default);
    string[] FindElements(string appId, string selector, string? rootElementId = null, CancellationToken ct = default);
    object ResolveValue(string appId, string selector, string? rootElementId = null, CancellationToken ct = default);
    string[] FindElementsAtPoint(string appId, double x, double y, string? rootElementId = null, CancellationToken ct = default);
    string FindFrontElementAtPoint(string appId, double x, double y, string? rootElementId = null, CancellationToken ct = default);
    string WaitForElement(string appId, string selector, string? rootElementId, uint timeout, CancellationToken ct = default);
    string[] WaitForElements(string appId, string selector, string? rootElementId, uint timeout, CancellationToken ct = default);
    object WaitForResolvedValue(string appId, string selector, string? rootElementId, uint timeout, CancellationToken ct = default);
    void WaitForVanish(string appId, string selector, string? rootElementId, uint timeout, CancellationToken ct = default);
    void Click(string elementId, string? anchor = null, int x = 0, int y = 0);
    void DoubleClick(string elementId, string? anchor = null, int x = 0, int y = 0);
    void RightClick(string elementId, string? anchor = null, int x = 0, int y = 0);
    void Hover(string elementId, string? anchor = null, int x = 0, int y = 0);
    void HitKeys(string elementId, string[] keys);
    void TypeText(string elementId, string text, int modifiers = 0);
    string GetText(string elementId);
    string GetAttribute(string elementId, string attribute);
    Dictionary<string, object?> GetAttributes(string elementId);
    string? GetParent(string elementId);
    string? GetTopLevelWindow(string elementId);
    bool IsEnabled(string elementId);
    bool IsVisible(string elementId);
    void Focus(string elementId);
    void SetForeground(string elementId);
    WcWindowState GetWindowState(string elementId);
    void SetWindowState(string elementId, WcWindowState state);
    string GetWindowTitle(string appId);
    object GetBoundingRect(string elementId);
    object GetWindowBoundingRect(string appId);
    object GetOcrText(string elementId);
    byte[] ScreenshotElement(string elementId);
    byte[] ScreenshotApp(string appId);
    string[] GetChildren(string elementId);
    object GetDescendants(string elementId);
    byte[] DesktopScreenshot();
    void StartRecording(string appId);
    byte[] StopRecording(string appId);
}
