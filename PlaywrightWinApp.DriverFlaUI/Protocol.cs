using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlaywrightWinApp.DriverFlaUI;

/// <summary>
/// Incoming command from the Client.
/// </summary>
public sealed class WinAppRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    /// <summary>
    /// Arbitrary named parameters. Values remain as raw JsonElement so each
    /// command handler can extract them with the typed helpers below.
    /// </summary>
    [JsonPropertyName("params")]
    public Dictionary<string, JsonElement> Params { get; set; } = new();

    public string GetString(string key, string fallback = "") =>
        Params.TryGetValue(key, out var v) ? v.GetString() ?? fallback : fallback;

    public string[] GetStringArray(string key) =>
        Params.TryGetValue(key, out var v)
            ? v.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
            : Array.Empty<string>();

    public int GetInt(string key, int fallback = 0) =>
        Params.TryGetValue(key, out var v) ? v.GetInt32() : fallback;

    public bool GetBool(string key, bool fallback = false) =>
        Params.TryGetValue(key, out var v) ? v.GetBoolean() : fallback;
}

/// <summary>
/// Response sent back to the Client.
/// </summary>
public sealed class WinAppResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public static WinAppResponse Ok(string id, object? result = null) =>
        new() { Id = id, Success = true, Result = result };

    public static WinAppResponse Fail(string id, string error) =>
        new() { Id = id, Success = false, Error = error };
}
