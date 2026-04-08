using WindowsConductor.Client;

namespace WindowsConductor.InspectorGUI;

internal sealed record SnapshotNode(
    string Label,
    BoundingRect? BoundingRect,
    Dictionary<string, object?> Attributes,
    List<SnapshotNode> Children);

internal sealed record SnapshotCapture(
    byte[] CroppedScreenshot,
    BoundingRect UnionRect,
    SnapshotNode RootNode,
    Dictionary<string, WcElement> ElementsById);
