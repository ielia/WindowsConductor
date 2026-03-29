using WindowsConductor.Client;
using WindowsConductor.DriverFlaUI;

// Usage: WindowsConductor.DriverFlaUI.exe [prefix] [--confine-to-app]
//   prefix             e.g. "http://localhost:9000/"
//   --confine-to-app   Prevent locators from navigating above the application root
bool confineToApp = args.Contains("--confine-to-app");
string prefix = args.FirstOrDefault(a => !a.StartsWith("--")) ?? WcDefaults.HttpPrefix;

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    Console.WriteLine("\nShutting down…");
    e.Cancel = true;
    cts.Cancel();
};

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    Console.Error.WriteLine($"Unhandled: {e.ExceptionObject}");

Console.WriteLine($"WindowsConductor Driver  |  .NET {Environment.Version}");

var server = new WsServer(prefix, confineToApp);
await server.StartAsync(cts.Token);

Console.WriteLine("Driver stopped.");
