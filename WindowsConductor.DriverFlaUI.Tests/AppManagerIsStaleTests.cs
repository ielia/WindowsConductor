using System.Collections.Concurrent;
using System.Reflection;
using FlaUI.Core.AutomationElements;

namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class AppManagerIsStaleTests
{
    private AppManager _mgr = null!;
    private ConcurrentDictionary<string, AutomationElement> _elements = null!;

    [SetUp]
    public void SetUp()
    {
        _mgr = new AppManager();
        _elements = (ConcurrentDictionary<string, AutomationElement>)
            typeof(AppManager)
                .GetField("_elements", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(_mgr)!;
    }

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
        _elements["stale-el"] = null!;
        Assert.That(_mgr.IsStale("stale-el"), Is.True);
        Assert.That(_elements.ContainsKey("stale-el"), Is.False);
    }

    [Test]
    public void IsStale_CalledTwiceOnStaleElement_ReturnsTrueBothTimes()
    {
        _elements["stale-el"] = null!;
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
        _elements["stale-el"] = null!;
        _mgr.WaitForElementVanish("stale-el", 1000);
        Assert.That(_elements.ContainsKey("stale-el"), Is.False);
    }

    [Test]
    public void WaitForElementVanish_AlreadyEvicted_ReturnsImmediately()
    {
        _elements["stale-el"] = null!;
        _mgr.IsStale("stale-el");
        _mgr.WaitForElementVanish("stale-el", 1000);
    }

    [Test]
    public void WaitForElementVanish_CalledTwiceOnStaleElement_ReturnsBothTimes()
    {
        _elements["stale-el"] = null!;
        _mgr.WaitForElementVanish("stale-el", 1000);
        _mgr.WaitForElementVanish("stale-el", 1000);
    }
}
