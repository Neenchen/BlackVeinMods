using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay.Systems;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
public static class DeathEventListenerSystemPatch
{
    public static void Prefix(DeathEventListenerSystem __instance)
    {
        if (GameEvents.OnPlayerDeath == null)
        {
            return;
        }

        var entities = __instance._DeathEventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var entity in entities)
            {
                try
                {
                    var deathEvent = entity.Read<DeathEvent>();
                    if (!PlayerService.TryFromCharacter(deathEvent.Died, out var victim))
                    {
                        continue;
                    }

                    Player? killer = null;
                    if (deathEvent.Killer.Exists() && deathEvent.Killer.Has<PlayerCharacter>())
                    {
                        PlayerService.TryFromCharacter(deathEvent.Killer, out killer);
                    }

                    GameEvents.OnPlayerDeath?.Invoke(victim, deathEvent, killer);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish player death event: {ex}");
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
}

[HarmonyPatch(typeof(VampireDownedServerEventSystem), nameof(VampireDownedServerEventSystem.OnUpdate))]
public static class VampireDownedServerEventSystemPatch
{
    public static void Prefix(VampireDownedServerEventSystem __instance)
    {
        if (GameEvents.OnPlayerDowned == null)
        {
            return;
        }

        var entities = __instance.__query_1174204813_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var entity in entities)
            {
                try
                {
                    if (!VampireDownedServerEventSystem.TryFindRootOwner(entity, 1, Core.EntityManager, out var victimEntity) ||
                        !PlayerService.TryFromCharacter(victimEntity, out var victim))
                    {
                        continue;
                    }

                    var downBuff = entity.Read<VampireDownedBuff>();
                    if (!VampireDownedServerEventSystem.TryFindRootOwner(downBuff.Source, 1, Core.EntityManager, out var killerEntity))
                    {
                        continue;
                    }

                    Player? pvpKiller = null;
                    if (killerEntity.Exists() && killerEntity.Has<PlayerCharacter>())
                    {
                        PlayerService.TryFromCharacter(killerEntity, out pvpKiller);
                    }

                    GameEvents.OnPlayerDowned?.Invoke(victim, entity, killerEntity, pvpKiller);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish player downed event: {ex}");
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
}

[HarmonyPatch(typeof(StatChangeSystem), nameof(StatChangeSystem.OnUpdate))]
public static class StatChangeSystemPatch
{
    public static void Prefix(StatChangeSystem __instance)
    {
        if (GameEvents.OnPlayerReceivedDamage == null &&
            GameEvents.OnUnitReceivedDamage == null &&
            GameEvents.OnPlayerHealed == null &&
            GameEvents.OnUnitHealed == null)
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
                    var statChangeEvent = entity.Read<StatChangeEvent>();
                    if (statChangeEvent.StatType != StatType.Health)
                    {
                        continue;
                    }

                    var totalChange = statChangeEvent.Change == 0
                        ? Math.Abs(statChangeEvent.OriginalChange)
                        : Math.Abs(statChangeEvent.Change);
                    if (totalChange == 0)
                    {
                        continue;
                    }

                    var source = ResolveInstigator(statChangeEvent.Source);
                    if (!source.Exists() || !statChangeEvent.Entity.Exists())
                    {
                        continue;
                    }

                    if (PlayerService.TryFromCharacter(statChangeEvent.Entity, out var targetPlayer))
                    {
                        if (statChangeEvent.Change > 0)
                        {
                            GameEvents.OnPlayerHealed?.Invoke(source, entity, statChangeEvent, targetPlayer);
                        }
                        else if (statChangeEvent.Change < 0 && statChangeEvent.Reason == StatChangeReason.DealDamageSystem_0)
                        {
                            GameEvents.OnPlayerReceivedDamage?.Invoke(source, entity, statChangeEvent, targetPlayer, 0f);
                        }

                        continue;
                    }

                    if (statChangeEvent.Change > 0)
                    {
                        GameEvents.OnUnitHealed?.Invoke(source, entity, statChangeEvent, statChangeEvent.Entity);
                    }
                    else if (statChangeEvent.Change < 0 && statChangeEvent.Reason == StatChangeReason.DealDamageSystem_0)
                    {
                        GameEvents.OnUnitReceivedDamage?.Invoke(source, entity, statChangeEvent, statChangeEvent.Entity);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameEvents] Failed to publish stat change event: {ex}");
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    private static Entity ResolveInstigator(Entity source)
    {
        if (!source.Exists())
        {
            return Entity.Null;
        }

        if (source.Has<PlayerCharacter>())
        {
            return source;
        }

        if (source.Has<EntityOwner>())
        {
            var owner = source.Read<EntityOwner>().Owner;
            if (owner.Exists())
            {
                return owner.Has<PlayerCharacter>() ? owner : ResolveInstigator(owner);
            }
        }

        if (source.Has<AbilityOwner>())
        {
            var ability = source.Read<AbilityOwner>().Ability._Entity;
            if (ability.Exists())
            {
                return ResolveInstigator(ability);
            }
        }

        if (source.Has<EntityCreator>())
        {
            var creator = source.Read<EntityCreator>().Creator._Entity;
            if (creator.Exists())
            {
                return ResolveInstigator(creator);
            }
        }

        return source;
    }
}
