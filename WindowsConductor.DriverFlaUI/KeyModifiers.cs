namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Bitmask of keyboard modifier keys held during a type operation.
/// Values must match <see cref="Client.KeyModifiers"/> (transmitted as int over the wire).
/// </summary>
[Flags]
internal enum KeyModifiers
{
    None = 0,
    Shift = 1 << 0,
    Ctrl = 1 << 1,
    Alt = 1 << 2,
    /// <summary>Windows key (PC) / Command key (Mac).</summary>
    Meta = 1 << 3,
}
