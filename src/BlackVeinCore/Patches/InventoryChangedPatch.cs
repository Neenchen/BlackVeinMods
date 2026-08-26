using System;
using BlackVeinCore.Events;
using HarmonyLib;
using ProjectM;
using Unity.Collections;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(ReactToInventoryChangedSystem), nameof(ReactToInventoryChangedSystem.OnUpdate))]
public static class InventoryChangedPatch
{
    public static void Prefix(ReactToInventoryChangedSystem __instance)
    {
        if (GameEvents.OnPlayerInventoryChanged == null && GameEvents.OnUnitInventoryChanged == null)
        {
            return;
        }

        try
        {
            var entities = __instance.__query_2096870026_0.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var entity in entities)
                {
                    var inventoryChangedEvent = entity.Read<InventoryChangedEvent>();
                    var inventoryEntity = inventoryChangedEvent.InventoryEntity;
                    if (!inventoryEntity.Has<InventoryConnection>())
                    {
                        GameEvents.OnUnitInventoryChanged?.Invoke(inventoryEntity, entity, inventoryChangedEvent);
                        continue;
                    }

                    var owner = inventoryEntity.Read<InventoryConnection>().InventoryOwner;
                    if (!owner.Exists())
                    {
                        GameEvents.OnUnitInventoryChanged?.Invoke(inventoryEntity, entity, inventoryChangedEvent);
                        continue;
                    }

                    if (PlayerService.TryFromCharacter(owner, out var player))
                    {
                        GameEvents.OnPlayerInventoryChanged?.Invoke(player, entity, inventoryChangedEvent);
                        continue;
                    }

                    GameEvents.OnUnitInventoryChanged?.Invoke(owner, entity, inventoryChangedEvent);
                }
            }
            finally
            {
                entities.Dispose();
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"[{Plugin.PluginName}] Inventory changed patch failed: {ex}");
        }
    }
}
