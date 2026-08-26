using System;
using BlackVeinCore;
using BlackVeinCore.Persistence;
using BlackVeinCore.Services;
using BlackVeinCore.Utilities;
using ProjectM;
using Stunlock.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Unity.Entities;

namespace LevelRecipeGate.Config
{
	internal static class ConfigStore
	{
		private static readonly TimeSpan ReloadDebounceDelay = TimeSpan.FromMilliseconds(500);
		private static string ConfigDir => BlackVeinConfigPaths.ConfigDirectory;
		private static readonly object SyncRoot = new object();
		private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			WriteIndented = true
		};
		private static ConfigReloadWatcher _watcher;
		private static int _watcherReloadPending;

		public static string LevelRecipeCfgFile => Path.Combine(ConfigDir, "level_recipe_blocks.json");

		internal static readonly Dictionary<int, int> EquippableMinLevelByGuid = new Dictionary<int, int>();
		internal static readonly Dictionary<int, int> ConsumableMinLevelByGuid = new Dictionary<int, int>();
		private static readonly Dictionary<int, int> JewelTierMinLevelByTier = new Dictionary<int, int>();
		private static readonly Dictionary<int, int> ConfiguredMinLevelByGuid = new Dictionary<int, int>();
		private static bool _runtimeGateIndexesDirty;
		internal static bool LevelRecipeBlocksEnabled = true;
		internal static string LevelRecipeBlockedMessage = "Unused for item equip/use blocking; kept for config compatibility.";
		internal static int SecondaryBloodPotionMinLevel = 80;

		private sealed class LevelRecipeConfigFile
		{
			public bool enabled { get; set; } = true;
			public string message { get; set; } = "Unused for item equip/use blocking; kept for config compatibility.";
			public int secondary_blood_potion_min_level { get; set; } = 80;
			public List<LevelRecipeBlockEntry> recipe_level_blocks { get; set; } = CreateDefaultLevelRecipeBlocks();
			public List<JewelTierLevelBlockEntry> jewel_tier_level_blocks { get; set; } = CreateDefaultJewelTierLevelBlocks();
		}

		private sealed class LevelRecipeBlockEntry
		{
			public int min_level { get; set; }
			public List<object> recipes { get; set; } = new List<object>();
		}

		private sealed class JewelTierLevelBlockEntry
		{
			public int tier { get; set; }
			public int min_level { get; set; }
		}

		internal static void EnsureConfigsExist()
		{
			Directory.CreateDirectory(ConfigDir);

			if (!File.Exists(LevelRecipeCfgFile))
			{
				JsonConfigStore<LevelRecipeConfigFile>.Save(
					LevelRecipeCfgFile,
					CreateDefaultLevelRecipeConfig(),
					SerializerOptions,
					message => Plugin.Logger.LogWarning(message));
			}
		}

		private static LevelRecipeConfigFile CreateDefaultLevelRecipeConfig()
		{
			return new LevelRecipeConfigFile
			{
				enabled = true,
				message = "Unused for item equip/use blocking; kept for config compatibility.",
				secondary_blood_potion_min_level = 80,
				recipe_level_blocks = CreateDefaultLevelRecipeBlocks(),
				jewel_tier_level_blocks = CreateDefaultJewelTierLevelBlocks()
			};
		}

		private static List<LevelRecipeBlockEntry> CreateDefaultLevelRecipeBlocks()
		{
			return new List<LevelRecipeBlockEntry>
			{
				new LevelRecipeBlockEntry
				{
					min_level = 33,
					recipes = new List<object>
					{
						"Recipe_Weapon_Axe_T05_Iron",
						"Recipe_Weapon_Claws_T05_Iron",
						"Recipe_Weapon_Crossbow_T05_Iron",
						"Recipe_Weapon_Daggers_T05_Iron",
						"Recipe_Weapon_GreatSword_T05_Iron",
						"Recipe_Weapon_Longbow_T05_Iron",
						"Recipe_Weapon_Mace_T05_Iron",
						"Recipe_Weapon_Pistols_T05_Iron",
						"Recipe_Weapon_Reaper_T05_Iron",
						"Recipe_Weapon_Slashers_T05_Iron",
						"Recipe_Weapon_Spear_T05_Iron",
						"Recipe_Weapon_Sword_T05_Iron",
						"Recipe_Weapon_TwinBlades_T05_Iron",
						"Recipe_Weapon_Whip_T05_Iron",
						"Item_MagicSource_General_T05_Relic"
					}
				},
				new LevelRecipeBlockEntry
				{
					min_level = 50,
					recipes = new List<object>
					{
						"Recipe_Armor_Boots_T06_Iron_Brute",
						"Recipe_Armor_Boots_T06_Iron_Rogue",
						"Recipe_Armor_Boots_T06_Iron_Scholar",
						"Recipe_Armor_Boots_T06_Iron_Warrior",
						"Recipe_Armor_Chest_T06_Iron_Brute",
						"Recipe_Armor_Chest_T06_Iron_Rogue",
						"Recipe_Armor_Chest_T06_Iron_Scholar",
						"Recipe_Armor_Chest_T06_Iron_Warrior",
						"Recipe_Armor_Gloves_T06_Iron_Brute",
						"Recipe_Armor_Gloves_T06_Iron_Rogue",
						"Recipe_Armor_Gloves_T06_Iron_Scholar",
						"Recipe_Armor_Gloves_T06_Iron_Warrior",
						"Recipe_Armor_Legs_T06_Iron_Brute",
						"Recipe_Armor_Legs_T06_Iron_Rogue",
						"Recipe_Armor_Legs_T06_Iron_Scholar",
						"Recipe_Armor_Legs_T06_Iron_Warrior",
						"Recipe_MagicSource_General_T06_AmethystPendant",
						"Recipe_MagicSource_General_T06_EmeraldNecklace",
						"Recipe_MagicSource_General_T06_MistStoneNecklace",
						"Recipe_MagicSource_General_T06_RubyPendant",
						"Recipe_MagicSource_General_T06_SapphirePendant",
						"Recipe_MagicSource_General_T06_TopazAmulet",
						"Recipe_Weapon_Axe_T06_Iron_Reinforced",
						"Recipe_Weapon_Claws_T06_Iron_Reinforced",
						"Recipe_Weapon_Crossbow_T06_Iron_Reinforced",
						"Recipe_Weapon_Daggers_T06_Iron_Reinforced",
						"Recipe_Weapon_GreatSword_T06_Iron_Reinforced",
						"Recipe_Weapon_Longbow_T06_Iron_Reinforced",
						"Recipe_Weapon_Mace_T06_Iron_Reinforced",
						"Recipe_Weapon_Pistols_T06_Iron_Reinforced",
						"Recipe_Weapon_Reaper_T06_Iron_Reinforced",
						"Recipe_Weapon_Slashers_T06_Iron_Reinforced",
						"Recipe_Weapon_Spear_T06_Iron_Reinforced",
						"Recipe_Weapon_Sword_T06_Iron_Reinforced",
						"Recipe_Weapon_TwinBlades_T06_Iron_Reinforced",
						"Recipe_Weapon_Whip_T06_Iron_Reinforced"
					}
				},
				new LevelRecipeBlockEntry
				{
					min_level = 65,
					recipes = new List<object>
					{
						"Recipe_Weapon_Axe_T07_DarkSilver",
						"Recipe_Weapon_Claws_T07_DarkSilver",
						"Recipe_Weapon_Crossbow_T07_DarkSilver",
						"Recipe_Weapon_Daggers_T07_DarkSilver",
						"Recipe_Weapon_GreatSword_T07_DarkSilver",
						"Recipe_Weapon_Longbow_T07_DarkSilver",
						"Recipe_Weapon_Mace_T07_DarkSilver",
						"Recipe_Weapon_Pistols_T07_DarkSilver",
						"Recipe_Weapon_Reaper_T07_DarkSilver",
						"Recipe_Weapon_Slashers_T07_DarkSilver",
						"Recipe_Weapon_Spear_T07_DarkSilver",
						"Recipe_Weapon_Sword_T07_DarkSilver",
						"Recipe_Weapon_TwinBlades_T07_DarkSilver",
						"Recipe_Weapon_Whip_T07_DarkSilver",
						"Recipe_MagicSource_General_T07_BloodwineAmulet"
					}
				},
				new LevelRecipeBlockEntry
				{
					min_level = 74,
					recipes = new List<object>
					{
						"Recipe_Armor_Boots_T08_DarkSilver_Brute",
						"Recipe_Armor_Boots_T08_DarkSilver_Rogue",
						"Recipe_Armor_Boots_T08_DarkSilver_Scholar",
						"Recipe_Armor_Boots_T08_DarkSilver_Warrior",
						"Recipe_Armor_Chest_T08_DarkSilver_Brute",
						"Recipe_Armor_Chest_T08_DarkSilver_Rogue",
						"Recipe_Armor_Chest_T08_DarkSilver_Scholar",
						"Recipe_Armor_Chest_T08_DarkSilver_Warrior",
						"Recipe_Armor_Gloves_T08_DarkSilver_Brute",
						"Recipe_Armor_Gloves_T08_DarkSilver_Rogue",
						"Recipe_Armor_Gloves_T08_DarkSilver_Scholar",
						"Recipe_Armor_Gloves_T08_DarkSilver_Warrior",
						"Recipe_Armor_Legs_T08_DarkSilver_Brute",
						"Recipe_Armor_Legs_T08_DarkSilver_Rogue",
						"Recipe_Armor_Legs_T08_DarkSilver_Scholar",
						"Recipe_Armor_Legs_T08_DarkSilver_Warrior",
						"Recipe_Weapon_Axe_T08_Sanguine",
						"Recipe_Weapon_Claws_T08_Sanguine",
						"Recipe_Weapon_Crossbow_T08_Sanguine",
						"Recipe_Weapon_Daggers_T08_Sanguine",
						"Recipe_Weapon_GreatSword_T08_Sanguine",
						"Recipe_Weapon_Longbow_T08_Sanguine",
						"Recipe_Weapon_Mace_T08_Sanguine",
						"Recipe_Weapon_Pistols_T08_Sanguine",
						"Recipe_Weapon_Reaper_T08_Sanguine",
						"Recipe_Weapon_Slashers_T08_Sanguine",
						"Recipe_Weapon_Spear_T08_Sanguine",
						"Recipe_Weapon_Sword_T08_Sanguine",
						"Recipe_Weapon_TwinBlades_T08_Sanguine",
						"Recipe_Weapon_Whip_T08_Sanguine",
						"Recipe_MagicSource_General_T08_Beast",
						"Recipe_MagicSource_General_T08_CrimsonSky",
						"Recipe_MagicSource_General_T08_Delusion",
						"Recipe_MagicSource_General_T08_FrozenCrypt",
						"Recipe_MagicSource_General_T08_Madness",
						"Recipe_MagicSource_General_T08_WickedProphet",
						"Item_Vampire_Coating_Frost",
						"Item_Vampire_Coating_Blood",
						"Item_Vampire_Coating_Chaos",
						"Item_Vampire_Coating_Illusion",
						"Item_Vampire_Coating_Storm",
						"Item_Vampire_Coating_Unholy"
					}
				},
				new LevelRecipeBlockEntry
				{
					min_level = 80,
					recipes = new List<object>()
				}
			};
		}

		private static List<JewelTierLevelBlockEntry> CreateDefaultJewelTierLevelBlocks()
		{
			return new List<JewelTierLevelBlockEntry>
			{
				new JewelTierLevelBlockEntry
				{
					tier = 3,
					min_level = 65
				},
				new JewelTierLevelBlockEntry
				{
					tier = 4,
					min_level = 74
				}
			};
		}

		internal static void InitializeFileWatcher()
		{
			_watcher?.Dispose();
			_watcher = new ConfigReloadWatcher(ConfigDir, Path.GetFileName(LevelRecipeCfgFile), ReloadFromWatcher, ReloadDebounceDelay);
			_watcher.Initialize();
		}

		internal static void DisposeFileWatcher()
		{
			_watcher?.Dispose();
			_watcher = null;
		}

		internal static void LoadLevelRecipeBlocksFromDisk()
		{
			try
			{
				EnsureConfigsExist();

				var result = JsonConfigStore<LevelRecipeConfigFile>.Load(
					LevelRecipeCfgFile,
					CreateDefaultLevelRecipeConfig,
					SerializerOptions,
					NormalizeLevelRecipeConfig,
					message => Plugin.Logger.LogWarning(message));

				if (!result.ShouldApplyValue)
				{
					return;
				}

				var cfg = result.Value!;
				var loadedConfiguredGates = new Dictionary<int, int>();
				var loadedJewelTierGates = new Dictionary<int, int>();
				bool enabled = cfg.enabled;
				string message = string.IsNullOrWhiteSpace(cfg.message)
					? "Unused for item equip/use blocking; kept for config compatibility."
					: cfg.message;
				bool needsRewrite = false;

				if (cfg.recipe_level_blocks != null)
				{
					foreach (var block in cfg.recipe_level_blocks)
					{
						if (block == null || block.recipes == null)
							continue;

						foreach (object recipe in block.recipes)
						{
							if (!TryReadRecipeGuid(recipe, out int guid, out bool shouldRewriteRecipe))
							{
								Plugin.Logger.LogWarning($"[{Plugin.Name}] Ignoring unknown recipe prefab '{recipe}' in {LevelRecipeCfgFile}.");
								continue;
							}

							needsRewrite |= shouldRewriteRecipe;

							if (!loadedConfiguredGates.TryGetValue(guid, out int existing) || block.min_level > existing)
								loadedConfiguredGates[guid] = block.min_level;
						}
					}
				}

				if (cfg.jewel_tier_level_blocks != null)
				{
					foreach (var block in cfg.jewel_tier_level_blocks)
					{
						if (block == null)
							continue;

						if (!TryNormalizeJewelTier(block.tier, out int tier))
						{
							Plugin.Logger.LogWarning($"[{Plugin.Name}] Ignoring invalid jewel tier '{block.tier}' in {LevelRecipeCfgFile}. Use tiers 1-4.");
							continue;
						}

						AddMaxLevel(loadedJewelTierGates, tier, block.min_level);
					}
				}

				lock (SyncRoot)
				{
					ConfiguredMinLevelByGuid.Clear();
					foreach (var kvp in loadedConfiguredGates)
						ConfiguredMinLevelByGuid[kvp.Key] = kvp.Value;

					JewelTierMinLevelByTier.Clear();
					foreach (var kvp in loadedJewelTierGates)
						JewelTierMinLevelByTier[kvp.Key] = kvp.Value;

					RebuildRuntimeGateIndexes();

					LevelRecipeBlocksEnabled = enabled;
					LevelRecipeBlockedMessage = message;
					SecondaryBloodPotionMinLevel = cfg.secondary_blood_potion_min_level;
				}

				Plugin.Logger.LogInfo($"[{Plugin.Name}] Loaded {loadedConfiguredGates.Count} configured item gate(s): {EquippableMinLevelByGuid.Count} equippable item and {ConsumableMinLevelByGuid.Count} consumable item runtime gate(s); {loadedJewelTierGates.Count} jewel tier gate(s). SecondaryBloodPotionMinLevel={cfg.secondary_blood_potion_min_level}. Enabled={enabled}.");

				if (result.NeedsRewrite || needsRewrite)
					SaveLevelRecipeBlocksToDisk();
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError($"[{Plugin.Name}] Failed loading level recipe config: {ex}");
			}
		}

		internal static void SaveLevelRecipeBlocksToDisk()
		{
			try
			{
				Directory.CreateDirectory(ConfigDir);

				Dictionary<int, int> snapshot = GetConfiguredLevelGatesSnapshot();
				Dictionary<int, int> jewelTierSnapshot = GetJewelTierGatesSnapshot();
				bool enabled;
				string message;
				int secondaryBloodPotionMinLevel;

				lock (SyncRoot)
				{
					enabled = LevelRecipeBlocksEnabled;
					message = LevelRecipeBlockedMessage;
					secondaryBloodPotionMinLevel = SecondaryBloodPotionMinLevel;
				}

				var grouped = snapshot
					.GroupBy(kvp => kvp.Value)
					.OrderBy(g => g.Key)
					.Select(g => new LevelRecipeBlockEntry
					{
						min_level = g.Key,
						recipes = g
							.Select(kvp => RecipePrefabLookup.ToConfigValue(kvp.Key))
							.OrderBy(value => value.ToString())
							.ToList()
					})
					.ToList();

				var cfg = new LevelRecipeConfigFile
				{
					enabled = enabled,
					message = message,
					secondary_blood_potion_min_level = secondaryBloodPotionMinLevel,
					recipe_level_blocks = grouped,
					jewel_tier_level_blocks = jewelTierSnapshot
						.OrderBy(kvp => kvp.Key)
						.Select(kvp => new JewelTierLevelBlockEntry
						{
							tier = kvp.Key,
							min_level = kvp.Value
						})
						.ToList()
				};

				JsonConfigStore<LevelRecipeConfigFile>.Save(
					LevelRecipeCfgFile,
					cfg,
					SerializerOptions,
					message => Plugin.Logger.LogWarning(message));
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError($"[{Plugin.Name}] Failed saving level recipe config: {ex}");
			}
		}

		internal static bool TryGetRequiredEquippableLevel(int itemGuid, out int requiredLevel)
		{
			lock (SyncRoot)
			{
				requiredLevel = 0;
				EnsureRuntimeGateIndexesCurrent();
				return LevelRecipeBlocksEnabled && EquippableMinLevelByGuid.TryGetValue(itemGuid, out requiredLevel);
			}
		}

		internal static bool TryGetRequiredConsumableLevel(int itemGuid, out int requiredLevel)
		{
			lock (SyncRoot)
			{
				requiredLevel = 0;
				EnsureRuntimeGateIndexesCurrent();
				return LevelRecipeBlocksEnabled && ConsumableMinLevelByGuid.TryGetValue(itemGuid, out requiredLevel);
			}
		}

		internal static bool TryGetRequiredJewelTierLevel(int tier, out int requiredLevel)
		{
			lock (SyncRoot)
			{
				requiredLevel = 0;
				return LevelRecipeBlocksEnabled &&
				       TryNormalizeJewelTier(tier, out int normalizedTier) &&
				       JewelTierMinLevelByTier.TryGetValue(normalizedTier, out requiredLevel);
			}
		}

		internal static Dictionary<int, int> GetAllLevelGatesSnapshot()
		{
			return GetConfiguredLevelGatesSnapshot();
		}

		internal static Dictionary<int, int> GetConfiguredLevelGatesSnapshot()
		{
			lock (SyncRoot)
			{
				return new Dictionary<int, int>(ConfiguredMinLevelByGuid);
			}
		}

		internal static Dictionary<int, int> GetJewelTierGatesSnapshot()
		{
			lock (SyncRoot)
			{
				return new Dictionary<int, int>(JewelTierMinLevelByTier);
			}
		}

		internal static bool TryResolveRecipePrefab(string value, out int recipeGuid)
		{
			return RecipePrefabLookup.TryResolve(value, out recipeGuid);
		}

		internal static string FormatRecipePrefab(int recipeGuid)
		{
			return RecipePrefabLookup.Format(recipeGuid);
		}

		internal static void SetRecipeLevelGate(int recipeGuid, int minLevel)
		{
			lock (SyncRoot)
			{
				ConfiguredMinLevelByGuid[recipeGuid] = minLevel;
				RebuildRuntimeGateIndexes();
			}

			SaveLevelRecipeBlocksToDisk();
		}

		internal static bool RemoveRecipeLevelGate(int recipeGuid)
		{
			bool removed;
			lock (SyncRoot)
			{
				removed = ConfiguredMinLevelByGuid.Remove(recipeGuid);
				if (removed)
					RebuildRuntimeGateIndexes();
			}

			if (removed)
				SaveLevelRecipeBlocksToDisk();
			return removed;
		}

		internal static bool TrySetJewelTierLevelGate(int tier, int minLevel)
		{
			if (!TryNormalizeJewelTier(tier, out int normalizedTier))
				return false;

			lock (SyncRoot)
			{
				JewelTierMinLevelByTier[normalizedTier] = minLevel;
			}

			SaveLevelRecipeBlocksToDisk();
			return true;
		}

		internal static bool RemoveJewelTierLevelGate(int tier)
		{
			if (!TryNormalizeJewelTier(tier, out int normalizedTier))
				return false;

			bool removed;
			lock (SyncRoot)
			{
				removed = JewelTierMinLevelByTier.Remove(normalizedTier);
			}

			if (removed)
				SaveLevelRecipeBlocksToDisk();
			return removed;
		}

		internal static bool TryGetRequiredSecondaryBloodPotionLevel(out int requiredLevel)
		{
			lock (SyncRoot)
			{
				requiredLevel = SecondaryBloodPotionMinLevel;
				return LevelRecipeBlocksEnabled && requiredLevel > 0;
			}
		}

		internal static bool TrySetSecondaryBloodPotionMinLevel(int minLevel)
		{
			if (minLevel < 0 || minLevel > 255)
				return false;

			lock (SyncRoot)
			{
				SecondaryBloodPotionMinLevel = minLevel;
			}

			SaveLevelRecipeBlocksToDisk();
			return true;
		}

		private static void ReloadFromWatcher()
		{
			if (!File.Exists(LevelRecipeCfgFile))
				return;

			if (System.Threading.Interlocked.Exchange(ref _watcherReloadPending, 1) == 1)
				return;

			Plugin.Logger.LogInfo($"[{Plugin.Name}] Detected config change, scheduling reload of {Path.GetFileName(LevelRecipeCfgFile)}.");
			ActionScheduler.RunActionOnMainThread(() =>
			{
				System.Threading.Interlocked.Exchange(ref _watcherReloadPending, 0);

				if (!File.Exists(LevelRecipeCfgFile))
					return;

				Plugin.Logger.LogInfo($"[{Plugin.Name}] Reloading {Path.GetFileName(LevelRecipeCfgFile)}.");
				LoadLevelRecipeBlocksFromDisk();
			});
		}

		private static void NormalizeLevelRecipeConfig(LevelRecipeConfigFile cfg)
		{
			cfg.message = string.IsNullOrWhiteSpace(cfg.message)
				? "Unused for item equip/use blocking; kept for config compatibility."
				: cfg.message;

			cfg.recipe_level_blocks ??= CreateDefaultLevelRecipeBlocks();
			cfg.jewel_tier_level_blocks ??= CreateDefaultJewelTierLevelBlocks();
		}

		private static bool TryReadRecipeGuid(object value, out int recipeGuid, out bool shouldRewrite)
		{
			recipeGuid = 0;
			shouldRewrite = false;

			if (value is JsonElement element)
			{
				if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out recipeGuid))
				{
					shouldRewrite = RecipePrefabLookup.HasName(recipeGuid);
					return true;
				}

				if (element.ValueKind == JsonValueKind.String)
					return TryReadRecipeGuidString(element.GetString(), out recipeGuid, out shouldRewrite);

				return false;
			}

			if (value is int intValue)
			{
				recipeGuid = intValue;
				shouldRewrite = RecipePrefabLookup.HasName(recipeGuid);
				return true;
			}

			if (value is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
			{
				recipeGuid = (int)longValue;
				shouldRewrite = RecipePrefabLookup.HasName(recipeGuid);
				return true;
			}

			if (value is string stringValue)
				return TryReadRecipeGuidString(stringValue, out recipeGuid, out shouldRewrite);

			return value != null && TryReadRecipeGuidString(value.ToString(), out recipeGuid, out shouldRewrite);
		}

		private static bool TryReadRecipeGuidString(string value, out int recipeGuid, out bool shouldRewrite)
		{
			recipeGuid = 0;
			shouldRewrite = false;

			if (!RecipePrefabLookup.TryResolve(value, out recipeGuid))
				return false;

			shouldRewrite = int.TryParse(value, out _) && RecipePrefabLookup.HasName(recipeGuid);
			return true;
		}

		private static void RebuildRuntimeGateIndexes()
		{
			EquippableMinLevelByGuid.Clear();
			ConsumableMinLevelByGuid.Clear();
			_runtimeGateIndexesDirty = false;

			foreach (var kvp in ConfiguredMinLevelByGuid)
			{
				try
				{
					AddConfiguredGuidToRuntimeIndexes(new PrefabGUID(kvp.Key), kvp.Value);
				}
				catch (Exception ex)
				{
					_runtimeGateIndexesDirty = true;
					Plugin.Logger.LogWarning($"[{Plugin.Name}] Could not classify configured prefab {kvp.Key}; will retry later. {ex.Message}");
				}
			}
		}

		private static void EnsureRuntimeGateIndexesCurrent()
		{
			if (_runtimeGateIndexesDirty)
				RebuildRuntimeGateIndexes();
		}

		private static void AddConfiguredGuidToRuntimeIndexes(PrefabGUID configuredGuid, int minLevel)
		{
			if (!Helper.TryGetPrefabEntity(configuredGuid, out Entity configuredPrefab))
			{
				Plugin.Logger.LogWarning($"[{Plugin.Name}] Could not resolve configured prefab {configuredGuid.GuidHash} ({RecipePrefabLookup.Format(configuredGuid.GuidHash)}); it will only be preserved in config.");
				return;
			}

			if (configuredPrefab.Has<RecipeData>() || configuredPrefab.Has<RecipeOutputBuffer>())
			{
				AddRecipeOutputItemsToRuntimeIndexes(configuredPrefab, configuredGuid, minLevel);
				return;
			}

			if (!AddItemToRuntimeIndexes(configuredGuid, minLevel))
			{
				Plugin.Logger.LogWarning($"[{Plugin.Name}] Configured prefab {configuredGuid.LookupName()} has neither RecipeData/RecipeOutputBuffer nor ConsumableCondition/EquippableData; it will only be preserved in config.");
			}
		}

		private static void AddRecipeOutputItemsToRuntimeIndexes(Entity recipePrefab, PrefabGUID recipeGuid, int minLevel)
		{
			if (!recipePrefab.Has<RecipeOutputBuffer>())
				return;

			var outputBuffer = recipePrefab.ReadBuffer<RecipeOutputBuffer>();
			for (int index = 0; index < outputBuffer.Length; index++)
			{
				PrefabGUID outputItem = outputBuffer[index].Guid;
				if (outputItem == PrefabGUID.Empty)
					continue;

				AddItemToRuntimeIndexes(outputItem, minLevel);
			}
		}

		private static bool AddItemToRuntimeIndexes(PrefabGUID itemGuid, int minLevel)
		{
			if (!Helper.TryGetPrefabEntity(itemGuid, out Entity itemPrefab))
				return false;

			if (itemPrefab.Has<ConsumableCondition>())
			{
				AddMaxLevel(ConsumableMinLevelByGuid, itemGuid.GuidHash, minLevel);
				return true;
			}

			if (itemPrefab.Has<EquippableData>())
			{
				AddMaxLevel(EquippableMinLevelByGuid, itemGuid.GuidHash, minLevel);
				return true;
			}

			return false;
		}

		private static void AddMaxLevel(Dictionary<int, int> target, int guid, int minLevel)
		{
			if (!target.TryGetValue(guid, out int existing) || minLevel > existing)
				target[guid] = minLevel;
		}

		private static bool TryNormalizeJewelTier(int tier, out int normalizedTier)
		{
			normalizedTier = tier;
			return tier >= 1 && tier <= 4;
		}
	}
}
