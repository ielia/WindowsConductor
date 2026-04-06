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

Type `help` for a full list of commands. Click on the screenshot to select the frontmost element at that point.

## Features

- Live screenshot of the inspected window with blinking highlight on the selected element.
- Attribute panel showing all UIAutomation properties of the selected element.
- Multi-match navigation (Alt+Left / Alt+Right) when a selector matches several elements.
- Tab-completion and command history.
