namespace WindowsConductor.DriverFlaUI;

/// <summary>Thrown when --confine-to-app is active and all matched elements belong to a different process.</summary>
public sealed class AccessRestrictedException(string message) : WcException(message);
