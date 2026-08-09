namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class AppManagerIsStaleTests
{
    private AppManager _mgr = null!;

    [SetUp]
    public void SetUp() => _mgr = new AppManager();

    [TearDown]
    public void TearDown() => _mgr.Dispose();

    [Test]
    public void IsStale_UnknownElementId_ReturnsTrue()
    {
        Assert.That(_mgr.IsStale("does-not-exist"), Is.True);
    }

    [Test]
    public void IsStale_StaleElement_ReturnsTrueAndEvicts()
    {
        _mgr.InjectElementForTesting("stale-el", null!);
        Assert.That(_mgr.IsStale("stale-el"), Is.True);
        Assert.That(_mgr.ElementCacheContains("stale-el"), Is.False);
    }

    [Test]
    public void IsStale_CalledTwiceOnStaleElement_ReturnsTrueBothTimes()
    {
        _mgr.InjectElementForTesting("stale-el", null!);
        Assert.That(_mgr.IsStale("stale-el"), Is.True);
        Assert.That(_mgr.IsStale("stale-el"), Is.True);
    }

    [Test]
    public void WaitForElementVanish_UnknownElementId_ReturnsImmediately()
    {
        _mgr.WaitForElementVanish("does-not-exist", 1000);
    }

    [Test]
    public void WaitForElementVanish_StaleElement_ReturnsAndEvicts()
    {
        _mgr.InjectElementForTesting("stale-el", null!);
        _mgr.WaitForElementVanish("stale-el", 1000);
        Assert.That(_mgr.ElementCacheContains("stale-el"), Is.False);
    }

    [Test]
    public void WaitForElementVanish_AlreadyEvicted_ReturnsImmediately()
    {
        _mgr.InjectElementForTesting("stale-el", null!);
        _mgr.IsStale("stale-el");
        _mgr.WaitForElementVanish("stale-el", 1000);
    }

    [Test]
    public void WaitForElementVanish_CalledTwiceOnStaleElement_ReturnsBothTimes()
    {
        _mgr.InjectElementForTesting("stale-el", null!);
        _mgr.WaitForElementVanish("stale-el", 1000);
        _mgr.WaitForElementVanish("stale-el", 1000);
    }

    [Test]
    public void TryEvictElement_RemovesFromCache()
    {
        _mgr.InjectElementForTesting("el-1", null!);
        _mgr.TryEvictElement("el-1");
        Assert.That(_mgr.ElementCacheContains("el-1"), Is.False);
    }

    [Test]
    public void TryEvictElement_UnknownId_DoesNotThrow()
    {
        _mgr.TryEvictElement("does-not-exist");
    }
}
