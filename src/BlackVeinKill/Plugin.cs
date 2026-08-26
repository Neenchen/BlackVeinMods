using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BlackVeinCore;
using BlackVeinKill.Database;
using BlackVeinKill.Managers;
using BlackVeinKill.Services;
using VampireCommandFramework;

[assembly: AssemblyMetadata("unloadable", "true")]
[assembly: AssemblyMetadata("reloadable", "true")]

namespace BlackVeinKill;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(BlackVeinCore.Plugin.PluginGuid)]
[BepInDependency("gg.deca.VampireCommandFramework")]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "blackvein.kill";
    public const string PluginName = "BlackVeinKill";
    public const string PluginVersion = "1.1.78";

    internal static ManualLogSource Logger { get; private set; } = null!;
    private static bool _loaded;

    public override void Load()
    {
        if (_loaded)
        {
            return;
        }

        Logger = Log;
        KillfeedSettings.Initialize();
        DatabaseWrapper.Initialize();
        PvPStatsService.Initialize();
        FfaZoneManager.Initialize();
        KillfeedManager.Initialize();
        CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());

        _loaded = true;
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    public override bool Unload()
    {
        if (!_loaded)
        {
            return true;
        }

        CommandRegistry.UnregisterAssembly(Assembly.GetExecutingAssembly());
        KillfeedManager.Dispose();
        FfaZoneManager.Dispose();
        PvPStatsService.Dispose();
        DatabaseWrapper.DisposeInstance();
        KillfeedSettings.Dispose();

        _loaded = false;
        Logger.LogInfo($"{PluginName} unloaded.");
        return true;
    }
}
