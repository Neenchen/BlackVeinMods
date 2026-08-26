using BlackVeinCore;
using BlackVeinCore.Data;
using BlackVeinCore.Events;
using BlackVeinCore.Services;
using BlackVeinCore.Tools;
using BlackVeinCore.Utilities;
using ProjectM;
using ProjectM.Shared.WarEvents;
using ProjectM.Terrain;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace BlackVeinKill.Managers;

public static class FfaZoneManager
{
    private const int PlayerZoneCheckIntervalFrames = 120;

    private static readonly HashSet<int> ActiveT2RiftZones = new();
    private static readonly HashSet<int> PendingActiveT2RiftZones = new();
    private static readonly HashSet<ulong> PlayersInFfaZones = new();
    private static readonly Dictionary<ulong, string> PlayersInLevelRuleZones = new();
    private static readonly HashSet<ulong> FfaVisualBuffOwners = new();
    private static readonly Dictionary<ulong, int> VisualBuffRemovalVersions = new();
    private static readonly Action ConfigReloadedHandler = () =>
    {
        Reload();
        ActionScheduler.RunActionOnMainThread(CheckForPlayerZones);
    };

    private static readonly Dictionary<int, TerrainChunk> RiftZoneToChunk = new()
    {
        { -1193144519, new TerrainChunk(18, 16) },
        { -1458777401, new TerrainChunk(16, 14) },
        { -1778959663, new TerrainChunk(16, 15) },
        { -486021887, new TerrainChunk(15, 16) },
        { 99793257, new TerrainChunk(17, 17) },
        { 1628438577, new TerrainChunk(15, 18) },
    };

    private static readonly Dictionary<TerrainChunk, int> ChunkToRiftZone = RiftZoneToChunk.ToDictionary(pair => pair.Value, pair => pair.Key);
    private static EntityQuery ActiveWarEventQuery;
    private static bool HasActiveWarEventQuery;
    private static bool IsRunning;
    private static FfaZoneSnapshot CurrentZones = FfaZoneSnapshot.Empty;
    private static MapZoneCollectionSystem? MapZoneCollectionSystem;
    private static MapZoneCollection MapZoneCollection;

    public static void Initialize()
    {
        Reload();
        KillfeedSettings.ConfigReloaded += ConfigReloadedHandler;
        GameEvents.OnPlayerConnected += HandlePlayerConnected;
        GameEvents.OnPlayerDisconnected += HandlePlayerDisconnected;
        IsRunning = true;
        CheckForPlayerZones();
        ScheduleNextZoneCheck();
    }

    public static void Dispose()
    {
        IsRunning = false;
        RemoveAllFfaVisualBuffs();
        KillfeedSettings.ConfigReloaded -= ConfigReloadedHandler;
        GameEvents.OnPlayerConnected -= HandlePlayerConnected;
        GameEvents.OnPlayerDisconnected -= HandlePlayerDisconnected;
        ActiveT2RiftZones.Clear();
        PendingActiveT2RiftZones.Clear();
        PlayersInFfaZones.Clear();
        PlayersInLevelRuleZones.Clear();
        FfaVisualBuffOwners.Clear();
        VisualBuffRemovalVersions.Clear();
        CurrentZones = FfaZoneSnapshot.Empty;
        MapZoneCollectionSystem = null;
        MapZoneCollection = default;
        DisposeActiveWarEventQuery();
    }

    public static bool IsFfaKill(Player killer, Player victim)
    {
        return IsInFfaZone(victim) || IsInFfaZone(killer);
    }

    public static bool TryGetLevelRuleMaxGap(Player killer, Player victim, out int maxLevelGap)
    {
        maxLevelGap = 0;
        if (!KillfeedSettings.EnableLevelRuleZones)
        {
            return false;
        }

        var snapshot = CurrentZones;
        foreach (var rule in snapshot.LevelRuleZones)
        {
            if (IsPlayerInLevelRuleZone(killer, rule) || IsPlayerInLevelRuleZone(victim, rule))
            {
                maxLevelGap = rule.MaxLevelGap;
                return true;
            }
        }

        return false;
    }

    public static bool IsInFfaZone(Player player)
    {
        if (!KillfeedSettings.EnableFfaZones)
        {
            return false;
        }

        return IsInRegionFfaZone(player) || IsInMapFfaZone(player);
    }

    private static void Reload()
    {
        var mapZoneIds = new HashSet<int>();
        var regionZones = new HashSet<WorldRegionType>();
        var levelRuleZones = new List<LevelRuleZoneSnapshot>();

        foreach (var zoneId in KillfeedSettings.FfaMapZoneIds)
        {
            mapZoneIds.Add(zoneId);
        }

        foreach (var token in KillfeedSettings.FfaRegionNames)
        {
            if (Enum.TryParse<WorldRegionType>(token, true, out var region))
            {
                regionZones.Add(region);
            }
            else
            {
                Plugin.Logger.LogWarning($"[BlackVeinKill] Ignoring invalid FFA region '{token}'.");
            }
        }

        foreach (var rule in KillfeedSettings.LevelRuleZoneRules)
        {
            var ruleRegionZones = new HashSet<WorldRegionType>();
            foreach (var token in rule.RegionNames)
            {
                if (Enum.TryParse<WorldRegionType>(token, true, out var region))
                {
                    ruleRegionZones.Add(region);
                }
                else
                {
                    Plugin.Logger.LogWarning($"[BlackVeinKill] Ignoring invalid level-rule region '{token}' in '{rule.Name}'.");
                }
            }

            var ruleMapZoneIds = rule.MapZoneIds.ToHashSet();
            if (ruleMapZoneIds.Count > 0 || ruleRegionZones.Count > 0)
            {
                levelRuleZones.Add(new LevelRuleZoneSnapshot(rule.Name, rule.MaxLevelGap, ruleMapZoneIds, ruleRegionZones));
            }
        }

        CurrentZones = new FfaZoneSnapshot(mapZoneIds, regionZones, levelRuleZones);
        Plugin.Logger.LogInfo($"[BlackVeinKill] Loaded {mapZoneIds.Count} FFA map zone(s), {regionZones.Count} FFA region(s), and {levelRuleZones.Count} level-rule zone(s).");
    }

    private static void ScheduleNextZoneCheck()
    {
        ActionScheduler.RunActionOnceAfterFrames(() =>
        {
            if (!IsRunning)
            {
                return;
            }

            CheckForPlayerZones();
            ScheduleNextZoneCheck();
        }, PlayerZoneCheckIntervalFrames);
    }

    private static void CheckForPlayerZones()
    {
        if (!IsRunning)
        {
            return;
        }

        var onlineSteamIds = new HashSet<ulong>();
        foreach (var player in PlayerService.OnlinePlayersWithCharacters)
        {
            if (player.SteamID == 0)
            {
                continue;
            }

            onlineSteamIds.Add(player.SteamID);
            var isInFfaZone = IsInFfaZone(player);
            var wasInFfaZone = PlayersInFfaZones.Contains(player.SteamID);

            RefreshLevelRuleZoneMembership(player, isInFfaZone);

            if (isInFfaZone == wasInFfaZone)
            {
                if (isInFfaZone)
                {
                    ApplyFfaVisualBuff(player);
                }

                continue;
            }

            if (isInFfaZone)
            {
                PlayersInFfaZones.Add(player.SteamID);
                ApplyFfaVisualBuff(player);
                SendZoneMessage(player, "You entered a free-for-all PvP zone. Level rules do not apply here.".Warning());
            }
            else
            {
                PlayersInFfaZones.Remove(player.SteamID);
                HandleFfaVisualBuffLeave(player);
                SendZoneMessage(player, "You have left the FFA zone!".Warning());
            }
        }

        PlayersInFfaZones.RemoveWhere(steamId =>
        {
            var isOffline = !onlineSteamIds.Contains(steamId);
            if (isOffline)
            {
                PlayersInLevelRuleZones.Remove(steamId);
                FfaVisualBuffOwners.Remove(steamId);
                VisualBuffRemovalVersions.Remove(steamId);
            }

            return isOffline;
        });
    }

    private static void HandlePlayerConnected(Player player)
    {
        if (!IsRunning)
        {
            return;
        }

        ActionScheduler.RunActionOnceAfterFrames(() =>
        {
            if (IsRunning)
            {
                CheckForPlayerZones();
            }
        }, 1);
    }

    private static void HandlePlayerDisconnected(Player player)
    {
        if (player.SteamID != 0)
        {
            PlayersInFfaZones.Remove(player.SteamID);
            PlayersInLevelRuleZones.Remove(player.SteamID);
            VisualBuffRemovalVersions.Remove(player.SteamID);
            RemoveFfaVisualBuff(player);
        }
    }

    private static void RefreshLevelRuleZoneMembership(Player player, bool isInFfaZone)
    {
        if (player.SteamID == 0)
        {
            return;
        }

        if (isInFfaZone || !TryGetLevelRuleZone(player, out var rule))
        {
            if (PlayersInLevelRuleZones.Remove(player.SteamID, out var previousRuleName))
            {
                SendZoneMessage(player, $"You have left {EmphasizeLevelRuleText(previousRuleName)}.".BaseColor());
            }

            return;
        }

        if (PlayersInLevelRuleZones.TryGetValue(player.SteamID, out var currentRule) &&
            string.Equals(currentRule, rule.Name, StringComparison.Ordinal))
        {
            return;
        }

        PlayersInLevelRuleZones[player.SteamID] = rule.Name;
        SendZoneMessage(player, $"You entered {EmphasizeLevelRuleText(rule.Name)}. Players may attack up to {EmphasizeLevelRuleText(rule.MaxLevelGap.ToString())} levels below them here.".BaseColor());
    }

    private static string EmphasizeLevelRuleText(string text)
    {
        return text.Warning();
    }

    private static void ApplyFfaVisualBuff(Player player)
    {
        if (player.SteamID == 0 || !player.Character.Exists())
        {
            return;
        }

        VisualBuffRemovalVersions.Remove(player.SteamID);
        if (Helper.HasBuff(player, Prefabs.AB_Militia_HoundMaster_QuickShot_Buff))
        {
            return;
        }

        if (Helper.BuffPlayer(player, Prefabs.AB_Militia_HoundMaster_QuickShot_Buff, out _, Helper.NoDuration, true))
        {
            FfaVisualBuffOwners.Add(player.SteamID);
        }
        else
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Failed applying FFA visual marker to {player.Name}.");
        }
    }

    private static void HandleFfaVisualBuffLeave(Player player)
    {
        if (player.SteamID == 0)
        {
            return;
        }

        if (!FfaVisualBuffOwners.Contains(player.SteamID))
        {
            VisualBuffRemovalVersions.Remove(player.SteamID);
            return;
        }

        var retentionSeconds = KillfeedSettings.FfaVisualBuffRetentionSeconds;
        if (retentionSeconds <= 0 || !IsInPvpCombat(player))
        {
            VisualBuffRemovalVersions.Remove(player.SteamID);
            RemoveFfaVisualBuff(player);
            return;
        }

        ScheduleFfaVisualBuffRemoval(player.SteamID, retentionSeconds);
    }

    private static void ScheduleFfaVisualBuffRemoval(ulong steamId, int retentionSeconds)
    {
        var version = BumpVisualBuffRemovalVersion(steamId);
        ActionScheduler.RunActionOnceAfterDelay(() =>
        {
            if (!IsRunning ||
                !VisualBuffRemovalVersions.TryGetValue(steamId, out var currentVersion) ||
                currentVersion != version ||
                PlayersInFfaZones.Contains(steamId))
            {
                return;
            }

            VisualBuffRemovalVersions.Remove(steamId);
            if (PlayerService.TryGetOnlinePlayerBySteamId(steamId, out var player))
            {
                RemoveFfaVisualBuff(player);
            }
        }, retentionSeconds);
    }

    private static int BumpVisualBuffRemovalVersion(ulong steamId)
    {
        VisualBuffRemovalVersions.TryGetValue(steamId, out var version);
        version++;
        VisualBuffRemovalVersions[steamId] = version;
        return version;
    }

    private static bool IsInPvpCombat(Player player)
    {
        return Helper.HasBuff(player, Prefabs.Buff_InCombat_PvPVampire);
    }

    private static void RemoveFfaVisualBuff(Player player)
    {
        try
        {
            if (FfaVisualBuffOwners.Remove(player.SteamID))
            {
                Helper.RemoveBuff(player, Prefabs.AB_Militia_HoundMaster_QuickShot_Buff);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Failed removing FFA visual marker from {player.Name}: {ex.Message}");
        }
    }

    private static void RemoveAllFfaVisualBuffs()
    {
        foreach (var player in PlayerService.OnlinePlayersWithCharacters)
        {
            RemoveFfaVisualBuff(player);
        }
    }

    private static void SendZoneMessage(Player player, string message)
    {
        try
        {
            player.SendMessage(message);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Failed sending FFA zone message to {player.Name}: {ex.Message}");
        }
    }

    private static bool IsInRegionFfaZone(Player player)
    {
        var snapshot = CurrentZones;
        if (snapshot.RegionZones.Count == 0 || !player.User.Has<CurrentWorldRegion>())
        {
            return false;
        }

        try
        {
            var currentRegion = player.User.Read<CurrentWorldRegion>().CurrentRegion;
            return snapshot.RegionZones.Contains(currentRegion);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Failed checking region FFA membership: {ex.Message}");
            return false;
        }
    }

    private static bool IsInMapFfaZone(Player player)
    {
        var snapshot = CurrentZones;
        if (snapshot.MapZoneIds.Count == 0 && !KillfeedSettings.EnableT2RiftFfaZones)
        {
            return false;
        }

        UpdateActiveT2Rifts();

        try
        {
            if (TryGetMapZoneAtPlayerPosition(player, out var positionZoneId))
            {
                return snapshot.MapZoneIds.Contains(positionZoneId) || ActiveT2RiftZones.Contains(positionZoneId);
            }

            if (player.Character.Has<CurrentMapZone>())
            {
                var currentMapZone = player.Character.Read<CurrentMapZone>();
                if (currentMapZone.ZoneEntity.Exists() && currentMapZone.ZoneEntity.Has<SpatialMapZoneData>())
                {
                    var mapZone = currentMapZone.ZoneEntity.Read<SpatialMapZoneData>();
                    var zoneId = mapZone.ZoneId.GetHashCode();
                    return snapshot.MapZoneIds.Contains(zoneId) || ActiveT2RiftZones.Contains(zoneId);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Failed checking map zone FFA membership: {ex.Message}");
        }

        return false;
    }

    private static bool IsPlayerInLevelRuleZone(Player player, LevelRuleZoneSnapshot rule)
    {
        if (player.SteamID == 0 || !player.Character.Exists())
        {
            return false;
        }

        if (rule.RegionZones.Count > 0 && TryGetPlayerWorldRegion(player, out var region) && rule.RegionZones.Contains(region))
        {
            return true;
        }

        return rule.MapZoneIds.Count > 0 &&
            TryGetPlayerMapZoneId(player, out var zoneId) &&
            rule.MapZoneIds.Contains(zoneId);
    }

    private static bool TryGetLevelRuleZone(Player player, out LevelRuleZoneSnapshot matchedRule)
    {
        matchedRule = null!;
        if (!KillfeedSettings.EnableLevelRuleZones)
        {
            return false;
        }

        var snapshot = CurrentZones;
        foreach (var rule in snapshot.LevelRuleZones)
        {
            if (IsPlayerInLevelRuleZone(player, rule))
            {
                matchedRule = rule;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPlayerWorldRegion(Player player, out WorldRegionType region)
    {
        region = default;
        if (!player.User.Has<CurrentWorldRegion>())
        {
            return false;
        }

        try
        {
            region = player.User.Read<CurrentWorldRegion>().CurrentRegion;
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Failed reading player region: {ex.Message}");
            return false;
        }
    }

    private static bool TryGetPlayerMapZoneId(Player player, out int zoneId)
    {
        if (TryGetMapZoneAtPlayerPosition(player, out zoneId))
        {
            return true;
        }

        zoneId = 0;
        try
        {
            if (!player.Character.Has<CurrentMapZone>())
            {
                return false;
            }

            var currentMapZone = player.Character.Read<CurrentMapZone>();
            if (!currentMapZone.ZoneEntity.Exists() || !currentMapZone.ZoneEntity.Has<SpatialMapZoneData>())
            {
                return false;
            }

            var mapZone = currentMapZone.ZoneEntity.Read<SpatialMapZoneData>();
            zoneId = mapZone.ZoneId.GetHashCode();
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Failed reading player map zone: {ex.Message}");
            return false;
        }
    }

    private static bool TryGetMapZoneAtPlayerPosition(Player player, out int zoneId)
    {
        zoneId = 0;
        if (!player.IsOnline || !player.Character.Exists())
        {
            return false;
        }

        EnsureMapZoneCollection();
        if (MapZoneCollectionSystem == null)
        {
            return false;
        }

        var position = player.Position;
        if (!MapZoneUtilities.TryGetMapZoneForWorldPos(
            Core.EntityManager,
            ref MapZoneCollection,
            new float2(position.x, position.z),
            out SpatialMapZoneData mapZone))
        {
            return false;
        }

        zoneId = mapZone.ZoneId.GetHashCode();
        return true;
    }

    private static void EnsureMapZoneCollection()
    {
        if (MapZoneCollectionSystem != null)
        {
            return;
        }

        try
        {
            MapZoneCollectionSystem = Core.Server.GetExistingSystemManaged<MapZoneCollectionSystem>();
            MapZoneCollection = MapZoneCollectionSystem.GetMapZoneCollection();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Failed loading map zone collection: {ex.Message}");
            MapZoneCollectionSystem = null;
            MapZoneCollection = default;
        }
    }

    private static void UpdateActiveT2Rifts()
    {
        if (!KillfeedSettings.EnableT2RiftFfaZones)
        {
            ActiveT2RiftZones.Clear();
            return;
        }

        EnsureActiveWarEventQuery();
        if (!HasActiveWarEventQuery)
        {
            ActiveT2RiftZones.Clear();
            return;
        }

        PendingActiveT2RiftZones.Clear();
        var entities = ActiveWarEventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var entity in entities)
            {
                var warEvent = entity.Read<WarEvent>();
                if (warEvent.EventType != WarEventType.Major || !entity.Has<WarEvent_ChildReference>())
                {
                    continue;
                }

                var buffer = entity.ReadBuffer<WarEvent_ChildReference>();
                foreach (var item in buffer)
                {
                    if (!item.Entity.Exists() || !item.Entity.Has<WarEvent_ActiveGate>())
                    {
                        continue;
                    }

                    var activeGate = item.Entity.Read<WarEvent_ActiveGate>();
                    if (ChunkToRiftZone.TryGetValue(activeGate.ChunkCoordinate, out var riftZone))
                    {
                        PendingActiveT2RiftZones.Add(riftZone);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Failed refreshing active T2 rift FFA zones: {ex.Message}");
        }
        finally
        {
            entities.Dispose();
        }

        ActiveT2RiftZones.Clear();
        ActiveT2RiftZones.UnionWith(PendingActiveT2RiftZones);
    }

    private static void EnsureActiveWarEventQuery()
    {
        if (HasActiveWarEventQuery)
        {
            return;
        }

        try
        {
            ActiveWarEventQuery = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WarEvent>());
            HasActiveWarEventQuery = true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Failed creating active war event query: {ex.Message}");
            ActiveWarEventQuery = default;
            HasActiveWarEventQuery = false;
        }
    }

    private static void DisposeActiveWarEventQuery()
    {
        if (!HasActiveWarEventQuery)
        {
            return;
        }

        ActiveWarEventQuery.Dispose();
        ActiveWarEventQuery = default;
        HasActiveWarEventQuery = false;
    }

    private sealed record FfaZoneSnapshot(
        HashSet<int> MapZoneIds,
        HashSet<WorldRegionType> RegionZones,
        IReadOnlyList<LevelRuleZoneSnapshot> LevelRuleZones)
    {
        public static readonly FfaZoneSnapshot Empty = new(
            new HashSet<int>(),
            new HashSet<WorldRegionType>(),
            Array.Empty<LevelRuleZoneSnapshot>());
    }

    private sealed record LevelRuleZoneSnapshot(
        string Name,
        int MaxLevelGap,
        HashSet<int> MapZoneIds,
        HashSet<WorldRegionType> RegionZones);
}
