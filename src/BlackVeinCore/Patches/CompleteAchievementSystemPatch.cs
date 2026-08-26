using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(CompleteAchievementSystem), nameof(CompleteAchievementSystem.OnUpdate))]
public static class CompleteAchievementSystemPatch
{
    public static void Prefix(CompleteAchievementSystem __instance)
    {
        NativeArray<Entity> eventEntities = default;
        try
        {
            eventEntities = __instance.__query_1557184233_0.ToEntityArray(Allocator.Temp);
            for (var index = 0; index < eventEntities.Length; index++)
            {
                try
                {
                    var progressEvent = eventEntities[index].Read<ProgressAchievementEvent>();
                    if (progressEvent.Achievement == PrefabGUID.Empty ||
                        !PlayerService.TryFromUser(progressEvent.User, out var player))
                    {
                        continue;
                    }

                    GameEvents.OnPlayerCompletedJournalAchievement?.Invoke(player, progressEvent.Achievement);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish journal completion event: {ex}");
                }
            }
        }
        finally
        {
            if (eventEntities.IsCreated)
            {
                eventEntities.Dispose();
            }
        }
    }
}
