# MCP Tools Reference

Complete reference for all tools exposed by the WindowsConductor MCP server.

## Session

### Connect

Connect to a running WindowsConductor driver. Must be called before any other tool.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `wsUri` | string | No | WebSocket URL of the driver (default: `ws://localhost:8765/`) |
| `authToken` | string | No | Bearer token for authentication |

### Disconnect

Disconnect from the driver and close all tracked applications.

---

## Application management

### LaunchApp

Launch a Windows application and return its `appId`.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | Yes | Executable path or name (e.g. `calc.exe`, `notepad.exe`) |
| `args` | string[] | No | Command-line arguments |
| `detachedTitleRegex` | string | No | Regex for matching a detached window title |
| `mainWindowTimeout` | uint | No | Timeout in ms to wait for the main window |

### AttachApp

Attach to an already-running application by matching its window title. The application will NOT be closed on disconnect.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `mainWindowTitleRegex` | string | Yes | Regex pattern to match against window titles |
| `mainWindowTimeout` | uint | No | Timeout in ms to wait for the window |

### CloseApp

Close a tracked application.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |

### GetAppTitle

Get the title of an application's main window.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |

### ListApps

List all currently tracked application IDs. No parameters.

---

## Finding elements

### FindElement

Find a single UI element using a selector. Returns its element ID.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `selector` | string | Yes | Element selector (see [Selectors](#selectors)) |

### FindElements

Find UI elements using a selector. Returns a JSON array of element IDs.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `selector` | string | Yes | Element selector (see [Selectors](#selectors)) |

### FindElementWithin

Find a single UI element scoped within a parent element.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `rootElementId` | string | Yes | Element ID of the parent to search within |
| `selector` | string | Yes | Element selector (see [Selectors](#selectors)) |

### FindElementsWithin

Find UI elements scoped within a parent element. Returns a JSON array of element IDs.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `rootElementId` | string | Yes | Element ID of the parent to search within |
| `selector` | string | Yes | Element selector (see [Selectors](#selectors)) |

### ResolveValue

Resolve a selector and return the typed result. Returns JSON with `type` and `value` (or `items` for lists). Element selectors return text values; attribute selectors return attribute values with element IDs.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `selector` | string | Yes | Element selector (see [Selectors](#selectors)) |
| `rootElementId` | string | No | Element ID to scope the search within |

---

## Waiting

These tools block server-side until the condition is met or the timeout elapses, avoiding the need for polling loops.

### WaitForElement

Wait for a single element matching the selector to appear.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `selector` | string | Yes | Element selector (see [Selectors](#selectors)) |
| `timeout` | uint | Yes | Timeout in milliseconds |
| `rootElementId` | string | No | Element ID to scope the search within |

### WaitForElements

Wait for at least one element matching the selector to appear. Returns all matches.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `selector` | string | Yes | Element selector (see [Selectors](#selectors)) |
| `timeout` | uint | Yes | Timeout in milliseconds |
| `rootElementId` | string | No | Element ID to scope the search within |

### WaitForResolvedValue

Wait for a selector to resolve to a non-empty value.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `selector` | string | Yes | Element selector (see [Selectors](#selectors)) |
| `timeout` | uint | Yes | Timeout in milliseconds |
| `rootElementId` | string | No | Element ID to scope the search within |

### WaitForVanish

Wait for all elements matching the selector to disappear.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `selector` | string | Yes | Element selector (see [Selectors](#selectors)) |
| `timeout` | uint | Yes | Timeout in milliseconds |
| `rootElementId` | string | No | Element ID to scope the search within |

---

## Element interaction

All element tools take an `elementId` parameter (returned by FindElement, FindElements, or wait tools).

### ClickElement / DoubleClickElement / RightClickElement / HoverElement

Click, double-click, right-click, or hover over an element at its center.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |

### ScrollElement

Scroll the mouse wheel over an element.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |
| `lines` | double | Yes | Lines to scroll (positive = down/right, negative = up/left) |
| `horizontal` | bool | No | If true, scroll horizontally instead of vertically (default: false) |

### ClickElementAt / DoubleClickElementAt / RightClickElementAt / HoverElementAt

Click, double-click, right-click, or hover at a specific position relative to an anchor point.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |
| `anchor` | string | Yes | Anchor point: `Center`, `North`, `NorthEast`, `East`, `SouthEast`, `South`, `SouthWest`, `West`, `NorthWest` |
| `offsetX` | int | Yes | Horizontal offset in pixels from the anchor |
| `offsetY` | int | Yes | Vertical offset in pixels from the anchor |

### DragToElement

Drag one element to another. Both source and target positions default to center if anchor/offset are not specified.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `sourceElementId` | string | Yes | Element ID to drag from |
| `targetElementId` | string | Yes | Element ID to drag to |
| `fromAnchor` | string | No | Anchor on the source element (default: `Center`) |
| `fromOffsetX` | int | No | Horizontal offset from source anchor (default: 0) |
| `fromOffsetY` | int | No | Vertical offset from source anchor (default: 0) |
| `toAnchor` | string | No | Anchor on the target element (default: `Center`) |
| `toOffsetX` | int | No | Horizontal offset from target anchor (default: 0) |
| `toOffsetY` | int | No | Vertical offset from target anchor (default: 0) |

### TypeText

Type text into an element (focuses it first).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |
| `text` | string | Yes | Text to type |
| `modifiers` | string[] | No | Modifier keys to hold while typing (e.g. `["Ctrl", "Shift"]`). Valid values: `Shift`, `Ctrl`, `Alt`, `Meta` |

### HitKeys

Press keyboard keys on an element.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |
| `keys` | string[] | Yes | Array of key names (e.g. `["CONTROL", "KEY_A"]`). Names: `ENTER`, `TAB`, `ESCAPE`, `BACK`, `DELETE`, `KEY_A`–`KEY_Z`, `F1`–`F12`, `CONTROL`, `SHIFT`, `ALT`, etc. |

### FocusElement

Focus an element.

### SetForeground

Bring an element's window to the foreground.

### GlobalHitKeys

Press keyboard keys globally without targeting a specific element.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `keys` | string[] | Yes | Array of key names to press (e.g. `["CONTROL", "KEY_A"]`) |

### GlobalTypeText

Type text globally without targeting a specific element.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `text` | string | Yes | Text to type |
| `modifiers` | string[] | No | Modifier keys to hold (e.g. `["Ctrl", "Shift"]`). Valid values: `Shift`, `Ctrl`, `Alt`, `Meta` |

---

## Element inspection

### GetText

Get the visible text of an element.

### GetAttribute

Get a specific UIAutomation attribute (e.g. `AutomationId`, `Name`, `ClassName`, `ControlType`).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |
| `attribute` | string | Yes | Attribute name |

### GetAttributes

Get all UIAutomation attributes of an element. Returns JSON.

### SetAttribute

Set a UIAutomation pattern property on an element. Supported attributes include `toggle_togglestate` (On/Off/Indeterminate), `expandcollapse_expandcollapsestate` (Expanded/Collapsed), `selectionitem_isselected` (True/False), `value_value` (string), `rangevalue_value` (number), `window_windowvisualstate` (Normal/Maximized/Minimized), `transform2_zoomlevel` (number), and `ischecked` (True/False).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |
| `attribute` | string | Yes | Attribute name (e.g. `toggle_togglestate`, `value_value`, `ischecked`) |
| `value` | string | Yes | Value to set (e.g. `On`, `True`, `some text`) |

### IsEnabled

Check if an element is enabled. Returns boolean.

### IsVisible

Check if an element is visible on screen. Returns boolean.

### GetBoundingRect

Get the bounding rectangle of an element. Returns JSON with `x`, `y`, `width`, `height` in screen pixels.

---

## Element tree navigation

### GetChildren

Get the direct children of an element. Returns a JSON array of element IDs.

### GetDescendants

Get the full descendant tree. Returns a JSON tree where each node has `id` and `children`.

### GetParent

Get the parent element. Returns the parent's element ID, or null.

### GetTopLevelWindow

Get the top-level window containing an element. Returns its element ID, or null.

### GetWindowState

Get the window state. Returns one of: `Normal`, `Maximized`, `Minimized`, `MinimizedMaximized`, `Hidden`.

### SetWindowState

Set the window state.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |
| `windowState` | string | Yes | `Normal`, `Maximized`, `Minimized`, or `MinimizedMaximized` |

---

## Screenshots

### ScreenshotApp

Take a screenshot of an application's main window. Returns base64-encoded PNG, or saves to file if `outputPath` is provided.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `outputPath` | string | No | Relative path under the screenshot root directory to save the PNG file. If omitted, returns base64-encoded PNG instead. |

### ScreenshotDesktop

Take a screenshot of the entire desktop. Returns base64-encoded PNG, or saves to file if `outputPath` is provided.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `outputPath` | string | No | Relative path under the screenshot root directory to save the PNG file. If omitted, returns base64-encoded PNG instead. |

### ScreenshotElement

Take a screenshot of a specific element. Returns base64-encoded PNG, or saves to file if `outputPath` is provided.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |
| `outputPath` | string | No | Relative path under the screenshot root directory to save the PNG file. If omitted, returns base64-encoded PNG instead. |

---

## OCR

### GetOcrText

Read text from an element using OCR. Returns JSON with the full recognized text, angle, bounding rectangle, and a hierarchy of lines and words with their own bounding rectangles.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |

### FindOcrText

Search for text within an element's OCR result using fuzzy matching. Returns a JSON array of matches, each with `matchedText`, `boundingRect`, `editDistance`, `lineText` (for disambiguation), and `lineBoundingRect`.

Use `ClickOcrText` / `DoubleClickOcrText` / `RightClickOcrText` / `HoverOcrText` to interact with matches directly.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |
| `searchText` | string | Yes | Text to search for |
| `maxEdits` | int | No | Maximum edit distance for fuzzy matching (default: 0 = exact) |

### ClickOcrText / DoubleClickOcrText / RightClickOcrText / HoverOcrText

Click, double-click, right-click, or hover on OCR-recognized text within an element. Combines OCR search and action in one step — finds the text using fuzzy matching, then acts at the specified anchor+offset relative to the match's bounding rectangle.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `elementId` | string | Yes | Element ID |
| `searchText` | string | Yes | Text to search for within the OCR result |
| `maxEdits` | int | No | Maximum edit distance for fuzzy matching (default: 0 = exact) |
| `matchIndex` | int | No | Zero-based index when multiple matches exist (default: 0 = first match) |
| `anchor` | string | No | Anchor point on the OCR match bounding rect (default: `Center`). Values: `Center`, `North`, `NorthEast`, `East`, `SouthEast`, `South`, `SouthWest`, `West`, `NorthWest` |
| `offsetX` | int | No | Horizontal offset in pixels from the anchor (default: 0) |
| `offsetY` | int | No | Vertical offset in pixels from the anchor (default: 0) |

---

## Hit-testing

These tools find elements by screen coordinates. They are **not fully reliable** — prefer selectors when possible.

### GetElementsAtPoint

Find all elements whose bounding rectangles contain a screen point. Returns a JSON array of element IDs.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `x` | double | Yes | X coordinate in screen pixels |
| `y` | double | Yes | Y coordinate in screen pixels |

### GetFrontElementAtPoint

Find the front-most (smallest) element at a screen point. Returns its element ID.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `x` | double | Yes | X coordinate in screen pixels |
| `y` | double | Yes | Y coordinate in screen pixels |

---

## Video recording

### StartRecording

Start video recording of an application's window.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |

### StopRecording

Stop video recording and save the video to a file. Returns the full file path.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `appId` | string | Yes | The appId returned by LaunchApp or AttachApp |
| `outputPath` | string | No | Relative path under the video root directory (e.g. `session1/test.mp4`). Subdirectories are created automatically. If omitted, a timestamped filename is generated. |

---

## Selectors

Selectors are used by all Find/Wait tools to locate elements. Supported formats:

| Format | Example | Description |
|---|---|---|
| Attribute | `[automationid=myId]` | Match by UIAutomation attribute |
| Name | `[name=OK]` | Match by element name |
| Text | `text=Save` | Match by visible text |
| Control type | `type=Button` | Match by control type |
| XPath | `//Button[@Name='7']` | XPath expression over the UI tree |
| XPath wildcard | `//*[@AutomationId='result']` | Match any control type |

See the [Driver XPath documentation](../WindowsConductor.DriverFlaUI/XPATH.md) for full XPath syntax details.
