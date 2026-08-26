using BlackVeinMods.Configs;
using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace BlackVeinMods.Managers;

public static class ClanProgressionManager
{
    private static bool _initialized;
    private static bool _handlingSharedEvent;
    private static readonly Dictionary<int, PrefabGUID[]> BossTechnologyUnlockCache = [];
    private static readonly Dictionary<int, ResearchUnlockPayload> ResearchUnlockPayloadCache = [];
    private static readonly Dictionary<JournalEventKey, long> RecentJournalEvents = [];
    private const long DuplicateJournalEventWindowMilliseconds = 500;
    private const int JournalEventCleanupThreshold = 64;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        GameEvents.OnPlayerConsumedVBlood += HandleVBloodConsumed;
        GameEvents.OnPlayerUnlockedResearch += HandleResearchUnlocked;
        GameEvents.OnPlayerCompletedJournalAchievement += HandleJournalAchievementCompleted;
        GameEvents.OnPlayerClaimedJournalAchievement += HandleJournalAchievementClaimed;
        _initialized = true;
    }

    public static void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        GameEvents.OnPlayerConsumedVBlood -= HandleVBloodConsumed;
        GameEvents.OnPlayerUnlockedResearch -= HandleResearchUnlocked;
        GameEvents.OnPlayerCompletedJournalAchievement -= HandleJournalAchievementCompleted;
        GameEvents.OnPlayerClaimedJournalAchievement -= HandleJournalAchievementClaimed;
        BossTechnologyUnlockCache.Clear();
        ResearchUnlockPayloadCache.Clear();
        RecentJournalEvents.Clear();
        _initialized = false;
    }

    private static void HandleVBloodConsumed(Player sourcePlayer, PrefabGUID bossPrefab)
    {
        if (!_initialized || _handlingSharedEvent)
        {
            return;
        }

        var config = BlackVeinConfig.Config.ClanProgression;
        if (!config.Enabled || bossPrefab == PrefabGUID.Empty)
        {
            return;
        }

        var clanmates = GetClanmates(sourcePlayer, config.IncludeOfflineClanMembers);
        if (clanmates.Count == 0)
        {
            return;
        }

        var technologyUnlocks = !config.ShareBossUnlocks && config.ShareTechnologyUnlocks
            ? GetTechnologyUnlocksForBoss(bossPrefab)
            : [];

        foreach (var clanmate in clanmates)
        {
            ShareProgression(sourcePlayer, clanmate, bossPrefab, technologyUnlocks, config);
        }
    }

    private static void HandleResearchUnlocked(Player sourcePlayer, PrefabGUID researchGuid)
    {
        if (!_initialized || _handlingSharedEvent)
        {
            return;
        }

        var config = BlackVeinConfig.Config.ClanProgression;
        if (!config.Enabled || !config.ShareResearchProgression || researchGuid == PrefabGUID.Empty)
        {
            return;
        }

        foreach (var clanmate in GetClanmates(sourcePlayer, config.IncludeOfflineClanMembers))
        {
            ShareResearch(sourcePlayer, clanmate, researchGuid);
        }
    }

    private static void HandleJournalAchievementClaimed(Player sourcePlayer, PrefabGUID achievementGuid)
    {
        HandleJournalAchievementCompleted(sourcePlayer, achievementGuid);
    }

    private static void HandleJournalAchievementCompleted(Player sourcePlayer, PrefabGUID achievementGuid)
    {
        if (!_initialized || _handlingSharedEvent)
        {
            return;
        }

        var config = BlackVeinConfig.Config.ClanProgression;
        if (!config.Enabled || !config.ShareJournalProgression || achievementGuid == PrefabGUID.Empty)
        {
            return;
        }

        if (IsDuplicateJournalEvent(sourcePlayer, achievementGuid))
        {
            return;
        }

        foreach (var clanmate in GetClanmates(sourcePlayer, config.IncludeOfflineClanMembers))
        {
            ShareJournalAchievement(sourcePlayer, clanmate, achievementGuid);
        }
    }

    private static void ShareProgression(
        Player sourcePlayer,
        Player targetPlayer,
        PrefabGUID bossPrefab,
        IReadOnlyList<PrefabGUID> technologyUnlocks,
        ClanProgressionConfig config)
    {
        var targetProgressionEntity = targetPlayer.ProgressionEntity;

        try
        {
            _handlingSharedEvent = true;

            if (config.ShareBossUnlocks && !HasBossUnlocked(targetProgressionEntity, bossPrefab))
            {
                UnlockBoss(targetPlayer, bossPrefab);
            }

            if (!config.ShareBossUnlocks && config.ShareTechnologyUnlocks)
            {
                foreach (var unlockId in technologyUnlocks)
                {
                    if (HasProgressionUnlocked(targetProgressionEntity, unlockId))
                    {
                        continue;
                    }

                    UnlockProgression(targetPlayer, unlockId);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning(
                $"[ClanProgression] Failed sharing {bossPrefab.GuidHash} from {sourcePlayer.Name} to {targetPlayer.Name}: {ex}");
        }
        finally
        {
            _handlingSharedEvent = false;
        }

    }

    private static void ShareResearch(
        Player sourcePlayer,
        Player targetPlayer,
        PrefabGUID researchGuid)
    {
        var researchUnlocks = GetResearchUnlockPayload(researchGuid);
        var targetProgressionEntity = targetPlayer.ProgressionEntity;

        try
        {
            _handlingSharedEvent = true;
            if (!HasProgressionUnlocked(targetProgressionEntity, researchGuid))
            {
                UnlockProgression(targetPlayer, researchGuid);
            }

            UnlockResearchRecipes(targetProgressionEntity, researchUnlocks.Recipes);
            UnlockResearchBlueprints(targetProgressionEntity, researchUnlocks.Blueprints);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning(
                $"[ClanProgression] Failed sharing research {researchGuid.GuidHash} from {sourcePlayer.Name} to {targetPlayer.Name}: {ex}");
            return;
        }
        finally
        {
            _handlingSharedEvent = false;
        }

    }

    private static void ShareJournalAchievement(
        Player sourcePlayer,
        Player targetPlayer,
        PrefabGUID achievementGuid)
    {
        if (HasClaimedAchievement(targetPlayer, achievementGuid))
        {
            return;
        }

        try
        {
            _handlingSharedEvent = true;
            if (!CompleteJournalAchievement(targetPlayer, achievementGuid))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning(
                $"[ClanProgression] Failed sharing journal achievement {achievementGuid.GuidHash} from {sourcePlayer.Name} to {targetPlayer.Name}: {ex}");
            return;
        }
        finally
        {
            _handlingSharedEvent = false;
        }

    }

    private static void UnlockBoss(Player targetPlayer, PrefabGUID bossPrefab)
    {
        if (!Helper.TryGetFromCharacter(targetPlayer, out var fromCharacter) || bossPrefab == PrefabGUID.Empty)
        {
            return;
        }

        var debugEventsSystem = Core.DebugEventsSystem;
        debugEventsSystem.UnlockVBloodEvent(
            debugEventsSystem,
            new UnlockVBlood
            {
                VBlood = bossPrefab
            },
            fromCharacter);
    }

    private static void UnlockProgression(Player targetPlayer, PrefabGUID unlockId)
    {
        if (!Helper.TryGetFromCharacter(targetPlayer, out var fromCharacter) || unlockId == PrefabGUID.Empty)
        {
            return;
        }

        Core.DebugEventsSystem.UnlockProgression(
            fromCharacter,
            new UnlockProgressionDebugEvent
            {
                PrefabGuid = unlockId
            });

        SendProgressionUnlock(targetPlayer, unlockId);
    }

    private static void SendProgressionUnlock(Player targetPlayer, PrefabGUID unlockId)
    {
        if (!targetPlayer.IsOnline)
        {
            return;
        }

        var user = targetPlayer.User.Read<User>();
        NetworkEvents.SendEvent(
            Core.EntityManager,
            new UnlockProgressionServerEvent
            {
                UnlockID = unlockId,
                IgnoreAnnouncement = true
            },
            ref user);
    }

    private static void UnlockResearchRecipes(Entity progressionEntity, IReadOnlyList<PrefabGUID> recipeUnlocks)
    {
        if (recipeUnlocks.Count == 0)
        {
            return;
        }

        if (!progressionEntity.Exists())
        {
            return;
        }

        var unlockedRecipes = progressionEntity.Has<UnlockedRecipeElement>()
            ? progressionEntity.ReadBuffer<UnlockedRecipeElement>()
            : progressionEntity.AddBuffer<UnlockedRecipeElement>();

        foreach (var recipeGuid in recipeUnlocks)
        {
            if (HasRecipeUnlocked(unlockedRecipes, recipeGuid))
            {
                continue;
            }

            unlockedRecipes.Add(new UnlockedRecipeElement
            {
                UnlockedRecipe = recipeGuid,
                UserHasRequiredContentFlags = true
            });
        }
    }

    private static void UnlockResearchBlueprints(Entity progressionEntity, IReadOnlyList<PrefabGUID> blueprintUnlocks)
    {
        if (blueprintUnlocks.Count == 0)
        {
            return;
        }

        if (!progressionEntity.Exists())
        {
            return;
        }

        var unlockedBlueprints = progressionEntity.Has<UnlockedBlueprintElement>()
            ? progressionEntity.ReadBuffer<UnlockedBlueprintElement>()
            : progressionEntity.AddBuffer<UnlockedBlueprintElement>();

        foreach (var blueprintGuid in blueprintUnlocks)
        {
            if (HasBlueprintUnlocked(unlockedBlueprints, blueprintGuid))
            {
                continue;
            }

            unlockedBlueprints.Add(new UnlockedBlueprintElement
            {
                UnlockedBlueprint = blueprintGuid,
                UserHasRequiredContentFlags = true
            });
        }
    }

    private static ResearchUnlockPayload GetResearchUnlockPayload(PrefabGUID researchGuid)
    {
        var key = researchGuid.GuidHash;
        if (ResearchUnlockPayloadCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var payload = BuildResearchUnlockPayload(researchGuid);
        ResearchUnlockPayloadCache[key] = payload;
        return payload;
    }

    private static ResearchUnlockPayload BuildResearchUnlockPayload(PrefabGUID researchGuid)
    {
        if (!Helper.TryGetPrefabEntity(researchGuid, out var researchEntity))
        {
            return ResearchUnlockPayload.Empty;
        }

        var recipes = new List<PrefabGUID>();
        var blueprints = new List<PrefabGUID>();

        if (researchEntity.Has<ProgressionBookRecipeElement>())
        {
            var progressionBookRecipes = researchEntity.ReadBuffer<ProgressionBookRecipeElement>();
            for (var index = 0; index < progressionBookRecipes.Length; index++)
            {
                var recipeGuid = progressionBookRecipes[index].Recipe;
                if (recipeGuid != PrefabGUID.Empty)
                {
                    AddDistinct(recipes, recipeGuid);
                }
            }
        }

        if (researchEntity.Has<TechUnlockRecipeBuffer>())
        {
            var techUnlockRecipes = researchEntity.ReadBuffer<TechUnlockRecipeBuffer>();
            for (var index = 0; index < techUnlockRecipes.Length; index++)
            {
                var recipeGuid = techUnlockRecipes[index].Guid;
                if (recipeGuid != PrefabGUID.Empty)
                {
                    AddDistinct(recipes, recipeGuid);
                }
            }
        }

        if (researchEntity.Has<ProgressionBookBlueprintElement>())
        {
            var progressionBookBlueprints = researchEntity.ReadBuffer<ProgressionBookBlueprintElement>();
            for (var index = 0; index < progressionBookBlueprints.Length; index++)
            {
                var blueprintGuid = progressionBookBlueprints[index].Blueprint;
                if (blueprintGuid != PrefabGUID.Empty)
                {
                    AddDistinct(blueprints, blueprintGuid);
                }
            }
        }

        if (researchEntity.Has<TechUnlockBlueprintBuffer>())
        {
            var techUnlockBlueprints = researchEntity.ReadBuffer<TechUnlockBlueprintBuffer>();
            for (var index = 0; index < techUnlockBlueprints.Length; index++)
            {
                var blueprintGuid = techUnlockBlueprints[index].Guid;
                if (blueprintGuid != PrefabGUID.Empty)
                {
                    AddDistinct(blueprints, blueprintGuid);
                }
            }
        }

        return new ResearchUnlockPayload(recipes.ToArray(), blueprints.ToArray());
    }

    private static IReadOnlyList<PrefabGUID> GetTechnologyUnlocksForBoss(PrefabGUID bossPrefab)
    {
        var key = bossPrefab.GuidHash;
        if (BossTechnologyUnlockCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var unlockIds = BuildTechnologyUnlocksForBoss(bossPrefab);
        BossTechnologyUnlockCache[key] = unlockIds;
        return unlockIds;
    }

    private static PrefabGUID[] BuildTechnologyUnlocksForBoss(PrefabGUID bossPrefab)
    {
        if (!Helper.TryGetPrefabEntity(bossPrefab, out var bossEntity) || !bossEntity.Has<VBloodUnlockTechBuffer>())
        {
            return Array.Empty<PrefabGUID>();
        }

        var unlocks = bossEntity.ReadBuffer<VBloodUnlockTechBuffer>();
        if (unlocks.Length == 0)
        {
            return Array.Empty<PrefabGUID>();
        }

        var result = new List<PrefabGUID>(unlocks.Length);
        for (var index = 0; index < unlocks.Length; index++)
        {
            var unlockId = unlocks[index].Guid;
            if (unlockId != PrefabGUID.Empty)
            {
                result.Add(unlockId);
            }
        }

        return result.ToArray();
    }

    private static List<Player> GetClanmates(Player sourcePlayer, bool includeOffline)
    {
        var clanmates = new List<Player>();
        var clanEntity = sourcePlayer.Clan;
        if (!clanEntity.Exists())
        {
            return clanmates;
        }

        var clanMemberEntities = new NativeList<Entity>(Allocator.Temp);
        try
        {
            TeamUtility.GetClanMembers(Core.EntityManager, clanEntity, clanMemberEntities);
            for (var index = 0; index < clanMemberEntities.Length; index++)
            {
                var userEntity = clanMemberEntities[index];
                if (!userEntity.Exists() || userEntity == sourcePlayer.User || !userEntity.Has<User>())
                {
                    continue;
                }

                var user = userEntity.Read<User>();
                if (!includeOffline && !user.IsConnected)
                {
                    continue;
                }

                var characterEntity = user.LocalCharacter._Entity;
                if (!includeOffline && !characterEntity.Exists())
                {
                    continue;
                }

                clanmates.Add(new Player(userEntity, characterEntity));
            }
        }
        finally
        {
            if (clanMemberEntities.IsCreated)
            {
                clanMemberEntities.Dispose();
            }
        }

        return clanmates;
    }

    private static bool HasBossUnlocked(Entity progressionEntity, PrefabGUID bossPrefab)
    {
        if (!progressionEntity.Exists() || !progressionEntity.Has<UnlockedVBlood>())
        {
            return false;
        }

        var unlockedVBloods = progressionEntity.ReadBuffer<UnlockedVBlood>();
        for (var index = 0; index < unlockedVBloods.Length; index++)
        {
            if (unlockedVBloods[index].VBlood == bossPrefab)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasProgressionUnlocked(Entity progressionEntity, PrefabGUID unlockId)
    {
        if (!progressionEntity.Exists() || !progressionEntity.Has<UnlockedProgressionElement>())
        {
            return false;
        }

        var unlockedProgression = progressionEntity.ReadBuffer<UnlockedProgressionElement>();
        for (var index = 0; index < unlockedProgression.Length; index++)
        {
            if (unlockedProgression[index].UnlockedPrefab == unlockId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRecipeUnlocked(DynamicBuffer<UnlockedRecipeElement> unlockedRecipes, PrefabGUID recipeGuid)
    {
        for (var index = 0; index < unlockedRecipes.Length; index++)
        {
            if (unlockedRecipes[index].UnlockedRecipe == recipeGuid)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBlueprintUnlocked(DynamicBuffer<UnlockedBlueprintElement> unlockedBlueprints, PrefabGUID blueprintGuid)
    {
        for (var index = 0; index < unlockedBlueprints.Length; index++)
        {
            if (unlockedBlueprints[index].UnlockedBlueprint == blueprintGuid)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasClaimedAchievement(Player player, PrefabGUID achievementGuid)
    {
        if (!TryGetAchievementOwnerEntity(player, out var achievementOwnerEntity) ||
            !achievementOwnerEntity.Has<AchievementClaimedElement>())
        {
            return false;
        }

        var claimedAchievements = achievementOwnerEntity.ReadBuffer<AchievementClaimedElement>();
        for (var index = 0; index < claimedAchievements.Length; index++)
        {
            if (claimedAchievements[index].AchievementPrefabGuid == achievementGuid)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CompleteJournalAchievement(Player player, PrefabGUID achievementGuid)
    {
        if (achievementGuid == PrefabGUID.Empty ||
            !player.User.Exists() ||
            !player.Character.Exists() ||
            !TryGetAchievementOwnerEntity(player, out var achievementOwnerEntity))
        {
            return false;
        }

        if (HasClaimedAchievement(player, achievementGuid))
        {
            return false;
        }

        var commandBuffer = Helper.CreateCommandBuffer();
        Core.ClaimAchievementSystem.CompleteAchievement(
            commandBuffer,
            achievementGuid,
            player.User,
            player.Character,
            achievementOwnerEntity,
            false,
            false);
        return true;
    }

    private static bool TryGetAchievementOwnerEntity(Player player, out Entity achievementOwnerEntity)
    {
        achievementOwnerEntity = Entity.Null;
        if (!player.User.Exists() || !player.User.Has<AchievementOwner>())
        {
            return false;
        }

        achievementOwnerEntity = player.User.Read<AchievementOwner>().Entity._Entity;
        return achievementOwnerEntity.Exists();
    }

    private static void AddDistinct(List<PrefabGUID> values, PrefabGUID value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] == value)
            {
                return;
            }
        }

        values.Add(value);
    }

    private static bool IsDuplicateJournalEvent(Player player, PrefabGUID achievementGuid)
    {
        var now = Environment.TickCount64;
        var key = new JournalEventKey(
            player.User.Index,
            player.Character.Index,
            achievementGuid.GuidHash);

        if (RecentJournalEvents.TryGetValue(key, out var lastPublishedAt) &&
            now - lastPublishedAt <= DuplicateJournalEventWindowMilliseconds)
        {
            return true;
        }

        RecentJournalEvents[key] = now;
        if (RecentJournalEvents.Count > JournalEventCleanupThreshold)
        {
            foreach (var entry in RecentJournalEvents.ToArray())
            {
                if (now - entry.Value > DuplicateJournalEventWindowMilliseconds)
                {
                    RecentJournalEvents.Remove(entry.Key);
                }
            }
        }

        return false;
    }

    private sealed record ResearchUnlockPayload(
        IReadOnlyList<PrefabGUID> Recipes,
        IReadOnlyList<PrefabGUID> Blueprints)
    {
        public static readonly ResearchUnlockPayload Empty = new(
            Array.Empty<PrefabGUID>(),
            Array.Empty<PrefabGUID>());
    }

    private readonly record struct JournalEventKey(
        int UserIndex,
        int CharacterIndex,
        int AchievementGuidHash);
}
