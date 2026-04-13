[CmdletBinding()]
param(
    [Parameter(Position=0)][int]$Port = 8765,
    [switch]$ConfineToApp,
    [string]$FfmpegPath,
    [string]$AuthToken,
    [string]$AuthTokenFile,
    [string]$HashToken,
    [string]$HashTokenFile,
    [int]$TlsPort,
    [switch]$TlsOnly,
    [string]$Cert,
    [string]$CertKey,
    [string]$CertPassword,
    [string]$CertPasswordFile,
    [string]$CertThumbprint,
    [switch]$CertSelfSigned
)
$args_ = @("$Port")
if ($ConfineToApp)     { $args_ += "--confine-to-app" }
if ($FfmpegPath)       { $args_ += "--ffmpeg-path";       $args_ += $FfmpegPath }
if ($AuthToken)        { $args_ += "--auth-token";        $args_ += $AuthToken }
if ($AuthTokenFile)    { $args_ += "--auth-token-file";   $args_ += $AuthTokenFile }
if ($HashToken)        { $args_ += "--hash-token";        $args_ += $HashToken }
if ($HashTokenFile)    { $args_ += "--hash-token-file";   $args_ += $HashTokenFile }
if ($TlsPort)          { $args_ += "--tls-port";          $args_ += "$TlsPort" }
if ($TlsOnly)          { $args_ += "--tls-only" }
if ($Cert)             { $args_ += "--cert";              $args_ += $Cert }
if ($CertKey)          { $args_ += "--cert-key";          $args_ += $CertKey }
if ($CertPassword)     { $args_ += "--cert-password";     $args_ += $CertPassword }
if ($CertPasswordFile) { $args_ += "--cert-password-file"; $args_ += $CertPasswordFile }
if ($CertThumbprint)   { $args_ += "--cert-thumbprint";   $args_ += $CertThumbprint }
if ($CertSelfSigned)   { $args_ += "--cert-self-signed" }
dotnet run --project WindowsConductor.DriverFlaUI -- @args_
