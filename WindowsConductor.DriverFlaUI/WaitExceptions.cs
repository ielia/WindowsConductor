namespace WindowsConductor.DriverFlaUI;

/// <summary>Thrown when a wait-for-* operation times out without finding a matching element.</summary>
public sealed class NoMatchException(string message) : WcException(message);

/// <summary>Thrown when a wait-for-* operation times out and the locator still matches.</summary>
public sealed class UnwantedMatchException(string message) : WcException(message);
