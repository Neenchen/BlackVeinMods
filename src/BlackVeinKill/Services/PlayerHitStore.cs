namespace BlackVeinKill.Services;

public readonly record struct HitInteraction(
    ulong AttackerSteamId,
    ulong VictimSteamId,
    string AttackerName,
    int AttackerLevel,
    string VictimName,
    int VictimLevel,
    long Timestamp,
    int DamageAmount);

public static class PlayerHitStore
{
    private const double PvpWindowSeconds = 30.0;
    private const int MaxHitsPerPlayer = 500;
    private static readonly Dictionary<ulong, List<HitInteraction>> DefensesByVictim = new();

    public static void AddHit(ulong attackerSteamId, string attackerName, int attackerLevel, ulong victimSteamId, string victimName, int victimLevel, int damageAmount)
    {
        if (!DefensesByVictim.TryGetValue(victimSteamId, out var hits))
        {
            hits = new List<HitInteraction>();
            DefensesByVictim[victimSteamId] = hits;
        }

        hits.Add(new HitInteraction(
            attackerSteamId,
            victimSteamId,
            attackerName,
            attackerLevel,
            victimName,
            victimLevel,
            DateTime.UtcNow.Ticks,
            damageAmount));

        if (hits.Count > MaxHitsPerPlayer)
        {
            Cleanup(victimSteamId);
        }
    }

    public static List<HitInteraction> GetRecentAttackers(ulong victimSteamId, double pvpWindowSeconds = PvpWindowSeconds)
    {
        if (!DefensesByVictim.TryGetValue(victimSteamId, out var hits))
        {
            return new List<HitInteraction>();
        }

        var earliest = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(pvpWindowSeconds)).Ticks;
        return hits
            .Where(hit => hit.Timestamp >= earliest && hit.AttackerSteamId != victimSteamId)
            .GroupBy(hit => hit.AttackerSteamId)
            .Select(group => group.OrderByDescending(hit => hit.AttackerLevel).First())
            .ToList();
    }

    public static void Cleanup(ulong victimSteamId)
    {
        if (!DefensesByVictim.TryGetValue(victimSteamId, out var hits))
        {
            return;
        }

        var earliest = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(PvpWindowSeconds)).Ticks;
        hits.RemoveAll(hit => hit.Timestamp < earliest);
        if (hits.Count == 0)
        {
            DefensesByVictim.Remove(victimSteamId);
        }
    }

    public static void ClearVictim(ulong victimSteamId)
    {
        DefensesByVictim.Remove(victimSteamId);
    }

    public static void Clear()
    {
        DefensesByVictim.Clear();
    }
}
