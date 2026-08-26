using Unity.Entities;
using UnityEngine;

namespace BlackVeinCore.Utilities;

public static class VWorld
{
    private const string ServerWorldName = "Server";
    private const string ClientWorldName = "Client_0";

    private static World? _serverWorld;
    private static World? _clientWorld;

    public static World Server
    {
        get
        {
            if (TryGetServerWorld(out var world))
            {
                return world;
            }

            throw new InvalidOperationException("There is no Server world yet. Did you install a server mod on the client?");
        }
    }

    public static World Client
    {
        get
        {
            if (TryGetClientWorld(out var world))
            {
                return world;
            }

            throw new InvalidOperationException("There is no Client world yet. Did you install a client mod on the server?");
        }
    }

    public static World Game => Application.productName == "VRising" ? Client : Server;

    public static bool TryGetServerWorld(out World world)
    {
        return TryGetWorld(ServerWorldName, ref _serverWorld, out world);
    }

    public static bool TryGetClientWorld(out World world)
    {
        return TryGetWorld(ClientWorldName, ref _clientWorld, out world);
    }

    private static bool TryGetWorld(string name, ref World? cachedWorld, out World world)
    {
        if (cachedWorld is { IsCreated: true })
        {
            world = cachedWorld;
            return true;
        }

        foreach (var candidate in World.s_AllWorlds)
        {
            if (candidate is { IsCreated: true } && candidate.Name == name)
            {
                cachedWorld = candidate;
                world = candidate;
                return true;
            }
        }

        world = null!;
        return false;
    }
}
