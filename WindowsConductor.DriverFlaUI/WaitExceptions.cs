namespace WindowsConductor.DriverFlaUI;

/// <summary>Thrown when a wait-for-visible operation times out without finding a matching element.</summary>
public sealed class ElementNotFoundException(string message) : Exception(message);

/// <summary>Thrown when a wait-for-vanish operation times out and the locator still matches.</summary>
public sealed class UnwantedElementFoundException(string message) : Exception(message);
