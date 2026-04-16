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

    [Test]
    public void Contains_PointInside_ReturnsTrue()
    {
        var r = new BoundingRect(10, 20, 100, 50);
        Assert.That(r.Contains(50, 40), Is.True);
    }

    [Test]
    public void Contains_PointOnEdge_ReturnsTrue()
    {
        var r = new BoundingRect(10, 20, 100, 50);
        Assert.That(r.Contains(10, 20), Is.True);
        Assert.That(r.Contains(110, 70), Is.True);
    }

    [Test]
    public void Contains_PointOutside_ReturnsFalse()
    {
        var r = new BoundingRect(10, 20, 100, 50);
        Assert.That(r.Contains(5, 40), Is.False);
        Assert.That(r.Contains(50, 15), Is.False);
        Assert.That(r.Contains(111, 40), Is.False);
        Assert.That(r.Contains(50, 71), Is.False);
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

    [Test]
    public void Locator_WithoutAppId_Throws()
    {
        var el = new WcElement("el-1", null!);
        Assert.Throws<InvalidOperationException>(() => el.Locator("./Button"));
    }

    [Test]
    public void Locator_WithAppId_ReturnsLocator()
    {
        var el = new WcElement("el-1", null!, appId: "app-1");
        var locator = el.Locator(".//Button");
        Assert.That(locator, Is.Not.Null);
        Assert.That(locator.ToString(), Does.Contain(".//Button"));
    }
}

[TestFixture]
[Category("Unit")]
public class WcAttrTests
{
    private static readonly WcElement DummyElement = new("el-1", null!);

    [TestCase(WcAttrType.BoolValue, true)]
    [TestCase(WcAttrType.IntValue, 42)]
    [TestCase(WcAttrType.DoubleValue, 3.14)]
    [TestCase(WcAttrType.LongValue, 123L)]
    [TestCase(WcAttrType.StringValue, "hello")]
    [TestCase(WcAttrType.NullValue, null)]
    public void Constructor_SetsTypeProperty(WcAttrType type, object? value)
    {
        var attr = new WcAttr(DummyElement, "TestAttr", type, value);
        Assert.That(attr.Type, Is.EqualTo(type));
        Assert.That(attr.Name, Is.EqualTo("TestAttr"));
        Assert.That(attr.Element, Is.SameAs(DummyElement));
        Assert.That(attr.Value, Is.EqualTo(value));
    }

    [Test]
    public void Constructor_SetsDateOnlyType()
    {
        var date = new DateOnly(2026, 4, 16);
        var attr = new WcAttr(DummyElement, "d", WcAttrType.DateOnlyValue, date);
        Assert.That(attr.Type, Is.EqualTo(WcAttrType.DateOnlyValue));
        Assert.That(attr.Value, Is.EqualTo(date));
    }

    [Test]
    public void Constructor_SetsDateTimeType()
    {
        var dt = new DateTime(2026, 4, 16, 10, 30, 0);
        var attr = new WcAttr(DummyElement, "dt", WcAttrType.DateTimeValue, dt);
        Assert.That(attr.Type, Is.EqualTo(WcAttrType.DateTimeValue));
        Assert.That(attr.Value, Is.EqualTo(dt));
    }

    [Test]
    public void Constructor_SetsTimeOnlyType()
    {
        var time = new TimeOnly(14, 30);
        var attr = new WcAttr(DummyElement, "t", WcAttrType.TimeOnlyValue, time);
        Assert.That(attr.Type, Is.EqualTo(WcAttrType.TimeOnlyValue));
        Assert.That(attr.Value, Is.EqualTo(time));
    }

    [Test]
    public void Constructor_SetsTimeSpanType()
    {
        var span = TimeSpan.FromMinutes(90);
        var attr = new WcAttr(DummyElement, "ts", WcAttrType.TimeSpanValue, span);
        Assert.That(attr.Type, Is.EqualTo(WcAttrType.TimeSpanValue));
        Assert.That(attr.Value, Is.EqualTo(span));
    }

    [Test]
    public void Type_CoversAllEnumValues_WithNull()
    {
        var allTypes = Enum.GetValues<WcAttrType>();
        foreach (var type in allTypes)
        {
            var attr = new WcAttr(DummyElement, "x", type, null);
            Assert.That(attr.Type, Is.EqualTo(type));
        }
    }

    [TestCase(WcAttrType.BoolValue, "true")]
    [TestCase(WcAttrType.IntValue, "42")]
    [TestCase(WcAttrType.DoubleValue, "3.14")]
    [TestCase(WcAttrType.LongValue, "123")]
    public void Constructor_MismatchedType_Throws(WcAttrType type, object value)
    {
        Assert.Throws<ArgumentException>(() => new WcAttr(DummyElement, "x", type, value));
    }

    [Test]
    public void Constructor_NullValueWithNonNullValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => new WcAttr(DummyElement, "x", WcAttrType.NullValue, "oops"));
    }

    [Test]
    public void Constructor_IntValueWithDouble_Throws()
    {
        Assert.Throws<ArgumentException>(() => new WcAttr(DummyElement, "x", WcAttrType.IntValue, 3.14));
    }

    [Test]
    public void Constructor_DateOnlyValueWithDateTime_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new WcAttr(DummyElement, "x", WcAttrType.DateOnlyValue, DateTime.Now));
    }

    [TestCase(WcAttrType.StringValue, 42)]
    [TestCase(WcAttrType.StringValue, true)]
    [TestCase(WcAttrType.StringValue, 3.14)]
    public void Constructor_StringValueAcceptsAnyType(WcAttrType type, object value)
    {
        var attr = new WcAttr(DummyElement, "x", type, value);
        Assert.That(attr.Value, Is.EqualTo(value));
    }

    [Test]
    public void Constructor_StringValueAcceptsDateOnly()
    {
        var date = new DateOnly(2026, 1, 1);
        var attr = new WcAttr(DummyElement, "x", WcAttrType.StringValue, date);
        Assert.That(attr.Value, Is.EqualTo(date));
    }
}

[TestFixture]
[Category("Unit")]
public class ClientProtocolTests
{
    private static readonly System.Text.Json.JsonSerializerOptions CaseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

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
            CaseInsensitiveJson)!;
        Assert.That(back.Id, Is.EqualTo("x"));
        Assert.That(back.Success, Is.True);
    }
}
