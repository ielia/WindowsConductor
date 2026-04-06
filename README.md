# WindowsConductor

Client-server solution for controlling Windows 10/11 desktop applications remotely through a WebSocket connection. Targets native Windows UI elements via UIAutomation (UIA3) and provides a code-based API for launching, inspecting, and interacting with them.

## Architecture

A **Driver** runs on the Windows machine and exposes a WebSocket endpoint. One or more **Clients** connect to it and send JSON commands to automate the desktop. The Driver translates those commands into native UIAutomation calls via FlaUI. Screenshots and video data are streamed over the WebSocket as binary -- no shared filesystem is required.

## Projects

| Project | Description |
|---|---|
| [WindowsConductor.Client](WindowsConductor.Client/README.md) | .NET client library. Async API for connecting to the Driver and automating applications. |
| [WindowsConductor.DriverFlaUI](WindowsConductor.DriverFlaUI/README.md) | Server-side driver. WebSocket endpoint backed by FlaUI.UIA3. |
| [WindowsConductor.InspectorGUI](WindowsConductor.InspectorGUI/README.md) | WPF inspector for interactively exploring the UI element tree. |

Test projects (`*.Tests`) accompany each of the above.

## Quick start

```bash
# 1. Start the Driver
dotnet run --project WindowsConductor.DriverFlaUI

# 2. In another terminal, launch the Inspector (optional)
dotnet run --project WindowsConductor.InspectorGUI

# 3. Or use the Client library from your own code (see Client README)
```

## Build and test

```bash
dotnet build
dotnet test
```

## Scripts

Convenience scripts are provided in `.bat`, `.ps1`, and `.sh` variants:

| Script | Description |
|---|---|
| `run-driver` | Start the Driver on a given port (default 8765). Usage: `run-driver [port]`. |
| `inspector-gui` | Launch the Inspector GUI. |
| `publish` | Publish framework-dependent and self-contained builds of the Driver and Inspector, and pack the Client NuGet package. Output goes to `publish/`. |
| `test-coverage` | Run unit tests with code coverage collection and generate an HTML report in `coverage-report/`. Requires `reportgenerator` as a global dotnet tool. |

## Requirements

- Windows 10/11
- .NET 8 SDK
- ffmpeg on PATH (only for video recording)
