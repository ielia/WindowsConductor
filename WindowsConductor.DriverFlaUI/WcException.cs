namespace WindowsConductor.DriverFlaUI;

public class WcException : Exception
{
    protected WcException() : base() { }
    protected WcException(string message) : base(message) { }
    protected WcException(string message, Exception innerException) : base(message, innerException) { }
}