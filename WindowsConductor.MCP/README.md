# WindowsConductor.MCP

MCP (Model Context Protocol) server that exposes the full WindowsConductor API as tools for AI assistants. Communicates over **stdio** transport — no network exposure required.

## Prerequisites

- Windows 10/11
- .NET 8 SDK
- A running WindowsConductor Driver (`dotnet run --project WindowsConductor.DriverFlaUI`)
- ffmpeg on PATH (only for video recording)

## Running

```bash
dotnet run --project WindowsConductor.MCP
```

### Command-line options

| Option | Description | Default |
|---|---|---|
| `--video-dir <path>` | Root directory for saved video recordings | `%TEMP%/WindowsConductor/recordings/` |
| `--screenshot-dir <path>` | Root directory for saved screenshots | `%TEMP%/WindowsConductor/screenshots/` |

Example:

```bash
dotnet run --project WindowsConductor.MCP -- --video-dir "D:/recordings" --screenshot-dir "D:/screenshots"
```

## Connecting from AI clients

### Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "windows-conductor": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/WindowsConductor.MCP"]
    }
  }
}
```

### Other MCP-compatible clients

Any client that supports stdio-based MCP servers can connect by spawning the process and communicating over stdin/stdout.

## Typical workflow

1. The AI calls **Connect** to establish a WebSocket connection to the Driver.
2. It calls **LaunchApp** or **AttachApp** to get an `appId`.
3. It uses **FindElement** / **FindElements** with selectors to locate UI elements.
4. It interacts with elements via **ClickElement**, **TypeText**, **HitKeys**, etc.
5. It can inspect elements with **GetText**, **GetAttribute**, **GetBoundingRect**, etc.
6. It can use **WaitForElement** / **WaitForVanish** to handle dynamic UI.
7. When done, it calls **CloseApp** and **Disconnect**.

## Tools reference

See [TOOLS.md](TOOLS.md) for the complete list of available tools and their parameters.

## Architecture

The MCP server is a thin layer over the `WindowsConductor.Client` library:

```
AI Client  ←stdio→  MCP Server  ←WebSocket→  Driver  ←UIA3→  Windows Desktop
```

The server maintains a `ConductorState` singleton that tracks the WebSocket session and all launched/attached applications across tool calls.

## Adding SSE transport

The server is designed so that SSE (HTTP-based) transport can be added later by swapping `WithStdioServerTransport()` for `WithHttpServerTransport()` in `Program.cs`. SSE transport requires OAuth 2.1 authentication — see the [MCP specification](https://modelcontextprotocol.io/) for details.
