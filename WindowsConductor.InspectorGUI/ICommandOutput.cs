namespace WindowsConductor.InspectorGUI;

internal interface ICommandOutput
{
    void ClearLog();
    void WriteInfo(string message);
    void WriteCommand(string command);
    void WriteError(string message);
    void ShowScreenshot(byte[] imageData, HighlightInfo? highlight = null, WindowDimensions? windowDimensions = null);
    void ClearScreenshot();
    void ClearHighlight();
    void ShowAttributes(string locatorChain, Dictionary<string, object?> attributes);
    void ClearAttributes();
    void UpdateMatchNavigation(int currentIndex, int totalCount);
    void ShowSleepCancel(int totalMilliseconds, Action cancelAction);
    Task HideSleepCancelAsync();
    void RequestExit();
}

/// <summary>
/// Highlight rectangle in window-relative coordinates, plus the window
/// bounding-rect dimensions so the renderer can compensate for DPI scaling
/// (UIAutomation rects may be in logical pixels while the screenshot is
/// captured at physical pixel resolution).
/// </summary>
internal sealed record HighlightInfo(
    double X, double Y, double Width, double Height,
    double WindowWidth, double WindowHeight);

internal sealed record WindowDimensions(double X, double Y, double Width, double Height);
