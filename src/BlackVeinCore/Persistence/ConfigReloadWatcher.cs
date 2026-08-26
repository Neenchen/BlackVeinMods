namespace BlackVeinCore.Persistence;

public sealed class ConfigReloadWatcher : IDisposable
{
    private readonly string _directory;
    private readonly string _fileName;
    private readonly string _fullPath;
    private readonly Action _reload;
    private readonly TimeSpan _debounceDelay;
    private readonly object _reloadLock = new();

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;

    public ConfigReloadWatcher(string directory, string fileName, Action reload, TimeSpan? debounceDelay = null)
    {
        _directory = directory;
        _fileName = fileName;
        _fullPath = Path.GetFullPath(Path.Combine(directory, fileName));
        _reload = reload;
        _debounceDelay = debounceDelay ?? TimeSpan.FromSeconds(1);
    }

    public void Initialize()
    {
        if (_watcher != null)
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        _watcher = new FileSystemWatcher
        {
            Path = _directory,
            Filter = _fileName,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnWatchedFileChanged;
        _watcher.Created += OnWatchedFileChanged;
        _watcher.Deleted += OnWatchedFileChanged;
        _watcher.Renamed += OnWatchedFileRenamed;
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;

        if (_watcher == null)
        {
            return;
        }

        _watcher.Changed -= OnWatchedFileChanged;
        _watcher.Created -= OnWatchedFileChanged;
        _watcher.Deleted -= OnWatchedFileChanged;
        _watcher.Renamed -= OnWatchedFileRenamed;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        if (InternalWriteTracker.IsInternalWrite(_fullPath))
        {
            return;
        }

        ScheduleReload();
    }

    private void OnWatchedFileRenamed(object sender, RenamedEventArgs e)
    {
        if (InternalWriteTracker.IsInternalWrite(_fullPath))
        {
            return;
        }

        ScheduleReload();
    }

    private void ScheduleReload()
    {
        Timer? timer = null;

        lock (_reloadLock)
        {
            _debounceTimer?.Dispose();
            timer = new Timer(_ =>
            {
                try
                {
                    _reload();
                }
                finally
                {
                    lock (_reloadLock)
                    {
                        if (ReferenceEquals(_debounceTimer, timer))
                        {
                            _debounceTimer = null;
                        }
                    }

                    timer?.Dispose();
                }
            }, null, _debounceDelay, Timeout.InfiniteTimeSpan);

            _debounceTimer = timer;
        }
    }
}
