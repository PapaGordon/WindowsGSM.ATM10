<p align="center">
  <img src="ATM10.cs/ATM10.png" alt="All the Mods 10" width="128">
</p>

<h1 align="center">WindowsGSM.ATM10</h1>

<p align="center">
  MeFriendos build for running Minecraft: All the Mods 10 with WindowsGSM.
</p>

<p align="center">
  <a href="https://github.com/Raziel7893/WindowsGSM/releases/tag/v1.25.2.2"><img src="https://img.shields.io/badge/WindowsGSM-Raziel%20v1.25.2.2-38CDD4" alt="Raziel WindowsGSM v1.25.2.2"></a>
  <a href="CHANGELOG.md"><img src="https://img.shields.io/badge/version-0.1.11-7AC943" alt="Version 0.1.11"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
</p>

WindowsGSM plugin for Minecraft: All the Mods 10 on NeoForge and Minecraft 1.21.1.

The plugin installs a locally supplied official ATM10 server pack, manages the NeoForge runtime and starts the server directly with Java 21.

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

- A compatible WindowsGSM build with per-server **Auto Firewall**, such as [our WindowsGSM build](https://github.com/PapaGordon/WindowsGSM)
- 64-bit Java 21 or newer
- The official ATM10 `ServerFiles-*.zip` from [CurseForge](https://www.curseforge.com/minecraft/modpacks/all-the-mods-10)

ATM10, NeoForge, Minecraft and the server pack are not distributed with this plugin.

## Plugin installation

1. Download the [latest release archive](https://github.com/PapaGordon/WindowsGSM.ATM10-MeFriendos/releases/latest).
2. Extract the folder `ATM10.cs` into the WindowsGSM `plugins` folder.
3. Click **Reload Plugins** or restart WindowsGSM.
4. Download the matching ATM10 server pack from CurseForge.
5. Place `ServerFiles-<version>.zip` in the WindowsGSM root folder, `plugins\ATM10.cs`, or the target server's `serverfiles` folder.
6. Install **Minecraft: All the Mods 10 (ATM10)** in WindowsGSM and accept the Minecraft EULA prompt.
7. Configure memory in `user_jvm_args.txt`, for example `-Xms8G` and `-Xmx12G`.

## Updating ATM10

1. Stop the server and create a world backup.
2. Download the new official ATM10 `ServerFiles-*.zip`.
3. Place it in one of the supported locations.
4. Click **Update** in WindowsGSM.

The update is prepared in a separate copy of the server directory. NeoForge must finish successfully before the new copy replaces `serverfiles`. The previous directory is kept in `servers\<id>\atm10-update-backups\<timestamp>\serverfiles`.

`config` and `defaultconfigs` are compared file by file with the previous pack defaults. Unchanged defaults receive the pack update; custom files and local deletions are kept. On the first update from an older plugin, existing files are kept because no previous defaults are available. New files are added.

If both you and the pack changed a file, your copy stays in place. Check `config-conflicts.txt` and the new defaults in `incoming-config` inside the backup folder before starting the server. Config files are not merged line by line.

Allow enough free disk space for another complete copy of `serverfiles`, the extracted pack and the installer. Backups are kept until you remove them. Linked files and directories need a manual update.

If preparation fails, the existing server directory stays untouched. If activation fails, the plugin tries to restore the previous directory. After an interrupted activation, `.wgsm-atm10-update-recovery.txt` beside `serverfiles` contains the recovery paths and blocks startup until checked.

Client and server ATM10 versions must match.

## Firewall

Firewall management follows WindowsGSM's per-server **Auto Firewall** setting. This plugin does not create, remove or inspect firewall rules and does not block startup for a plugin firewall check.

Use a WindowsGSM build with **Auto Firewall**, such as our [WindowsGSM build](https://github.com/PapaGordon/WindowsGSM). For the MeFriendos setup with manual port rules, turn **Auto Firewall** off for this server in WindowsGSM and configure the required ports yourself. Router forwarding remains separate.

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

- Source: [PapaGordon/WindowsGSM.ATM10-MeFriendos](https://github.com/PapaGordon/WindowsGSM.ATM10-MeFriendos)
- ATM10: [CurseForge](https://www.curseforge.com/minecraft/modpacks/all-the-mods-10)
- WindowsGSM: [Raziel7893/WindowsGSM](https://github.com/Raziel7893/WindowsGSM/releases)
- Community: [mefriendos.de](https://mefriendos.de)

This is an independent community plugin. It is not affiliated with or endorsed by the ATM Team, CurseForge, NeoForge, Mojang, Microsoft or WindowsGSM.

## License

Maintained by Brandon Hanke (PapaGordon), owner of MeFriendos.de.

Released under the [MIT License](LICENSE).

You can use, modify and redistribute this plugin, including commercially. Keep the copyright and license notices with any copies or substantial portions of the code.
