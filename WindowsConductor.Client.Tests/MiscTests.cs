using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

[TestFixture]
[Category("Unit")]
public class BoundingRectTests
{
    [Test]
    public void Constructor_SetsAllProperties()
    {
        var r = new BoundingRect(10, 20, 300, 400);
        Assert.That(r.X, Is.EqualTo(10));
        Assert.That(r.Y, Is.EqualTo(20));
        Assert.That(r.Width, Is.EqualTo(300));
        Assert.That(r.Height, Is.EqualTo(400));
    }

    [Test]
    public void Equality_SameValues_AreEqual()
    {
        var a = new BoundingRect(1, 2, 3, 4);
        var b = new BoundingRect(1, 2, 3, 4);
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a == b, Is.True);
    }

    [Test]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new BoundingRect(1, 2, 3, 4);
        var b = new BoundingRect(1, 2, 3, 5);
        Assert.That(a, Is.Not.EqualTo(b));
        Assert.That(a != b, Is.True);
    }

    [Test]
    public void ToString_ContainsAllValues()
    {
        var r = new BoundingRect(10, 20, 300, 400);
        var str = r.ToString();
        Assert.That(str, Does.Contain("10"));
        Assert.That(str, Does.Contain("20"));
        Assert.That(str, Does.Contain("300"));
        Assert.That(str, Does.Contain("400"));
    }

    [Test]
    public void Deconstruct_ReturnsAllValues()
    {
        var (x, y, w, h) = new BoundingRect(5, 10, 100, 200);
        Assert.That(x, Is.EqualTo(5));
        Assert.That(y, Is.EqualTo(10));
        Assert.That(w, Is.EqualTo(100));
        Assert.That(h, Is.EqualTo(200));
    }

    [Test]
    public void GetHashCode_SameValues_SameHash()
    {
        var a = new BoundingRect(1, 2, 3, 4);
        var b = new BoundingRect(1, 2, 3, 4);
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }
}

[TestFixture]
[Category("Unit")]
public class WcExceptionTests
{
    [Test]
    public void Constructor_SetsMessage()
    {
        var ex = new WcException("test error");
        Assert.That(ex.Message, Is.EqualTo("test error"));
    }

    [Test]
    public void IsException()
    {
        var ex = new WcException("err");
        Assert.That(ex, Is.InstanceOf<Exception>());
    }
}

[TestFixture]
[Category("Unit")]
public class WcElementTests
{
    [Test]
    public void ToString_ContainsElementId()
    {
        var el = new WcElement("abc123", null!);
        Assert.That(el.ToString(), Is.EqualTo("WcElement(abc123)"));
    }

    [Test]
    public void ElementId_ReturnsConstructorValue()
    {
        var el = new WcElement("xyz", null!);
        Assert.That(el.ElementId, Is.EqualTo("xyz"));
    }
}

[TestFixture]
[Category("Unit")]
public class ClientProtocolTests
{
    [Test]
    public void WcRequest_CanBeCreated()
    {
        var req = new WcRequest { Id = "1", Command = "click", Params = new { elementId = "e1" } };
        Assert.That(req.Id, Is.EqualTo("1"));
        Assert.That(req.Command, Is.EqualTo("click"));
        Assert.That(req.Params, Is.Not.Null);
    }

    [Test]
    public void WcRequest_RoundTripsViaJson()
    {
        var req = new WcRequest { Id = "abc", Command = "launch", Params = new { path = "calc.exe" } };
        var json = System.Text.Json.JsonSerializer.Serialize(req);
        Assert.That(json, Does.Contain("\"id\":\"abc\""));
        Assert.That(json, Does.Contain("\"command\":\"launch\""));
    }

    [Test]
    public void WcResponse_DefaultValues()
    {
        var resp = new WcResponse();
        Assert.That(resp.Id, Is.EqualTo(""));
        Assert.That(resp.Success, Is.False);
        Assert.That(resp.Result, Is.Null);
        Assert.That(resp.Error, Is.Null);
    }

    [Test]
    public void WcResponse_CanSetProperties()
    {
        var resp = new WcResponse { Id = "r1", Success = true, Error = "none" };
        Assert.That(resp.Id, Is.EqualTo("r1"));
        Assert.That(resp.Success, Is.True);
        Assert.That(resp.Error, Is.EqualTo("none"));
    }

    [Test]
    public void WcResponse_RoundTripsViaJson()
    {
        var resp = new WcResponse { Id = "x", Success = true };
        var json = System.Text.Json.JsonSerializer.Serialize(resp);
        var back = System.Text.Json.JsonSerializer.Deserialize<WcResponse>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.That(back.Id, Is.EqualTo("x"));
        Assert.That(back.Success, Is.True);
    }
}
