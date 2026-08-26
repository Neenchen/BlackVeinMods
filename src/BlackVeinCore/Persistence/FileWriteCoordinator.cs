using System.Text;

namespace BlackVeinCore.Persistence;

public enum FileWriteMode
{
    CreateNew,
    Overwrite
}

public static class FileWriteCoordinator
{
    private const int RetryCount = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    public static string ReadAllTextWithRetry(string path, Action<string>? logWarning = null)
    {
        for (var attempt = 1; attempt <= RetryCount; attempt++)
        {
            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (IOException) when (attempt < RetryCount)
            {
                Thread.Sleep(RetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < RetryCount)
            {
                Thread.Sleep(RetryDelay);
            }
        }

        logWarning?.Invoke($"Failed to read '{path}' after {RetryCount} attempts.");
        return File.ReadAllText(path, Encoding.UTF8);
    }

    public static bool WriteAllText(string path, string contents, Action<string>? logWarning = null, FileWriteMode mode = FileWriteMode.Overwrite)
    {
        var normalizedPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(normalizedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            InternalWriteTracker.Track(normalizedPath);
            var fileMode = mode == FileWriteMode.CreateNew ? FileMode.CreateNew : FileMode.Create;
            using var stream = new FileStream(normalizedPath, fileMode, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(contents);
            return true;
        }
        catch (Exception ex)
        {
            logWarning?.Invoke($"Failed to write '{normalizedPath}': {ex}");
            return false;
        }
    }
}
