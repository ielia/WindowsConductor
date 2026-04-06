param(
    [Parameter(Position=0)][int]$Port = 8765,
    [switch]$ConfineToApp,
    [string]$FfmpegPath
)
$args_ = @("http://localhost:$Port/")
if ($ConfineToApp) { $args_ += "--confine-to-app" }
if ($FfmpegPath) { $args_ += "--ffmpeg-path"; $args_ += $FfmpegPath }
dotnet run --project WindowsConductor.DriverFlaUI -- @args_
