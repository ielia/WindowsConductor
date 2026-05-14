using System.ComponentModel;
using ModelContextProtocol.Server;
using WindowsConductor.Client;

namespace WindowsConductor.MCP.Tools;

[McpServerToolType]
public sealed class SessionTools(ConductorState state)
{
    [McpServerTool, Description(
        "Connect to a running WindowsConductor driver. " +
        "Must be called before any other tool. " +
        "The driver must already be running (start it with: dotnet run --project WindowsConductor.DriverFlaUI).")]
    public async Task<string> Connect(
        [Description("WebSocket URL of the driver (default: ws://localhost:8765/)")] string? wsUri = null,
        [Description("Bearer token for authentication (optional)")] string? authToken = null)
    {
        var session = await state.ConnectAsync(
            wsUri ?? WcDefaults.WebSocketUrl,
            authToken);
        return $"Connected to WindowsConductor driver v{session.ServerVersion}.";
    }

    [McpServerTool, Description("Disconnect from the WindowsConductor driver and close all tracked applications.")]
    public async Task<string> Disconnect()
    {
        await state.DisposeAsync();
        return "Disconnected.";
    }
}
