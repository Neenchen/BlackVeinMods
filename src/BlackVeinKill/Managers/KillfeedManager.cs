using BlackVeinCore.Events;
using BlackVeinCore.Tools;
using BlackVeinCore.Utilities;
using BlackVeinKill.Services;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;

namespace BlackVeinKill.Managers;

public static class KillfeedManager
{
    private static readonly Dictionary<ulong, Player> RecentDownersByVictim = new();

    public static void Initialize()
    {
        GameEvents.OnPlayerReceivedDamage += HandlePlayerReceivedDamage;
        GameEvents.OnPlayerDowned += HandlePlayerDowned;
        GameEvents.OnPlayerDeath += HandlePlayerDeath;
    }

    public static void Dispose()
    {
        GameEvents.OnPlayerReceivedDamage -= HandlePlayerReceivedDamage;
        GameEvents.OnPlayerDowned -= HandlePlayerDowned;
        GameEvents.OnPlayerDeath -= HandlePlayerDeath;
        RecentDownersByVictim.Clear();
        PlayerHitStore.Clear();
    }

    public static void SimulateKill(KillfeedPlayerSnapshot killer, KillfeedPlayerSnapshot victim)
    {
        if (string.Equals(killer.Name, victim.Name, StringComparison.OrdinalIgnoreCase) ||
            (killer.SteamID != 0 && killer.SteamID == victim.SteamID))
        {
            throw new InvalidOperationException("Killer and victim must be different players.");
        }

        var isFfaKill =
            (killer.OnlinePlayer != null && FfaZoneManager.IsInFfaZone(killer.OnlinePlayer)) ||
            (victim.OnlinePlayer != null && FfaZoneManager.IsInFfaZone(victim.OnlinePlayer));
        var killAllowed = isFfaKill || IsKillAllowed(killer, victim);

        PvPStatsService.RecordKill(killer, victim, isFfaKill);

        var chatMessage = BuildChatMessage(killer, victim, killAllowed, isFfaKill);
        SendSystemMessageToAll(chatMessage);

        DiscordWebhookManager.SendKillMessage(BuildDiscordKillMessage(killer, victim, isFfaKill));
        DiscordWebhookManager.UpdateLeaderboard();
    }

    private static void HandlePlayerReceivedDamage(Entity source, Entity eventEntity, StatChangeEvent statChangeEvent, Player victim, float absorbed)
    {
        if (!PlayerService.TryFromCharacter(source, out var attacker) ||
            attacker.SteamID == 0 ||
            victim.SteamID == 0 ||
            attacker.SteamID == victim.SteamID)
        {
            return;
        }

        var useMaxLevel = KillfeedSettings.LevelTrackingMode == 1;
        var attackerLevel = PvPStatsService.GetTrackedLevel(attacker, useMaxLevel);
        var victimLevel = PvPStatsService.GetTrackedLevel(victim, useMaxLevel);
        var damage = (int)Math.Abs(Math.Round(statChangeEvent.Change == 0 ? statChangeEvent.OriginalChange : statChangeEvent.Change));
        if (damage <= 0)
        {
            return;
        }

        PlayerHitStore.AddHit(attacker.SteamID, attacker.Name, attackerLevel, victim.SteamID, victim.Name, victimLevel, damage);
        PvPStatsService.RegisterDamage(attacker, victim, damage);
    }

    private static void HandlePlayerDowned(Player victim, Entity downedBuff, Entity killerEntity, Player? pvpKiller)
    {
        var killer = pvpKiller;
        if (killer == null && killerEntity.Exists() && killerEntity.Has<PlayerCharacter>())
        {
            PlayerService.TryFromCharacter(killerEntity, out killer);
        }

        if (killer == null || killer.SteamID == 0 || victim.SteamID == 0 || killer.SteamID == victim.SteamID)
        {
            return;
        }

        RecentDownersByVictim[victim.SteamID] = killer;
    }

    private static void HandlePlayerDeath(Player victim, DeathEvent deathEvent, Player? pvpKiller)
    {
        var killer = pvpKiller;
        if (killer == null && RecentDownersByVictim.TryGetValue(victim.SteamID, out var downer))
        {
            killer = downer;
        }

        if (killer == null && deathEvent.Killer.Exists() && deathEvent.Killer.Has<PlayerCharacter>())
        {
            PlayerService.TryFromCharacter(deathEvent.Killer, out killer);
        }

        RecentDownersByVictim.Remove(victim.SteamID);

        if (killer == null ||
            killer.SteamID == 0 ||
            victim.SteamID == 0 ||
            killer.SteamID == victim.SteamID)
        {
            return;
        }

        PvPStatsService.RegisterClanIfMissing(killer);
        PvPStatsService.RegisterClanIfMissing(victim);
        PvPStatsService.UpdatePlayerLevel(killer);
        PvPStatsService.UpdatePlayerLevel(victim);

        var useMaxLevel = KillfeedSettings.LevelTrackingMode == 1;
        var killerLevel = PvPStatsService.GetTrackedLevel(killer, useMaxLevel);
        var victimLevel = PvPStatsService.GetTrackedLevel(victim, useMaxLevel);
        var isFfaKill = FfaZoneManager.IsFfaKill(killer, victim);
        var killAllowed = isFfaKill || IsKillAllowed(killer, victim, killerLevel, victimLevel);
        var assisters = PlayerHitStore.GetRecentAttackers(victim.SteamID)
            .Where(hit => hit.AttackerSteamId != killer.SteamID)
            .ToList();

        PvPStatsService.RecordKill(killer, victim, assisters, isFfaKill);

        var chatMessage = BuildChatMessage(killer, victim, killerLevel, victimLevel, killAllowed, isFfaKill, assisters);
        SendSystemMessageToAll(chatMessage);

        DiscordWebhookManager.SendKillMessage(BuildDiscordKillMessage(killer, victim, killerLevel, victimLevel, isFfaKill, assisters));
        DiscordWebhookManager.UpdateLeaderboard();
        PlayerHitStore.ClearVictim(victim.SteamID);
    }

    private static string BuildChatMessage(Player killer, Player victim, int killerLevel, int victimLevel, bool killAllowed, bool isFfaKill, List<HitInteraction> assisters)
    {
        var levelColor = killAllowed ? KillfeedSettings.AllowedLevelColor : KillfeedSettings.ForbiddenLevelColor;
        var killerDisplay = BuildPlayerDisplay(
            killer,
            killerLevel,
            KillfeedSettings.KillerNameColor,
            levelColor);
        var victimDisplay = BuildPlayerDisplay(
            victim,
            victimLevel,
            KillfeedSettings.VictimNameColor,
            levelColor);

        var message = $"{killerDisplay} killed {victimDisplay}";
        if (isFfaKill)
        {
            message = $"{ "[FFA]".Error() } {message}";
        }

        if (assisters.Count > 0)
        {
            var details = assisters.Select(hit => $"{hit.AttackerName} ({hit.AttackerLevel})");
            message += $"\n{$"[Assist(s): {string.Join(", ", details)}]".BaseColor()}";
        }

        return message;
    }

    private static string BuildChatMessage(KillfeedPlayerSnapshot killer, KillfeedPlayerSnapshot victim, bool killAllowed, bool isFfaKill)
    {
        var levelColor = killAllowed ? KillfeedSettings.AllowedLevelColor : KillfeedSettings.ForbiddenLevelColor;
        var killerDisplay = BuildPlayerDisplay(
            killer,
            KillfeedSettings.KillerNameColor,
            levelColor);
        var victimDisplay = BuildPlayerDisplay(
            victim,
            KillfeedSettings.VictimNameColor,
            levelColor);

        var message = $"{killerDisplay} killed {victimDisplay}";
        if (isFfaKill)
        {
            message = $"{ "[FFA]".Error() } {message}";
        }

        return message;
    }

    private static string BuildPlayerDisplay(Player player, int level, string nameColor, string levelColor)
    {
        var clan = FormatClanTag(player.ClanName);
        var name = player.Name.Colorify(nameColor);
        var displayLevel = level.ToString().Colorify(levelColor);
        return $"{clan}{name}[{displayLevel}]";
    }

    private static string BuildPlayerDisplay(KillfeedPlayerSnapshot player, string nameColor, string levelColor)
    {
        var clan = FormatClanTag(player.ClanName);
        var name = player.Name.Colorify(nameColor);
        var displayLevel = player.Level.ToString().Colorify(levelColor);
        return $"{clan}{name}[{displayLevel}]";
    }

    private static string BuildDiscordKillMessage(Player killer, Player victim, int killerLevel, int victimLevel, bool isFfaKill, List<HitInteraction> assisters)
    {
        var prefix = isFfaKill ? "[FFA] " : string.Empty;
        var killerClan = FormatClan(killer.ClanName);
        var victimClan = FormatClan(victim.ClanName);
        var message = $"{prefix}[PvP] {killerClan}**{DiscordWebhookManager.EscapeDiscordMarkdown(killer.Name)}** ({killerLevel}) killed {victimClan}**{DiscordWebhookManager.EscapeDiscordMarkdown(victim.Name)}** ({victimLevel})";

        if (assisters.Count > 0)
        {
            var details = assisters.Select(hit => $"*{DiscordWebhookManager.EscapeDiscordMarkdown(hit.AttackerName)}* ({hit.AttackerLevel})");
            message += $"\n-- Assisters: {string.Join(", ", details)}";
        }

        return message;
    }

    private static string BuildDiscordKillMessage(KillfeedPlayerSnapshot killer, KillfeedPlayerSnapshot victim, bool isFfaKill)
    {
        var prefix = isFfaKill ? "[FFA] " : string.Empty;
        var killerClan = FormatClan(killer.ClanName);
        var victimClan = FormatClan(victim.ClanName);
        return $"{prefix}[PvP] {killerClan}**{DiscordWebhookManager.EscapeDiscordMarkdown(killer.Name)}** ({killer.Level}) killed {victimClan}**{DiscordWebhookManager.EscapeDiscordMarkdown(victim.Name)}** ({victim.Level})";
    }

    private static void SendSystemMessageToAll(string message)
    {
        foreach (var chunk in SplitIntoChunks(message, 500))
        {
            var fixedString = new Unity.Collections.FixedString512Bytes(chunk);
            ServerChatUtils.SendSystemMessageToAllClients(BlackVeinCore.Core.EntityManager, ref fixedString);
        }
    }

    private static IEnumerable<string> SplitIntoChunks(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            yield return value;
            yield break;
        }

        for (var offset = 0; offset < value.Length; offset += maxLength)
        {
            yield return value.Substring(offset, Math.Min(maxLength, value.Length - offset));
        }
    }

    private static bool IsKillAllowed(Player killer, Player victim, int killerLevel, int victimLevel)
    {
        if (FfaZoneManager.TryGetLevelRuleMaxGap(killer, victim, out var zoneMaxLevelGap))
        {
            return IsKillAllowedByMaxGap(killerLevel, victimLevel, zoneMaxLevelGap);
        }

        return IsKillAllowedByGlobalRules(killerLevel, victimLevel);
    }

    private static bool IsKillAllowed(KillfeedPlayerSnapshot killer, KillfeedPlayerSnapshot victim)
    {
        if (killer.OnlinePlayer != null &&
            victim.OnlinePlayer != null &&
            FfaZoneManager.TryGetLevelRuleMaxGap(killer.OnlinePlayer, victim.OnlinePlayer, out var zoneMaxLevelGap))
        {
            return IsKillAllowedByMaxGap(killer.Level, victim.Level, zoneMaxLevelGap);
        }

        return IsKillAllowedByGlobalRules(killer.Level, victim.Level);
    }

    private static bool IsKillAllowedByGlobalRules(int killerLevel, int victimLevel)
    {
        var diff = killerLevel - victimLevel;
        return killerLevel >= 91
            ? diff <= KillfeedSettings.MaxLevelGapHigh
            : diff <= KillfeedSettings.MaxLevelGapNormal;
    }

    private static bool IsKillAllowedByMaxGap(int killerLevel, int victimLevel, int maxLevelGap)
    {
        return killerLevel - victimLevel <= maxLevelGap;
    }

    private static string FormatClanTag(string clanName)
    {
        var truncated = TruncateClan(clanName);
        return string.IsNullOrWhiteSpace(truncated)
            ? string.Empty
            : $"[{truncated}]".Colorify(KillfeedSettings.ClanTagColor);
    }

    private static string TruncateClan(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var trimmed = name.Trim().Trim('[', ']');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return trimmed.Length <= 3
            ? trimmed.ToUpperInvariant()
            : trimmed[..3].ToUpperInvariant();
    }

    private static string FormatClan(string clanName)
    {
        return string.IsNullOrWhiteSpace(clanName)
            ? string.Empty
            : $"{DiscordWebhookManager.EscapeDiscordMarkdown(clanName.Trim())} ";
    }
}
