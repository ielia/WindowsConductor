namespace WindowsConductor.InspectorGUI;

internal interface ICommandOutput
{
    void WriteInfo(string message);
    void WriteError(string message);
    void ShowScreenshot(byte[] imageData, HighlightInfo? highlight = null);
    void ClearHighlight();
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
