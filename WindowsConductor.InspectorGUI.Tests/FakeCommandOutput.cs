using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

internal sealed class FakeCommandOutput : ICommandOutput
{
    public List<string> InfoMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();
    public List<(byte[] Data, HighlightInfo? Highlight)> Screenshots { get; } = new();
    public int ClearScreenshotCount { get; private set; }
    public int ClearHighlightCount { get; private set; }
    public List<(string LocatorChain, Dictionary<string, object?> Attributes)> AttributesSets { get; } = new();
    public int ClearAttributesCount { get; private set; }

    public int ClearLogCount { get; private set; }

    public void ClearLog() => ClearLogCount++;
    public void WriteInfo(string message) => InfoMessages.Add(message);
    public void WriteError(string message) => ErrorMessages.Add(message);

    public void ShowScreenshot(byte[] imageData, HighlightInfo? highlight = null) =>
        Screenshots.Add((imageData, highlight));

    public void ClearScreenshot() => ClearScreenshotCount++;
    public void ClearHighlight() => ClearHighlightCount++;

    public void ShowAttributes(string locatorChain, Dictionary<string, object?> attributes) =>
        AttributesSets.Add((locatorChain, attributes));
    public void ClearAttributes() => ClearAttributesCount++;

    public List<(int CurrentIndex, int TotalCount)> MatchNavigationUpdates { get; } = new();
    public void UpdateMatchNavigation(int currentIndex, int totalCount) =>
        MatchNavigationUpdates.Add((currentIndex, totalCount));

    public int RequestExitCount { get; private set; }
    public void RequestExit() => RequestExitCount++;
}
