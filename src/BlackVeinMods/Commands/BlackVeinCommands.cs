using BlackVeinMods.Configs;
using VampireCommandFramework;

namespace BlackVeinMods.Commands;

[CommandGroup("blackvein", "bv")]
public static class BlackVeinCommands
{
    [Command("status", description: "Shows BlackVeinMods status.", adminOnly: true)]
    public static void Status(ICommandContext ctx)
    {
        var config = BlackVeinConfig.Config.ClanProgression;
        ctx.Reply(
            $"Clan progression: enabled={config.Enabled}, " +
            $"shareBoss={config.ShareBossUnlocks}, " +
            $"shareBossTechWhenBossSharingOff={config.ShareTechnologyUnlocks}, " +
            $"shareJournal={config.ShareJournalProgression}, " +
            $"shareResearch={config.ShareResearchProgression}, " +
            $"includeOffline={config.IncludeOfflineClanMembers}.");
    }
}
