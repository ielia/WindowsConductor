using FlaUI.Core.WindowsAPI;

namespace WindowsConductor.DriverFlaUI;

internal static class KeyTranslator
{
    private static readonly Dictionary<string, VirtualKeyShort> VirtualKeys = [];

    static KeyTranslator()
    {
        foreach (var name in Enum.GetNames<Client.Key>())
        {
            VirtualKeys.Add(name.ToLowerInvariant(), Enum.Parse<VirtualKeyShort>(name));
        }
    }

    internal static VirtualKeyShort Get(string keyName) => VirtualKeys[keyName.ToLowerInvariant()];

    internal static VirtualKeyShort Get(Client.Key key) => Get(Enum.GetName(key)!);

    internal static VirtualKeyShort[] GetAll(string[] keyNames) => [.. keyNames.Select(Get)];

    internal static VirtualKeyShort[] GetAll(Client.Key[] keys) => [.. keys.Select(Get)];

    internal static string[] KeyNames => [.. VirtualKeys.Keys];
}