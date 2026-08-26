using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using Unity.Collections;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(ReactToPlayerCharacterSpawnSystem), nameof(ReactToPlayerCharacterSpawnSystem.OnUpdate))]
public static class ReactToPlayerCharacterSpawnSystemPatch
{
    public static void Prefix(ReactToPlayerCharacterSpawnSystem __instance)
    {
        if (GameEvents.OnPlayerCharacterSpawned == null)
        {
            return;
        }

        var entities = __instance._Query.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var entity in entities)
            {
                var player = PlayerService.RegisterCharacterSpawned(entity);
                if (player != null)
                {
                    GameEvents.OnPlayerCharacterSpawned?.Invoke(player, entity);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[GameEvents] Failed to publish player character spawned event: {ex}");
        }
        finally
        {
            entities.Dispose();
        }
    }
}
