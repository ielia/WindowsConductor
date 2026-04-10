using WindowsConductor.Client;
using WindowsConductor.DriverFlaUI;

// Usage: WindowsConductor.DriverFlaUI.exe [prefix] [--confine-to-app] [--ffmpeg-path <path>]
//          [--auth-token <token>] [--auth-token-file <file>]
//          [--hash-token <salt:iterations:hash>] [--hash-token-file <file>]
//   prefix                e.g. "http://localhost:9000/"
//   --confine-to-app      Prevent locators from navigating above the application root
//   --ffmpeg-path         Path to the ffmpeg executable (overrides FFMPEG_PATH env var)
//   --auth-token          Plain bearer token required for client connections
//   --auth-token-file     File containing a plain bearer token
//   --hash-token          PBKDF2 triplet (salt:iterations:hash, base64) for token validation
//   --hash-token-file     File containing a PBKDF2 triplet
bool confineToApp = args.Contains("--confine-to-app");

string? ffmpegPath = null;
var ffmpegIndex = Array.IndexOf(args, "--ffmpeg-path");
if (ffmpegIndex >= 0 && ffmpegIndex + 1 < args.Length)
    ffmpegPath = args[ffmpegIndex + 1];
ffmpegPath ??= Environment.GetEnvironmentVariable("FFMPEG_PATH");

var authValidator = ParseAuthValidator(args);

var valuedFlags = new HashSet<int>();
AddValuedFlag(valuedFlags, args, "--ffmpeg-path");
AddValuedFlag(valuedFlags, args, "--auth-token");
AddValuedFlag(valuedFlags, args, "--auth-token-file");
AddValuedFlag(valuedFlags, args, "--hash-token");
AddValuedFlag(valuedFlags, args, "--hash-token-file");

string prefix = args
    .Where((a, i) => !a.StartsWith("--") && !valuedFlags.Contains(i))
    .FirstOrDefault() ?? WcDefaults.HttpPrefix;

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

var server = new WsServer(prefix, confineToApp, ffmpegPath, authValidator);
await server.StartAsync(cts.Token);

Console.WriteLine("Driver stopped.");

static AuthTokenValidator ParseAuthValidator(string[] args)
{
    var authTokenIdx = Array.IndexOf(args, "--auth-token");
    var authTokenFileIdx = Array.IndexOf(args, "--auth-token-file");
    var hashTokenIdx = Array.IndexOf(args, "--hash-token");
    var hashTokenFileIdx = Array.IndexOf(args, "--hash-token-file");

    int flagCount = (authTokenIdx >= 0 ? 1 : 0)
        + (authTokenFileIdx >= 0 ? 1 : 0)
        + (hashTokenIdx >= 0 ? 1 : 0)
        + (hashTokenFileIdx >= 0 ? 1 : 0);

    if (flagCount > 1)
    {
        Console.Error.WriteLine("Error: Only one of --auth-token, --auth-token-file, --hash-token, --hash-token-file may be specified.");
        Environment.Exit(1);
    }

    if (authTokenIdx >= 0)
    {
        if (authTokenIdx + 1 >= args.Length)
        {
            Console.Error.WriteLine("Error: --auth-token requires a value.");
            Environment.Exit(1);
        }
        return AuthTokenValidator.FromPlainToken(args[authTokenIdx + 1]);
    }

    if (authTokenFileIdx >= 0)
    {
        if (authTokenFileIdx + 1 >= args.Length)
        {
            Console.Error.WriteLine("Error: --auth-token-file requires a file path.");
            Environment.Exit(1);
        }
        var token = File.ReadAllText(args[authTokenFileIdx + 1]).Trim();
        return AuthTokenValidator.FromPlainToken(token);
    }

    if (hashTokenIdx >= 0)
    {
        if (hashTokenIdx + 1 >= args.Length)
        {
            Console.Error.WriteLine("Error: --hash-token requires a salt:iterations:hash triplet.");
            Environment.Exit(1);
        }
        return AuthTokenValidator.FromHashTriplet(args[hashTokenIdx + 1]);
    }

    if (hashTokenFileIdx >= 0)
    {
        if (hashTokenFileIdx + 1 >= args.Length)
        {
            Console.Error.WriteLine("Error: --hash-token-file requires a file path.");
            Environment.Exit(1);
        }
        var triplet = File.ReadAllText(args[hashTokenFileIdx + 1]).Trim();
        return AuthTokenValidator.FromHashTriplet(triplet);
    }

    return AuthTokenValidator.None();
}

static void AddValuedFlag(HashSet<int> indices, string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    if (idx >= 0 && idx + 1 < args.Length)
        indices.Add(idx + 1);
}
