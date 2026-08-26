using BlackVeinCore.Events;
using BlackVeinCore.Utilities;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;

namespace BlackVeinCore.Patches;

public static class ResearchEventPublisher
{
    private const long DuplicateEventWindowMilliseconds = 500;
    private const int CleanupThreshold = 64;
    private static readonly Dictionary<ResearchEventKey, long> RecentResearchEvents = [];

    public static void Publish(PrefabGUID researchGuid, FromCharacter fromCharacter, string failureContext)
    {
        try
        {
            if (researchGuid == PrefabGUID.Empty ||
                !PlayerService.TryFromUserAndCharacter(fromCharacter.User, fromCharacter.Character, out var player))
            {
                return;
            }

            var key = new ResearchEventKey(
                fromCharacter.User.Index,
                fromCharacter.Character.Index,
                researchGuid.GuidHash);
            if (IsDuplicate(key))
            {
                return;
            }

            GameEvents.OnPlayerUnlockedResearch?.Invoke(player, researchGuid);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[GameEvents] Failed to publish {failureContext}: {ex}");
        }
    }

    private static bool IsDuplicate(ResearchEventKey key)
    {
        var now = Environment.TickCount64;
        if (RecentResearchEvents.TryGetValue(key, out var lastPublishedAt) &&
            now - lastPublishedAt <= DuplicateEventWindowMilliseconds)
        {
            return true;
        }

        RecentResearchEvents[key] = now;
        if (RecentResearchEvents.Count > CleanupThreshold)
        {
            Cleanup(now);
        }

        return false;
    }

    private static void Cleanup(long now)
    {
        foreach (var entry in RecentResearchEvents.ToArray())
        {
            if (now - entry.Value > DuplicateEventWindowMilliseconds)
            {
                RecentResearchEvents.Remove(entry.Key);
            }
        }
    }

    private readonly record struct ResearchEventKey(
        int UserIndex,
        int CharacterIndex,
        int ResearchGuidHash);
}

[HarmonyPatch(typeof(UnlockResearchSystem), nameof(UnlockResearchSystem.HandleEvent))]
public static class UnlockResearchSystemPatch
{
    public static void Prefix(UnlockResearchEvent unlockResearchEvent, FromCharacter fromCharacter)
    {
        ResearchEventPublisher.Publish(
            unlockResearchEvent.ResearchGUID,
            fromCharacter,
            "research unlock event");
    }
}

[HarmonyPatch(typeof(DiscoverResearchSystem), nameof(DiscoverResearchSystem.UnlockProgression))]
public static class DiscoverResearchSystemPatch
{
    public static void Prefix(ResearchBuffer randomResearch, FromCharacter fromCharacter)
    {
        ResearchEventPublisher.Publish(
            randomResearch.ResearchGuid,
            fromCharacter,
            "discovered research event");
    }
}
