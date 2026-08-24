# WindowsConductor.DriverFlaUI

Server-side driver that exposes Windows desktop UI automation over a WebSocket endpoint. Built on FlaUI (UIA3). Receives JSON commands from `WindowsConductor.Client` and translates them into native UIAutomation calls.

## Running

```bash
# Default port (8765)
dotnet run --project WindowsConductor.DriverFlaUI

# Custom port
dotnet run --project WindowsConductor.DriverFlaUI -- 9000

# Restrict navigation to the launched/attached application's process
dotnet run --project WindowsConductor.DriverFlaUI -- --confine-to-app

# Specify the path to ffmpeg (overrides FFMPEG_PATH env var)
dotnet run --project WindowsConductor.DriverFlaUI -- --ffmpeg-path "C:\tools\ffmpeg.exe"

# Bearer token authentication
dotnet run --project WindowsConductor.DriverFlaUI -- --auth-token MY_SECRET

# Enable file logging
dotnet run --project WindowsConductor.DriverFlaUI -- --log-file driver.log

# TLS with a self-signed certificate
dotnet run --project WindowsConductor.DriverFlaUI -- --tls-port 8443 --cert-self-signed

# TLS with a PFX certificate, plain HTTP disabled
dotnet run --project WindowsConductor.DriverFlaUI -- --tls-port 8443 --tls-only --cert server.pfx --cert-password secret
```

Or use the convenience scripts at the repository root: `run-driver.bat`, `run-driver.ps1`, `run-driver.sh`.

### CLI flags

| Flag | Description |
|---|---|
| `[port]` | Listening port for HTTP/WS (default: 8765). Positional argument. |
| `--confine-to-app` | Prevent locators from navigating above the application root. |
| `--ffmpeg-path <path>` | Path to the ffmpeg executable (overrides `FFMPEG_PATH` env var). |
| `--log-file <path>` | Path to a log file. Enables file logging (Debug level) in addition to console (Information level). |
| `--auth-token <token>` | Plain bearer token required for client connections. |
| `--auth-token-file <file>` | File containing a plain bearer token. |
| `--hash-token <salt:iter:hash>` | PBKDF2 triplet (base64) for token validation. |
| `--hash-token-file <file>` | File containing a PBKDF2 triplet. |
| `--tls-port <port>` | Port for HTTPS/WSS listener (requires a certificate option). |
| `--tls-only` | Disable plain HTTP listener (requires `--tls-port`). |
| `--cert <path>` | Path to a `.pfx`/`.p12` or `.pem` certificate file. |
| `--cert-key <path>` | Path to PEM private key file (only with a PEM `--cert`). |
| `--cert-password <password>` | Password for encrypted `.pfx` or PEM key. |
| `--cert-password-file <file>` | File containing the certificate password. |
| `--cert-thumbprint <hex>` | Load certificate from `CurrentUser\My` store by thumbprint. |
| `--cert-self-signed` | Generate an ephemeral self-signed certificate at startup. |
| `--max-concurrency <n>` | Maximum concurrent requests per session (default: 8). |
| `--max-element-cache <n>` | Maximum cached element handles per session before LRU eviction (default: 100000). |

## Logging

The driver uses [Serilog](https://serilog.net/) for structured logging. By default only the console sink is active at `Information` level. When `--log-file` is provided, a file sink is added at `Debug` level with daily rolling, 7-day retention, and a 50 MB size limit.

Both sinks are configured via `appsettings.json` (copied alongside the executable). You can customise output templates, minimum levels, rolling intervals, and source-context overrides without recompiling.

## What it does

- Listens on all network interfaces (`0.0.0.0`) for WebSocket connections on the configured port (and optionally HTTPS/WSS).
- Optionally authenticates clients via bearer token (plain or PBKDF2 hashed).
- Each client gets an isolated session with its own element cache.
- Translates JSON commands into native UIAutomation calls via FlaUI.
- Screenshots and video data are sent as binary (base64-encoded in JSON) over the WebSocket — no shared filesystem required.

## WebSocket commands

All commands are sent as JSON objects with a `command` field and a `params` object.

### Session

| Command | Description |
|---|---|
| `version` | Returns the driver version. |

### Application lifecycle

| Command | Description |
|---|---|
| `launch` | Launch an application by executable path. |
| `attach` | Attach to a running application by window title regex. |
| `close` | Close a tracked application. |

### Finding elements

| Command | Description |
|---|---|
| `findElement` | Find a single element by selector. |
| `findElements` | Find all elements matching a selector. |
| `findElementByIndex` | Find the element at a specific index among all matches. |
| `countElements` | Count elements matching a selector. |
| `resolveValue` | Evaluate a selector and return typed values. |
| `findElementsAtPoint` | Find elements whose bounding rect contains a screen point. |
| `findFrontElementAtPoint` | Find the front-most (smallest) element at a screen point. |

### Waiting

| Command | Description |
|---|---|
| `waitForElement` | Wait for a single element to appear. |
| `waitForElements` | Wait for at least one element to appear. |
| `waitForResolvedValue` | Wait for a selector to resolve to a non-empty value. |
| `waitForVanish` | Wait for all matching elements to disappear. |
| `waitForVisible` | Wait for an element to become visible (not offscreen). Locator or element ID. |
| `waitForHidden` | Wait for an element to become hidden (offscreen). Locator or element ID. |

### Mouse interaction

| Command | Description |
|---|---|
| `click` | Click an element (supports anchor + offset). |
| `doubleClick` | Double-click an element (supports anchor + offset). |
| `rightClick` | Right-click an element (supports anchor + offset). |
| `hover` | Hover over an element (supports anchor + offset). |
| `dragTo` | Drag from one element to another (supports anchor + offset on both). |
| `scroll` | Scroll the mouse wheel over an element. |

### Keyboard

| Command | Description |
|---|---|
| `hitKeys` | Press keys on an element. |
| `typeText` | Type text into an element. |
| `globalHitKeys` | Press keys globally (no target element). |
| `globalTypeText` | Type text globally (no target element). |

### Element inspection

| Command | Description |
|---|---|
| `getText` | Get the visible text of an element. |
| `getAttribute` | Get a single UIAutomation attribute. |
| `getAttributes` | Get all UIAutomation attributes. |
| `setAttribute` | Set a UIAutomation pattern property (toggle, expand/collapse, value, etc.). |
| `isEnabled` | Check if an element is enabled. |
| `isVisible` | Check if an element is visible. |
| `getBoundingRect` | Get the bounding rectangle of an element. |
| `getWindowBoundingRect` | Get the bounding rectangle of an element's top-level window. |
| `getWindowTitle` | Get the title of an application's main window. |
| `getWindowState` | Get the window state (Normal, Maximized, etc.). |
| `setWindowState` | Set the window state. |

### Focus and foreground

| Command | Description |
|---|---|
| `focus` | Focus an element. |
| `setForeground` | Bring an element's window to the foreground. |

### Tree navigation

| Command | Description |
|---|---|
| `getParent` | Get the parent of an element. |
| `getTopLevelWindow` | Get the top-level window containing an element. |
| `getChildren` | Get direct children of an element. |
| `getDescendants` | Get the full descendant tree of an element. |

### OCR

| Command | Description |
|---|---|
| `getOcrText` | Read text from an element using OCR. |

### Screenshots and video

| Command | Description |
|---|---|
| `screenshot` | Take a screenshot of an element. |
| `screenshotApp` | Take a screenshot of an application's main window. |
| `desktopScreenshot` | Take a screenshot of the entire desktop. |
| `startRecording` | Start video recording of an application's window. |
| `stopRecording` | Stop video recording and return the video data. |

## Requirements

- Windows 10/11
- .NET 8 runtime
- ffmpeg on PATH (only for video recording)
