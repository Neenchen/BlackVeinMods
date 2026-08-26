using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(EquipItemSystem), nameof(EquipItemSystem.OnUpdate))]
public static class EquipItemPatch
{
    [HarmonyPrefix]
    public static void Prefix(EquipItemSystem __instance)
    {
        if (GameEvents.OnEquipItem == null)
        {
            return;
        }

        var entities = __instance._EventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var eventEntity in entities)
            {
                try
                {
                    if (!eventEntity.Has<FromCharacter>() || !eventEntity.Has<EquipItemEvent>())
                    {
                        continue;
                    }

                    var fromCharacter = eventEntity.Read<FromCharacter>();
                    var equipItemEvent = eventEntity.Read<EquipItemEvent>();
                    if (!InventoryUtilities.TryGetItemAtSlot(__instance.EntityManager, fromCharacter.Character, equipItemEvent.SlotIndex, out InventoryBuffer item))
                    {
                        continue;
                    }

                    GameEvents.OnEquipItem?.Invoke(eventEntity, item.ItemEntity._Entity, item.ItemType);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish equip item event: {ex}");
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
}

[HarmonyPatch(typeof(EquipItemFromInventorySystem), nameof(EquipItemFromInventorySystem.OnUpdate))]
public static class EquipItemFromInventoryPatch
{
    [HarmonyPrefix]
    public static void Prefix(EquipItemFromInventorySystem __instance)
    {
        if (GameEvents.OnEquipItem == null)
        {
            return;
        }

        var entities = __instance._Query.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var eventEntity in entities)
            {
                try
                {
                    if (!eventEntity.Has<EquipItemFromInventoryEvent>())
                    {
                        continue;
                    }

                    var equipItemFromInventoryEvent = eventEntity.Read<EquipItemFromInventoryEvent>();
                    if (!EcsExtensions.TryGetEntityFromNetworkId(equipItemFromInventoryEvent.FromInventory, out var fromInventory))
                    {
                        continue;
                    }

                    if (!InventoryUtilities.TryGetItemAtSlot(__instance.EntityManager, fromInventory, equipItemFromInventoryEvent.SlotIndex, out InventoryBuffer item))
                    {
                        continue;
                    }

                    GameEvents.OnEquipItem?.Invoke(eventEntity, item.ItemEntity._Entity, item.ItemType);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish equip item from inventory event: {ex}");
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
}
