using NUnit.Framework;
using WindowsConductor.Client;

namespace WindowsConductor.MCP.Tests;

[TestFixture]
[Category("Unit")]
public class ConductorStateTests
{
    private ConductorState _state = null!;

    [SetUp]
    public void SetUp() => _state = new ConductorState();

    [TearDown]
    public async Task TearDown() => await _state.DisposeAsync();

    [Test]
    public void RequireSession_WhenNotConnected_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _state.RequireSession());
    }

    [Test]
    public void TrackApp_And_GetApp_RoundTrips()
    {
        var transport = new FakeTransport();
        var app = new WcApp("app-1", transport);
        _state.TrackApp("app-1", app);

        var retrieved = _state.GetApp("app-1");
        Assert.That(retrieved, Is.SameAs(app));
    }

    [Test]
    public void GetApp_UnknownId_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _state.GetApp("nonexistent"));
    }

    [Test]
    public void TryRemoveApp_ExistingApp_ReturnsTrue()
    {
        var transport = new FakeTransport();
        var app = new WcApp("app-1", transport);
        _state.TrackApp("app-1", app);

        Assert.That(_state.TryRemoveApp("app-1"), Is.True);
    }

    [Test]
    public void TryRemoveApp_UnknownApp_ReturnsFalse()
    {
        Assert.That(_state.TryRemoveApp("nonexistent"), Is.False);
    }

    [Test]
    public void AppIds_ReturnsTrackedIds()
    {
        var transport = new FakeTransport();
        _state.TrackApp("a", new WcApp("a", transport));
        _state.TrackApp("b", new WcApp("b", transport));

        Assert.That(_state.AppIds, Is.EquivalentTo(new[] { "a", "b" }));
    }

    [Test]
    public void AppIds_WhenEmpty_ReturnsEmpty()
    {
        Assert.That(_state.AppIds, Is.Empty);
    }

    [Test]
    public async Task DisposeAsync_ClearsAppsAndSession()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport, ownsApp: false));

        await _state.DisposeAsync();

        Assert.That(_state.Session, Is.Null);
        Assert.That(_state.AppIds, Is.Empty);
    }

    [Test]
    public void TrackApp_OverwritesExistingId()
    {
        var transport = new FakeTransport();
        var app1 = new WcApp("app-1", transport);
        var app2 = new WcApp("app-1", transport);

        _state.TrackApp("app-1", app1);
        _state.TrackApp("app-1", app2);

        Assert.That(_state.GetApp("app-1"), Is.SameAs(app2));
    }

    [Test]
    public void GetApp_AfterRemove_Throws()
    {
        var transport = new FakeTransport();
        _state.TrackApp("app-1", new WcApp("app-1", transport));
        _state.TryRemoveApp("app-1");

        Assert.Throws<InvalidOperationException>(() => _state.GetApp("app-1"));
    }
}
