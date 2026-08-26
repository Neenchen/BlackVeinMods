using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using ProjectM;
using ProjectM.Network;
using ProjectM.Gameplay.Scripting;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;
using static ProjectM.Metrics;

namespace BlackVeinCore.Utilities;

public static class Helper
{
    public const float NoDuration = 0f;

    public static bool TryGetPrefabEntity(PrefabGUID prefabGuid, out Entity prefabEntity)
    {
        prefabEntity = Entity.Null;
        return prefabGuid != PrefabGUID.Empty &&
            Core.PrefabCollectionSystem._PrefabLookupMap.TryGetValue(prefabGuid, out prefabEntity) &&
            prefabEntity.Exists();
    }

    public static Entity GetPrefabEntity(PrefabGUID prefabGuid)
    {
        return Core.PrefabCollectionSystem._PrefabLookupMap[prefabGuid];
    }

    public static bool TryGetFromCharacter(Player player, out FromCharacter fromCharacter)
    {
        fromCharacter = default;
        if (!player.User.Exists() || !player.Character.Exists())
        {
            return false;
        }

        fromCharacter = player.ToFromCharacter();
        return true;
    }

    public static EntityCommandBuffer CreateCommandBuffer()
    {
        return Core.CreateCommandBuffer();
    }

    public static Entity CreateEntityWithComponents(Il2CppStructArray<ComponentType> componentTypes)
    {
        var archetype = Core.EntityManager.CreateArchetype(componentTypes);
        return Core.EntityManager.CreateEntity(archetype);
    }

    public static Entity CreateEntityWithComponents<T1>()
    {
        var componentTypes = new Il2CppStructArray<ComponentType>(1);
        componentTypes[0] = new ComponentType(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite);
        return CreateEntityWithComponents(componentTypes);
    }

    public static Entity CreateEntityWithComponents<T1, T2>()
    {
        var componentTypes = new Il2CppStructArray<ComponentType>(2);
        componentTypes[0] = new ComponentType(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite);
        componentTypes[1] = new ComponentType(Il2CppType.Of<T2>(), ComponentType.AccessMode.ReadWrite);
        return CreateEntityWithComponents(componentTypes);
    }

    public static Entity CreateEntityWithComponents<T1, T2, T3>()
    {
        var componentTypes = new Il2CppStructArray<ComponentType>(3);
        componentTypes[0] = new ComponentType(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite);
        componentTypes[1] = new ComponentType(Il2CppType.Of<T2>(), ComponentType.AccessMode.ReadWrite);
        componentTypes[2] = new ComponentType(Il2CppType.Of<T3>(), ComponentType.AccessMode.ReadWrite);
        return CreateEntityWithComponents(componentTypes);
    }

    public static bool HasBuff(Entity entity, PrefabGUID buff)
    {
        return entity.Exists() && buff != PrefabGUID.Empty && BuffUtility.HasBuff(Core.EntityManager, entity, buff);
    }

    public static bool HasBuff(Player player, PrefabGUID buff)
    {
        return player.Character.Exists() && HasBuff(player.Character, buff);
    }

    public static bool TryGetBuff(Entity entity, PrefabGUID buff, out Entity buffEntity)
    {
        buffEntity = Entity.Null;
        return entity.Exists() &&
            buff != PrefabGUID.Empty &&
            BuffUtility.TryGetBuff(Core.EntityManager, entity, buff, out buffEntity);
    }

    public static bool TryGetBuff(Player player, PrefabGUID buff, out Entity buffEntity)
    {
        return TryGetBuff(player.Character, buff, out buffEntity);
    }

    public static bool BuffPlayer(Player player, PrefabGUID buff, out Entity buffEntity, float duration = NoDuration, bool persistThroughDeath = false)
    {
        buffEntity = Entity.Null;
        return player.Character.Exists() && BuffEntity(player.Character, buff, out buffEntity, duration, persistThroughDeath);
    }

    public static bool BuffEntity(Entity entity, PrefabGUID buff, out Entity buffEntity, float duration = NoDuration, bool persistThroughDeath = false)
    {
        buffEntity = Entity.Null;
        if (!entity.Exists() || buff == PrefabGUID.Empty)
        {
            return false;
        }

        if (!HasBuff(entity, buff) && !Core.ServerGameManager.TryInstantiateBuffEntityImmediate(entity, entity, buff, out buffEntity))
        {
            return false;
        }

        if (!TryGetBuff(entity, buff, out buffEntity))
        {
            return false;
        }

        ConfigureBuffLifetime(buffEntity, duration, persistThroughDeath);
        return true;
    }

    public static void RemoveBuff(Player player, PrefabGUID buff)
    {
        RemoveBuff(player.Character, buff);
    }

    public static void RemoveBuff(Entity entity, PrefabGUID buff)
    {
        if (TryGetBuff(entity, buff, out var buffEntity))
        {
            DestroyUtility.Destroy(Core.EntityManager, buffEntity, DestroyDebugReason.TryRemoveBuff);
        }
    }

    private static void ConfigureBuffLifetime(Entity buffEntity, float duration, bool persistThroughDeath)
    {
        if (!buffEntity.Exists())
        {
            return;
        }

        if (persistThroughDeath || duration == NoDuration)
        {
            if (!buffEntity.Has<Buff_Persists_Through_Death>())
            {
                buffEntity.Add<Buff_Persists_Through_Death>();
            }

            if (buffEntity.Has<Buff_Destroy_On_Owner_Death>())
            {
                buffEntity.Remove<Buff_Destroy_On_Owner_Death>();
            }

            if (buffEntity.Has<RemoveBuffOnGameplayEventEntry>())
            {
                buffEntity.ReadBuffer<RemoveBuffOnGameplayEventEntry>().Clear();
            }
        }
        else
        {
            if (buffEntity.Has<Buff_Persists_Through_Death>())
            {
                buffEntity.Remove<Buff_Persists_Through_Death>();
            }

            if (!buffEntity.Has<Buff_Destroy_On_Owner_Death>())
            {
                buffEntity.Add<Buff_Destroy_On_Owner_Death>();
            }
        }

        if (!buffEntity.Has<LifeTime>())
        {
            return;
        }

        var lifetime = buffEntity.Read<LifeTime>();
        if (duration == NoDuration)
        {
            if (buffEntity.Has<Age>())
            {
                buffEntity.Remove<Age>();
            }

            lifetime.Duration = 0;
            lifetime.EndAction = LifeTimeEndAction.None;
        }
        else
        {
            if (!buffEntity.Has<Age>())
            {
                buffEntity.Add<Age>();
            }

            var age = buffEntity.Read<Age>();
            age.Value = 0;
            buffEntity.Write(age);
            lifetime.Duration = duration;
            lifetime.EndAction = LifeTimeEndAction.Destroy;
        }

        buffEntity.Write(lifetime);
    }
}
