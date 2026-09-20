VERSION 0.1.8
Java detection checks JAVA_HOME and common Java 21 JDK install folders (including C:\Program Files\Java\jdk-21*) before PATH.

WindowsGSM.ATM10 - MeFriendos build 0.1.8
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
- existing -Xms / -Xmx values from user_jvm_args.txt

The modpack-managed directories from the new server pack are replaced with the versions from the new pack to avoid stale mods.

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

0.1.8 FIREWALL COMPATIBILITY
----------------------------
- Replaced the PowerShell NetSecurity firewall cleanup with the Windows Firewall COM API used by WindowsGSM itself.
- Removes the exact automatic startserver.bat application exception before Java starts.
- Verifies the exception is gone before startup continues.
- Leaves manually configured port rules unchanged.
