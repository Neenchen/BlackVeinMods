using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(VBloodSystem), nameof(VBloodSystem.OnUpdate))]
public static class VBloodSystemPatch
{
    public static void Prefix(VBloodSystem __instance)
    {
        if (__instance.EventList.Length == 0)
        {
            return;
        }

        for (var index = 0; index < __instance.EventList.Length; index++)
        {
            try
            {
                var consumed = __instance.EventList[index];
                if (PlayerService.TryFromCharacter(consumed.Target, out var player))
                {
                    GameEvents.OnPlayerConsumedVBlood?.Invoke(player, consumed.Source);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[GameEvents] Failed to publish V Blood consume event: {ex}");
            }
        }
    }
}
