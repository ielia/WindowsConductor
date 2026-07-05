# WindowsConductor.Client

.NET client library for controlling Windows desktop applications remotely via the WindowsConductor Driver. Provides an async API for launching, attaching to, and interacting with native Windows UI elements over a WebSocket connection.

## Installation

The package is not published to nuget.org. To use it locally:

```bash
# Pack the library into a .nupkg
dotnet pack WindowsConductor.Client -c Release

# Add a local NuGet source pointing to the output folder
dotnet nuget add source ./WindowsConductor.Client/bin/Release -n WindowsConductorLocal

# Install in your project
dotnet add package WindowsConductor.Client --version 0.10.0
```

## Quick start

```csharp
using WindowsConductor.Client;

await using var session = await WcSession.ConnectAsync("ws://localhost:8765/");
await using var app = await session.LaunchAsync("notepad.exe");

var editor = app.Locator("type=Edit");
await editor.TypeAsync("Hello from WindowsConductor");

using var screenshot = await app.ScreenshotAsync(); // returns SKBitmap
```

## Key types

### `WcSession`

WebSocket connection to the Driver. Entry point for the API.

| Method | Description |
|---|---|
| `ConnectAsync(url)` | Connect to the Driver. |
| `ConnectAsync(url, authToken)` | Connect with bearer token authentication. |
| `ConnectAsync(url, authToken, allowSelfSignedCerts)` | Connect with TLS options. |
| `LaunchAsync(path, args?, detachedTitleRegex?, mainWindowTimeout?)` | Launch an application. |
| `AttachAsync(mainWindowTitleRegex, mainWindowTimeout?)` | Attach to a running application (will not be closed on disconnect). |
| `GlobalHitKeysAsync(keys)` | Press keys globally without targeting an element. |
| `GlobalTypeAsync(text, modifiers?)` | Type text globally without targeting an element. |
| `DesktopScreenshotAsync()` | Screenshot the entire desktop (returns `SKBitmap`). |
| `DesktopScreenshotBytesAsync()` | Screenshot the entire desktop (returns `byte[]`). |

### `WcApp`

A launched or attached application. Implements `IWcScope` and `IWcScreenshottable`.

| Method | Description |
|---|---|
| `GetTitleAsync()` | Get the main window title. |
| `StartRecordingAsync()` / `StopRecordingAsync(outputPath?)` | Video recording (returns `byte[]` on stop). |
| `CloseAsync()` | Close the application. |

### `IWcScope`

Scoping interface for locating child elements. Implemented by `WcApp`, `WcElement`, and `WcLocator`.

| Method | Description |
|---|---|
| `Locator(selector)` | Create a child locator. |
| `GetByAutomationId(id)` / `GetByName(name)` / `GetByText(text)` / `GetByXPath(xpath)` / `GetByControlType(type)` | Shorthand locator factories. |
| `GetAtAsync(x, y)` / `GetFrontAtAsync(x, y)` | Find all or front-most element at a screen point. |

### `IWcScreenshottable`

Screenshot interface. Implemented by `WcApp`, `WcElement`, and `WcLocator`.

| Method | Description |
|---|---|
| `ScreenshotAsync()` | Screenshot (returns `SKBitmap`). |
| `ScreenshotBytesAsync()` | Screenshot (returns `byte[]`). |

### `IWcWidget`

Extends `IWcScope` and `IWcScreenshottable`. Common interface implemented by both `WcLocator` and `WcElement`. Adds interaction, inspection, tree navigation, and OCR.

| Method | Description |
|---|---|
| `GetElementAsync()` | Resolve to a `WcElement` (locator queries the Driver; element returns itself). |
| `ClickAsync()` / `ClickAsync(anchor, offset)` | Click (with optional anchor + offset). |
| `DoubleClickAsync()` / `DoubleClickAsync(anchor, offset)` | Double-click. |
| `RightClickAsync()` / `RightClickAsync(anchor, offset)` | Right-click. |
| `HoverAsync()` / `HoverAsync(anchor, offset)` | Hover. |
| `DragToAsync(target, ...)` | Drag to another element or locator (with optional anchors + offsets). |
| `ScrollAsync(lines, horizontal?)` | Scroll the mouse wheel. |
| `TypeAsync(text)` / `TypeAsync(text, modifiers)` / `HitKeysAsync(keys)` | Keyboard input. |
| `FocusAsync()` / `SetForegroundAsync()` | Focus or bring window to foreground. |
| `GetWindowStateAsync()` / `SetWindowStateAsync(state)` | Window state management. |
| `GetTextAsync()` / `GetAttributeAsync(name)` / `GetAttributesAsync()` | Inspect element text and attributes. |
| `GetAutomationIdAsync()` / `GetClassNameAsync()` / `GetControlTypeAsync()` / `GetNameAsync()` / `GetProcessIdAsync()` | Shorthand for common attributes. |
| `SetAttributeAsync(name, value)` | Set a UIAutomation pattern property (toggle, expand/collapse, value, etc.). |
| `ExistsAsync()` / `IsEnabledAsync()` / `IsVisibleAsync()` | Element state queries. |
| `WaitForVanishAsync(timeout)` | Wait for the element to disappear. |
| `WaitForVisibleAsync(timeout)` | Wait for the element to become visible (not offscreen). |
| `WaitForHiddenAsync(timeout)` | Wait for the element to become hidden (offscreen). |
| `GetBoundingRectAsync()` | Bounding rectangle (returns `BoundingRect`). |
| `ParentAsync()` / `TopLevelWindowAsync()` | Tree navigation (returns `WcElement?`). |
| `ChildrenAsync()` / `DescendantsAsync()` | Child/descendant tree (returns `WcElement[]` / `IReadOnlyTreeNode<WcElement>`). |
| `GetOcrTextAsync()` | Read text using OCR (returns `WcElementOcrResult`). |

### `WcLocator`

Lazy element selector that re-queries on each call. Implements `IWcWidget`. Additional methods beyond the interface:

| Method | Description |
|---|---|
| `Parent()` | Locator for the parent element. |
| `GetAllElementsAsync()` | Resolve all matching elements. |
| `GetResolvedValueAsync()` / `WaitForResolvedValueAsync(timeout)` | Resolve/wait for a typed value (`WcValue`). |
| `WaitForElementAsync(timeout)` / `WaitForAllElementsAsync(timeout)` | Wait for element(s) to appear. |
| `WaitForVanishAsync(timeout)` | Wait for all matching elements to disappear. |
| `WaitForVisibleAsync(timeout)` | Wait for a matching element to become visible. |
| `WaitForHiddenAsync(timeout)` | Wait for a matching element to become hidden. |

### `WcElement`

Resolved element handle for direct interaction. Implements `IWcWidget`. Additional methods beyond the interface:

| Method | Description |
|---|---|
| `IsStaleAsync()` | Check if this element handle is stale (underlying UI element no longer exists). Evicts from Driver cache if stale. |

### OCR types

`WcElement.GetOcrTextAsync()` returns a `WcElementOcrResult` containing a hierarchy of recognized text:

- **`WcElementOcrResult`** — Full OCR result with `Text`, `BoundingRect`, `Angle`, and `Lines`.
- **`WcElementOcrLine`** — A line of text with its `Words`.
- **`WcElementOcrWord`** — A single word.
- **`WcElementOcrMatch`** — A fuzzy search match with `Distance`, `Fragments`, and overlap-checking methods.

All OCR text types inherit from `WcElementOcrText` and expose action methods:

| Method | Description |
|---|---|
| `ClickAsync()` / `ClickAsync(anchor, offset)` | Click at the OCR text's bounding rect (anchor + offset resolved against the text rect). |
| `DoubleClickAsync()` / `DoubleClickAsync(anchor, offset)` | Double-click. |
| `RightClickAsync()` / `RightClickAsync(anchor, offset)` | Right-click. |
| `HoverAsync()` / `HoverAsync(anchor, offset)` | Hover. |
| `FindBestByEdits(text, maxEdits?)` | Find the best fuzzy substring match (returns `WcElementOcrMatch?`). |
| `FindAllByEdits(text, maxEdits?)` | Find all non-overlapping fuzzy matches. |

### Supporting types

| Type | Description |
|---|---|
| `Anchor` | Enum: `Center`, `North`, `NorthEast`, `East`, `SouthEast`, `South`, `SouthWest`, `West`, `NorthWest`. |
| `Key` | Virtual key codes for `HitKeysAsync` (`ENTER`, `TAB`, `KEY_A`–`KEY_Z`, `F1`–`F12`, etc.). |
| `KeyModifiers` | Flags: `None`, `Shift`, `Ctrl`, `Alt`, `Meta`. |
| `WcWindowState` | Enum: `Normal`, `Maximized`, `Minimized`, `MinimizedMaximized`, `Hidden`. |
| `BoundingRect` | Record with `X`, `Y`, `Width`, `Height`, `Bottom`, `Right`, `Center`. |
| `WcValue` / `WcAttr` | Typed values from `ResolveValue` / `GetAttributeAsync` with conversion methods (`GetAsInt`, `GetAsString`, etc.). |

### Exceptions

| Type | Description |
|---|---|
| `WcException` | Base exception for all WindowsConductor errors. |
| `NoMatchException` | Thrown when a wait operation times out. |
| `UnwantedMatchException` | Thrown when `WaitForVanishAsync` times out. |
| `VisibilityException` | Thrown when `WaitForVisibleAsync` or `WaitForHiddenAsync` times out. |
| `LocationOutOfRangeException` | Thrown when a click target is outside element bounds. |
