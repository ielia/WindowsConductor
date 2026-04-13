using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WindowsConductor.Client;
using WindowsConductor.DriverFlaUI;

// Usage: WindowsConductor.DriverFlaUI.exe [port] [--confine-to-app] [--ffmpeg-path <path>]
//          [--auth-token <token>] [--auth-token-file <file>]
//          [--hash-token <salt:iterations:hash>] [--hash-token-file <file>]
//          [--tls-port <port>] [--tls-only]
//          [--cert <path>] [--cert-key <path>]
//          [--cert-password <password>] [--cert-password-file <file>]
//          [--cert-thumbprint <hex>] [--cert-self-signed]
//   port                  Listening port (default 8765)
//   --confine-to-app      Prevent locators from navigating above the application root
//   --ffmpeg-path         Path to the ffmpeg executable (overrides FFMPEG_PATH env var)
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
        Console.Error.WriteLine("Error: --tls-port must be a valid port number (1–65535).");
        Environment.Exit(1);
    }
    tlsPort = tp;
}

if (tlsOnly && tlsPort is null)
{
    Console.Error.WriteLine("Error: --tls-only requires --tls-port.");
    Environment.Exit(1);
}

var httpsCert = LoadCertificate(args);

if (tlsPort is not null && httpsCert is null)
{
    Console.Error.WriteLine("Error: --tls-port requires a certificate (--cert, --cert-thumbprint, or --cert-self-signed).");
    Environment.Exit(1);
}

if (httpsCert is not null && tlsPort is null)
{
    Console.Error.WriteLine("Error: Certificate options require --tls-port.");
    Environment.Exit(1);
}

// Parse the HTTP port from positional args (skip all --flag and their values)
var valuedFlags = new HashSet<int>();
foreach (var flag in new[]
{
    "--ffmpeg-path", "--auth-token", "--auth-token-file", "--hash-token", "--hash-token-file",
    "--tls-port", "--cert", "--cert-key", "--cert-password", "--cert-password-file", "--cert-thumbprint"
})
    AddValuedFlag(valuedFlags, args, flag);

int httpPort = int.Parse(WcDefaults.Port);
var portArg = args
    .Where((a, i) => !a.StartsWith("--") && !valuedFlags.Contains(i))
    .FirstOrDefault();
if (portArg is not null)
{
    if (!int.TryParse(portArg, out httpPort) || httpPort <= 0 || httpPort > 65535)
    {
        Console.Error.WriteLine("Error: Port must be a valid number (1–65535).");
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
    Console.WriteLine("\nShutting down…");
    e.Cancel = true;
    cts.Cancel();
};

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    Console.Error.WriteLine($"Unhandled: {e.ExceptionObject}");

Console.WriteLine($"WindowsConductor Driver  |  .NET {Environment.Version}");

var server = new WsServer(effectiveHttpPort, tlsPort, httpsCert, confineToApp, ffmpegPath, authValidator);
await server.StartAsync(cts.Token);

Console.WriteLine("Driver stopped.");

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
        Console.Error.WriteLine("Error: Only one of --auth-token, --auth-token-file, --hash-token, --hash-token-file may be specified.");
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
        Console.Error.WriteLine("Error: Only one of --cert, --cert-thumbprint, --cert-self-signed may be specified.");
        Environment.Exit(1);
    }

    if (sourceCount == 0)
    {
        // Warn about orphan options
        if (certKeyPath is not null || certPassword is not null || certPasswordFile is not null)
        {
            Console.Error.WriteLine("Error: --cert-key, --cert-password, and --cert-password-file require --cert.");
            Environment.Exit(1);
        }
        return null;
    }

    if (certPassword is not null && certPasswordFile is not null)
    {
        Console.Error.WriteLine("Error: Only one of --cert-password, --cert-password-file may be specified.");
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
            Console.Error.WriteLine($"Error: No certificate with thumbprint '{certThumbprint}' found in CurrentUser\\My store.");
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

    Console.WriteLine($"Self-signed certificate thumbprint: {cert.Thumbprint}");

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
