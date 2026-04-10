param(
    [Parameter(Position=0)][int]$Port = 8765,
    [switch]$ConfineToApp,
    [string]$FfmpegPath,
    [string]$AuthToken,
    [string]$AuthTokenFile,
    [string]$HashToken,
    [string]$HashTokenFile
)
$args_ = @("http://localhost:$Port/")
if ($ConfineToApp) { $args_ += "--confine-to-app" }
if ($FfmpegPath)     { $args_ += "--ffmpeg-path";     $args_ += $FfmpegPath }
if ($AuthToken)      { $args_ += "--auth-token";      $args_ += $AuthToken }
if ($AuthTokenFile)  { $args_ += "--auth-token-file"; $args_ += $AuthTokenFile }
if ($HashToken)      { $args_ += "--hash-token";      $args_ += $HashToken }
if ($HashTokenFile)  { $args_ += "--hash-token-file"; $args_ += $HashTokenFile }
dotnet run --project WindowsConductor.DriverFlaUI -- @args_
