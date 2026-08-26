using BlackVeinCore.Utilities;
using BlackVeinKill.Database;

namespace BlackVeinKill.Services;

public sealed record KillfeedPlayerSnapshot(
    string Name,
    ulong SteamID,
    int Level,
    string ClanName,
    Player? OnlinePlayer)
{
    public static KillfeedPlayerSnapshot FromPlayer(Player player, int level)
    {
        return new KillfeedPlayerSnapshot(
            player.Name,
            player.SteamID,
            level,
            player.ClanName,
            player);
    }

    public static KillfeedPlayerSnapshot FromStats(PlayerStats stats, string fallbackName)
    {
        var name = string.IsNullOrWhiteSpace(stats.Name) ? fallbackName : stats.Name;
        return new KillfeedPlayerSnapshot(
            name,
            stats.SteamID,
            stats.MaxGearScore ?? 0,
            string.Empty,
            null);
    }
}
