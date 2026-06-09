<#
.SYNOPSIS
    Installs or uninstalls a scheduled task that keeps the desktop session alive
    when an RDP (or other remote desktop) connection is disconnected.

.DESCRIPTION
    When a remote desktop client disconnects (e.g. minimizing Windows App),
    the desktop compositor stops rendering and UI Automation calls fail.
    This script registers a scheduled task that automatically redirects the
    session to the local console on disconnect, keeping the desktop alive.

    Requires administrator privileges.

.PARAMETER Install
    Register the scheduled task and create the park script.

.PARAMETER Uninstall
    Remove the scheduled task and delete the park script.

.EXAMPLE
    .\session-park.ps1 -Install
    .\session-park.ps1 -Uninstall
#>

[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$TaskName = "WindowsConductor_ParkSession"
$ScriptPath = Join-Path $env:ProgramData "WindowsConductor\ParkSession.ps1"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Error "This script requires administrator privileges. Run it from an elevated prompt."
        exit 1
    }
}

function Install-ParkTask {
    Assert-Administrator

    $existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "Session parking task is already installed."
        return
    }

    $scriptDir = Split-Path $ScriptPath -Parent
    if (-not (Test-Path $scriptDir)) {
        New-Item -Path $scriptDir -ItemType Directory -Force | Out-Null
    }

    @'
# The trigger passes the event as XML; extract the session ID from it.
$event = Get-WinEvent -LogName "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational" `
    -FilterXPath "*[System[EventID=24]]" -MaxEvents 1 -ErrorAction SilentlyContinue
if ($event) {
    $sessionId = ([xml]$event.ToXml()).Event.UserData.EventXML.SessionID
    if ($sessionId) {
        & tscon $sessionId /dest:console 2>&1 | Out-Null
    }
}
'@ | Set-Content -Path $ScriptPath -Encoding UTF8

    $action = New-ScheduledTaskAction `
        -Execute "powershell.exe" `
        -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$ScriptPath`""

    $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -RunLevel Highest

    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit (New-TimeSpan -Seconds 30)

    $subscriptionXml = @"
<QueryList>
  <Query Id="0" Path="Microsoft-Windows-TerminalServices-LocalSessionManager/Operational">
    <Select Path="Microsoft-Windows-TerminalServices-LocalSessionManager/Operational">*[System[EventID=24]]</Select>
  </Query>
</QueryList>
"@

    $trigger = New-CimInstance `
        -CimClass (Get-CimClass -ClassName MSFT_TaskEventTrigger -Namespace Root/Microsoft/Windows/TaskScheduler) `
        -ClientOnly `
        -Property @{ Enabled = $true; Subscription = $subscriptionXml }

    Register-ScheduledTask `
        -TaskName $TaskName `
        -Action $action `
        -Principal $principal `
        -Settings $settings `
        -Trigger $trigger `
        -Description "Redirects the desktop session to the console when a remote connection disconnects, keeping the desktop alive for UI Automation." | Out-Null

    Write-Host "Session parking task installed."
}

function Uninstall-ParkTask {
    Assert-Administrator

    $existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if (-not $existing) {
        Write-Host "Session parking task is not installed."
        return
    }

    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false

    if (Test-Path $ScriptPath) {
        Remove-Item $ScriptPath -Force
        $scriptDir = Split-Path $ScriptPath -Parent
        if ((Get-ChildItem $scriptDir -Force -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0) {
            Remove-Item $scriptDir -Force
        }
    }

    Write-Host "Session parking task uninstalled."
}

if ($Install -and $Uninstall) {
    Write-Error "Specify either -Install or -Uninstall, not both."
    exit 1
}

if ($Install) {
    Install-ParkTask
} elseif ($Uninstall) {
    Uninstall-ParkTask
} else {
    Write-Host "Usage: .\session-park.ps1 -Install | -Uninstall"
    Write-Host ""
    Write-Host "  -Install     Register the session parking scheduled task."
    Write-Host "  -Uninstall   Remove the session parking scheduled task."
}
