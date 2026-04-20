using WindowsConductor.Client;
using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

[TestFixture]
[Category("Unit")]
public class WcValueYamlFormatterTests
{
    private static readonly WcElement DummyEl = new("el-1", null!);

    // ── Primitive values ────────────────────────────────────────────────────

    [Test]
    public void Format_StringValue_DoubleQuoted()
    {
        var v = new WcValue(WcAttrType.StringValue, "hello");
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("\"hello\""));
    }

    [Test]
    public void Format_StringValue_EscapesControlChars()
    {
        var v = new WcValue(WcAttrType.StringValue, "line1\nline2\ttab");
        var result = WcValueYamlFormatter.Format(v);
        Assert.That(result, Does.Contain("\\n"));
        Assert.That(result, Does.Contain("\\t"));
        Assert.That(result, Does.StartWith("\""));
        Assert.That(result, Does.EndWith("\""));
    }

    [Test]
    public void Format_StringValue_EscapesBackslash()
    {
        var v = new WcValue(WcAttrType.StringValue, @"C:\path\to\file");
        var result = WcValueYamlFormatter.Format(v);
        Assert.That(result, Does.Contain("\\\\"));
    }

    [Test]
    public void Format_StringValue_EscapesQuotes()
    {
        var v = new WcValue(WcAttrType.StringValue, "say \"hi\"");
        var result = WcValueYamlFormatter.Format(v);
        Assert.That(result, Does.Contain("\\\""));
    }

    [Test]
    public void Format_BoolValue_True()
    {
        var v = new WcValue(WcAttrType.BoolValue, true);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("true"));
    }

    [Test]
    public void Format_BoolValue_False()
    {
        var v = new WcValue(WcAttrType.BoolValue, false);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("false"));
    }

    [Test]
    public void Format_IntValue()
    {
        var v = new WcValue(WcAttrType.IntValue, 42);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("42"));
    }

    [Test]
    public void Format_LongValue()
    {
        var v = new WcValue(WcAttrType.LongValue, 9876543210L);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("9876543210"));
    }

    [Test]
    public void Format_DoubleValue()
    {
        var v = new WcValue(WcAttrType.DoubleValue, 3.14);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("3.14"));
    }

    [Test]
    public void Format_NullValue()
    {
        var v = new WcValue(WcAttrType.NullValue, null);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("null"));
    }

    // ── Date/time values ────────────────────────────────────────────────────

    [Test]
    public void Format_DateOnlyValue_Iso()
    {
        var v = new WcValue(WcAttrType.DateOnlyValue, new DateOnly(2026, 4, 20));
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("2026-04-20"));
    }

    [Test]
    public void Format_DateTimeValue_NoFraction()
    {
        var dt = new DateTime(2026, 4, 20, 14, 30, 0, DateTimeKind.Utc);
        var v = new WcValue(WcAttrType.DateTimeValue, dt);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("2026-04-20T14:30:00Z"));
    }

    [Test]
    public void Format_DateTimeValue_WithFraction()
    {
        var dt = new DateTime(2026, 4, 20, 14, 30, 1, 500, DateTimeKind.Utc);
        var v = new WcValue(WcAttrType.DateTimeValue, dt);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("2026-04-20T14:30:01.5Z"));
    }

    [Test]
    public void Format_TimeOnlyValue_NoFraction()
    {
        var v = new WcValue(WcAttrType.TimeOnlyValue, new TimeOnly(14, 30, 0));
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("14:30:00"));
    }

    [Test]
    public void Format_TimeOnlyValue_WithFraction()
    {
        var v = new WcValue(WcAttrType.TimeOnlyValue, new TimeOnly(14, 30, 1, 500));
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("14:30:01.5"));
    }

    [Test]
    public void Format_TimeSpanValue_Simple()
    {
        var v = new WcValue(WcAttrType.TimeSpanValue, new TimeSpan(1, 30, 0));
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("PT1H30M"));
    }

    [Test]
    public void Format_TimeSpanValue_WithDaysAndSeconds()
    {
        var v = new WcValue(WcAttrType.TimeSpanValue, new TimeSpan(2, 3, 4, 5));
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("P2DT3H4M5S"));
    }

    [Test]
    public void Format_TimeSpanValue_WithFraction()
    {
        var v = new WcValue(WcAttrType.TimeSpanValue, new TimeSpan(0, 0, 0, 1, 500));
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("PT1.5S"));
    }

    [Test]
    public void Format_TimeSpanValue_Zero()
    {
        var v = new WcValue(WcAttrType.TimeSpanValue, TimeSpan.Zero);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("PT0S"));
    }

    [Test]
    public void Format_TimeSpanValue_Negative()
    {
        var v = new WcValue(WcAttrType.TimeSpanValue, TimeSpan.FromHours(-2));
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("-PT2H"));
    }

    // ── Point / Rectangle values ─────────────────────────────────────────────

    [Test]
    public void Format_PointValue()
    {
        var v = new WcValue(WcAttrType.PointValue, new System.Drawing.Point(10, 20));
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("x: 10\ny: 20"));
    }

    [Test]
    public void Format_RectangleValue()
    {
        var v = new WcValue(WcAttrType.RectangleValue, new System.Drawing.Rectangle(5, 10, 300, 400));
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("x: 5\ny: 10\nwidth: 300\nheight: 400"));
    }

    [Test]
    public void Format_WcAttr_PointValue()
    {
        var attr = new WcAttr(DummyEl, "position", WcAttrType.PointValue, new System.Drawing.Point(10, 20));
        Assert.That(WcValueYamlFormatter.Format(attr), Is.EqualTo("position:\n  x: 10\n  y: 20"));
    }

    [Test]
    public void Format_WcAttr_RectangleValue()
    {
        var attr = new WcAttr(DummyEl, "bounds", WcAttrType.RectangleValue, new System.Drawing.Rectangle(5, 10, 300, 400));
        Assert.That(WcValueYamlFormatter.Format(attr), Is.EqualTo("bounds:\n  x: 5\n  y: 10\n  width: 300\n  height: 400"));
    }

    [Test]
    public void Format_ListOfPoints()
    {
        IReadOnlyList<WcValue> items =
        [
            new WcValue(WcAttrType.PointValue, new System.Drawing.Point(1, 2)),
            new WcValue(WcAttrType.PointValue, new System.Drawing.Point(3, 4))
        ];
        var v = new WcValue(WcAttrType.ListValue, items);
        var expected = "- x: 1\n  y: 2\n- x: 3\n  y: 4";
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo(expected));
    }

    // ── Attribute values ────────────────────────────────────────────────────

    [Test]
    public void Format_WcAttr_NameColonValue()
    {
        var attr = new WcAttr(DummyEl, "automationid", WcAttrType.StringValue, "btn1");
        Assert.That(WcValueYamlFormatter.Format(attr), Is.EqualTo("automationid: \"btn1\""));
    }

    [Test]
    public void Format_WcAttr_NullValue()
    {
        var attr = new WcAttr(DummyEl, "tooltip", WcAttrType.NullValue, null);
        Assert.That(WcValueYamlFormatter.Format(attr), Is.EqualTo("tooltip: null"));
    }

    [Test]
    public void Format_WcAttr_IntValue()
    {
        var attr = new WcAttr(DummyEl, "count", WcAttrType.IntValue, 7);
        Assert.That(WcValueYamlFormatter.Format(attr), Is.EqualTo("count: 7"));
    }

    // ── List values ─────────────────────────────────────────────────────────

    [Test]
    public void Format_EmptyList()
    {
        var v = new WcValue(WcAttrType.ListValue, (IReadOnlyList<WcValue>)[]);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("[]"));
    }

    [Test]
    public void Format_ListOfStrings()
    {
        IReadOnlyList<WcValue> items =
        [
            new WcValue(WcAttrType.StringValue, "alpha"),
            new WcValue(WcAttrType.StringValue, "beta")
        ];
        var v = new WcValue(WcAttrType.ListValue, items);
        var expected = "- \"alpha\"\n- \"beta\"";
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo(expected));
    }

    [Test]
    public void Format_ListOfAttrs()
    {
        IReadOnlyList<WcValue> items =
        [
            new WcAttr(DummyEl, "id", WcAttrType.StringValue, "btn1"),
            new WcAttr(DummyEl, "id", WcAttrType.StringValue, "btn2")
        ];
        var v = new WcValue(WcAttrType.ListValue, items);
        var expected = "- id: \"btn1\"\n- id: \"btn2\"";
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo(expected));
    }

    [Test]
    public void Format_ListOfMixed()
    {
        IReadOnlyList<WcValue> items =
        [
            new WcAttr(DummyEl, "name", WcAttrType.StringValue, "OK"),
            new WcValue(WcAttrType.IntValue, 42)
        ];
        var v = new WcValue(WcAttrType.ListValue, items);
        var expected = "- name: \"OK\"\n- 42";
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo(expected));
    }

    [Test]
    public void Format_NullListValue()
    {
        var v = new WcValue(WcAttrType.ListValue, null);
        Assert.That(WcValueYamlFormatter.Format(v), Is.EqualTo("[]"));
    }
}
