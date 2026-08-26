using BlackVeinCore.Utilities;
using BlackVeinKill.Database;

namespace BlackVeinKill.Services;

public static class PvPStatsService
{
    private static readonly object SyncRoot = new();

    public static void Initialize()
    {
        Plugin.Logger.LogInfo("[BlackVeinKill] PvP stats service initialized.");
    }

    public static void Dispose()
    {
    }

    public static Dictionary<string, PlayerStats> GetAllStats()
    {
        lock (SyncRoot)
        {
            return DatabaseWrapper.Instance.Collection.FindAll().ToDictionary(stats => stats.Name, stats => stats);
        }
    }

    public static PlayerStats GetStats(string name)
    {
        lock (SyncRoot)
        {
            return DatabaseWrapper.Instance.Collection.FindById(name) ?? new PlayerStats { Name = name };
        }
    }

    public static PlayerStats GetStatsByName(string name)
    {
        lock (SyncRoot)
        {
            return DatabaseWrapper.Instance.Collection.FindById(name) ??
                DatabaseWrapper.Instance.Collection.FindAll().FirstOrDefault(stats => string.Equals(stats.Name, name, StringComparison.OrdinalIgnoreCase)) ??
                new PlayerStats { Name = name };
        }
    }

    public static PlayerStats GetStatsBySteamId(ulong steamId)
    {
        lock (SyncRoot)
        {
            return DatabaseWrapper.Instance.Collection.FindOne(stats => stats.SteamID == steamId)
                ?? new PlayerStats { SteamID = steamId };
        }
    }

    public static PlayerStats GetOrCreate(Player player)
    {
        lock (SyncRoot)
        {
            var stats = DatabaseWrapper.Instance.Collection.FindById(player.Name);
            if (stats != null)
            {
                if (stats.SteamID == 0)
                {
                    stats.SteamID = player.SteamID;
                    DatabaseWrapper.Instance.Collection.Update(stats);
                }

                return stats;
            }

            stats = new PlayerStats
            {
                Name = player.Name,
                SteamID = player.SteamID,
                MaxGearScore = player.Level
            };
            DatabaseWrapper.Instance.Collection.Insert(stats);
            return stats;
        }
    }

    public static void UpdatePlayerLevel(Player player)
    {
        if (player.SteamID == 0)
        {
            return;
        }

        lock (SyncRoot)
        {
            var stats = DatabaseWrapper.Instance.Collection.FindById(player.Name)
                ?? new PlayerStats { Name = player.Name, SteamID = player.SteamID };

            stats.SteamID = player.SteamID;
            if (player.Level > (stats.MaxGearScore ?? 0))
            {
                stats.MaxGearScore = player.Level;
            }

            DatabaseWrapper.Instance.Collection.Upsert(stats);
        }
    }

    public static int GetTrackedLevel(Player player, bool useMaxLevel)
    {
        if (!useMaxLevel)
        {
            return player.Level;
        }

        UpdatePlayerLevel(player);
        return Math.Max(player.Level, GetStats(player.Name).MaxGearScore ?? 0);
    }

    public static void RegisterDamage(Player attacker, Player victim, int amount)
    {
        if (amount <= 0 || attacker.SteamID == 0 || victim.SteamID == 0)
        {
            return;
        }

        lock (SyncRoot)
        {
            var attackerStats = DatabaseWrapper.Instance.Collection.FindById(attacker.Name)
                ?? new PlayerStats { Name = attacker.Name, SteamID = attacker.SteamID };
            attackerStats.SteamID = attacker.SteamID;
            attackerStats.Damage = (attackerStats.Damage ?? 0) + amount;
            if (attacker.Level > (attackerStats.MaxGearScore ?? 0))
            {
                attackerStats.MaxGearScore = attacker.Level;
            }
            DatabaseWrapper.Instance.Collection.Upsert(attackerStats);

            var victimStats = DatabaseWrapper.Instance.Collection.FindById(victim.Name)
                ?? new PlayerStats { Name = victim.Name, SteamID = victim.SteamID };
            victimStats.SteamID = victim.SteamID;
            victimStats.DamageTaken = (victimStats.DamageTaken ?? 0) + amount;
            if (victim.Level > (victimStats.MaxGearScore ?? 0))
            {
                victimStats.MaxGearScore = victim.Level;
            }
            DatabaseWrapper.Instance.Collection.Upsert(victimStats);
        }
    }

    public static void RecordKill(Player killer, Player victim, IEnumerable<HitInteraction> assisters, bool isFfaKill)
    {
        lock (SyncRoot)
        {
            var collection = DatabaseWrapper.Instance.Collection;
            var killerStats = collection.FindById(killer.Name)
                ?? new PlayerStats { Name = killer.Name, SteamID = killer.SteamID };
            killerStats.SteamID = killer.SteamID;
            killerStats.Kills = (killerStats.Kills ?? 0) + 1;
            killerStats.KillStreak = (killerStats.KillStreak ?? 0) + 1;
            killerStats.MaxStreak = Math.Max(killerStats.MaxStreak ?? 0, killerStats.KillStreak ?? 0);
            killerStats.MaxGearScore = Math.Max(killerStats.MaxGearScore ?? 0, killer.Level);
            if (isFfaKill)
            {
                killerStats.FfaKills = (killerStats.FfaKills ?? 0) + 1;
            }
            collection.Upsert(killerStats);

            var victimStats = collection.FindById(victim.Name)
                ?? new PlayerStats { Name = victim.Name, SteamID = victim.SteamID };
            victimStats.SteamID = victim.SteamID;
            victimStats.Deaths = (victimStats.Deaths ?? 0) + 1;
            victimStats.KillStreak = 0;
            victimStats.MaxGearScore = Math.Max(victimStats.MaxGearScore ?? 0, victim.Level);
            if (isFfaKill)
            {
                victimStats.FfaDeaths = (victimStats.FfaDeaths ?? 0) + 1;
            }
            collection.Upsert(victimStats);

            foreach (var assister in assisters.Where(hit => hit.AttackerSteamId != killer.SteamID).GroupBy(hit => hit.AttackerSteamId).Select(group => group.First()))
            {
                var assistStats = collection.FindOne(stats => stats.SteamID == assister.AttackerSteamId)
                    ?? new PlayerStats { Name = assister.AttackerName, SteamID = assister.AttackerSteamId };
                assistStats.Name = string.IsNullOrWhiteSpace(assistStats.Name) ? assister.AttackerName : assistStats.Name;
                assistStats.Assists = (assistStats.Assists ?? 0) + 1;
                assistStats.MaxGearScore = Math.Max(assistStats.MaxGearScore ?? 0, assister.AttackerLevel);
                collection.Upsert(assistStats);
            }
        }
    }

    public static void RecordKill(KillfeedPlayerSnapshot killer, KillfeedPlayerSnapshot victim, bool isFfaKill)
    {
        lock (SyncRoot)
        {
            var collection = DatabaseWrapper.Instance.Collection;
            var killerStats = collection.FindById(killer.Name)
                ?? collection.FindAll().FirstOrDefault(stats => string.Equals(stats.Name, killer.Name, StringComparison.OrdinalIgnoreCase))
                ?? new PlayerStats { Name = killer.Name, SteamID = killer.SteamID };
            killerStats.Name = string.IsNullOrWhiteSpace(killerStats.Name) ? killer.Name : killerStats.Name;
            if (killer.SteamID != 0)
            {
                killerStats.SteamID = killer.SteamID;
            }
            killerStats.Kills = (killerStats.Kills ?? 0) + 1;
            killerStats.KillStreak = (killerStats.KillStreak ?? 0) + 1;
            killerStats.MaxStreak = Math.Max(killerStats.MaxStreak ?? 0, killerStats.KillStreak ?? 0);
            killerStats.MaxGearScore = Math.Max(killerStats.MaxGearScore ?? 0, killer.Level);
            if (isFfaKill)
            {
                killerStats.FfaKills = (killerStats.FfaKills ?? 0) + 1;
            }
            collection.Upsert(killerStats);

            var victimStats = collection.FindById(victim.Name)
                ?? collection.FindAll().FirstOrDefault(stats => string.Equals(stats.Name, victim.Name, StringComparison.OrdinalIgnoreCase))
                ?? new PlayerStats { Name = victim.Name, SteamID = victim.SteamID };
            victimStats.Name = string.IsNullOrWhiteSpace(victimStats.Name) ? victim.Name : victimStats.Name;
            if (victim.SteamID != 0)
            {
                victimStats.SteamID = victim.SteamID;
            }
            victimStats.Deaths = (victimStats.Deaths ?? 0) + 1;
            victimStats.KillStreak = 0;
            victimStats.MaxGearScore = Math.Max(victimStats.MaxGearScore ?? 0, victim.Level);
            if (isFfaKill)
            {
                victimStats.FfaDeaths = (victimStats.FfaDeaths ?? 0) + 1;
            }
            collection.Upsert(victimStats);
        }
    }

    public static void RegisterClanIfMissing(Player player)
    {
        if (player.SteamID == 0 || string.IsNullOrWhiteSpace(player.ClanName))
        {
            return;
        }

        lock (SyncRoot)
        {
            var recordId = $"{player.SteamID}:{player.ClanName}";
            if (DatabaseWrapper.Instance.ClanMembersCollection.FindById(recordId) != null)
            {
                return;
            }

            DatabaseWrapper.Instance.ClanMembersCollection.Upsert(new ClanMemberRecord
            {
                Id = recordId,
                SteamID = player.SteamID,
                ClanName = player.ClanName,
                JoinedAt = DateTime.UtcNow
            });
        }
    }
}
