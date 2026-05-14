using NUnit.Framework;
using WindowsConductor.MCP.Tools;

namespace WindowsConductor.MCP.Tests;

[TestFixture]
[Category("Unit")]
public class SessionToolsTests
{
    private ConductorState _state = null!;
    private SessionTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _state = new ConductorState();
        _tools = new SessionTools(_state);
    }

    [TearDown]
    public async Task TearDown() => await _state.DisposeAsync();

    [Test]
    public async Task Disconnect_WhenNotConnected_DoesNotThrow()
    {
        var result = await _tools.Disconnect();
        Assert.That(result, Is.EqualTo("Disconnected."));
    }

    [Test]
    public async Task Disconnect_ClearsSession()
    {
        // Even without a real connection, dispose should work cleanly
        await _tools.Disconnect();
        Assert.That(_state.Session, Is.Null);
    }
}
