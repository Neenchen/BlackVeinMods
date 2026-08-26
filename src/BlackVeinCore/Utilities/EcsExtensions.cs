using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace BlackVeinCore.Utilities;

public static class EcsExtensions
{
    public static T Read<T>(this Entity entity) where T : struct
    {
        return Core.EntityManager.GetComponentData<T>(entity);
    }

    public static void Write<T>(this Entity entity, T componentData) where T : struct
    {
        Core.EntityManager.SetComponentData(entity, componentData);
    }

    public static DynamicBuffer<T> ReadBuffer<T>(this Entity entity) where T : struct
    {
        return Core.EntityManager.GetBuffer<T>(entity);
    }

    public static DynamicBuffer<T> AddBuffer<T>(this Entity entity) where T : struct
    {
        return Core.EntityManager.AddBuffer<T>(entity);
    }

    public static void Add<T>(this Entity entity)
    {
        Core.EntityManager.AddComponent<T>(entity);
    }

    public static void Remove<T>(this Entity entity)
    {
        Core.EntityManager.RemoveComponent<T>(entity);
    }

    public static bool Has<T>(this Entity entity)
    {
        return entity.Exists() && Core.EntityManager.HasComponent<T>(entity);
    }

    public static bool Exists(this Entity entity)
    {
        return entity.Index > 0 && VWorld.TryGetServerWorld(out var world) && world.EntityManager.Exists(entity);
    }

    public static void Destroy(this Entity entity)
    {
        Core.EntityManager.DestroyEntity(entity);
    }

    public static string LookupName(this PrefabGUID prefabGuid)
    {
        try
        {
            if (Core.PrefabCollectionSystem._PrefabDataLookup.TryGetValue(prefabGuid, out var dataLookup))
            {
                return dataLookup.AssetName.ToString();
            }
        }
        catch
        {
            // Best-effort lookup for logging only.
        }

        return prefabGuid.GuidHash.ToString();
    }

    public static PrefabGUID GetPrefabGUID(this Entity entity)
    {
        return entity.Exists() && entity.Has<PrefabGUID>()
            ? entity.Read<PrefabGUID>()
            : PrefabGUID.Empty;
    }

    public static bool TryGetInventoryBuffer(this Entity entity, out DynamicBuffer<InventoryBuffer> buffer)
    {
        if (entity.Has<InventoryBuffer>())
        {
            buffer = entity.ReadBuffer<InventoryBuffer>();
            return true;
        }

        if (entity.Has<InventoryInstanceElement>())
        {
            var inventoryInstances = entity.ReadBuffer<InventoryInstanceElement>();
            if (inventoryInstances.Length > 0)
            {
                var inventoryEntity = inventoryInstances[0].ExternalInventoryEntity._Entity;
                if (inventoryEntity.Has<InventoryBuffer>())
                {
                    buffer = inventoryEntity.ReadBuffer<InventoryBuffer>();
                    return true;
                }
            }
        }

        buffer = default;
        return false;
    }

    public static string LookupName(this Entity entity)
    {
        return entity.GetPrefabGUID().LookupName();
    }

    public static bool TryGetEntityFromNetworkId(NetworkId networkId, out Entity entity)
    {
        entity = Entity.Null;

        var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<NetworkIdSystem.Singleton>() },
            Options = EntityQueryOptions.IncludeSystems
        });

        var entities = query.ToEntityArray(Allocator.Temp);
        try
        {
            if (entities.Length == 0)
            {
                return false;
            }

            var singleton = entities[0].Read<NetworkIdSystem.Singleton>();
            return singleton.GetNetworkIdLookupRW().TryGetValue(networkId, out entity);
        }
        finally
        {
            entities.Dispose();
            query.Dispose();
        }
    }
}
