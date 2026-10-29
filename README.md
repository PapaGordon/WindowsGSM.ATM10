<p align="center">
  <img src="ATM10.cs/ATM10.png" alt="All the Mods 10" width="128">
</p>

<h1 align="center">WindowsGSM.ATM10</h1>

<p align="center">
  MeFriendos build for running Minecraft: All the Mods 10 with WindowsGSM.
</p>

<p align="center">
  <a href="https://github.com/PapaGordon/WindowsGSM/releases/latest"><img src="https://img.shields.io/badge/WindowsGSM-MeFriendos%20latest-38CDD4" alt="Latest MeFriendos WindowsGSM"></a>
  <a href="CHANGELOG.md"><img src="https://img.shields.io/badge/version-0.1.15-7AC943" alt="Version 0.1.15"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
</p>

WindowsGSM plugin for Minecraft: All the Mods 10 on NeoForge and Minecraft 1.21.1.

The plugin installs a locally supplied official ATM10 server pack, manages the NeoForge runtime and starts the server directly with Java 21.


## WindowsGSM and Steam login

Use the [latest MeFriendos WindowsGSM release](https://github.com/PapaGordon/WindowsGSM/releases/latest). This server is already built into that fork, so select its normal built-in entry there. The standalone plugin remains available for other compatible WindowsGSM builds.

This plugin does not use SteamCMD or a Steam account. Encrypted Steam credentials do not affect its installation or updates.

The plugin leaves firewall management to the host. On other WindowsGSM builds, configure game/query port rules manually if the host does not manage them. Router forwarding is separate.

## Features

- Installs ATM10 from an official local `ServerFiles-*.zip`.
- Detects Java 21 through `JAVA_HOME`, common Windows JDK locations and `PATH`.
- Runs the bundled NeoForge installer.
- Supports the embedded WindowsGSM console and graceful shutdown.
- Writes the WindowsGSM server port, query port and player limit to `server.properties`.
- Keeps a full backup after updates and preserves worlds, configuration changes, player lists and memory settings.
- Skips a reinstall when the selected server pack is already installed.
- Leaves firewall management to WindowsGSM's per-server **Auto Firewall** setting.

## Requirements

MeFriendos uses this plugin with [WindowsGSM (latest release)](https://github.com/PapaGordon/WindowsGSM/releases/latest). The Windows x64 download includes the .NET runtime.

- [MeFriendos WindowsGSM (latest release)](https://github.com/PapaGordon/WindowsGSM/releases/latest) with per-server **Auto Firewall**
- 64-bit Java 21 or newer
- The official ATM10 `ServerFiles-*.zip` from [CurseForge](https://www.curseforge.com/minecraft/modpacks/all-the-mods-10)

ATM10, NeoForge, Minecraft and the server pack are not distributed with this plugin.

## Plugin installation

1. Download the [latest release archive](https://github.com/PapaGordon/WindowsGSM.ATM10/releases/latest).
2. Extract the folder `ATM10.cs` into the WindowsGSM `plugins` folder.
3. Click **Reload Plugins** or restart WindowsGSM.
4. Download the matching ATM10 server pack from CurseForge.
5. Place `ServerFiles-<version>.zip` in the WindowsGSM root folder, `plugins\ATM10.cs`, or the target server's `serverfiles` folder.
6. Install **Minecraft: All the Mods 10 (ATM10)** in WindowsGSM and accept the Minecraft EULA prompt.
7. Configure memory in `user_jvm_args.txt`, for example `-Xms8G` and `-Xmx12G`.

## Pack versions and additional mods

The offered update version comes from the BCC identity inside the selected local server ZIP, not a cached CurseForge page or the ZIP filename alone. The installed version follows the BCC value that clients actually see, with the legacy WGSM marker used only when no BCC file exists.

After the normal configuration merge, the pack-managed BCC identity (`modpackName`, `modpackProjectID`, `modpackVersion` and `useMetadata`) follows the selected original pack. Other local BCC settings remain unchanged. This fixes a server still advertising 8.1 after its pack files were updated to 8.3. The WGSM host also refreshes its success message after an update in v1.25.4.3 or newer.

Additional custom mod JARs are kept. The updater records the official pack files and their SHA-256 hashes in `.wgsm-atm10-config-defaults/pack-mods.sha256`. It replaces or removes old pack files only when their current bytes match that record. It does not guess ownership from a filename. The pack replaces older JARs with the same mod IDs, even if a legacy installer left them outside the ownership inventory. Separate additional mod IDs are preserved. An overlapping bundle with extra IDs still stops preparation before activation.

**First update from an older plugin:** if no ownership record exists, leave the previous original `ServerFiles-<installed-version>.zip` beside the new ZIP. For example, keep both the original 8.1 pack and the new 8.3 pack available. The updater reads the old ZIP to identify its pack-owned mods. Without an inventory or a matching original ZIP, it refuses to delete unknown mods and leaves the running installation unchanged. Do not create the old ZIP from a server folder containing your additional mods.

The first migration may take longer because the original pack files are hashed. Keep your world backup and allow space for the prepared server copy. A mod added by you is not automatically downloaded or upgraded; you remain responsible for its compatibility with the new pack.
## Updating ATM10

1. Stop the server and create a world backup.
2. Download the new official ATM10 `ServerFiles-*.zip`.
3. Place it in one of the supported locations.
4. Click **Update** in WindowsGSM.

The update is prepared in a separate copy of the server directory. NeoForge must finish successfully before the new copy replaces `serverfiles`. The previous directory is kept in `servers\<id>\atm10-update-backups\<timestamp>\serverfiles`.

`config` and `defaultconfigs` are compared file by file with the previous pack defaults. Unchanged defaults receive the pack update; custom files and local deletions are kept. On the first update from an older plugin, existing files are kept because no previous defaults are available. New files are added.

For ordinary configuration files, if both you and the pack changed a file, your copy stays in place. The BCC pack identity and the registry definition described below follow the pack. Check `config-conflicts.txt` and the new defaults in `incoming-config` inside the backup folder before starting the server. Config files are not merged line by line.

Allow enough free disk space for another complete copy of `serverfiles`, the extracted pack and the installer. Backups are kept until you remove them. Linked files and directories need a manual update.

If preparation fails, the existing server directory stays untouched. If activation fails, the plugin tries to restore the previous directory. After an interrupted activation, `.wgsm-atm10-update-recovery.txt` beside `serverfiles` contains the recovery paths and blocks startup until checked.

Client and server ATM10 versions must match.

## Firewall

Firewall management follows WindowsGSM's per-server **Auto Firewall** setting. This plugin does not create, remove or inspect firewall rules and does not block startup for a plugin firewall check.

Use [MeFriendos WindowsGSM](https://github.com/PapaGordon/WindowsGSM/releases/) with **Auto Firewall**. For the MeFriendos setup with manual port rules, turn **Auto Firewall** off for this server in WindowsGSM and configure the required ports yourself. Router forwarding remains separate.

Create only the Minecraft game and query rules required by your setup. Keep administrative services restricted to a Private VPN or trusted subnet.

## Troubleshooting

### Java 21 was not found

Install a 64-bit Java 21 JDK. Set `JAVA_HOME` or add its `bin` directory to `PATH`. Common locations such as `C:\Program Files\Java\jdk-21*` are checked automatically.

### ATM10 server pack was not found

Make sure you downloaded the separate official server pack named `ServerFiles-*.zip`, not the normal client modpack.

### NeoForge runtime was not found

Run **Install** or **Update** again and inspect the WindowsGSM console for errors from the NeoForge installer.

## Running the update checks

With the .NET 8 SDK installed, run `dotnet run --project tests/UpdateSafety.csproj`. The checks compile the plugin as C# 6 with WindowsGSM stubs and exercise configuration handling, backups and failed updates. They do not start Minecraft. A WindowsGSM update on Windows still needs an installation test.

## Testing checklist

- WindowsGSM loads `ATM10.cs` without a plugin error.
- Java 21 and the NeoForge `win_args.txt` are detected.
- Install and Update finish without a stuck WindowsGSM progress state.
- Existing config files and user_jvm_args remain unchanged after Install/Update.
- The embedded console receives server output and the **Stop** action shuts down cleanly.
- Auto Firewall on/off is controlled by WindowsGSM and is not overridden by the plugin.
- Manually configured port rules remain present.

## Project links

- Source: [PapaGordon/WindowsGSM.ATM10](https://github.com/PapaGordon/WindowsGSM.ATM10)
- ATM10: [CurseForge](https://www.curseforge.com/minecraft/modpacks/all-the-mods-10)
- WindowsGSM: [MeFriendos WindowsGSM (latest release)](https://github.com/PapaGordon/WindowsGSM/releases/latest)
- Community: [mefriendos.de](https://mefriendos.de)

This is an independent community plugin. It is not affiliated with or endorsed by the ATM Team, CurseForge, NeoForge, Mojang, Microsoft or WindowsGSM.

## License

Maintained by Brandon Hanke (PapaGordon), owner of MeFriendos.de.

Released under the [MIT License](LICENSE).

You can use, modify and redistribute this plugin, including commercially. Keep the copyright and license notices with any copies or substantial portions of the code.

Older leftover JARs absent from the incoming pack are replaced when all their mod IDs are supplied by that pack. This fixes leftover CC:Tweaked versions such as 1.113.1 beside 1.120.2. A JAR containing additional IDs as well remains a conflict. Full previous server files remain in the update backup.

## Multiple versions shipped by the pack

Keep the original ServerFiles ZIP unchanged. If it supplies multiple versions of the same mod, such as CC:Tweaked 1.113.1 and 1.120.2, the updater keeps both candidates. [NeoForge's Minecraft 1.21.1 discovery](https://github.com/neoforged/FancyModLoader/blob/1.21.1/loader/src/main/java/net/neoforged/fml/loading/UniqueModListBuilder.java) selects the newest candidate for the same first mod ID. The updater does not duplicate that loader decision. This fixes a false WGSM conflict in v0.1.13 / WGSM v1.25.4.4. Additional mod conflicts are still checked separately.

## Neo Vitae registry definition

config/neovitae/materials.json defines registered items and must agree with the matching client pack. From plugin v0.1.15 / WGSM v1.25.4.6, this file follows the selected original server ZIP, including when a local edit exists. Other preferences keep their normal merge behavior. The previous file stays under serverfiles in the full update backup; pack-managed-configs.txt lists the synchronized definition.

Stop the server and click Update again even if WGSM already reports 8.3. A stale registry definition now allows that same-version repair; matching data still skips the reinstall. Restart afterward. Use the same ATM10 client version, and keep the original ZIP unchanged.

The pack intentionally removed the Iesnium, Titanium, Tungsten, generic Xychorium, Bauxite and Yellorite material definitions. Existing stacks of those Neo Vitae items can be lost when the world loads with the new definitions. Keep the world/update backup. Do not delete your world or remove Neo Vitae itself. See the [official pack change](https://github.com/AllTheMods/ATM-10/pull/4370).

Checked with 50 update-safety fixtures per source, including a local reproduction using the official 8.1 and 8.3 material files and checks in the final self-contained WGSM EXE. Multiplayer joins still need checking on the target server.
