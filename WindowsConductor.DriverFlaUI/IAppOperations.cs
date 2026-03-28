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
    void Click(string elementId);
    void DoubleClick(string elementId);
    void RightClick(string elementId);
    void TypeText(string elementId, string text);
    string GetText(string elementId);
    string GetAttribute(string elementId, string attribute);
    Dictionary<string, object?> GetAttributes(string elementId);
    string GetParent(string elementId);
    bool IsEnabled(string elementId);
    bool IsVisible(string elementId);
    void Focus(string elementId);
    string GetWindowTitle(string appId);
    object GetBoundingRect(string elementId);
    object GetWindowBoundingRect(string appId);
    string ScreenshotElement(string elementId, string? path);
    string ScreenshotApp(string appId, string? path);
    string StartRecording(string appId, string? path, string? ffmpegPath);
    string StopRecording(string appId);
}
