# Changelog

## 0.1.9 — 2026-10-16

- Keeps custom files in `config` and `defaultconfigs` during updates.
- Updates unchanged defaults when the previous pack defaults are available.
- Lists conflicting files and keeps the new pack defaults alongside the backup.
- Prepares updates separately and keeps the previous server folder as a full backup.
- Leaves the current server untouched if preparation or the NeoForge installer fails.
- Handles read-only files in the prepared copy and adds recovery instructions for interrupted updates.

## 0.1.8 — 2026-09-10

- Replaced the PowerShell `NetSecurity` firewall cleanup with the Windows Firewall COM API used by WindowsGSM itself.
- Removes WindowsGSM's automatic program exception for the exact `startserver.bat` path before Java starts.
- Verifies that the automatic exception is gone before allowing the server to launch.
- Keeps manually configured port rules unchanged.
- Avoids startup failures caused by unavailable or unreliable `Get-NetFirewall*` cmdlets.

## 0.1.7 — 2026-09-09

- Uses the PapaGordon GitHub repository as the plugin project URL.
- Removes WindowsGSM's broad automatic inbound rule for this server's exact `startserver.bat` path before Java starts.
- Prevents server startup if the firewall safety check itself fails.
- Keeps automatic port opening disabled and leaves manual firewall rules unchanged.
- Added GitHub-ready documentation, security guidance and a testing checklist.

## 0.1.6

- Install/Reinstall preserves existing `-Xms` and `-Xmx` values from `user_jvm_args.txt`.

## 0.1.5

- Preserves an existing `motd` in `server.properties`.

## 0.1.4

- Skips reinstalling an already installed ATM10 server-pack version.
- Waits for the NeoForge updater internally so WindowsGSM no longer remains stuck on the update screen.
- Preserves worlds, server properties, player lists and JVM memory settings during updates.

## 0.1.3

- Fixed the WindowsGSM process-stream conflict during Install/Update.

## 0.1.2 and earlier

- Added local server-pack installation, NeoForge setup, embedded-console support and version tracking.
