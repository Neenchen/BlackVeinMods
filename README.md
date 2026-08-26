# BlackVeinMods

Standalone V Rising server mods.

## Projects

- `BlackVeinCore`: shared core plugin for reusable ECS helpers, prefab data, player wrappers, config persistence, file reload watchers, raw Harmony patches, and generic game events. This behaves like a small local ModCore for the BlackVein mods.
- `BlackVeinMods`: clan progression sharing mod. Depends on `BlackVeinCore`.
- `LevelRecipeGate`: recipe crafting, forge repair, and weapon equip gating by recorded maximum player gear level. Depends on `BlackVeinCore`.
- `BlackVeinKill`: CustomKill-compatible PvP killfeed, stats, assists, FFA kill marking, and auto-updated Discord top 25 leaderboard. Depends on `BlackVeinCore`.

## Dependencies

- BepInEx Pack for V Rising
- VampireCommandFramework (`deca-VampireCommandFramework-0.11.0`)
- Bloodpebble (`cheesasaurus-Bloodpebble-1.4.2`) for hot reload support
- HookDOTS API (`cheesasaurus-HookDOTS_API-1.1.1`)

Bloodpebble is not referenced by the projects. HookDOTS API is a runtime dependency of `BlackVeinCore`. The plugin assemblies are marked reloadable/unloadable and clean up Harmony patches, VCF command registrations, managers, and file watchers during `Unload()`.

Build the solution to copy all plugin assemblies into `BepInEx/BloodpebblePlugins`:

```powershell
dotnet build .\BlackVeinMods.sln -c Release
```

## Clan Progression

BlackVeinMods currently shares V Blood boss progression, V Blood tech unlocks, research unlocks, and claimed journal entries with online clan members. Harmony patches only publish internal events; managers subscribe during `Initialize()` and unsubscribe during `Dispose()`.

Configuration lives at:

`BepInEx/config/BlackVeinMods/blackvein_config.json`

Relevant options:

- `ClanProgression.Enabled`
- `ClanProgression.ShareBossUnlocks`
- `ClanProgression.ShareTechnologyUnlocks` - shares the boss's tech unlocks only when `ShareBossUnlocks` is `false`; sharing the boss itself already grants its tech.
- `ClanProgression.ShareJournalProgression`
- `ClanProgression.ShareResearchProgression`
- `ClanProgression.IncludeOfflineClanMembers`

Set `ShareBossUnlocks` to `false` and keep `ShareTechnologyUnlocks` as `true` for tech-only sharing.

## Level Recipe Gate

LevelRecipeGate stores config at:

`BepInEx/config/LevelRecipeGate/level_recipe_blocks.json`

It stores recorded maximum player gear levels at:

`BepInEx/config/LevelRecipeGate/player_highest_levels.json`

It blocks configured recipes across normal crafting, ancestral forge repair/start, and weapon equip attempts by resolving weapon items through their repair recipe. The raw game patches live in `BlackVeinCore` and publish `GameEvents`; LevelRecipeGate only subscribes to those events and applies its gate logic. Config file reloads and writes use `BlackVeinCore.Persistence`.

## BlackVeinKill

BlackVeinKill keeps CustomKill storage paths for drop-in compatibility:

- Config: `BepInEx/config/BlackVeinMods/blackvein_kill.json`
- Database: `BepInEx/config/CustomKillDB/PlayerStats.db`

If `blackvein_kill.json` does not exist yet, BlackVeinKill writes a commented default config. Changes to `blackvein_kill.json` are watched and reloaded automatically.

FFA zones are configured under `FfaZones`:

- `EnableFfaZones`: enables FFA hot zones where level rules do not apply.
- `EnableT2RiftFfaZones`: marks active major rift zones as FFA hot zones.
- `MapZoneIds`: JSON array of map-zone hashes.
- `RegionNames`: JSON array of `WorldRegionType` names.

Set `Discord.LeaderboardWebhookURL` to keep a single Discord webhook message updated with the top 25 players after every kill.
