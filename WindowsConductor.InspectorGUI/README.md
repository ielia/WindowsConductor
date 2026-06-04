# WindowsConductor.InspectorGUI

WPF desktop application for interactively inspecting Windows UI elements through the WindowsConductor Driver. Provides a command-line interface with live screenshot display, element highlighting, and attribute inspection.

## Running

```bash
dotnet run --project WindowsConductor.InspectorGUI
```

Or use the convenience scripts at the repository root: `inspector-gui.bat`, `inspector-gui.ps1`, `inspector-gui.sh`.

## Usage

1. Start the Driver (see `WindowsConductor.DriverFlaUI`).
2. Launch the Inspector.
3. Use the command input at the bottom of the window:

```
connect ws://localhost:8765/
attach Calculator
locate type=Button
click
parent
```

Type `help` for a full list of commands, or `help <command>` for detailed help on a specific command. Click on the screenshot to select the frontmost element at that point.

Multiple commands can be chained with `;`:

```
locate "//button"; click; sleep 1000; nextmatch; click
```

## Commands

### Connection

| Command | Description |
|---|---|
| `connect [url] [authToken]` | Connect to the driver (default: `ws://localhost:8765/`). |
| `disconnect` | Disconnect from the driver. |

### Application lifecycle

| Command | Description |
|---|---|
| `launch <path> ["arg1", ...] [detachedTitleRegex] [mainWindowTimeout]` | Launch and attach to an application. |
| `attach <mainWindowTitleRegex> [mainWindowTimeout]` | Attach to a running application by window title. |
| `detach` | Detach from the current application without closing it. |
| `close` | Close the current application. |

### Element selection

| Command | Description |
|---|---|
| `locate <selector> [>> <selector> ...]` | Find elements matching a selector chain. |
| `resolve <xpath>` | Evaluate an XPath expression and print the result in YAML format. |
| `matchindex <N>` | Select the Nth match (1-based). |
| `nextmatch [N]` | Move forward N matches (default 1), cycling around. |
| `prevmatch [N]` | Move back N matches (default 1), cycling around. |
| `unselect` | Clear the current element selection. |
| `reset` | Unselect and re-select the application root. |
| `parent` | Navigate to the parent of the selected element. |
| `children` | Locate all direct children of the selected element. |

### Mouse actions

| Command | Description |
|---|---|
| `click ["ocrText" [maxDistance] [#matchIndex]] [<anchor> (<x>, <y>)]` | Click the selected element, or click OCR-matched text. |
| `doubleclick ["ocrText" [maxDistance] [#matchIndex]] [<anchor> (<x>, <y>)]` | Double-click the selected element or OCR text. |
| `rightclick ["ocrText" [maxDistance] [#matchIndex]] [<anchor> (<x>, <y>)]` | Right-click the selected element or OCR text. |
| `hover ["ocrText" [maxDistance] [#matchIndex]] [<anchor> (<x>, <y>)]` | Hover over the selected element or OCR text. |
| `drag [<anchor> (<x>, <y>)] to <locator> [<anchor> (<x>, <y>)]` | Drag the selected element to a target element. |
| `scroll <lines> [horizontal]` | Scroll the mouse wheel (positive = down/right). |

OCR text must be quoted. `maxDistance` sets the fuzzy match tolerance (default: 0 = exact). `#matchIndex` selects which match to act on (0-based) when multiple matches exist.

Anchors for `click`, `doubleclick`, `rightclick`, `hover`, and `drag`: `center`, `north`, `northeast`, `east`, `southeast`, `south`, `southwest`, `west`, `northwest`.

### Keyboard

| Command | Description |
|---|---|
| `hitkeys {key}+` | Press and release keyboard keys simultaneously. |
| `ghitkeys {key}+` | Send keyboard keys globally (no target element). |
| `type <text> [[{ctrl,alt,shift,meta}+]]` | Type text into the selected element with optional modifiers. |
| `gtype <text> [[{ctrl,alt,shift,meta}+]]` | Type text globally with optional modifiers. |

### Inspection

| Command | Description |
|---|---|
| `attribute <name|*>` | Get a named UIAutomation property, or `*` for all. |
| `setattribute <name> <value>` | Set a UIAutomation pattern property (e.g. `toggle_togglestate`, `ischecked`, `value_value`). |
| `text` | Get the visible text of the selected element. |
| `ocr` | Perform OCR on the selected element and print recognized text. |
| `screenshot` | Capture a screenshot of the selected element. |
| `snapshot` | Open snapshot mode for the selected element's subtree. |
| `windowstate [state]` | Get or set the window state (`normal`, `maximized`, `minimized`, etc.). |

### Other

| Command | Description |
|---|---|
| `focus` | Set keyboard focus on the selected element. |
| `foreground` | Bring the selected element's window to the foreground. |
| `refresh` | Refresh the screenshot and attributes. |
| `clear` | Clear the output log. |
| `sleep <ms>` | Pause for the specified number of milliseconds. |
| `help [command]` | Show help for all commands or a specific command. |
| `exit` / `quit` | Disconnect and exit the inspector. |

## Key bindings

| Key | Action |
|---|---|
| `Shift+PgUp` / `Shift+PgDown` | Scroll log |
| `Up` / `Down Arrow` | Command history |
| `Tab` | Autocomplete command |
| `Ctrl+Tab` / `Shift+Ctrl+Tab` | Cycle panels |
| `Alt+Left` / `Alt+Right` | Previous / next match |
| `Alt+B` | Go back |
| `Alt+C` | Copy attributes |
| `Alt+L` | Toggle clickless mode |
| `Alt+R` | Refresh |
| `Alt+S` | Stop sleep and remaining commands |

## Features

- Live screenshot of the inspected window with blinking highlight on the selected element.
- Attribute panel showing all UIAutomation properties of the selected element.
- Multi-match navigation when a selector matches several elements.
- Tab-completion and command history.
- Command chaining with `;` for scripting sequences.
- Clickless mode for inspecting elements without clicking them.
