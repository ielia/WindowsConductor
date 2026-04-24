namespace WindowsConductor.Client;

public static class WcDefaults
{
    public const string Port = "8765";
    public const string WebSocketUrl = "ws://localhost:" + Port + "/";
    public const string HttpPrefix = "http://localhost:" + Port + "/";

    public static string Version { get; } =
        // typeof(WcDefaults).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        typeof(WcDefaults).Assembly.GetName().Version?.ToString()
        ?? "Unknown";
}
