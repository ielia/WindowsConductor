using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

internal sealed class FakeCommandOutput : ICommandOutput
{
    public List<string> InfoMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();
    public List<(byte[] Data, HighlightInfo? Highlight)> Screenshots { get; } = new();
    public int ClearScreenshotCount { get; private set; }
    public int ClearHighlightCount { get; private set; }

    public void WriteInfo(string message) => InfoMessages.Add(message);
    public void WriteError(string message) => ErrorMessages.Add(message);

    public void ShowScreenshot(byte[] imageData, HighlightInfo? highlight = null) =>
        Screenshots.Add((imageData, highlight));

    public void ClearScreenshot() => ClearScreenshotCount++;
    public void ClearHighlight() => ClearHighlightCount++;
}
