using System;
using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(UseConsumableSystem), nameof(UseConsumableSystem.OnUpdate))]
public static class UseConsumableSystemPatch
{
    public static void Prefix(UseConsumableSystem __instance)
    {
        if (GameEvents.OnPlayerUsedConsumable == null)
        {
            return;
        }

        var entities = __instance._Query.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var entity in entities)
            {
                try
                {
                    if (!entity.Has<FromCharacter>() || !entity.Has<UseItemEvent>())
                    {
                        continue;
                    }

                    var fromCharacter = entity.Read<FromCharacter>();
                    var useItemEvent = entity.Read<UseItemEvent>();
                    if (!InventoryUtilities.TryGetItemAtSlot(__instance.EntityManager, fromCharacter.Character, useItemEvent.SlotIndex, out var item))
                    {
                        continue;
                    }

                    if (PlayerService.TryFromUserAndCharacter(fromCharacter.User, fromCharacter.Character, out var player))
                    {
                        GameEvents.OnPlayerUsedConsumable?.Invoke(player, entity, useItemEvent, item);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish use consumable event: {ex}");
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
}
