using WindowsConductor.Client;
using WindowsConductor.DriverFlaUI;

// Usage: WindowsConductor.DriverFlaUI.exe [prefix] [--confine-to-app] [--ffmpeg-path <path>]
//   prefix             e.g. "http://localhost:9000/"
//   --confine-to-app   Prevent locators from navigating above the application root
//   --ffmpeg-path      Path to the ffmpeg executable (overrides FFMPEG_PATH env var)
bool confineToApp = args.Contains("--confine-to-app");

string? ffmpegPath = null;
var ffmpegIndex = Array.IndexOf(args, "--ffmpeg-path");
if (ffmpegIndex >= 0 && ffmpegIndex + 1 < args.Length)
    ffmpegPath = args[ffmpegIndex + 1];
ffmpegPath ??= Environment.GetEnvironmentVariable("FFMPEG_PATH");

string prefix = args.FirstOrDefault(a => !a.StartsWith("--") && (ffmpegIndex < 0 || a != args.ElementAtOrDefault(ffmpegIndex + 1)))
    ?? WcDefaults.HttpPrefix;

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

var server = new WsServer(prefix, confineToApp, ffmpegPath);
await server.StartAsync(cts.Token);

Console.WriteLine("Driver stopped.");
