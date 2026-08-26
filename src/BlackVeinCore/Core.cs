using BlackVeinCore.Utilities;
using ProjectM;
using ProjectM.Scripting;
using Unity.Entities;

namespace BlackVeinCore;

public static class Core
{
    private static PrefabCollectionSystem? _prefabCollectionSystem;
    private static DebugEventsSystem? _debugEventsSystem;
    private static EntityCommandBufferSystem? _entityCommandBufferSystem;
    private static EndSimulationEntityCommandBufferSystem? _endSimulationEntityCommandBufferSystem;
    private static ClaimAchievementSystem? _claimAchievementSystem;
    private static GameDataSystem? _gameDataSystem;
    private static ServerScriptMapper? _serverScriptMapper;

    public static World Server => VWorld.Server;
    public static EntityManager EntityManager => Server.EntityManager;

    public static PrefabCollectionSystem PrefabCollectionSystem =>
        _prefabCollectionSystem ??= Server.GetExistingSystemManaged<PrefabCollectionSystem>();

    public static DebugEventsSystem DebugEventsSystem =>
        _debugEventsSystem ??= Server.GetExistingSystemManaged<DebugEventsSystem>();

    public static EntityCommandBufferSystem EntityCommandBufferSystem =>
        _entityCommandBufferSystem ??= Server.GetExistingSystemManaged<EntityCommandBufferSystem>();

    public static EndSimulationEntityCommandBufferSystem EndSimulationEntityCommandBufferSystem =>
        _endSimulationEntityCommandBufferSystem ??= Server.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();

    public static ClaimAchievementSystem ClaimAchievementSystem =>
        _claimAchievementSystem ??= Server.GetExistingSystemManaged<ClaimAchievementSystem>();

    public static GameDataSystem GameDataSystem =>
        _gameDataSystem ??= Server.GetExistingSystemManaged<GameDataSystem>();

    public static ServerScriptMapper ServerScriptMapper =>
        _serverScriptMapper ??= Server.GetExistingSystemManaged<ServerScriptMapper>();

    public static ServerGameManager ServerGameManager => ServerScriptMapper.GetServerGameManager();

    public static EntityCommandBuffer CreateCommandBuffer()
    {
        return EntityCommandBufferSystem.CreateCommandBuffer();
    }

    public static void Reset()
    {
        _prefabCollectionSystem = null;
        _debugEventsSystem = null;
        _entityCommandBufferSystem = null;
        _endSimulationEntityCommandBufferSystem = null;
        _claimAchievementSystem = null;
        _gameDataSystem = null;
        _serverScriptMapper = null;
    }
}
