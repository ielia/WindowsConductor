using System.Text.Json;

namespace WindowsConductor.Client;

/// <summary>
/// Abstraction over the command transport to the Driver.
/// Implemented by <see cref="WcSession"/>; also used for testing.
/// </summary>
internal interface IWcTransport
{
    Task<JsonElement> SendAsync(string command, object? @params, CancellationToken ct = default);
}
