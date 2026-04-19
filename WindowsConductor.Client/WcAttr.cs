namespace WindowsConductor.Client;

/// <summary>This record represents a valued attribute for a WcElement.</summary>
public record WcAttr(WcElement Element, string Name, WcValue TypedValue)
{
    public WcAttr(WcElement element, string name, WcAttrType type, object? value)
        : this(element, name, new WcValue(type, value)) { }

    public WcAttrType Type => TypedValue.Type;
    public object? Value => TypedValue.Value;

    public bool? GetAsBool() => TypedValue.GetAsBool();
    public DateOnly? GetAsDateOnly() => TypedValue.GetAsDateOnly();
    public DateTime? GetAsDateTime() => TypedValue.GetAsDateTime();
    public double? GetAsDouble() => TypedValue.GetAsDouble();
    public int? GetAsInt() => TypedValue.GetAsInt();
    public long? GetAsLong() => TypedValue.GetAsLong();
    public string? GetAsString() => TypedValue.GetAsString();
    public TimeOnly? GetAsTimeOnly() => TypedValue.GetAsTimeOnly();
    public TimeSpan? GetAsTimeSpan() => TypedValue.GetAsTimeSpan();
}

/// <summary>Thrown when a value is requested to have a different unconvertible type.</summary>
public sealed class UnconvertibleAttrValueTypeException(string name, WcAttrType from, WcAttrType to, Exception innerException)
    : WcException($"Attribute '{name}' with value type {from} cannot be converted to {to}", innerException);
