using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using Stunlock.Network;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
public static class OnUserConnectedPatch
{
    public static void Prefix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        try
        {
            if (!TryGetUserEntity(__instance, netConnectionId, out var userEntity))
            {
                return;
            }

            var player = PlayerService.RegisterConnectedUser(userEntity);
            if (player != null)
            {
                GameEvents.OnPlayerConnected?.Invoke(player);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[GameEvents] Failed to publish player connected event: {ex}");
        }
    }

    internal static bool TryGetUserEntity(ServerBootstrapSystem system, NetConnectionId netConnectionId, out Unity.Entities.Entity userEntity)
    {
        userEntity = Unity.Entities.Entity.Null;
        if (!system._NetEndPointToApprovedUserIndex.TryGetValue(netConnectionId, out var userIndex) ||
            userIndex < 0 ||
            userIndex >= system._ApprovedUsersLookup.Length)
        {
            return false;
        }

        userEntity = system._ApprovedUsersLookup[userIndex].UserEntity;
        return userEntity.Exists();
    }
}

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserDisconnected))]
public static class OnUserDisconnectedPatch
{
    public static void Prefix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        try
        {
            if (!OnUserConnectedPatch.TryGetUserEntity(__instance, netConnectionId, out var userEntity))
            {
                return;
            }

            var player = PlayerService.RegisterDisconnectedUser(userEntity);
            if (player != null)
            {
                GameEvents.OnPlayerDisconnected?.Invoke(player);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[GameEvents] Failed to publish player disconnected event: {ex}");
        }
    }
}
