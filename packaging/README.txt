Spiffo's Valheim AI Companions

Download the current package from:
https://github.com/NotUrSpiffo/Spiffos-AI-Companions/releases/latest

The checksum file attached to that page is the one that matches the files published there. This text does not pin an older package hash.

This zip is the playable package. The source code is the Git repository, not this zip.

INSTALL

Close Valheim first.

1. Valheim needs BepInEx. If BepInEx is already installed, do not replace its files.
2. Copy install/BepInEx/plugins/SpiffoAiCompanions.dll into the game folder BepInEx/plugins/. If you are updating this same mod, replace that DLL only.
3. Do not also install Offline Companions Reborn. Reborn 1.5.8 is already inside this DLL. If the log says another copy of Offline Companions Reborn was found, delete that other file with Valheim closed. Do not delete SpiffoAiCompanions.dll.
4. Optional Spanish Reborn menus: copy the Translations folder from install/BepInEx/plugins/OfflineCompanionsReborn/ into the game folder BepInEx/plugins/OfflineCompanionsReborn/. That copy does not replace the DLL. Leave the file name Spanish.json as it is. The game reads that name.

PLAYER2

The mod's default AI provider is Player2. Leave Provider set to Player2 and leave ApiKey empty. Open the Player2 app before you start Valheim. Voice input uses that app. The long setup is in the repository README:
https://github.com/NotUrSpiffo/Spiffos-AI-Companions

CONFIGURATION EXAMPLES

The folder "configuration examples" holds sample files only:
  SpiffoAiCompanions.cfg
  Maddhattr.OfflineCompanionsReborn.cfg

They are optional. On first launch the game writes its own config files under BepInEx/config/. You do not need to copy these samples. If you already edited your config, do not overwrite it with a sample.

NOTICES

Third-party credits and the Reborn MIT license are in the licenses folder. Those texts are unchanged from the package this zip was built from. This package does not add a license for Spiffo's own code.

Spanish manual: LEEME.txt
