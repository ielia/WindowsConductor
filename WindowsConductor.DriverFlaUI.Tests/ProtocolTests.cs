using System.Text.Json;
using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

internal static partial class TestOptions
{
    internal static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };
}

[TestFixture]
[Category("Unit")]
public class WcRequestTests
{
    private static JsonElement ToJsonElement(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    // ── GetString ────────────────────────────────────────────────────────────

    [Test]
    public void GetString_ExistingKey_ReturnsValue()
    {
        var req = new WcRequest
        {
            Id = "1",
            Command = "test",
            Params = new() { ["path"] = ToJsonElement("C:\\app.exe") }
        };
        Assert.That(req.GetString("path"), Is.EqualTo("C:\\app.exe"));
    }

    [Test]
    public void GetString_MissingKey_ReturnsFallback()
    {
        var req = new WcRequest { Id = "1", Command = "test" };
        Assert.That(req.GetString("missing"), Is.EqualTo(""));
    }

    [Test]
    public void GetString_MissingKey_ReturnsCustomFallback()
    {
        var req = new WcRequest { Id = "1", Command = "test" };
        Assert.That(req.GetString("missing", "default"), Is.EqualTo("default"));
    }

    [Test]
    public void GetString_NullJsonValue_ReturnsFallback()
    {
        var req = new WcRequest
        {
            Id = "1",
            Command = "test",
            Params = new() { ["key"] = ToJsonElement((string?)null!) }
        };
        Assert.That(req.GetString("key", "fb"), Is.EqualTo("fb"));
    }

    // ── GetStringArray ───────────────────────────────────────────────────────

    [Test]
    public void GetStringArray_ExistingKey_ReturnsArray()
    {
        var req = new WcRequest
        {
            Id = "1",
            Command = "test",
            Params = new() { ["args"] = ToJsonElement(new[] { "a", "b", "c" }) }
        };
        Assert.That(req.GetStringArray("args"), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void GetStringArray_MissingKey_ReturnsEmpty()
    {
        var req = new WcRequest { Id = "1", Command = "test" };
        Assert.That(req.GetStringArray("args"), Is.Empty);
    }

    [Test]
    public void GetStringArray_EmptyArray_ReturnsEmpty()
    {
        var req = new WcRequest
        {
            Id = "1",
            Command = "test",
            Params = new() { ["args"] = ToJsonElement(Array.Empty<string>()) }
        };
        Assert.That(req.GetStringArray("args"), Is.Empty);
    }

    // ── GetInt ───────────────────────────────────────────────────────────────

    [Test]
    public void GetInt_ExistingKey_ReturnsValue()
    {
        var req = new WcRequest
        {
            Id = "1",
            Command = "test",
            Params = new() { ["timeout"] = ToJsonElement(5000) }
        };
        Assert.That(req.GetInt("timeout"), Is.EqualTo(5000));
    }

    [Test]
    public void GetInt_MissingKey_ReturnsFallback()
    {
        var req = new WcRequest { Id = "1", Command = "test" };
        Assert.That(req.GetInt("timeout"), Is.EqualTo(0));
    }

    [Test]
    public void GetInt_MissingKey_ReturnsCustomFallback()
    {
        var req = new WcRequest { Id = "1", Command = "test" };
        Assert.That(req.GetInt("timeout", 3000), Is.EqualTo(3000));
    }

    // ── GetBool ──────────────────────────────────────────────────────────────

    [Test]
    public void GetBool_True_ReturnsTrue()
    {
        var req = new WcRequest
        {
            Id = "1",
            Command = "test",
            Params = new() { ["flag"] = ToJsonElement(true) }
        };
        Assert.That(req.GetBool("flag"), Is.True);
    }

    [Test]
    public void GetBool_False_ReturnsFalse()
    {
        var req = new WcRequest
        {
            Id = "1",
            Command = "test",
            Params = new() { ["flag"] = ToJsonElement(false) }
        };
        Assert.That(req.GetBool("flag"), Is.False);
    }

    [Test]
    public void GetBool_MissingKey_ReturnsFallback()
    {
        var req = new WcRequest { Id = "1", Command = "test" };
        Assert.That(req.GetBool("flag"), Is.False);
    }

    [Test]
    public void GetBool_MissingKey_ReturnsCustomFallback()
    {
        var req = new WcRequest { Id = "1", Command = "test" };
        Assert.That(req.GetBool("flag", true), Is.True);
    }

    // ── Default property values ──────────────────────────────────────────────

    [Test]
    public void NewRequest_HasEmptyDefaults()
    {
        var req = new WcRequest();
        Assert.That(req.Id, Is.EqualTo(""));
        Assert.That(req.Command, Is.EqualTo(""));
        Assert.That(req.Params, Is.Not.Null);
        Assert.That(req.Params, Is.Empty);
    }

    // ── JSON round-trip ──────────────────────────────────────────────────────

    [Test]
    public void Request_RoundTripsViaJson()
    {
        var req = new WcRequest
        {
            Id = "abc",
            Command = "launch",
            Params = new()
            {
                ["path"] = ToJsonElement("calc.exe"),
                ["args"] = ToJsonElement(new[] { "--flag" })
            }
        };

        var json = JsonSerializer.Serialize(req);
        var deserialized = JsonSerializer.Deserialize<WcRequest>(json,
            TestOptions.CaseInsensitive)!;

        Assert.That(deserialized.Id, Is.EqualTo("abc"));
        Assert.That(deserialized.Command, Is.EqualTo("launch"));
        Assert.That(deserialized.GetString("path"), Is.EqualTo("calc.exe"));
        Assert.That(deserialized.GetStringArray("args"), Is.EqualTo(new[] { "--flag" }));
    }
}

[TestFixture]
[Category("Unit")]
public class WcResponseTests
{
    // ── Ok factory ───────────────────────────────────────────────────────────

    [Test]
    public void Ok_WithoutResult_SetsSuccessTrue()
    {
        var r = WcResponse.Ok("id1");
        Assert.That(r.Id, Is.EqualTo("id1"));
        Assert.That(r.Success, Is.True);
        Assert.That(r.Result, Is.Null);
        Assert.That(r.Error, Is.Null);
    }

    [Test]
    public void Ok_WithStringResult_SetsResult()
    {
        var r = WcResponse.Ok("id2", "hello");
        Assert.That(r.Success, Is.True);
        Assert.That(r.Result, Is.EqualTo("hello"));
    }

    [Test]
    public void Ok_WithObjectResult_SetsResult()
    {
        var r = WcResponse.Ok("id3", new { x = 1, y = 2 });
        Assert.That(r.Success, Is.True);
        Assert.That(r.Result, Is.Not.Null);
    }

    [Test]
    public void Ok_WithBoolResult_SetsResult()
    {
        var r = WcResponse.Ok("id4", true);
        Assert.That(r.Result, Is.EqualTo(true));
    }

    [Test]
    public void Ok_WithArrayResult_SetsResult()
    {
        var ids = new[] { "a", "b", "c" };
        var r = WcResponse.Ok("id5", ids);
        Assert.That(r.Result, Is.EqualTo(ids));
    }

    // ── Fail factory ─────────────────────────────────────────────────────────

    [Test]
    public void Fail_SetsSuccessFalseAndError()
    {
        var r = WcResponse.Fail("id1", "Something went wrong");
        Assert.That(r.Id, Is.EqualTo("id1"));
        Assert.That(r.Success, Is.False);
        Assert.That(r.Error, Is.EqualTo("Something went wrong"));
        Assert.That(r.Result, Is.Null);
    }

    // ── Default property values ──────────────────────────────────────────────

    [Test]
    public void NewResponse_HasEmptyDefaults()
    {
        var r = new WcResponse();
        Assert.That(r.Id, Is.EqualTo(""));
        Assert.That(r.Success, Is.False);
        Assert.That(r.Result, Is.Null);
        Assert.That(r.Error, Is.Null);
    }

    // ── JSON round-trip ──────────────────────────────────────────────────────

    [Test]
    public void Response_RoundTripsViaJson()
    {
        var r = WcResponse.Ok("xyz", "result-value");
        var json = JsonSerializer.Serialize(r);
        var deserialized = JsonSerializer.Deserialize<WcResponse>(json,
            TestOptions.CaseInsensitive)!;

        Assert.That(deserialized.Id, Is.EqualTo("xyz"));
        Assert.That(deserialized.Success, Is.True);
    }
}
