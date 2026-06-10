# Session Parking for Remote Desktops

When running WindowsConductor on a remote machine (e.g. an Azure Cloud PC) accessed via RDP or Windows App, closing or disconnecting the remote desktop connection causes the desktop compositor to stop rendering. This makes UI Automation calls fail with "Access is denied" — screenshots, clicks, and all other element interactions break.

The `session-park.ps1` script solves this by installing a Windows scheduled task that automatically redirects the desktop session to the local console whenever a remote connection disconnects. This keeps the desktop alive so UI Automation continues to work even when no remote client is connected.

## How it works

1. You disconnect from the remote desktop (close the window, use "Disconnect" from the start menu, or lose connection).
2. Windows logs a Terminal Services disconnect event (Event ID 24).
3. The scheduled task fires, waits for the session state to settle, reads the disconnected session ID from the event, and runs `tscon <sessionId> /dest:console`.
4. The session is redirected to the local console. The desktop compositor keeps rendering.
5. UI Automation, screenshots, clicks — everything continues working.
6. When you reconnect (reopen Windows App), you pick up the same session seamlessly.

**Important:** to trigger session parking, you must **disconnect** (close the remote desktop window or use "Disconnect"), not **minimize**. See [Known limitations](#known-limitations) below.

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
2. Configure auto-logon so an interactive desktop session exists after reboot (see [limitation #1](#known-limitations)):
   ```powershell
   $regPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
   Set-ItemProperty -Path $regPath -Name AutoAdminLogon -Value "1"
   Set-ItemProperty -Path $regPath -Name DefaultUserName -Value "your-user"
   Set-ItemProperty -Path $regPath -Name DefaultPassword -Value "your-password"
   ```
3. Run the pipeline agent interactively (not as a Windows Service) in the auto-logon session.

With this setup, the desktop stays alive regardless of RDP connection state, and UI Automation tests run reliably.

## Known limitations

1. **Auto-logon required after reboot.** Session parking redirects an existing desktop session to the console — it cannot create one. If the machine reboots and nobody logs in, there is no session to park and UI Automation will not work. Configure auto-logon (see [CI/CD pipelines](#use-in-cicd-pipelines) above) to ensure a desktop session is always available.

2. **Minimizing the remote desktop window breaks UI Automation.** When a remote desktop client (e.g. Windows App) is minimized rather than disconnected, the RDP session stays "Active" but the client stops requesting graphics updates. The desktop compositor pauses rendering, causing UI Automation calls to fail with "Access is denied." Session parking cannot help in this case because no disconnect event fires. To avoid this, always **disconnect** (close the window or use "Disconnect") instead of minimizing. The parked session will keep the desktop alive and you can reconnect at any time.

3. **5-second delay after disconnection.** After a remote session disconnects, the park script waits 5 seconds before redirecting the session to the console. This delay is necessary to distinguish a genuine disconnect from a reconnection (which briefly fires a disconnect event for the console session). During this window, UI Automation calls may fail. For interactive use this is negligible; for latency-sensitive pipelines, be aware of this brief gap after disconnection.
