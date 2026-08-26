using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace BlackVeinCore.Utilities;

public sealed class Player
{
    public Player(Entity user, Entity character)
    {
        User = user;
        Character = character;
    }

    public Entity User { get; }
    public Entity Character { get; }

    public Entity Clan => User.Exists() ? User.Read<User>().ClanEntity._Entity : Entity.Null;
    public bool IsOnline => User.Exists() && User.Read<User>().IsConnected;
    public bool IsAdmin => User.Exists() && User.Read<User>().IsAdmin;
    public ulong SteamID => User.Exists() ? User.Read<User>().PlatformId : 0;
    public Entity Inventory => GetInventory();
    public float3 Position => Character.Has<Translation>() ? Character.Read<Translation>().Value : default;
    public int Level => GetLevel();
    public string ClanName => GetClanName();

    public FromCharacter ToFromCharacter()
    {
        return new FromCharacter
        {
            Character = Character,
            User = User
        };
    }

    public void SendMessage(string message)
    {
        if (!IsOnline || !User.Has<User>() || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var user = User.Read<User>();
        foreach (var chunk in SplitIntoChunks(message, 500))
        {
            var fixedString = new FixedString512Bytes(chunk);
            ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, user, ref fixedString);
        }
    }

    public Entity ProgressionEntity
    {
        get
        {
            if (!User.Exists() || !User.Has<AttachedBuffer>())
            {
                return Entity.Null;
            }

            var attachedBuffer = User.ReadBuffer<AttachedBuffer>();
            for (var index = 0; index < attachedBuffer.Length; index++)
            {
                if (attachedBuffer[index].PrefabGuid == Prefabs.ProgressionCollection)
                {
                    return attachedBuffer[index].Entity;
                }
            }

            return Entity.Null;
        }
    }

    private Entity GetInventory()
    {
        if (!Character.Has<InventoryInstanceElement>())
        {
            return Entity.Null;
        }

        var inventories = Character.ReadBuffer<InventoryInstanceElement>();
        return inventories.Length > 0
            ? inventories[0].ExternalInventoryEntity._Entity
            : Entity.Null;
    }

    public string Name
    {
        get
        {
            if (!User.Exists())
            {
                return "<unknown>";
            }

            var user = User.Read<User>();
            return user.CharacterName.ToString();
        }
    }

    private int GetLevel()
    {
        if (!Character.Has<Equipment>())
        {
            return 0;
        }

        return Mathf.RoundToInt(Character.Read<Equipment>().GetFullLevel());
    }

    private string GetClanName()
    {
        if (Character.Exists() && Character.Has<PlayerCharacter>())
        {
            var character = Character.Read<PlayerCharacter>();
            if (!character.SmartClanName.IsEmpty)
            {
                return character.SmartClanName.ToString();
            }
        }

        if (Clan.Exists() && Clan.Has<ClanTeam>())
        {
            return Clan.Read<ClanTeam>().Name.ToString();
        }

        return string.Empty;
    }

    private static IEnumerable<string> SplitIntoChunks(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            yield return value;
            yield break;
        }

        for (var offset = 0; offset < value.Length; offset += maxLength)
        {
            yield return value.Substring(offset, Math.Min(maxLength, value.Length - offset));
        }
    }
}
