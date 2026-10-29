WindowsGSM.ATM10 v0.1.12 - MeFriendos
====================================

Use the latest host: https://github.com/PapaGordon/WindowsGSM/releases/latest
On the MeFriendos fork, install WindowsGSM v1.25.4.4 or newer: the built-in ATM10 code comes from its EXE. A plugin ZIP alone does not replace built-in code.

For other compatible hosts, extract the entire ATM10.cs folder into plugins and reload plugins.
Use 64-bit Java 21 and an official ATM10 ServerFiles-*.zip. Place the ZIP in the WGSM root, plugins\ATM10.cs or this server's serverfiles directory. Install, accept the Minecraft EULA if you agree, and set RAM in user_jvm_args.txt.

Update safely
-------------
Stop the server and back up the world. Place the new original ServerFiles ZIP in a supported location and click Update.
The update is prepared separately. Worlds, lists, JVM settings and additional custom mod JARs are retained. The previous serverfiles directory is kept in atm10-update-backups.
The pack-managed Better Compatibility Checker identity follows the selected original ZIP, so an 8.3 update no longer advertises 8.1. Other local BCC settings are preserved.

First migration from older plugin versions
-----------------------------------------
A SHA-256 ownership inventory identifies the files belonging to the official pack. If it does not exist yet, leave the previous original ServerFiles-<installed-version>.zip beside the new ZIP. Never create that original ZIP from your customized server folder.
If a previous update already wrote an 8.3 marker but BCC still says 8.1, the available original 8.3 ZIP can provide the ownership baseline while the stale identity is repaired.
Without a saved inventory or a matching original ZIP, the updater stops rather than guessing which JARs are yours. Additional file/mod-ID conflicts also stop before activation and name the problem.

Read README.md for configuration merging, disk space, recovery and migration details. Backups are not deleted automatically.
Firewall rules are owned by the WGSM host. This plugin does not modify them; configure game/query ports manually if the host does not manage them.
The native ATM10 logo belongs to the All the Mods Team. See licenses/Native-artwork.txt. The game, modpack and Java are not redistributed here.

Older JARs containing only mod IDs supplied by the incoming pack are replaced by the new pack versions. This fixes leftover CC:Tweaked versions such as 1.113.1 beside 1.120.2. A JAR containing additional IDs as well remains a conflict. Full previous server files remain in the update backup.
