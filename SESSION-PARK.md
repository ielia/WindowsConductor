# Session Parking for Remote Desktops

When running WindowsConductor on a remote machine (e.g. an Azure Cloud PC) accessed via RDP or Windows App, minimizing or closing the remote desktop connection causes the desktop compositor to stop rendering. This makes UI Automation calls fail with "Access is denied" — screenshots, clicks, and all other element interactions break.

The `session-park.ps1` script solves this by installing a Windows scheduled task that automatically redirects the desktop session to the local console whenever a remote connection disconnects. This keeps the desktop alive so UI Automation continues to work even when no remote client is connected.

## How it works

1. You disconnect from the remote desktop (minimize, close, or lose connection).
2. Windows logs a Terminal Services disconnect event (Event ID 24).
3. The scheduled task fires, reads the disconnected session ID from the event, and runs `tscon <sessionId> /dest:console`.
4. The session is redirected to the local console. The desktop compositor keeps rendering.
5. UI Automation, screenshots, clicks — everything continues working.
6. When you reconnect (reopen Windows App), you pick up the same session seamlessly.

## Installation

Run once from an **elevated** (Administrator) PowerShell prompt on the remote machine:

```powershell
.\session-park.ps1 -Install
```

This creates:
- A park script at `%ProgramData%\WindowsConductor\ParkSession.ps1`
- A scheduled task named `WindowsConductor_ParkSession` that runs as SYSTEM and triggers on remote session disconnect

The task persists across reboots. No manual steps are needed after installation — session parking is fully automatic.

## Uninstallation

```powershell
.\session-park.ps1 -Uninstall
```

Removes the scheduled task and deletes the park script.

## Requirements

- Administrator privileges (for installation/uninstallation only)
- `tscon.exe` must be available on the machine (present on standard Windows 10/11 and Windows Server installations)

## Use in CI/CD pipelines

For Azure DevOps or other CI pipelines running UI tests on a remote machine:

1. Install the session parking task once during machine setup.
2. Configure auto-logon so an interactive desktop session exists after reboot:
   ```powershell
   $regPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
   Set-ItemProperty -Path $regPath -Name AutoAdminLogon -Value "1"
   Set-ItemProperty -Path $regPath -Name DefaultUserName -Value "your-user"
   Set-ItemProperty -Path $regPath -Name DefaultPassword -Value "your-password"
   ```
3. Run the pipeline agent interactively (not as a Windows Service) in the auto-logon session.

With this setup, the desktop stays alive regardless of RDP connection state, and UI Automation tests run reliably.
