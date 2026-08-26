using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(StartCraftingSystem), nameof(StartCraftingSystem.OnUpdate))]
public static class CraftingPatch
{
    [HarmonyPrefix]
    public static void Prefix(StartCraftingSystem __instance)
    {
        if (GameEvents.OnStartCraftItem == null)
        {
            return;
        }

        var entities = __instance._StartCraftItemEventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var eventEntity in entities)
            {
                try
                {
                    if (!eventEntity.Has<StartCraftItemEvent>())
                    {
                        continue;
                    }

                    var recipeGuid = eventEntity.Read<StartCraftItemEvent>().RecipeId;
                    if (recipeGuid.GuidHash == 0)
                    {
                        continue;
                    }

                    GameEvents.OnStartCraftItem?.Invoke(eventEntity, recipeGuid);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish craft item event: {ex}");
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
}
