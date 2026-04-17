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
    string FindElement(string appId, string selector, string? rootElementId = null);
    string[] FindElements(string appId, string selector, string? rootElementId = null);
    object[] ResolveAttrs(string appId, string selector, string? rootElementId = null);
    string[] FindElementsAtPoint(string appId, double x, double y, string? rootElementId = null);
    string FindFrontElementAtPoint(string appId, double x, double y, string? rootElementId = null);
    string WaitForElement(string appId, string selector, string? rootElementId, uint timeout);
    string[] WaitForElements(string appId, string selector, string? rootElementId, uint timeout);
    object[] WaitForResolvedAttrs(string appId, string selector, string? rootElementId, uint timeout);
    void WaitForVanish(string appId, string selector, string? rootElementId, uint timeout);
    void Click(string elementId);
    void DoubleClick(string elementId);
    void RightClick(string elementId);
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
    byte[] ScreenshotElement(string elementId);
    byte[] ScreenshotApp(string appId);
    string[] GetChildren(string elementId);
    object GetDescendants(string elementId);
    byte[] DesktopScreenshot();
    void StartRecording(string appId);
    byte[] StopRecording(string appId);
}
