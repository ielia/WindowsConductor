using System.Text.Json;

namespace WindowsConductor.Client.Tests;

/// <summary>
/// Test double for <see cref="IWcTransport"/>.
/// Records every command sent and returns preconfigured responses.
/// </summary>
internal sealed class FakeTransport : IWcTransport
{
    public sealed record Call(string Command, string ParamsJson);

    private readonly Queue<object> _responses = new();
    private readonly List<Call> _calls = new();

    public IReadOnlyList<Call> Calls => _calls;

    public void Enqueue(object? result)
    {
        var json = JsonSerializer.Serialize(result);
        _responses.Enqueue(JsonDocument.Parse(json).RootElement.Clone());
    }

    public void EnqueueException(Exception exception) =>
        _responses.Enqueue(exception);

    public Task<JsonElement> SendAsync(string command, object? parameters, CancellationToken ct = default)
    {
        var paramsJson = JsonSerializer.Serialize(parameters);
        _calls.Add(new Call(command, paramsJson));

        if (_responses.Count == 0)
            return Task.FromResult(default(JsonElement));

        var next = _responses.Dequeue();
        if (next is Exception ex)
            return Task.FromException<JsonElement>(ex);

        return Task.FromResult((JsonElement)next);
    }
}