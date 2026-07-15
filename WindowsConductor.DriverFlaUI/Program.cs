using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using WindowsConductor.Client;
using WindowsConductor.DriverFlaUI;

// Usage: WindowsConductor.DriverFlaUI.exe [port] [--confine-to-app] [--ffmpeg-path <path>]
//          [--log-file <path>]
//          [--auth-token <token>] [--auth-token-file <file>]
//          [--hash-token <salt:iterations:hash>] [--hash-token-file <file>]
//          [--tls-port <port>] [--tls-only]
//          [--cert <path>] [--cert-key <path>]
//          [--cert-password <password>] [--cert-password-file <file>]
//          [--cert-thumbprint <hex>] [--cert-self-signed]
//   port                  Listening port (default 8765)
//   --confine-to-app      Prevent locators from navigating above the application root
//   --ffmpeg-path         Path to the ffmpeg executable (overrides FFMPEG_PATH env var)
//   --log-file            Path to a log file (enables file logging in addition to console)
//   --auth-token          Plain bearer token required for client connections
//   --auth-token-file     File containing a plain bearer token
//   --hash-token          PBKDF2 triplet (salt:iterations:hash, base64) for token validation
//   --hash-token-file     File containing a PBKDF2 triplet
//   --tls-port            Port for HTTPS/WSS listener (requires a certificate option)
//   --tls-only            Disable plain HTTP listener (requires --tls-port)
//   --cert                Path to a .pfx/.p12 or .pem certificate file
//   --cert-key            Path to PEM private key file (only with a PEM --cert)
//   --cert-password       Password for encrypted .pfx or PEM key
//   --cert-password-file  File containing the certificate password
//   --cert-thumbprint     Load certificate from CurrentUser\My store by thumbprint
//   --cert-self-signed    Generate an ephemeral self-signed certificate at startup

// ── Logging bootstrap ──────────────────────────────────────────────────────

string? logFile = GetFlagValue(args, "--log-file");

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .Build();

var serilogSection = configuration.GetSection("Serilog");
var consoleSection = serilogSection.GetSection("Console");
var fileSection = serilogSection.GetSection("File");

var consoleTemplate = consoleSection["OutputTemplate"] ?? "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
var consoleMinLevel = Enum.TryParse<LogEventLevel>(consoleSection["MinimumLevel"], true, out var cml) ? cml : LogEventLevel.Information;
var fileTemplate = fileSection["OutputTemplate"] ?? "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";
var fileMinLevel = Enum.TryParse<LogEventLevel>(fileSection["MinimumLevel"], true, out var fml) ? fml : LogEventLevel.Debug;
var fileRollingInterval = Enum.TryParse<RollingInterval>(fileSection["RollingInterval"], true, out var fri) ? fri : RollingInterval.Day;
var fileRetainedCount = int.TryParse(fileSection["RetainedFileCountLimit"], out var frc) ? frc : 7;
var fileSizeLimit = long.TryParse(fileSection["FileSizeLimitBytes"], out var fsl) ? fsl : 50L * 1024 * 1024;

var minLevelSection = serilogSection.GetSection("MinimumLevel");
var globalMinLevel = Enum.TryParse<LogEventLevel>(minLevelSection["Default"], true, out var gml) ? gml : LogEventLevel.Information;

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Is(globalMinLevel)
    .WriteTo.Console(restrictedToMinimumLevel: consoleMinLevel, outputTemplate: consoleTemplate, formatProvider: null);

// Apply source-context overrides
var overrides = minLevelSection.GetSection("Override");
foreach (var entry in overrides.GetChildren())
{
    if (Enum.TryParse<LogEventLevel>(entry.Value, true, out var overrideLevel))
        loggerConfig.MinimumLevel.Override(entry.Key, overrideLevel);
}

if (logFile is not null)
{
    loggerConfig.WriteTo.File(
        logFile,
        restrictedToMinimumLevel: fileMinLevel,
        outputTemplate: fileTemplate,
        formatProvider: null,
        rollingInterval: fileRollingInterval,
        retainedFileCountLimit: fileRetainedCount,
        fileSizeLimitBytes: fileSizeLimit);
}

Log.Logger = loggerConfig.CreateLogger();

try
{
    RunDriver(args, logFile);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Driver terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// ── Driver main ─────────────────────────────────────────────────────────────

static void RunDriver(string[] args, string? logFile)
{
    bool confineToApp = args.Contains("--confine-to-app");
    bool tlsOnly = args.Contains("--tls-only");

    string? ffmpegPath = GetFlagValue(args, "--ffmpeg-path");
    ffmpegPath ??= Environment.GetEnvironmentVariable("FFMPEG_PATH");

    var authValidator = ParseAuthValidator(args);

    int? tlsPort = null;
    var tlsPortStr = GetFlagValue(args, "--tls-port");
    if (tlsPortStr is not null)
    {
        if (!int.TryParse(tlsPortStr, out var tp) || tp <= 0 || tp > 65535)
        {
            Log.Fatal("--tls-port must be a valid port number (1–65535)");
            Environment.Exit(1);
        }
        tlsPort = tp;
    }

    if (tlsOnly && tlsPort is null)
    {
        Log.Fatal("--tls-only requires --tls-port");
        Environment.Exit(1);
    }

    var httpsCert = LoadCertificate(args);

    if (tlsPort is not null && httpsCert is null)
    {
        Log.Fatal("--tls-port requires a certificate (--cert, --cert-thumbprint, or --cert-self-signed)");
        Environment.Exit(1);
    }

    if (httpsCert is not null && tlsPort is null)
    {
        Log.Fatal("Certificate options require --tls-port");
        Environment.Exit(1);
    }

    // Parse the HTTP port from positional args (skip all --flag and their values)
    var valuedFlags = new HashSet<int>();
    foreach (var flag in new[]
    {
        "--ffmpeg-path", "--log-file",
        "--auth-token", "--auth-token-file", "--hash-token", "--hash-token-file",
        "--tls-port", "--cert", "--cert-key", "--cert-password", "--cert-password-file", "--cert-thumbprint"
    })
        AddValuedFlag(valuedFlags, args, flag);

    int httpPort = int.Parse(WcDefaults.Port, System.Globalization.CultureInfo.InvariantCulture);
    var portArg = args
        .Where((a, i) => !a.StartsWith("--", StringComparison.Ordinal) && !valuedFlags.Contains(i))
        .FirstOrDefault();
    if (portArg is not null)
    {
        if (!int.TryParse(portArg, out httpPort) || httpPort <= 0 || httpPort > 65535)
        {
            Log.Fatal("Port must be a valid number (1–65535)");
            Environment.Exit(1);
        }
    }

    // When TLS port equals HTTP port, TLS wins — can't serve both on one port.
    if (tlsPort == httpPort)
        tlsOnly = true;

    int? effectiveHttpPort = tlsOnly ? null : httpPort;

    using var cts = new CancellationTokenSource();

    Console.CancelKeyPress += (_, e) =>
    {
        Log.Information("Shutting down…");
        e.Cancel = true;
        cts.Cancel();
    };

    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        Log.Fatal("Unhandled exception: {Exception}", e.ExceptionObject);

    Log.Information("WindowsConductor Driver v{Version}  |  .NET {DotNetVersion}", WcDefaults.Version, Environment.Version);
    if (logFile is not null)
        Log.Information("Logging to file: {LogFile}", logFile);

    var server = new WsServer(effectiveHttpPort, tlsPort, httpsCert, confineToApp, ffmpegPath, authValidator);
    server.StartAsync(cts.Token).GetAwaiter().GetResult();

    Log.Information("Driver stopped");
}

static AuthTokenValidator ParseAuthValidator(string[] args)
{
    var authTokenVal = GetFlagValue(args, "--auth-token");
    var authTokenFileVal = GetFlagValue(args, "--auth-token-file");
    var hashTokenVal = GetFlagValue(args, "--hash-token");
    var hashTokenFileVal = GetFlagValue(args, "--hash-token-file");

    int flagCount = (authTokenVal is not null ? 1 : 0)
        + (authTokenFileVal is not null ? 1 : 0)
        + (hashTokenVal is not null ? 1 : 0)
        + (hashTokenFileVal is not null ? 1 : 0);

    if (flagCount > 1)
    {
        Log.Fatal("Only one of --auth-token, --auth-token-file, --hash-token, --hash-token-file may be specified");
        Environment.Exit(1);
    }

    if (authTokenVal is not null)
        return AuthTokenValidator.FromPlainToken(authTokenVal);

    if (authTokenFileVal is not null)
    {
        var token = File.ReadAllText(authTokenFileVal).Trim();
        return AuthTokenValidator.FromPlainToken(token);
    }

    if (hashTokenVal is not null)
        return AuthTokenValidator.FromHashTriplet(hashTokenVal);

    if (hashTokenFileVal is not null)
    {
        var triplet = File.ReadAllText(hashTokenFileVal).Trim();
        return AuthTokenValidator.FromHashTriplet(triplet);
    }

    return AuthTokenValidator.None();
}

static X509Certificate2? LoadCertificate(string[] args)
{
    var certPath = GetFlagValue(args, "--cert");
    var certKeyPath = GetFlagValue(args, "--cert-key");
    var certPassword = GetFlagValue(args, "--cert-password");
    var certPasswordFile = GetFlagValue(args, "--cert-password-file");
    var certThumbprint = GetFlagValue(args, "--cert-thumbprint");
    bool selfSigned = args.Contains("--cert-self-signed");

    // Validate mutual exclusivity of cert sources
    int sourceCount = (certPath is not null ? 1 : 0)
        + (certThumbprint is not null ? 1 : 0)
        + (selfSigned ? 1 : 0);
    if (sourceCount > 1)
    {
        Log.Fatal("Only one of --cert, --cert-thumbprint, --cert-self-signed may be specified");
        Environment.Exit(1);
    }

    if (sourceCount == 0)
    {
        // Warn about orphan options
        if (certKeyPath is not null || certPassword is not null || certPasswordFile is not null)
        {
            Log.Fatal("--cert-key, --cert-password, and --cert-password-file require --cert");
            Environment.Exit(1);
        }
        return null;
    }

    if (certPassword is not null && certPasswordFile is not null)
    {
        Log.Fatal("Only one of --cert-password, --cert-password-file may be specified");
        Environment.Exit(1);
    }

    var password = certPassword ?? (certPasswordFile is not null ? File.ReadAllText(certPasswordFile).Trim() : null);

    if (selfSigned)
        return GenerateSelfSignedCert();

    if (certThumbprint is not null)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        var certs = store.Certificates.Find(X509FindType.FindByThumbprint, certThumbprint, false);
        if (certs.Count == 0)
        {
            Log.Fatal("No certificate with thumbprint {Thumbprint} found in CurrentUser\\My store", certThumbprint);
            Environment.Exit(1);
        }
        return certs[0];
    }

    // --cert path
    if (certKeyPath is not null)
    {
        // PEM cert + PEM key
        var cert = password is not null
            ? X509Certificate2.CreateFromEncryptedPemFile(certPath!, password, certKeyPath)
            : X509Certificate2.CreateFromPemFile(certPath!, certKeyPath);
        // Windows SChannel requires persisted key container
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
    }

    // PFX / P12
    return new X509Certificate2(certPath!, password);
}

static X509Certificate2 GenerateSelfSignedCert()
{
    using var rsa = RSA.Create(2048);
    var req = new CertificateRequest(
        "CN=WindowsConductor Self-Signed", rsa,
        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    var sanBuilder = new SubjectAlternativeNameBuilder();
    sanBuilder.AddDnsName("localhost");
    sanBuilder.AddIpAddress(IPAddress.Loopback);
    sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
    req.CertificateExtensions.Add(sanBuilder.Build());

    var cert = req.CreateSelfSigned(
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddYears(1));

    Log.Information("Self-signed certificate thumbprint: {Thumbprint}", cert.Thumbprint);

    // Windows SChannel requires persisted key container
    return new X509Certificate2(cert.Export(X509ContentType.Pfx));
}

static string? GetFlagValue(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    if (idx < 0 || idx + 1 >= args.Length) return null;
    return args[idx + 1];
}

static void AddValuedFlag(HashSet<int> indices, string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    if (idx >= 0 && idx + 1 < args.Length)
        indices.Add(idx + 1);
}
