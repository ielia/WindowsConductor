using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsConductor.InspectorGUI;

internal static class InspectorSettings
{
    internal const string DataDirName = ".wc-rc";
    internal const string SubDirName = "inspector-gui";
    internal const string HistoryFileName = "history";
    internal const string StateFileName = "state";

    internal const double DefaultWindowWidth = 1000;
    internal const double DefaultWindowHeight = 700;
    internal const double DefaultOutputLogHeight = 120;
    internal const double DefaultAttributesPanelWidth = 320;
    internal const double DefaultSnapshotPanelWidth = 280;

    internal static string GetDataDirPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DataDirName, SubDirName);

    internal static List<string> LoadHistory(string dirPath)
    {
        var path = Path.Combine(dirPath, HistoryFileName);
        if (!File.Exists(path)) return [];
        try
        {
            return [.. File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l))];
        }
        catch
        {
            return [];
        }
    }

    internal static void SaveHistory(string dirPath, IReadOnlyList<string> entries)
    {
        Directory.CreateDirectory(dirPath);
        File.WriteAllLines(Path.Combine(dirPath, HistoryFileName), entries);
    }

    internal static StateData LoadState(string dirPath)
    {
        var path = Path.Combine(dirPath, StateFileName);
        if (!File.Exists(path)) return new StateData();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<StateData>(json) ?? new StateData();
        }
        catch
        {
            return new StateData();
        }
    }

    internal static void SaveState(string dirPath, StateData state)
    {
        Directory.CreateDirectory(dirPath);
        var json = JsonSerializer.Serialize(state, StateJsonContext.Default.StateData);
        File.WriteAllText(Path.Combine(dirPath, StateFileName), json);
    }

    internal static void ResetAll(string dirPath)
    {
        var historyPath = Path.Combine(dirPath, HistoryFileName);
        var statePath = Path.Combine(dirPath, StateFileName);
        if (File.Exists(historyPath)) File.Delete(historyPath);
        if (File.Exists(statePath)) File.Delete(statePath);
    }

    internal static bool IsTitleBarVisible(double left, double top, double width)
    {
        const double minVisibleWidth = 50;
        var screenLeft = System.Windows.SystemParameters.VirtualScreenLeft;
        var screenTop = System.Windows.SystemParameters.VirtualScreenTop;
        var screenRight = screenLeft + System.Windows.SystemParameters.VirtualScreenWidth;
        var screenBottom = screenTop + System.Windows.SystemParameters.VirtualScreenHeight;
        var overlapLeft = Math.Max(left, screenLeft);
        var overlapRight = Math.Min(left + width, screenRight);
        var horizontalOverlap = overlapRight - overlapLeft;
        return horizontalOverlap >= minVisibleWidth &&
               top >= screenTop && top < screenBottom;
    }

    internal static bool FitsInScreen(double width, double height) =>
        width <= System.Windows.SystemParameters.VirtualScreenWidth &&
        height <= System.Windows.SystemParameters.VirtualScreenHeight;
}

internal sealed class StateData
{
    [JsonPropertyName("stopOnError")]
    public bool StopOnError { get; set; }

    [JsonPropertyName("allowSelfSignedCerts")]
    public bool AllowSelfSignedCerts { get; set; } = true;

    [JsonPropertyName("prunedLocate")]
    public bool PrunedLocate { get; set; } = true;

    [JsonPropertyName("highlightColor")]
    public int HighlightColor { get; set; }

    [JsonPropertyName("pinnedAttributes")]
    public string[] PinnedAttributes { get; set; } = [];

    [JsonPropertyName("clicklessMode")]
    public bool ClicklessMode { get; set; }

    [JsonPropertyName("wrapAttributes")]
    public bool WrapAttributes { get; set; }

    [JsonPropertyName("windowLeft")]
    public double? WindowLeft { get; set; }

    [JsonPropertyName("windowTop")]
    public double? WindowTop { get; set; }

    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; } = InspectorSettings.DefaultWindowWidth;

    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; } = InspectorSettings.DefaultWindowHeight;

    [JsonPropertyName("windowState")]
    public string WindowState { get; set; } = "Normal";

    [JsonPropertyName("outputLogHeight")]
    public double OutputLogHeight { get; set; } = InspectorSettings.DefaultOutputLogHeight;

    [JsonPropertyName("attributesPanelWidth")]
    public double AttributesPanelWidth { get; set; } = InspectorSettings.DefaultAttributesPanelWidth;

    [JsonPropertyName("snapshotPanelWidth")]
    public double SnapshotPanelWidth { get; set; } = InspectorSettings.DefaultSnapshotPanelWidth;
}

[JsonSerializable(typeof(StateData))]
internal sealed partial class StateJsonContext : JsonSerializerContext;
