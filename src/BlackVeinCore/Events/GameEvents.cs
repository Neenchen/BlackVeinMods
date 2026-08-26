using BlackVeinCore.Utilities;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;

namespace BlackVeinCore.Events;

public static class GameEvents
{
    public delegate void PlayerConnectedHandler(Player player);
    public delegate void PlayerDisconnectedHandler(Player player);
    public delegate void PlayerCharacterSpawnedHandler(Player player, Entity character);
    public delegate void PlayerConsumedVBloodHandler(Player player, PrefabGUID bossPrefab);
    public delegate void PlayerUnlockedResearchHandler(Player player, PrefabGUID researchGuid);
    public delegate void PlayerCompletedJournalAchievementHandler(Player player, PrefabGUID achievementGuid);
    public delegate void PlayerClaimedJournalAchievementHandler(Player player, PrefabGUID achievementGuid);
    public delegate void StartCraftItemHandler(Entity eventEntity, PrefabGUID recipeGuid);
    public delegate void ForgeRepairItemHandler(Entity eventEntity, PrefabGUID repairRecipe, Entity forgeEntity, int itemGuidHash);
    public delegate void EquipItemHandler(Entity eventEntity, Entity itemEntity, PrefabGUID itemGuid);
    public delegate void EquipJewelHandler(Entity eventEntity, Entity itemEntity, PrefabGUID itemGuid, int tier);
    public delegate void BeforeTryAddItemHandler(Entity target, PrefabGUID itemGuid, ref AddItemSettings addItemSettings);
    public delegate void PlayerUsedConsumableHandler(Player player, Entity eventEntity, UseItemEvent useItemEvent, InventoryBuffer item);
    public delegate void PlayerInventoryChangedHandler(Player player, Entity eventEntity, InventoryChangedEvent inventoryChangedEvent);
    public delegate void UnitInventoryChangedHandler(Entity unit, Entity eventEntity, InventoryChangedEvent inventoryChangedEvent);
    public delegate void PlayerDeathHandler(Player victim, DeathEvent deathEvent, Player? pvpKiller);
    public delegate void PlayerDownedHandler(Player victim, Entity downedBuff, Entity killerEntity, Player? pvpKiller);
    public delegate void PlayerReceivedDamageHandler(Entity source, Entity eventEntity, StatChangeEvent statChangeEvent, Player target, float absorbed);
    public delegate void UnitReceivedDamageHandler(Entity source, Entity eventEntity, StatChangeEvent statChangeEvent, Entity target);
    public delegate void PlayerHealedHandler(Entity source, Entity eventEntity, StatChangeEvent statChangeEvent, Player target);
    public delegate void UnitHealedHandler(Entity source, Entity eventEntity, StatChangeEvent statChangeEvent, Entity target);

    public static PlayerConnectedHandler? OnPlayerConnected;
    public static PlayerDisconnectedHandler? OnPlayerDisconnected;
    public static PlayerCharacterSpawnedHandler? OnPlayerCharacterSpawned;
    public static PlayerConsumedVBloodHandler? OnPlayerConsumedVBlood;
    public static PlayerUnlockedResearchHandler? OnPlayerUnlockedResearch;
    public static PlayerCompletedJournalAchievementHandler? OnPlayerCompletedJournalAchievement;
    public static PlayerClaimedJournalAchievementHandler? OnPlayerClaimedJournalAchievement;
    public static StartCraftItemHandler? OnStartCraftItem;
    public static ForgeRepairItemHandler? OnForgeRepairItem;
    public static EquipItemHandler? OnEquipItem;
    public static EquipJewelHandler? OnEquipJewel;
    public static BeforeTryAddItemHandler? OnBeforeTryAddItem;
    public static PlayerUsedConsumableHandler? OnPlayerUsedConsumable;
    public static PlayerInventoryChangedHandler? OnPlayerInventoryChanged;
    public static UnitInventoryChangedHandler? OnUnitInventoryChanged;
    public static PlayerDeathHandler? OnPlayerDeath;
    public static PlayerDownedHandler? OnPlayerDowned;
    public static PlayerReceivedDamageHandler? OnPlayerReceivedDamage;
    public static UnitReceivedDamageHandler? OnUnitReceivedDamage;
    public static PlayerHealedHandler? OnPlayerHealed;
    public static UnitHealedHandler? OnUnitHealed;
}
