using WindowsConductor.Client;
using WindowsConductor.DriverFlaUI;

// Pass a custom prefix as the first argument, e.g.:
//   WindowsConductor.DriverFlaUI.exe "http://localhost:9000/"
string prefix = args.Length > 0 ? args[0] : WcDefaults.HttpPrefix;

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

var server = new WsServer(prefix);
await server.StartAsync(cts.Token);

Console.WriteLine("Driver stopped.");
