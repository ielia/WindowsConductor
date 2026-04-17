namespace WindowsConductor.Client;

/// <summary>Thrown when the WcApp Driver returns an error response.</summary>
public class WcException : Exception
{
    public WcException() : base() {}
    public WcException(string message) : base(message) {}
    public WcException(string message, Exception innerException) : base(message, innerException) {}
}