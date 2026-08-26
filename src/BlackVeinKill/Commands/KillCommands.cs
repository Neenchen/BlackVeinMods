using System.Text;
using BlackVeinCore.Utilities;
using BlackVeinCore.Tools;
using BlackVeinKill.Managers;
using BlackVeinKill.Services;
using VampireCommandFramework;

namespace BlackVeinKill.Commands;

internal static class KillCommands
{
    private const int TopPlayerCount = 10;

    [Command("top", usage: "[category] [subcategory]", description: "Displays leaderboards for players or clans.", adminOnly: false)]
    public static void Top(ChatCommandContext ctx, string category = "", string metric = "")
    {
        category = (category ?? string.Empty).ToLowerInvariant();
        metric = (metric ?? string.Empty).ToLowerInvariant();

        if (category == "clan")
        {
            HandleClanTop(ctx, metric);
            return;
        }

        var allStats = PvPStatsService.GetAllStats();
        if (allStats.Count == 0)
        {
            ctx.Reply("No player stats found.".White());
            return;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            ctx.Reply(BuildTopUsage());
            return;
        }

        if (category == "ms")
        {
            category = "maxstreak";
        }

        var validCategories = new[] { "damage", "kills", "deaths", "assists", "maxstreak" };
        if (!validCategories.Contains(category))
        {
            ctx.Reply(BuildTopUsage());
            return;
        }

        if (IsRestricted(category) && !ctx.Event.User.IsAdmin)
        {
            ctx.Reply($"Access to the '{category}' leaderboard is restricted to admins only.".Error());
            return;
        }

        var topPlayers = category switch
        {
            "damage" => allStats.Values.Where(stats => (stats.Damage ?? 0) > 0).OrderByDescending(stats => stats.Damage ?? 0),
            "kills" => allStats.Values.Where(stats => (stats.Kills ?? 0) > 0).OrderByDescending(stats => stats.Kills ?? 0),
            "deaths" => allStats.Values.Where(stats => (stats.Deaths ?? 0) > 0).OrderByDescending(stats => stats.Deaths ?? 0),
            "assists" => allStats.Values.Where(stats => (stats.Assists ?? 0) > 0).OrderByDescending(stats => stats.Assists ?? 0),
            "maxstreak" => allStats.Values.Where(stats => (stats.MaxStreak ?? 0) > 0).OrderByDescending(stats => stats.MaxStreak ?? 0),
            _ => Enumerable.Empty<Database.PlayerStats>()
        };

        var leaders = topPlayers.Take(TopPlayerCount).ToList();
        if (leaders.Count == 0)
        {
            ctx.Reply($"No {category} to display.".White());
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Top {TopPlayerCount} players by {category}:".Warning());
        for (var index = 0; index < leaders.Count; index++)
        {
            var stats = leaders[index];
            var value = category switch
            {
                "damage" => stats.Damage ?? 0,
                "kills" => stats.Kills ?? 0,
                "deaths" => stats.Deaths ?? 0,
                "assists" => stats.Assists ?? 0,
                "maxstreak" => stats.MaxStreak ?? 0,
                _ => 0
            };
            sb.AppendLine($"{index + 1}. {stats.Name.BaseColor()} - {FormatTopValue(category, value)}");
        }

        ctx.Reply(sb.ToString());
    }

    [Command("stats", usage: "[player]", description: "Shows PvP stats.", adminOnly: false)]
    public static void Stats(ChatCommandContext ctx, string playerName = "")
    {
        var name = string.IsNullOrWhiteSpace(playerName)
            ? ctx.Event.User.CharacterName.ToString()
            : playerName;
        var stats = PvPStatsService.GetStats(name);

        if (string.IsNullOrWhiteSpace(stats.Name) || stats.Name == name && stats.SteamID == 0 && (stats.Kills ?? 0) == 0 && (stats.Deaths ?? 0) == 0)
        {
            ctx.Reply($"No stats found for {name}.".White());
            return;
        }

        ctx.Reply(
            $"{stats.Name.Warning()} | " +
            $"Kills: {(stats.Kills ?? 0).ToString().Success()} | " +
            $"Deaths: {(stats.Deaths ?? 0).ToString().Error()} | " +
            $"Assists: {(stats.Assists ?? 0).ToString().Colorify("#55ddff")} | " +
            $"Damage: {(stats.Damage ?? 0).ToString().Colorify("#ffaa00")} | " +
            $"Streak: {(stats.KillStreak ?? 0).ToString().BaseColor()} | " +
            $"Max: {(stats.MaxStreak ?? 0).ToString().BaseColor()} | " +
            $"FFA: {(stats.FfaKills ?? 0).ToString().Colorify("#ff7777")}");
    }

    [Command("kd", description: "Shows your kill/death ratio.", adminOnly: false)]
    public static void Kd(ChatCommandContext ctx)
    {
        var stats = PvPStatsService.GetStats(ctx.Event.User.CharacterName.ToString());
        var kills = stats.Kills ?? 0;
        var deaths = stats.Deaths ?? 0;
        var ratio = deaths == 0 ? kills : Math.Round((double)kills / deaths, 2);
        ctx.Reply($"K/D: {$"{ratio:0.##}".Success()} ({kills}/{deaths})");
    }

    [Command("kda", description: "Shows your kill/death/assist ratio.", adminOnly: false)]
    public static void Kda(ChatCommandContext ctx)
    {
        var stats = PvPStatsService.GetStats(ctx.Event.User.CharacterName.ToString());
        var kills = stats.Kills ?? 0;
        var deaths = stats.Deaths ?? 0;
        var assists = stats.Assists ?? 0;
        var ratio = deaths == 0 ? kills + assists * 0.5 : Math.Round((kills + assists * 0.5) / deaths, 2);
        ctx.Reply($"KDA: {$"{ratio:0.##}".Success()} ({kills}/{deaths}/{assists})");
    }

    [Command("posttop", description: "Posts the top PvP stats to the configured stats webhook.", adminOnly: true)]
    public static async void PostTop(ChatCommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(KillfeedSettings.StatsWebhookURL))
        {
            ctx.Reply("StatsWebhookURL is not configured.".Error());
            return;
        }

        await DiscordWebhookManager.PostStatsSnapshotAsync(KillfeedSettings.StatsWebhookURL);
        ctx.Reply("Stats posted to Discord.".Success());
    }

    [Command("resetstats", description: "Resets all BlackVeinKill PvP stats.", adminOnly: true)]
    public static void ResetStats(ChatCommandContext ctx)
    {
        var collection = Database.DatabaseWrapper.Instance.Collection;
        collection.DeleteAll();
        ctx.Reply("BlackVeinKill stats reset.".Success());
        DiscordWebhookManager.UpdateLeaderboard();
    }

    [Command("simulatekill", usage: "<killer> [victim]", description: "Simulates a PvP kill for testing.", adminOnly: true)]
    public static void SimulateKill(ChatCommandContext ctx, string target, string target2 = "")
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            ctx.Reply($"{ "Usage:".Error() } .simulatekill <killer> [victim]");
            return;
        }

        var senderName = ctx.Event.User.CharacterName.ToString();
        var killerName = string.IsNullOrWhiteSpace(target2) ? senderName : target;
        var victimName = string.IsNullOrWhiteSpace(target2) ? target : target2;

        var killer = ResolveKillfeedPlayer(killerName);
        var victim = ResolveKillfeedPlayer(victimName);

        try
        {
            KillfeedManager.SimulateKill(killer, victim);
            ctx.Reply($"Simulated kill: {killer.Name.Warning()} killed {victim.Name.Warning()}.".Success());
        }
        catch (Exception ex)
        {
            ctx.Reply(ex.Message.Error());
        }
    }

    private static void HandleClanTop(ChatCommandContext ctx, string metric)
    {
        if (string.IsNullOrWhiteSpace(metric))
        {
            metric = "kills";
        }

        if (metric != "kills" && metric != "damage")
        {
            var usageBuilder = new StringBuilder();
            usageBuilder.AppendLine($"{ "Usage:".Warning() } {".top clan [category]".Warning()}");
            usageBuilder.AppendLine("Categories:".Warning());
            usageBuilder.AppendLine($" kills".Success());
            usageBuilder.AppendLine($" damage".White());
            ctx.Reply(usageBuilder.ToString());
            return;
        }

        var days = KillfeedSettings.ClanTrackingDays;
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var members = Database.DatabaseWrapper.Instance.ClanMembersCollection
            .Find(member => member.JoinedAt <= cutoff)
            .ToList();

        if (members.Count == 0)
        {
            ctx.Reply($"No clans with members older than {days} day{(days == 1 ? string.Empty : "s")}.".White());
            return;
        }

        var clanStats = members
            .Where(member => !string.IsNullOrWhiteSpace(member.ClanName))
            .GroupBy(member => member.ClanName)
            .Select(group =>
            {
                var memberStats = group
                    .Select(member => PvPStatsService.GetStatsBySteamId(member.SteamID))
                    .ToList();

                return new
                {
                    Clan = group.Key,
                    Kills = memberStats.Sum(stats => stats.Kills ?? 0),
                    Damage = memberStats.Sum(stats => stats.Damage ?? 0)
                };
            })
            .ToList();

        var topClans = (metric == "damage"
                ? clanStats.OrderByDescending(clan => clan.Damage)
                : clanStats.OrderByDescending(clan => clan.Kills))
            .Take(3)
            .ToList();

        if (topClans.Count == 0)
        {
            ctx.Reply("No clan stats to display.".White());
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(metric == "damage"
            ? $"Top Clans by {"Damage".White()}".Warning()
            : $"Top Clans by {"Kills".Success()}".Warning());

        for (var index = 0; index < topClans.Count; index++)
        {
            var clan = topClans[index];
            var value = metric == "damage" ? clan.Damage : clan.Kills;
            sb.AppendLine($"{index + 1}. {clan.Clan.Warning()} - {FormatTopValue(metric, value)}");
        }

        ctx.Reply(sb.ToString());
    }

    private static bool IsRestricted(string category)
    {
        return (category == "damage" && KillfeedSettings.RestrictDamageToAdmin) ||
            (category == "kills" && KillfeedSettings.RestrictKillsToAdmin) ||
            (category == "deaths" && KillfeedSettings.RestrictDeathsToAdmin) ||
            (category == "assists" && KillfeedSettings.RestrictAssistsToAdmin) ||
            (category == "maxstreak" && KillfeedSettings.RestrictMaxStreakToAdmin);
    }

    private static string BuildTopUsage()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{ "Missing or invalid category.".Error() } Usage: {".top [category]".Warning()}");
        sb.AppendLine("Categories:");
        sb.AppendLine($" {"clan".Warning()}");
        sb.AppendLine($" {"damage".Warning()}{RestrictedLabel("damage")}");
        sb.AppendLine($" {"kills".Warning()}{RestrictedLabel("kills")}");
        sb.AppendLine($" {"deaths".Warning()}{RestrictedLabel("deaths")}");
        sb.AppendLine($" {"assists".Warning()}{RestrictedLabel("assists")}");
        sb.AppendLine($" {"maxstreak".Warning()}{RestrictedLabel("maxstreak")}");
        return sb.ToString();
    }

    private static string RestrictedLabel(string category)
    {
        return IsRestricted(category) ? " (Admin Only)" : string.Empty;
    }

    private static string FormatTopValue(string category, int value)
    {
        return category switch
        {
            "damage" => value.ToString().White(),
            "kills" => value.ToString().Success(),
            "deaths" => value.ToString().Error(),
            "assists" => value.ToString().Colorify("#aaaaaa"),
            "maxstreak" => value.ToString().Colorify("#55aaff"),
            _ => value.ToString().BaseColor()
        };
    }

    private static KillfeedPlayerSnapshot ResolveKillfeedPlayer(string name)
    {
        name = (name ?? string.Empty).Trim();
        if (PlayerService.TryGetOnlinePlayerByName(name, out var player))
        {
            var useMaxLevel = KillfeedSettings.LevelTrackingMode == 1;
            return KillfeedPlayerSnapshot.FromPlayer(player, PvPStatsService.GetTrackedLevel(player, useMaxLevel));
        }

        var stats = PvPStatsService.GetStatsByName(name);
        return KillfeedPlayerSnapshot.FromStats(stats, name);
    }
}
