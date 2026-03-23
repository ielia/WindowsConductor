using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlaywrightWinApp.Client;

/// <summary>
/// Command sent by the Client to the Driver over WebSocket.
/// </summary>
internal sealed class WinAppRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>
    /// Serialized as a JSON object.  Anonymous C# objects are accepted here
    /// and are serialized with camelCase property names.
    /// </summary>
    [JsonPropertyName("params")]
    public object? Params { get; init; }
}

/// <summary>
/// Response received from the Driver.
/// </summary>
internal sealed class WinAppResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Raw JSON token; callers inspect its <see cref="JsonValueKind"/> to extract
    /// the concrete value (string, bool, array …).
    /// </summary>
    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
