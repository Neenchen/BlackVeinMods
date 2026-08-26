using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Unity.Collections;
using Unity.Entities;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(JewelNetworkEventsSystem), nameof(JewelNetworkEventsSystem.OnUpdate))]
public static class JewelEquipPatch
{
    [HarmonyPrefix]
    public static void Prefix(JewelNetworkEventsSystem __instance)
    {
        if (GameEvents.OnEquipJewel == null)
        {
            return;
        }

        var entities = __instance._EquipEventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var eventEntity in entities)
            {
                try
                {
                    if (!eventEntity.Has<FromCharacter>() || !eventEntity.Has<EquipJewelEvent>())
                    {
                        continue;
                    }

                    var fromCharacter = eventEntity.Read<FromCharacter>();
                    var equipJewelEvent = eventEntity.Read<EquipJewelEvent>();
                    if (!InventoryUtilities.TryGetItemAtSlot(__instance.EntityManager, fromCharacter.Character, equipJewelEvent.InventoryIndex, out InventoryBuffer item))
                    {
                        continue;
                    }

                    Entity itemEntity = item.ItemEntity._Entity;
                    if (!itemEntity.Exists() || !itemEntity.Has<JewelInstance>())
                    {
                        continue;
                    }

                    int tier = Math.Clamp((int)itemEntity.Read<JewelInstance>().TierIndex + 1, 1, 4);
                    GameEvents.OnEquipJewel?.Invoke(eventEntity, itemEntity, item.ItemType, tier);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish equip jewel event: {ex}");
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
}
