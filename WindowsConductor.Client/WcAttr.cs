namespace WindowsConductor.Client;

/// <summary>This record represents a valued attribute for a WcElement.</summary>
public record WcAttr(WcElement Element, string Name, WcAttrType Type, object? Value) : WcValue(Type, Value);

/// <summary>Thrown when a value is requested to have a different unconvertible type.</summary>
public sealed class UnconvertibleAttrValueTypeException(string name, WcAttrType from, WcAttrType to, Exception innerException)
    : WcException($"Attribute '{name}' with value type {from} cannot be converted to {to}", innerException);
