using System.Text.Json;
using WindowsConductor.Client;

namespace WindowsConductor.MCP.Tests;

internal sealed class FakeTransport : IWcTransport
{
    public sealed record Call(string Command, string ParamsJson);

    private readonly Queue<JsonElement> _responses = new();
    private readonly List<Call> _calls = new();

    public IReadOnlyList<Call> Calls => _calls;

    public void Enqueue(object? result)
    {
        var json = JsonSerializer.Serialize(result);
        _responses.Enqueue(JsonDocument.Parse(json).RootElement.Clone());
    }

    public Task<JsonElement> SendAsync(string command, object? parameters, CancellationToken ct = default)
    {
        var paramsJson = JsonSerializer.Serialize(parameters);
        _calls.Add(new Call(command, paramsJson));

        if (_responses.Count == 0)
            return Task.FromResult(default(JsonElement));

        return Task.FromResult(_responses.Dequeue());
    }
}
