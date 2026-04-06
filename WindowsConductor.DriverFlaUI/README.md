# WindowsConductor.DriverFlaUI

Server-side driver that exposes Windows desktop UI automation over a WebSocket endpoint. Built on FlaUI (UIA3). Receives JSON commands from `WindowsConductor.Client` and translates them into native UIAutomation calls.

## Running

```bash
# Default port (8765)
dotnet run --project WindowsConductor.DriverFlaUI

# Custom port
dotnet run --project WindowsConductor.DriverFlaUI -- "http://localhost:9000/"

# Restrict navigation to the launched/attached application's process
dotnet run --project WindowsConductor.DriverFlaUI -- --confine-to-app
```

Or use the convenience scripts at the repository root: `run-driver.bat`, `run-driver.ps1`, `run-driver.sh`.

## What it does

- Listens for WebSocket connections on the configured HTTP prefix.
- Each client gets an isolated session with its own element cache.
- Supports launching, attaching, element lookup (attribute selectors and XPath), clicks, typing, screenshots, and video recording.
- Screenshots and video data are sent as binary (base64-encoded in JSON) over the WebSocket -- no shared filesystem required between Driver and Client.

## Requirements

- Windows 10/11
- .NET 8 runtime
- ffmpeg on PATH (only for video recording)
