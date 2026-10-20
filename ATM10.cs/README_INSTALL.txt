VERSION 0.1.9
Java detection checks JAVA_HOME and common Java 21 JDK install folders (including C:\Program Files\Java\jdk-21*) before PATH.

WindowsGSM.ATM10 - MeFriendos build 0.1.9
==========================================

Purpose
-------
WindowsGSM plugin for Minecraft All the Mods 10 (ATM10) on NeoForge / Minecraft 1.21.1.

Requirements
------------
- Raziel7893/WindowsGSM v1.25.2.1 or a compatible Raziel build
- 64-bit Java 21 available through JAVA_HOME, a common JDK installation folder or PATH
- Official ATM10 ServerFiles-*.zip from CurseForge

Plugin installation
-------------------
1. Extract the folder "ATM10.cs" into <WindowsGSM>\plugins\.
2. Reload plugins or restart WindowsGSM.
3. Download the current ATM10 Server Pack from CurseForge.
4. Put ServerFiles-<version>.zip in the WindowsGSM root folder, plugins\ATM10.cs, or the target server's serverfiles folder.
5. Add/install "Minecraft: All the Mods 10 (ATM10)" in WindowsGSM.
6. Accept the Minecraft EULA prompt.
7. Configure RAM in <serverfiles>\user_jvm_args.txt.
8. Start the server from WindowsGSM.

What the plugin manages
-----------------------
- Starts NeoForge directly with Java 21.
- Embedded WindowsGSM console support.
- Graceful "stop" command.
- server.properties values from WindowsGSM.
- Local ATM10 version marker.
- Best-effort latest-version detection from the public CurseForge project page.
- Removal of WindowsGSM's automatic application exception for this server's exact startserver.bat path.

Firewall behavior
-----------------
Automatic firewall port opening is intentionally disabled in this MeFriendos build.

WindowsGSM may create an automatic application exception for startserver.bat before the server starts. The plugin removes that exact program exception through the same Windows Firewall COM API family used by WindowsGSM itself, then verifies that the exception is gone before continuing.

The cleanup does not depend on PowerShell NetSecurity cmdlets. It does not create or remove manually configured port rules, and it does not select rules for other program paths.

Configure only the required Minecraft ports manually with the correct protocol, Windows profile and remote scope.

If Windows cannot verify or remove the matching automatic exception, the plugin refuses to start the server and shows an error. Run WindowsGSM as administrator.

Updating ATM10
--------------
1. Stop the server and create a world backup.
2. Download the new official ServerFiles-*.zip from CurseForge.
3. Put it in one of the supported locations.
4. Click UPDATE in WindowsGSM.

During update the plugin preserves:
- eula.txt
- server.properties
- ops.json
- whitelist.json
- banned-ips.json
- banned-players.json
- world / world_nether / world_the_end
- logs / crash-reports / backups
- user_jvm_args.txt

Updates are prepared in a separate copy. The previous serverfiles directory is kept in servers\<id>\atm10-update-backups\<timestamp>\serverfiles after a successful update.

Custom files in config and defaultconfigs are kept. Unchanged defaults can be updated when the previous defaults are known. On the first update from an older plugin, existing configuration files are kept conservatively.

Check config-conflicts.txt in the backup folder before starting the server. The new pack defaults are saved in incoming-config for comparison. Files are compared as a whole, not merged line by line.

Keep enough free disk space for a complete copy of serverfiles, the extracted pack and the installer. Backups are not removed automatically. Linked files and directories require a manual update.

If preparation fails, the existing server stays untouched. If activation fails, the plugin tries to restore the previous server. An interrupted activation leaves recovery instructions beside serverfiles and blocks startup until checked.

Important
---------
- Always make a world backup before an ATM10 update.
- Client and server ATM10 versions must match.
- The plugin does not distribute ATM10 files, mods, NeoForge or Minecraft.
- This is a custom MeFriendos plugin and has not been officially endorsed by the ATM Team, CurseForge or WindowsGSM.

Troubleshooting
---------------
"Java was not found" / "requires Java 21"
  Install 64-bit Java 21 and make sure java -version reports Java 21.

"ATM10 server pack not found"
  Put ServerFiles-*.zip in one of the supported locations and click Install/Update again.

"NeoForge runtime not found"
  The NeoForge installer did not finish successfully. Check the WindowsGSM console and ensure Java 21 is active.
