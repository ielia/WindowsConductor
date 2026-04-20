using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using WindowsConductor.Client;

namespace WindowsConductor.InspectorGUI;

internal static class WcValueYamlFormatter
{
    private const string Indent = "  ";

    internal static string Format(WcValue value) => Format(value, 0);

    private static string Format(WcValue value, int depth)
    {
        if (value is WcAttr attr)
            return FormatAttr(attr, depth);

        return value.Type switch
        {
            WcAttrType.ListValue => FormatList(value, depth),
            WcAttrType.PointValue => FormatPoint((Point)value.Value!, depth),
            WcAttrType.RectangleValue => FormatRectangle((Rectangle)value.Value!, depth),
            WcAttrType.NullValue => "null",
            _ => FormatPrimitive(value)
        };
    }

    private static string FormatAttr(WcAttr attr, int depth)
    {
        var formatted = FormatValueForAttr(attr, depth);
        var separator = formatted.StartsWith('\n') ? ":" : ": ";
        return $"{attr.Name}{separator}{formatted}";
    }

    private static string FormatValueForAttr(WcValue value, int depth)
    {
        if (value.Type == WcAttrType.ListValue)
        {
            var list = value.GetAsList();
            if (list is null or { Count: 0 })
                return "[]";
            var sb = new StringBuilder();
            sb.Append('\n');
            foreach (var item in list)
            {
                sb.Append(RepeatIndent(depth + 1));
                sb.Append("- ");
                sb.Append(Format(item, depth + 2));
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd();
        }

        if (value.Type is WcAttrType.PointValue or WcAttrType.RectangleValue)
        {
            var obj = value.Type == WcAttrType.PointValue
                ? FormatPoint((Point)value.Value!, depth + 1)
                : FormatRectangle((Rectangle)value.Value!, depth + 1);
            return $"\n{RepeatIndent(depth + 1)}{obj}";
        }

        return value.Type == WcAttrType.NullValue ? "null" : FormatPrimitive(value);
    }

    private static string FormatList(WcValue value, int depth)
    {
        var list = value.GetAsList();
        if (list is null or { Count: 0 })
            return "[]";

        var sb = new StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
                sb.Append(RepeatIndent(depth));
            }
            sb.Append("- ");
            sb.Append(Format(list[i], depth + 1));
        }
        return sb.ToString();
    }

    private static string FormatPrimitive(WcValue value) => value.Type switch
    {
        WcAttrType.BoolValue => value.Value is true ? "true" : "false",
        WcAttrType.IntValue => ((int)value.Value!).ToString(CultureInfo.InvariantCulture),
        WcAttrType.LongValue => ((long)value.Value!).ToString(CultureInfo.InvariantCulture),
        WcAttrType.DoubleValue => ((double)value.Value!).ToString(CultureInfo.InvariantCulture),
        WcAttrType.DateOnlyValue => ((DateOnly)value.Value!).ToString("o", CultureInfo.InvariantCulture),
        WcAttrType.DateTimeValue => FormatDateTime((DateTime)value.Value!),
        WcAttrType.TimeOnlyValue => FormatTimeOnly((TimeOnly)value.Value!),
        WcAttrType.TimeSpanValue => FormatTimeSpan((TimeSpan)value.Value!),
        WcAttrType.StringValue => EscapeString(value.Value?.ToString() ?? ""),
        _ => EscapeString(value.Value?.ToString() ?? "")
    };

    private static string FormatPoint(Point p, int depth)
    {
        var indent = RepeatIndent(depth);
        return $"x: {p.X}\n{indent}y: {p.Y}";
    }

    private static string FormatRectangle(Rectangle r, int depth)
    {
        var indent = RepeatIndent(depth);
        return $"x: {r.X}\n{indent}y: {r.Y}\n{indent}width: {r.Width}\n{indent}height: {r.Height}";
    }

    private static string FormatDateTime(DateTime dt)
    {
        var format = dt.Millisecond != 0 || dt.Microsecond != 0
            ? "yyyy-MM-ddTHH:mm:ss.FFFFFFFK"
            : "yyyy-MM-ddTHH:mm:ssK";
        return dt.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string FormatTimeOnly(TimeOnly t)
    {
        var format = t.Millisecond != 0 || t.Microsecond != 0
            ? "HH:mm:ss.FFFFFFF"
            : "HH:mm:ss";
        return t.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        var sb = new StringBuilder();
        if (ts < TimeSpan.Zero)
        {
            sb.Append('-');
            ts = ts.Negate();
        }
        sb.Append('P');
        if (ts.Days > 0)
            sb.Append(CultureInfo.InvariantCulture, $"{ts.Days}D");
        sb.Append('T');
        if (ts.Hours > 0)
            sb.Append(CultureInfo.InvariantCulture, $"{ts.Hours}H");
        if (ts.Minutes > 0)
            sb.Append(CultureInfo.InvariantCulture, $"{ts.Minutes}M");
        var fractionalSeconds = ts.Seconds + ts.Milliseconds / 1000.0 + ts.Microseconds / 1_000_000.0;
        if (fractionalSeconds > 0 || sb.Length == 2)
            sb.Append(fractionalSeconds.ToString("0.#######", CultureInfo.InvariantCulture)).Append('S');
        return sb.ToString();
    }

    private static readonly JsonSerializerOptions EscapeOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string EscapeString(string s)
    {
        // JsonSerializer.Serialize produces a JSON string literal with all
        // control characters and special chars properly escaped using
        // backslash sequences (\n, \t, \\, \", etc.).
        return JsonSerializer.Serialize(s, EscapeOptions);
    }

    private static string RepeatIndent(int depth)
    {
        var sb = new StringBuilder(depth * Indent.Length);
        for (int i = 0; i < depth; i++)
            sb.Append(Indent);
        return sb.ToString();
    }
}
