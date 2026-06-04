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

A launched or attached application.

| Method | Description |
|---|---|
| `Locator(selector)` | Create a locator scoped to the app's main window. |
| `GetByAutomationId(id)` / `GetByName(name)` / `GetByText(text)` / `GetByXPath(xpath)` / `GetByControlType(type)` | Shorthand locator factories. |
| `GetTitleAsync()` | Get the main window title. |
| `GetAtAsync(x, y)` | Find all elements at a screen point. |
| `GetFrontAtAsync(x, y)` | Find the front-most element at a screen point. |
| `ScreenshotAsync()` / `ScreenshotBytesAsync()` | Screenshot the main window. |
| `StartRecordingAsync()` / `StopRecordingAsync(outputPath?)` | Video recording (returns `byte[]` on stop). |
| `CloseAsync()` | Close the application. |

### `WcLocator`

Lazy element selector that re-queries on each call. Supports all interaction methods — they resolve the element, perform the action, and discard the handle.

| Method | Description |
|---|---|
| `Locator(selector)` | Chain a child locator. |
| `GetByAutomationId(id)` / `GetByName(name)` / `GetByText(text)` / `GetByXPath(xpath)` / `GetByControlType(type)` | Shorthand child locator factories. |
| `Parent()` | Locator for the parent element. |
| `GetElementAsync()` / `GetAllElementsAsync()` | Resolve to `WcElement`(s). |
| `GetResolvedValueAsync()` / `WaitForResolvedValueAsync(timeout)` | Resolve/wait for a typed value (`WcValue`). |
| `WaitForElementAsync(timeout)` / `WaitForAllElementsAsync(timeout)` | Wait for element(s) to appear. |
| `WaitForVanishAsync(timeout)` | Wait for all matching elements to disappear. |
| `ClickAsync()` / `ClickAsync(anchor, offset)` | Click (with optional anchor + offset). |
| `DoubleClickAsync()` / `DoubleClickAsync(anchor, offset)` | Double-click. |
| `RightClickAsync()` / `RightClickAsync(anchor, offset)` | Right-click. |
| `HoverAsync()` / `HoverAsync(anchor, offset)` | Hover. |
| `DragToAsync(target, ...)` | Drag to another element or locator (with optional anchors + offsets). |
| `ScrollAsync(lines, horizontal?)` | Scroll the mouse wheel. |
| `TypeAsync(text, modifiers?)` / `HitKeysAsync(keys)` | Keyboard input. |
| `FocusAsync()` / `SetForegroundAsync()` | Focus or bring window to foreground. |
| `GetTextAsync()` / `GetAttributeAsync(name)` / `GetAttributesAsync()` | Inspect element text and attributes. |
| `SetAttributeAsync(name, value)` | Set a UIAutomation pattern property (toggle, expand/collapse, value, etc.). |
| `IsEnabledAsync()` / `IsVisibleAsync()` | Element state queries. |
| `GetBoundingRectAsync()` | Bounding rectangle (returns `BoundingRect`). |
| `GetWindowStateAsync()` / `SetWindowStateAsync(state)` | Window state management. |
| `ScreenshotAsync()` / `ScreenshotBytesAsync()` | Element screenshot. |

### `WcElement`

Resolved element handle for direct interaction. Has the same interaction methods as `WcLocator`, plus:

| Method | Description |
|---|---|
| `Locator(selector)` | Create a child locator scoped to this element. |
| `ParentAsync()` / `TopLevelWindowAsync()` | Tree navigation (returns `WcElement?`). |
| `ChildrenAsync()` / `DescendantsAsync()` | Child/descendant tree (returns `WcElement[]` / `IReadOnlyTreeNode<WcElement>`). |
| `GetOcrTextAsync()` | Read text using OCR (returns `WcElementOcrResult`). |

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
| `LocationOutOfRangeException` | Thrown when a click target is outside element bounds. |
