using LiteDB;

namespace BlackVeinKill.Database;

public sealed class PlayerStats
{
    [BsonId]
    public string Name { get; set; } = string.Empty;
    public ulong SteamID { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? Kills { get; set; } = 0;
    public int? Damage { get; set; } = 0;
    public int? DamageTaken { get; set; } = 0;
    public int? Deaths { get; set; } = 0;
    public int? Assists { get; set; } = 0;
    public int? MaxStreak { get; set; } = 0;
    public int? KillStreak { get; set; } = 0;
    public int? MaxGearScore { get; set; } = 0;
    public int? FfaKills { get; set; } = 0;
    public int? FfaDeaths { get; set; } = 0;
}
