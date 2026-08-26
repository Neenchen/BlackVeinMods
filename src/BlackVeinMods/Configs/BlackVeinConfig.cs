using System.Text.Json;
using System.Text.Json.Serialization;
using BlackVeinCore.Persistence;

namespace BlackVeinMods.Configs;

public static class BlackVeinConfig
{
    private const string ConfigFileName = "blackvein_config.json";

    private static readonly string ConfigDirectory = BlackVeinConfigPaths.ConfigDirectory;

    private static readonly string FullPath = Path.Combine(ConfigDirectory, ConfigFileName);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private static ConfigReloadWatcher? _watcher;

    public static BlackVeinConfigData Config { get; private set; } = new();

    public static void Initialize()
    {
        Load();
        _watcher = new ConfigReloadWatcher(ConfigDirectory, ConfigFileName, Load);
        _watcher.Initialize();
    }

    public static void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    public static void Load()
    {
        var result = JsonConfigStore<BlackVeinConfigData>.Load(
            FullPath,
            () => new BlackVeinConfigData(),
            SerializerOptions,
            Normalize,
            message => Plugin.Logger.LogWarning(message));

        if (!result.ShouldApplyValue)
        {
            return;
        }

        Config = result.Value!;
        if (result.NeedsRewrite)
        {
            Save();
        }

    }

    public static void Save()
    {
        JsonConfigStore<BlackVeinConfigData>.Save(
            FullPath,
            Config,
            SerializerOptions,
            message => Plugin.Logger.LogWarning(message));
    }

    private static void Normalize(BlackVeinConfigData config)
    {
        config.ClanProgression ??= config.ClanBossProgression ?? new ClanProgressionConfig();
        config.ClanBossProgression = null;
    }
}

public sealed class BlackVeinConfigData
{
    public ClanProgressionConfig ClanProgression { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClanProgressionConfig? ClanBossProgression { get; set; }
}

public sealed class ClanProgressionConfig
{
    public bool Enabled { get; set; } = true;
    public bool ShareBossUnlocks { get; set; } = true;
    public bool ShareTechnologyUnlocks { get; set; } = true;
    public bool ShareJournalProgression { get; set; } = true;
    public bool ShareResearchProgression { get; set; } = true;
    public bool IncludeOfflineClanMembers { get; set; } = false;
}
