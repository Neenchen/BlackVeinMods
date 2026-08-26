namespace BlackVeinCore.Persistence;

public static class InternalWriteTracker
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, DateTime> RecentWrites = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan SuppressionWindow = TimeSpan.FromSeconds(2);

    public static void Track(string path)
    {
        lock (SyncRoot)
        {
            RecentWrites[Path.GetFullPath(path)] = DateTime.UtcNow;
            Prune();
        }
    }

    public static bool IsInternalWrite(string path)
    {
        lock (SyncRoot)
        {
            Prune();
            var normalizedPath = Path.GetFullPath(path);
            if (!RecentWrites.ContainsKey(normalizedPath))
            {
                return false;
            }

            RecentWrites.Remove(normalizedPath);
            return true;
        }
    }

    private static void Prune()
    {
        var threshold = DateTime.UtcNow - SuppressionWindow;
        foreach (var stalePath in RecentWrites.Where(x => x.Value < threshold).Select(x => x.Key).ToList())
        {
            RecentWrites.Remove(stalePath);
        }
    }
}
