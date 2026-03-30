namespace WindowsConductor.Client;

/// <summary>
/// Bitmask of keyboard modifier keys held during a type operation.
/// </summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1 << 0,
    Ctrl = 1 << 1,
    Alt = 1 << 2,
    /// <summary>Windows key (PC) / Command key (Mac).</summary>
    Meta = 1 << 3,
}
