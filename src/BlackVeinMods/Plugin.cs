using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BlackVeinCore;
using BlackVeinMods.Commands;
using BlackVeinMods.Configs;
using BlackVeinMods.Managers;
using VampireCommandFramework;

[assembly: AssemblyMetadata("unloadable", "true")]
[assembly: AssemblyMetadata("reloadable", "true")]

namespace BlackVeinMods;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(BlackVeinCore.Plugin.PluginGuid)]
[BepInDependency("gg.deca.VampireCommandFramework")]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "blackvein.mods";
    public const string PluginName = "BlackVeinMods";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Logger { get; private set; } = null!;
    private static bool _loaded;

    public override void Load()
    {
        if (_loaded)
        {
            return;
        }

        Logger = Log;

        BlackVeinConfig.Initialize();
        ClanProgressionManager.Initialize();
        CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());

        _loaded = true;
    }

    public override bool Unload()
    {
        if (!_loaded)
        {
            return true;
        }

        CommandRegistry.UnregisterAssembly(Assembly.GetExecutingAssembly());
        ClanProgressionManager.Dispose();
        BlackVeinConfig.Dispose();

        _loaded = false;
        return true;
    }
}
