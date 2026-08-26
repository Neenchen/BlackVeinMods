using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BlackVeinCore.Services;
using BlackVeinCore.Utilities;
using HarmonyLib;

[assembly: AssemblyMetadata("unloadable", "true")]
[assembly: AssemblyMetadata("reloadable", "true")]

namespace BlackVeinCore;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "blackvein.core";
    public const string PluginName = "BlackVeinCore";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony Harmony { get; private set; } = null!;

    private static bool _loaded;

    public override void Load()
    {
        if (_loaded)
        {
            return;
        }

        Logger = Log;
        EmbeddedDependencyResolver.Initialize();
        Core.Reset();
        PlayerService.Initialize();
        Harmony = new Harmony(PluginGuid);
        Harmony.PatchAll(Assembly.GetExecutingAssembly());

        _loaded = true;
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    public override bool Unload()
    {
        if (!_loaded)
        {
            return true;
        }

        Harmony.UnpatchSelf();
        ActionScheduler.Clear();
        PlayerService.Dispose();
        _loaded = false;
        Core.Reset();
        Logger.LogInfo($"{PluginName} unloaded.");
        return true;
    }
}
