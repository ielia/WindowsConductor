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
dotnet add package WindowsConductor.Client --version 0.1.0
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

- `WcSession` -- WebSocket connection to the Driver.
- `WcApp` -- A launched or attached application.
- `WcLocator` -- Lazy element selector that re-queries on each call.
- `WcElement` -- Resolved element handle for direct interaction.

Screenshots return `SkiaSharp.SKBitmap`. Raw PNG bytes are available via `ScreenshotBytesAsync()`. Video recordings return `byte[]` on stop.
