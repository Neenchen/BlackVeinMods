using BepInEx;
using LiteDB;

namespace BlackVeinKill.Database;

public sealed class ClanMemberRecord
{
    public string Id { get; set; } = string.Empty;
    public ulong SteamID { get; set; }
    public string ClanName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public sealed class DatabaseWrapper : IDisposable
{
    private static DatabaseWrapper? _instance;
    public static DatabaseWrapper Instance => _instance ??= new DatabaseWrapper();

    private readonly LiteDatabase _database;

    public ILiteCollection<PlayerStats> Collection { get; }
    public ILiteCollection<ClanMemberRecord> ClanMembersCollection => _database.GetCollection<ClanMemberRecord>("clan_members");

    private DatabaseWrapper()
    {
        var dbFolder = Path.Combine(Paths.ConfigPath, "CustomKillDB");
        Directory.CreateDirectory(dbFolder);

        var databasePath = Path.Combine(dbFolder, "PlayerStats.db");
        Plugin.Logger.LogInfo($"[BlackVeinKill] Database path: {databasePath}");

        _database = new LiteDatabase($"Filename={databasePath}; Connection=shared");
        Collection = _database.GetCollection<PlayerStats>("player_stats");
        Collection.EnsureIndex(x => x.Name);
        Collection.EnsureIndex(x => x.SteamID);
        ClanMembersCollection.EnsureIndex(x => x.Id);
        ClanMembersCollection.EnsureIndex(x => x.JoinedAt);
    }

    public static void Initialize()
    {
        _ = Instance;
    }

    public static void DisposeInstance()
    {
        _instance?.Dispose();
        _instance = null;
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}
