namespace WindowsConductor.DriverFlaUI;

public class WcException : Exception
{
    protected WcException() : base() { }
    protected WcException(string message) : base(message) { }
    protected WcException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Thrown when an XPath value cannot be cast to the required type.</summary>
public sealed class XPathCastException(string message) : WcException(message)
{
    public XPathCastException(string value, string from, string to)
        : this($"Cannot cast XPath value '{value}' of type '{from}' to '{to}'.") { }
}