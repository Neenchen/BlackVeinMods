using System;
using System.Linq;
using LevelRecipeGate.Config;

#if !NoVCF
using VampireCommandFramework;
#endif

namespace LevelRecipeGate.Commands
{
#if !NoVCF
	[CommandGroup("levelrecipegate", null)]
	public static class RecipeGateCommands
	{
		[Command("reload", null, null, "Reload level_recipe_blocks.json", null, true)]
		public static void Reload(ICommandContext ctx)
		{
			ConfigStore.LoadLevelRecipeBlocksFromDisk();
			ctx.Reply($"[LevelRecipeGate] Reloaded {ConfigStore.GetAllLevelGatesSnapshot().Count} item gate(s) and {ConfigStore.GetJewelTierGatesSnapshot().Count} jewel tier gate(s). SecondaryBloodPotionMinLevel={ConfigStore.SecondaryBloodPotionMinLevel}. Enabled={ConfigStore.LevelRecipeBlocksEnabled}.");
		}

		[Command("list", null, null, "List level-gated recipe/item GUIDs and jewel tiers", null, true)]
		public static void List(ICommandContext ctx)
		{
			var recipeGates = ConfigStore.GetAllLevelGatesSnapshot();
			var jewelTierGates = ConfigStore.GetJewelTierGatesSnapshot();

			if (recipeGates.Count == 0 && jewelTierGates.Count == 0)
			{
				ctx.Reply("[LevelRecipeGate] No level gates configured.");
				return;
			}

			foreach (var group in recipeGates.GroupBy(kvp => kvp.Value).OrderBy(g => g.Key))
			{
				string[] values = group.Select(kvp => ConfigStore.FormatRecipePrefab(kvp.Key)).OrderBy(x => x).ToArray();
				ctx.Reply($"[LevelRecipeGate] Level {group.Key}: {values.Length} gate(s).");

				for (int i = 0; i < values.Length; i += 10)
					ctx.Reply(string.Join(", ", values.Skip(i).Take(10)));
			}

			foreach (var gate in jewelTierGates.OrderBy(kvp => kvp.Key))
				ctx.Reply($"[LevelRecipeGate] Jewel tier {gate.Key}: requires current gear level {gate.Value}.");

			ctx.Reply($"[LevelRecipeGate] Mixed blood potions require max recorded gear level {ConfigStore.SecondaryBloodPotionMinLevel}.");
		}

		[Command("add", null, null, "Add or update a level gate: .levelrecipegate add <level> <recipeOrItemPrefab>", null, true)]
		public static void Add(ICommandContext ctx, int minLevel, string recipePrefab)
		{
			if (minLevel < 0 || minLevel > 255)
			{
				ctx.Reply("[LevelRecipeGate] Invalid level. Use 0-255.");
				return;
			}

			if (!ConfigStore.TryResolveRecipePrefab(recipePrefab, out int recipeGuid))
			{
				ctx.Reply($"[LevelRecipeGate] Unknown recipe or item prefab: {recipePrefab}");
				return;
			}

			ConfigStore.SetRecipeLevelGate(recipeGuid, minLevel);
			ctx.Reply($"[LevelRecipeGate] {ConfigStore.FormatRecipePrefab(recipeGuid)} now requires gear level {minLevel}.");
		}

		[Command("remove", null, null, "Remove a level gate: .levelrecipegate remove <recipeOrItemPrefab>", null, true)]
		public static void Remove(ICommandContext ctx, string recipePrefab)
		{
			if (!ConfigStore.TryResolveRecipePrefab(recipePrefab, out int recipeGuid))
			{
				ctx.Reply($"[LevelRecipeGate] Unknown recipe or item prefab: {recipePrefab}");
				return;
			}

			if (ConfigStore.RemoveRecipeLevelGate(recipeGuid))
				ctx.Reply($"[LevelRecipeGate] Removed level gate for {ConfigStore.FormatRecipePrefab(recipeGuid)}.");
			else
				ctx.Reply($"[LevelRecipeGate] {ConfigStore.FormatRecipePrefab(recipeGuid)} was not level-gated.");
		}

		[Command("addjeweltier", null, null, "Add or update a jewel tier gate: .levelrecipegate addjeweltier <tier> <level>", null, true)]
		public static void AddJewelTier(ICommandContext ctx, int tier, int minLevel)
		{
			if (minLevel < 0 || minLevel > 255)
			{
				ctx.Reply("[LevelRecipeGate] Invalid level. Use 0-255.");
				return;
			}

			if (!ConfigStore.TrySetJewelTierLevelGate(tier, minLevel))
			{
				ctx.Reply("[LevelRecipeGate] Invalid jewel tier. Use 1-4.");
				return;
			}

			ctx.Reply($"[LevelRecipeGate] Jewel tier {tier} now requires current gear level {minLevel}.");
		}

		[Command("removejeweltier", null, null, "Remove a jewel tier gate: .levelrecipegate removejeweltier <tier>", null, true)]
		public static void RemoveJewelTier(ICommandContext ctx, int tier)
		{
			if (ConfigStore.RemoveJewelTierLevelGate(tier))
				ctx.Reply($"[LevelRecipeGate] Removed level gate for jewel tier {tier}.");
			else
				ctx.Reply($"[LevelRecipeGate] Jewel tier {tier} was not level-gated.");
		}

		[Command("secondarybloodlevel", null, null, "Set the mixed/secondary blood potion level gate: .levelrecipegate secondarybloodlevel <level>", null, true)]
		public static void SecondaryBloodLevel(ICommandContext ctx, int minLevel)
		{
			if (!ConfigStore.TrySetSecondaryBloodPotionMinLevel(minLevel))
			{
				ctx.Reply("[LevelRecipeGate] Invalid level. Use 0-255.");
				return;
			}

			ctx.Reply($"[LevelRecipeGate] Mixed blood potions now require max recorded gear level {minLevel}.");
		}
	}
#else
	internal static class RecipeGateCommands { }
#endif
}
