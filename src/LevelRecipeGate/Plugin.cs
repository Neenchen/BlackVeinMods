using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BlackVeinCore;
using LevelRecipeGate.Config;
using LevelRecipeGate.Services;

#if !NoVCF
using VampireCommandFramework;
#endif

namespace LevelRecipeGate
{
	[BepInPlugin(Id, Name, Version)]
	[BepInDependency(BlackVeinCore.Plugin.PluginGuid)]
	[BepInDependency("gg.deca.VampireCommandFramework")]
	public sealed class Plugin : BasePlugin
	{
		public const string Id = "com.yourname.LevelRecipeGate";
		public const string Name = "LevelRecipeGate";
		public const string Version = "1.0.0";

		internal static ManualLogSource Logger;
		private static bool _loaded;

		public override void Load()
		{
			if (_loaded)
				return;

			Logger = Log;

			ConfigStore.EnsureConfigsExist();
			ConfigStore.LoadLevelRecipeBlocksFromDisk();
			ConfigStore.InitializeFileWatcher();

			PlayerLevelService.Load();
			RecipeGateEventBlocker.Initialize();

#if !NoVCF
			try
			{
				CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());
			}
			catch (Exception ex)
			{
				Logger.LogError($"[{Name}] Failed to register VCF commands: {ex}");
			}
#endif

			_loaded = true;
			Logger.LogInfo($"[{Name}] {Version} loaded.");
			Logger.LogInfo($"[{Name}] Configured level gates: {ConfigStore.GetConfiguredLevelGatesSnapshot().Count} item gate(s), {ConfigStore.GetJewelTierGatesSnapshot().Count} jewel tier gate(s), SecondaryBloodPotionMinLevel={ConfigStore.SecondaryBloodPotionMinLevel} | Enabled={ConfigStore.LevelRecipeBlocksEnabled}");
			Logger.LogInfo($"[{Name}] Config: {Path.GetFullPath(ConfigStore.LevelRecipeCfgFile)}");
		}

		public override bool Unload()
		{
			if (!_loaded)
				return true;

			try
			{
#if !NoVCF
				CommandRegistry.UnregisterAssembly(Assembly.GetExecutingAssembly());
#endif
				RecipeGateEventBlocker.Dispose();
				ConfigStore.DisposeFileWatcher();
				_loaded = false;
				Logger?.LogInfo($"[{Name}] unloaded.");
			}
			catch (Exception ex)
			{
				Logger?.LogError(ex);
			}

			return true;
		}
	}
}
