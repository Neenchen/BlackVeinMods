using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(ClaimAchievementSystem), nameof(ClaimAchievementSystem.OnUpdate))]
public static class ClaimAchievementSystemPatch
{
    public static void Prefix(ClaimAchievementSystem __instance)
    {
        NativeArray<Entity> eventEntities = default;
        try
        {
            eventEntities = __instance._ClaimEventQuery.ToEntityArray(Allocator.Temp);
            for (var index = 0; index < eventEntities.Length; index++)
            {
                try
                {
                    var eventEntity = eventEntities[index];
                    var claimEvent = eventEntity.Read<ClaimAchievementEvent>();
                    var fromCharacter = eventEntity.Read<FromCharacter>();
                    if (PlayerService.TryFromUserAndCharacter(fromCharacter.User, fromCharacter.Character, out var player))
                    {
                        GameEvents.OnPlayerClaimedJournalAchievement?.Invoke(player, claimEvent.Achievement);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish journal achievement claim event: {ex}");
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
