using System.Drawing;
using SkiaSharp;

namespace WindowsConductor.Client;

/// <summary>
/// Something that can scope into child UI elements via locators and hit-testing.
/// Implemented by <see cref="WcApp"/>, <see cref="WcElement"/>, and <see cref="WcLocator"/>.
/// </summary>
public interface IWcScope
{
    WcLocator Locator(string selector);
    WcLocator GetByAutomationId(string automationId);
    WcLocator GetByName(string name);
    WcLocator GetByText(string text);
    WcLocator GetByXPath(string xpath);
    WcLocator GetByControlType(string controlType);
    Task<IReadOnlyList<WcElement>> GetAtAsync(double x, double y, CancellationToken ct = default);
    Task<WcElement> GetFrontAtAsync(double x, double y, CancellationToken ct = default);
}

/// <summary>
/// Something that can be captured as a screenshot.
/// Implemented by <see cref="WcApp"/>, <see cref="WcElement"/>, and <see cref="WcLocator"/>.
/// </summary>
public interface IWcScreenshottable
{
    Task<byte[]> ScreenshotBytesAsync(CancellationToken ct = default);
    Task<SKBitmap> ScreenshotAsync(CancellationToken ct = default);
}

/// <summary>
/// Common interface for <see cref="WcElement"/> and <see cref="WcLocator"/>.
/// Both represent a reference to a UI element that can be interacted with,
/// queried, navigated, and screenshotted.
/// </summary>
public interface IWcWidget : IWcScope, IWcScreenshottable
{
    Task<WcElement> GetElementAsync(CancellationToken ct = default);

    // ── Actions ─────────────────────────────────────────────────────────────

    Task ClickAsync(CancellationToken ct = default);
    Task ClickAsync(Anchor anchor, Point offset, CancellationToken ct = default);
    Task DoubleClickAsync(CancellationToken ct = default);
    Task DoubleClickAsync(Anchor anchor, Point offset, CancellationToken ct = default);
    Task RightClickAsync(CancellationToken ct = default);
    Task RightClickAsync(Anchor anchor, Point offset, CancellationToken ct = default);
    Task HoverAsync(CancellationToken ct = default);
    Task HoverAsync(Anchor anchor, Point offset, CancellationToken ct = default);

    Task DragToAsync(WcElement target, CancellationToken ct = default);
    Task DragToAsync(WcElement target, Anchor toAnchor, Point toOffset = default, CancellationToken ct = default);
    Task DragToAsync(Anchor fromAnchor, Point fromOffset, WcElement target, CancellationToken ct = default);
    Task DragToAsync(Anchor fromAnchor, Point fromOffset, WcElement target, Anchor toAnchor, Point toOffset = default, CancellationToken ct = default);
    Task DragToAsync(WcLocator target, CancellationToken ct = default);
    Task DragToAsync(WcLocator target, Anchor toAnchor, Point toOffset = default, CancellationToken ct = default);
    Task DragToAsync(Anchor fromAnchor, Point fromOffset, WcLocator target, CancellationToken ct = default);
    Task DragToAsync(Anchor fromAnchor, Point fromOffset, WcLocator target, Anchor toAnchor, Point toOffset = default, CancellationToken ct = default);

    Task ScrollAsync(double lines, bool horizontal = false, CancellationToken ct = default);
    Task HitKeysAsync(Key[] keys, CancellationToken ct = default);
    Task TypeAsync(string text, CancellationToken ct = default);
    Task TypeAsync(string text, KeyModifiers modifiers, CancellationToken ct = default);
    Task FocusAsync(CancellationToken ct = default);
    Task SetForegroundAsync(CancellationToken ct = default);

    Task<WcWindowState> GetWindowStateAsync(CancellationToken ct = default);
    Task SetWindowStateAsync(WcWindowState state, CancellationToken ct = default);

    // ── Queries ─────────────────────────────────────────────────────────────

    Task<string> GetTextAsync(CancellationToken ct = default);
    Task<string> GetAutomationIdAsync(CancellationToken ct = default);
    Task<string> GetClassNameAsync(CancellationToken ct = default);
    Task<string> GetControlTypeAsync(CancellationToken ct = default);
    Task<string> GetNameAsync(CancellationToken ct = default);
    Task<string> GetProcessIdAsync(CancellationToken ct = default);
    Task<string> GetAttributeAsync(string attribute, CancellationToken ct = default);
    Task<Dictionary<string, object?>> GetAttributesAsync(CancellationToken ct = default);
    Task SetAttributeAsync(string attribute, string value, CancellationToken ct = default);
    Task<bool> ExistsAsync(CancellationToken ct = default);
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
    Task<bool> IsVisibleAsync(CancellationToken ct = default);
    Task<BoundingRect> GetBoundingRectAsync(CancellationToken ct = default);
    Task WaitForVanishAsync(uint timeout, CancellationToken ct = default);
    Task WaitForVisibleAsync(uint timeout, CancellationToken ct = default);
    Task WaitForHiddenAsync(uint timeout, CancellationToken ct = default);

    // ── Tree navigation ─────────────────────────────────────────────────────

    Task<WcElement?> ParentAsync(CancellationToken ct = default);
    Task<WcElement?> TopLevelWindowAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WcElement>> ChildrenAsync(CancellationToken ct = default);
    Task<IReadOnlyTreeNode<WcElement>> DescendantsAsync(CancellationToken ct = default);

    // ── OCR ─────────────────────────────────────────────────────────────────

    Task<WcElementOcrResult> GetOcrTextAsync(CancellationToken ct = default);
}
