using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace BlackVeinCore.Utilities;

public static class PlayerService
{
    private static readonly Dictionary<Entity, Player> UserCache = new();
    private static readonly Dictionary<Entity, Player> CharacterCache = new();
    private static readonly Dictionary<ulong, Player> SteamIdCache = new();
    private static readonly Dictionary<ulong, Player> OnlinePlayersBySteamId = new();
    private static readonly Dictionary<ulong, Player> OnlinePlayersWithCharactersBySteamId = new();

    private static EntityQuery _userQuery;
    private static bool _hasUserQuery;

    public static IReadOnlyCollection<Player> OnlinePlayers => OnlinePlayersBySteamId.Values.ToList();
    public static IReadOnlyCollection<Player> OnlinePlayersWithCharacters => OnlinePlayersWithCharactersBySteamId.Values.ToList();

    public static bool TryGetOnlinePlayerByName(string name, out Player player)
    {
        player = null!;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalizedName = name.Trim();
        player = OnlinePlayersWithCharactersBySteamId.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, normalizedName, StringComparison.OrdinalIgnoreCase))!;

        return player != null;
    }

    public static bool TryGetOnlinePlayerBySteamId(ulong steamId, out Player player)
    {
        return OnlinePlayersWithCharactersBySteamId.TryGetValue(steamId, out player!);
    }

    public static void Initialize()
    {
        Clear();
        LoadOnlinePlayers();
    }

    public static void Dispose()
    {
        Clear();
        DisposeQueries();
    }

    public static Player? RegisterConnectedUser(Entity userEntity)
    {
        if (!userEntity.Exists() || !userEntity.Has<User>())
        {
            return null;
        }

        var user = userEntity.Read<User>();
        var characterEntity = user.LocalCharacter._Entity;
        var player = CachePlayer(userEntity, characterEntity);
        if (player.SteamID == 0)
        {
            return player;
        }

        OnlinePlayersBySteamId[player.SteamID] = player;
        if (player.Character.Exists())
        {
            OnlinePlayersWithCharactersBySteamId[player.SteamID] = player;
        }

        return player;
    }

    public static Player? RegisterDisconnectedUser(Entity userEntity)
    {
        if (!userEntity.Exists() || !userEntity.Has<User>())
        {
            return null;
        }

        var user = userEntity.Read<User>();
        var player = CachePlayer(userEntity, user.LocalCharacter._Entity);
        if (player.SteamID != 0)
        {
            OnlinePlayersBySteamId.Remove(player.SteamID);
            OnlinePlayersWithCharactersBySteamId.Remove(player.SteamID);
        }

        return player;
    }

    public static Player? RegisterCharacterSpawned(Entity characterEntity)
    {
        if (!characterEntity.Exists() || !characterEntity.Has<PlayerCharacter>())
        {
            return null;
        }

        var userEntity = characterEntity.Read<PlayerCharacter>().UserEntity;
        if (!userEntity.Exists() || !userEntity.Has<User>())
        {
            return null;
        }

        var player = CachePlayer(userEntity, characterEntity);
        if (player.SteamID == 0)
        {
            return player;
        }

        if (player.IsOnline)
        {
            OnlinePlayersBySteamId[player.SteamID] = player;
            OnlinePlayersWithCharactersBySteamId[player.SteamID] = player;
        }

        return player;
    }

    public static bool TryFromCharacter(Entity character, out Player player)
    {
        player = null!;
        if (!character.Exists() || !character.Has<PlayerCharacter>())
        {
            return false;
        }

        if (CharacterCache.TryGetValue(character, out player!))
        {
            return true;
        }

        var userEntity = character.Read<PlayerCharacter>().UserEntity;
        if (!userEntity.Exists() || !userEntity.Has<User>())
        {
            return false;
        }

        player = CachePlayer(userEntity, character);
        return true;
    }

    public static bool TryFromUser(Entity userEntity, out Player player)
    {
        player = null!;
        if (!userEntity.Exists() || !userEntity.Has<User>())
        {
            return false;
        }

        if (UserCache.TryGetValue(userEntity, out player!))
        {
            return true;
        }

        var characterEntity = userEntity.Read<User>().LocalCharacter._Entity;
        if (!characterEntity.Exists() || !characterEntity.Has<PlayerCharacter>())
        {
            return false;
        }

        player = CachePlayer(userEntity, characterEntity);
        return true;
    }

    public static bool TryFromUserAndCharacter(Entity userEntity, Entity characterEntity, out Player player)
    {
        player = null!;
        if (!userEntity.Exists() || !userEntity.Has<User>())
        {
            return false;
        }

        if (!characterEntity.Exists())
        {
            characterEntity = userEntity.Read<User>().LocalCharacter._Entity;
        }

        if (!characterEntity.Exists() || !characterEntity.Has<PlayerCharacter>())
        {
            return false;
        }

        player = CachePlayer(userEntity, characterEntity);
        return true;
    }

    private static void LoadOnlinePlayers()
    {
        EnsureUserQuery();
        var users = _userQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var userEntity in users)
            {
                if (!userEntity.Exists() || !userEntity.Has<User>())
                {
                    continue;
                }

                var user = userEntity.Read<User>();
                if (user.IsConnected)
                {
                    RegisterConnectedUser(userEntity);
                }
            }
        }
        finally
        {
            users.Dispose();
        }
    }

    private static Player CachePlayer(Entity userEntity, Entity characterEntity)
    {
        var steamId = userEntity.Exists() && userEntity.Has<User>()
            ? userEntity.Read<User>().PlatformId
            : 0;

        if (UserCache.TryGetValue(userEntity, out var player) &&
            player.Character == characterEntity)
        {
            if (steamId != 0)
            {
                SteamIdCache[steamId] = player;
            }

            if (characterEntity.Exists())
            {
                CharacterCache[characterEntity] = player;
            }

            return player;
        }

        if (steamId != 0 && SteamIdCache.TryGetValue(steamId, out var existingPlayer))
        {
            if (existingPlayer.User.Exists())
            {
                UserCache.Remove(existingPlayer.User);
            }

            if (existingPlayer.Character.Exists())
            {
                CharacterCache.Remove(existingPlayer.Character);
            }
        }

        player = new Player(userEntity, characterEntity);
        UserCache[userEntity] = player;
        if (characterEntity.Exists())
        {
            CharacterCache[characterEntity] = player;
        }

        if (steamId != 0)
        {
            SteamIdCache[steamId] = player;
        }

        return player;
    }

    private static void Clear()
    {
        UserCache.Clear();
        CharacterCache.Clear();
        SteamIdCache.Clear();
        OnlinePlayersBySteamId.Clear();
        OnlinePlayersWithCharactersBySteamId.Clear();
    }

    private static void EnsureUserQuery()
    {
        if (_hasUserQuery)
        {
            return;
        }

        _userQuery = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<User>());
        _hasUserQuery = true;
    }

    private static void DisposeQueries()
    {
        if (!_hasUserQuery)
        {
            return;
        }

        _userQuery.Dispose();
        _userQuery = default;
        _hasUserQuery = false;
    }
}
