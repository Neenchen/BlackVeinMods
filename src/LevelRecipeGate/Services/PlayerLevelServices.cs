using System;
using BlackVeinCore;
using BlackVeinCore.Persistence;
using BlackVeinCore.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;

namespace LevelRecipeGate.Services
{
	internal static class PlayerLevelService
	{
		private static string ConfigDir => BlackVeinConfigPaths.ConfigDirectory;
		private static string FilePath => Path.Combine(ConfigDir, "player_highest_levels.json");

		private static readonly Dictionary<ulong, PlayerLevelRecord> Records = new();

		private sealed class PlayerLevelRecord
		{
			public string name { get; set; } = "";
			public ulong steam_id { get; set; }
			public int highest_gear_level { get; set; }
		}

		internal static void Load()
		{
			Directory.CreateDirectory(ConfigDir);

			if (!File.Exists(FilePath))
			{
				FileWriteCoordinator.WriteAllText(FilePath, "{}", message => Plugin.Logger.LogWarning(message));
				return;
			}

			string json = FileWriteCoordinator.ReadAllTextWithRetry(FilePath, message => Plugin.Logger.LogWarning(message));
			var loaded = JsonSerializer.Deserialize<Dictionary<ulong, PlayerLevelRecord>>(json);

			Records.Clear();

			if (loaded == null)
				return;

			foreach (var pair in loaded)
				Records[pair.Key] = pair.Value;
		}

		internal static void Save()
		{
			Directory.CreateDirectory(ConfigDir);
			FileWriteCoordinator.WriteAllText(
				FilePath,
				JsonSerializer.Serialize(Records, new JsonSerializerOptions { WriteIndented = true }),
				message => Plugin.Logger.LogWarning(message));
		}

		internal static int UpdateAndGetHighestGearLevel(EntityManager em, User user, Entity characterEntity)
		{
			int current = GetCurrentGearScore(em, characterEntity);
			if (current < 0)
				return GetRecordedHighestGearLevel(user);

			ulong steamId = user.PlatformId;
			string name = user.CharacterName.ToString();

			if (!Records.TryGetValue(steamId, out PlayerLevelRecord record))
			{
				record = new PlayerLevelRecord
				{
					name = name,
					steam_id = steamId,
					highest_gear_level = current
				};

				Records[steamId] = record;
				Save();

				return record.highest_gear_level;
			}

			record.name = name;

			if (current > record.highest_gear_level)
			{
				record.highest_gear_level = current;
				Save();
			}

			return record.highest_gear_level;
		}

		internal static int GetCurrentGearLevel(EntityManager em, Entity characterEntity)
		{
			return GetCurrentGearScore(em, characterEntity);
		}

		private static int GetRecordedHighestGearLevel(User user)
		{
			return Records.TryGetValue(user.PlatformId, out PlayerLevelRecord record)
				? record.highest_gear_level
				: -1;
		}

		private static int GetCurrentGearScore(EntityManager em, Entity characterEntity)
		{
			try
			{
				if (characterEntity == Entity.Null || !em.Exists(characterEntity))
					return -1;

				if (!em.HasComponent<Equipment>(characterEntity))
					return -1;

				Equipment equipment = em.GetComponentData<Equipment>(characterEntity);

				float gearScore = equipment.ArmorLevel + equipment.SpellLevel + equipment.WeaponLevel;
				return (int)Math.Round(gearScore, MidpointRounding.AwayFromZero);
			}
			catch
			{
				return -1;
			}
		}
	}
}
