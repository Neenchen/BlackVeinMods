using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(ForgeSystem_Events), nameof(ForgeSystem_Events.OnUpdate))]
public static class ForgeRepairPatch
{
    [HarmonyPrefix]
    public static void Prefix(ForgeSystem_Events __instance)
    {
        if (GameEvents.OnForgeRepairItem == null)
        {
            return;
        }

        var entities = __instance._RepairItemEventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var eventEntity in entities)
            {
                try
                {
                    if (!eventEntity.Has<FromCharacter>())
                    {
                        continue;
                    }

                    var fromCharacter = eventEntity.Read<FromCharacter>();
                    if (!__instance.TryGetInteractedForge(fromCharacter.Character, out var forgeEntity, out Forge_Shared forge))
                    {
                        continue;
                    }

                    var itemEntity = forge.ItemEntity._Entity;
                    if (!TryGetRepairRecipe(itemEntity, out var repairRecipe))
                    {
                        continue;
                    }

                    GameEvents.OnForgeRepairItem?.Invoke(eventEntity, repairRecipe, forgeEntity, itemEntity.GetPrefabGUID().GuidHash);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish forge repair item event: {ex}");
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    private static bool TryGetRepairRecipe(Entity itemEntity, out PrefabGUID repairRecipe)
    {
        repairRecipe = default;

        if (!itemEntity.Exists() || !itemEntity.Has<Durability>())
        {
            return false;
        }

        repairRecipe = itemEntity.Read<Durability>().RepairRecipe;
        return repairRecipe.GuidHash != 0;
    }
}
