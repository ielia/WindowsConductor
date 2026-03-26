using System.Text.Json;

namespace WindowsConductor.Client.Tests;

/// <summary>
/// Test double for <see cref="IWcTransport"/>.
/// Records every command sent and returns preconfigured responses.
/// </summary>
internal sealed class FakeTransport : IWcTransport
{
    public record Call(string Command, string ParamsJson);

    private readonly Queue<JsonElement> _responses = new();
    private readonly List<Call> _calls = new();

    public IReadOnlyList<Call> Calls => _calls;

    public void Enqueue(object? result)
    {
        var json = JsonSerializer.Serialize(result);
        _responses.Enqueue(JsonDocument.Parse(json).RootElement.Clone());
    }

    public Task<JsonElement> SendAsync(string command, object? @params, CancellationToken ct = default)
    {
        var paramsJson = JsonSerializer.Serialize(@params);
        _calls.Add(new Call(command, paramsJson));

        if (_responses.Count == 0)
            return Task.FromResult(default(JsonElement));

        return Task.FromResult(_responses.Dequeue());
    }
}