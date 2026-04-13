using WindowsConductor.Client;

namespace WindowsConductor.InspectorGUI;

internal interface IInspectorSession
{
    bool IsConnected { get; }
    bool HasApp { get; }
    bool HasSelectedElement { get; }
    bool AllowSelfSignedCerts { get; set; }

    Task ConnectAsync(string url, string? authToken = null, CancellationToken ct = default);
    Task LaunchAsync(string path, string[] args, string? detachedTitleRegex, uint? mainWindowTimeout, CancellationToken ct = default);
    Task AttachAsync(string mainWindowTitleRegex, uint? mainWindowTimeout, CancellationToken ct = default);
    Task CloseAppAsync(CancellationToken ct = default);
    Task DetachAppAsync();

    Task<byte[]> WindowScreenshotAsync(CancellationToken ct = default);
    Task<BoundingRect> GetWindowBoundingRectAsync(CancellationToken ct = default);
    Task<byte[]> ElementWindowScreenshotAsync(CancellationToken ct = default);
    Task<BoundingRect> GetElementWindowBoundingRectAsync(CancellationToken ct = default);

    Task<string> LocateAsync(string[] selectors, CancellationToken ct = default);
    Task<string> LocateFromElementAsync(string[] selectors, CancellationToken ct = default);
    Task<int> LocateAllAsync(string[] selectors, CancellationToken ct = default);
    Task<int> LocateAllFromElementAsync(string[] selectors, CancellationToken ct = default);
    Task<string> SelectMatchAsync(int index, CancellationToken ct = default);
    void Unselect();

    Task<string> GetAttributeAsync(string attributeName, CancellationToken ct = default);
    Task<Dictionary<string, object?>> GetAttributesAsync(CancellationToken ct = default);
    Task<string?> ParentAsync(CancellationToken ct = default);
    Task<bool> IsSelectedElementRootAsync(CancellationToken ct = default);
    Task ClickAsync(CancellationToken ct = default);
    Task DoubleClickAsync(CancellationToken ct = default);
    Task RightClickAsync(CancellationToken ct = default);
    Task TypeAsync(string text, KeyModifiers modifiers = KeyModifiers.None, CancellationToken ct = default);
    Task FocusAsync(CancellationToken ct = default);
    Task<string> GetTextAsync(CancellationToken ct = default);
    Task<byte[]> ScreenshotElementAsync(CancellationToken ct = default);
    Task<BoundingRect> GetElementBoundingRectAsync(CancellationToken ct = default);

    Task<IReadOnlyList<WcElement>> GetChildrenAsync(CancellationToken ct = default);
    Task<IReadOnlyTreeNode<WcElement>> GetDescendantsAsync(CancellationToken ct = default);
    Task<byte[]> DesktopScreenshotAsync(CancellationToken ct = default);

    Task DisconnectAsync();
}
