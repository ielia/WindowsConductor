namespace WindowsConductor.DriverFlaUI;

/// <summary>Thrown when a click target point falls outside the element's bounding rectangle.</summary>
public sealed class LocationOutOfRangeException(string message) : WcException(message);
