using Unity.Entities;

namespace LeadAHorseToWater.Compat;

/// <summary>
/// Drop-in replacement for Bloodstone.API.VWorld.
///
/// Bloodstone is abandoned and its network serialization hooks call game methods
/// that no longer exist in V Rising 1.1.x, which breaks every network event on the
/// server. This mod only ever needed Bloodstone to say "give me the server World",
/// so we fetch it straight from Unity ECS instead and drop the dependency entirely.
/// </summary>
public static class VWorld
{
    private static World _server;

    public static World Server
    {
        get
        {
            if (_server != null && _server.IsCreated) return _server;
            _server = GetWorld("Server");
            return _server;
        }
    }

    // Bloodstone exposed Game separately; on a dedicated server they are the same world.
    public static World Game => Server;

    // Plugin is restricted to VRisingServer.exe via [BepInProcess], so this is always true.
    public static bool IsServer => true;
    public static bool IsClient => false;

    private static World GetWorld(string name)
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world.Name == name) return world;
        }
        return null;
    }
}
