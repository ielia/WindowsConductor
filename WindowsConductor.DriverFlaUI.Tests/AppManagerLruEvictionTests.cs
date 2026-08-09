namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class AppManagerLruEvictionTests
{
    [Test]
    public void CacheExceedsCapacity_EvictsOldest()
    {
        using var mgr = new AppManager(maxElementCacheSize: 3);
        mgr.InjectElementForTesting("a", null!);
        mgr.InjectElementForTesting("b", null!);
        mgr.InjectElementForTesting("c", null!);
        mgr.InjectElementForTesting("d", null!);

        Assert.That(mgr.ElementCacheContains("a"), Is.False);
        Assert.That(mgr.ElementCacheContains("b"), Is.True);
        Assert.That(mgr.ElementCacheContains("c"), Is.True);
        Assert.That(mgr.ElementCacheContains("d"), Is.True);
        Assert.That(mgr.ElementCacheCount, Is.EqualTo(3));
    }

    [Test]
    public void GetElement_TouchesLru_PreventsEviction()
    {
        using var mgr = new AppManager(maxElementCacheSize: 3);
        mgr.InjectElementForTesting("a", null!);
        mgr.InjectElementForTesting("b", null!);
        mgr.InjectElementForTesting("c", null!);

        // Touch "a" to move it to front
        mgr.GetElement("a");

        // Insert "d" — should evict "b" (now oldest), not "a"
        mgr.InjectElementForTesting("d", null!);

        Assert.That(mgr.ElementCacheContains("a"), Is.True);
        Assert.That(mgr.ElementCacheContains("b"), Is.False);
        Assert.That(mgr.ElementCacheContains("c"), Is.True);
        Assert.That(mgr.ElementCacheContains("d"), Is.True);
    }

    [Test]
    public void TryEvictElement_RemovesFromLru()
    {
        using var mgr = new AppManager(maxElementCacheSize: 3);
        mgr.InjectElementForTesting("a", null!);
        mgr.InjectElementForTesting("b", null!);

        mgr.TryEvictElement("a");

        // Insert two more — should not evict "b" since we're still within capacity
        mgr.InjectElementForTesting("c", null!);
        mgr.InjectElementForTesting("d", null!);

        Assert.That(mgr.ElementCacheContains("a"), Is.False);
        Assert.That(mgr.ElementCacheContains("b"), Is.True);
        Assert.That(mgr.ElementCacheContains("c"), Is.True);
        Assert.That(mgr.ElementCacheContains("d"), Is.True);
        Assert.That(mgr.ElementCacheCount, Is.EqualTo(3));
    }

    [Test]
    public void HighVolume_MaintainsCapacity()
    {
        using var mgr = new AppManager(maxElementCacheSize: 10);
        for (int i = 0; i < 100; i++)
            mgr.InjectElementForTesting($"el-{i}", null!);

        Assert.That(mgr.ElementCacheCount, Is.EqualTo(10));

        // Only the last 10 should survive
        for (int i = 90; i < 100; i++)
            Assert.That(mgr.ElementCacheContains($"el-{i}"), Is.True);
        for (int i = 0; i < 90; i++)
            Assert.That(mgr.ElementCacheContains($"el-{i}"), Is.False);
    }
}
