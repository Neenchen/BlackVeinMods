using System.Text;
using System.Text.Json;
using BlackVeinCore.Persistence;

namespace BlackVeinKill;

public static class KillfeedSettings
{
    private const string ConfigFileName = "blackvein_kill.json";

    private static readonly string ConfigDirectory = BlackVeinConfigPaths.ConfigDirectory;
    private static readonly string FullPath = Path.Combine(ConfigDirectory, ConfigFileName);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static ConfigReloadWatcher? _watcher;

    public static event Action? ConfigReloaded;
    public static KillfeedConfigData Config { get; private set; } = new();
    public static string ConfigPath => FullPath;

    public static int LevelTrackingMode => Config.LevelTrackingMode;
    public static int ClanTrackingDays => Config.ClanMemberStats.ClanMemberMinDaysForStats;
    public static int MaxLevelGapNormal => Config.LevelRestrictions.MaxLevelGapNormal;
    public static int MaxLevelGapHigh => Config.LevelRestrictions.MaxLevelGapHigh;
    public static string WebhookURL => Config.Discord.WebhookURL;
    public static string StatsWebhookURL => Config.Discord.StatsWebhookURL;
    public static string LeaderboardWebhookURL => Config.Discord.LeaderboardWebhookURL;
    public static string LeaderboardWebhookMessageId => Config.Discord.LeaderboardWebhookMessageId;
    public static bool EnableFfaZones => Config.FfaZones.EnableFfaZones;
    public static bool EnableT2RiftFfaZones => Config.FfaZones.EnableT2RiftFfaZones;
    public static int FfaVisualBuffRetentionSeconds => Config.FfaZones.VisualBuffRetentionSeconds;
    public static IReadOnlyCollection<int> FfaMapZoneIds => Config.FfaZones.MapZoneIds;
    public static IReadOnlyCollection<string> FfaRegionNames => Config.FfaZones.RegionNames;
    public static bool EnableLevelRuleZones => Config.LevelRuleZones.EnableLevelRuleZones;
    public static IReadOnlyList<LevelRuleZoneConfig> LevelRuleZoneRules => Config.LevelRuleZones.Rules;
    public static string KillerNameColor => Config.KillfeedColors.KillerNameColor;
    public static string VictimNameColor => Config.KillfeedColors.VictimNameColor;
    public static string ClanTagColor => Config.KillfeedColors.ClanTagColor;
    public static string AllowedLevelColor => Config.KillfeedColors.AllowedLevelColor;
    public static string ForbiddenLevelColor => Config.KillfeedColors.ForbiddenLevelColor;
    public static bool RestrictDamageToAdmin => Config.TopCommandAccess.RestrictDamageToAdmin;
    public static bool RestrictKillsToAdmin => Config.TopCommandAccess.RestrictKillsToAdmin;
    public static bool RestrictDeathsToAdmin => Config.TopCommandAccess.RestrictDeathsToAdmin;
    public static bool RestrictAssistsToAdmin => Config.TopCommandAccess.RestrictAssistsToAdmin;
    public static bool RestrictMaxStreakToAdmin => Config.TopCommandAccess.RestrictMaxStreakToAdmin;

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
        var result = JsonConfigStore<KillfeedConfigData>.Load(
            FullPath,
            () => new KillfeedConfigData(),
            SerializerOptions,
            Normalize,
            message => Plugin.Logger.LogWarning(message));

        if (!result.ShouldApplyValue)
        {
            return;
        }

        Config = result.Value!;
        if (result.Status == JsonConfigLoadStatus.MissingFile)
        {
            Save();
        }
        else if (result.NeedsRewrite)
        {
            AddMissingFieldsToConfigFile();
        }

        ConfigReloaded?.Invoke();
    }

    public static void Save()
    {
        Normalize(Config);
        WriteConfig(Config);
    }

    public static void SetLeaderboardWebhookMessageId(string messageId)
    {
        var config = LoadConfigForRuntimeSave() ?? Config;
        config.Discord.LeaderboardWebhookMessageId = messageId ?? string.Empty;
        Normalize(config);
        Config = config;
        AddMissingFieldsToConfigFile();

        var field = new JsonConfigFieldPatch(
            new[] { "Discord" },
            "LeaderboardWebhookMessageId",
            JsonString(Config.Discord.LeaderboardWebhookMessageId),
            new[] { "// Message id for the auto-updated leaderboard. Leave blank; BlackVeinKill fills this after creating the message." });

        if (!JsonConfigFieldPatcher.SetFieldValue(FullPath, field, message => Plugin.Logger.LogWarning(message)))
        {
            if (!File.Exists(FullPath))
            {
                WriteConfig(Config);
            }
            else
            {
                Plugin.Logger.LogWarning("[BlackVeinKill] Could not update LeaderboardWebhookMessageId without rewriting the config file.");
            }
        }

        ConfigReloaded?.Invoke();
    }

    private static void Normalize(KillfeedConfigData config)
    {
        config.ClanMemberStats ??= new ClanMemberStatsConfig();
        config.Discord ??= new DiscordConfig();
        config.TopCommandAccess ??= new TopCommandAccessConfig();
        config.KillfeedColors ??= new KillfeedColorsConfig();
        config.LevelRestrictions ??= new LevelRestrictionsConfig();
        config.FfaZones ??= new FfaZonesConfig();
        config.LevelRuleZones ??= new LevelRuleZonesConfig();

        config.LevelTrackingMode = Math.Max(1, Math.Min(2, config.LevelTrackingMode));
        config.ClanMemberStats.ClanMemberMinDaysForStats = Math.Max(0, config.ClanMemberStats.ClanMemberMinDaysForStats);
        config.LevelRestrictions.MaxLevelGapNormal = Math.Max(0, config.LevelRestrictions.MaxLevelGapNormal);
        config.LevelRestrictions.MaxLevelGapHigh = Math.Max(0, config.LevelRestrictions.MaxLevelGapHigh);
        config.Discord.WebhookURL ??= string.Empty;
        config.Discord.StatsWebhookURL ??= string.Empty;
        config.Discord.LeaderboardWebhookURL ??= string.Empty;
        config.Discord.LeaderboardWebhookMessageId ??= string.Empty;
        config.KillfeedColors.KillerNameColor = NormalizeColor(config.KillfeedColors.KillerNameColor, "#ffffff");
        config.KillfeedColors.VictimNameColor = NormalizeColor(config.KillfeedColors.VictimNameColor, "#ffffff");
        config.KillfeedColors.ClanTagColor = NormalizeColor(config.KillfeedColors.ClanTagColor, "#888888");
        config.KillfeedColors.AllowedLevelColor = NormalizeColor(config.KillfeedColors.AllowedLevelColor, "#55ff55");
        config.KillfeedColors.ForbiddenLevelColor = NormalizeColor(config.KillfeedColors.ForbiddenLevelColor, "#ff5555");
        config.FfaZones.MapZoneIds ??= new List<int>();
        config.FfaZones.RegionNames ??= new List<string>();
        config.FfaZones.VisualBuffRetentionSeconds = Math.Max(0, config.FfaZones.VisualBuffRetentionSeconds);
        config.FfaZones.MapZoneIds = config.FfaZones.MapZoneIds.Distinct().ToList();
        config.FfaZones.RegionNames = config.FfaZones.RegionNames
            .Where(region => !string.IsNullOrWhiteSpace(region))
            .Select(region => region.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.LevelRuleZones.Rules ??= new List<LevelRuleZoneConfig>();
        foreach (var rule in config.LevelRuleZones.Rules)
        {
            NormalizeLevelRuleZone(rule);
        }

        config.LevelRuleZones.Rules = config.LevelRuleZones.Rules
            .Where(rule => rule.MaxLevelGap >= 0 && (rule.MapZoneIds.Count > 0 || rule.RegionNames.Count > 0))
            .ToList();
    }

    private static void NormalizeLevelRuleZone(LevelRuleZoneConfig rule)
    {
        rule.Name = string.IsNullOrWhiteSpace(rule.Name) ? "Level Rule Zone" : rule.Name.Trim();
        rule.MaxLevelGap = Math.Max(0, rule.MaxLevelGap);
        rule.MapZoneIds ??= new List<int>();
        rule.RegionNames ??= new List<string>();
        rule.MapZoneIds = rule.MapZoneIds.Distinct().ToList();
        rule.RegionNames = rule.RegionNames
            .Where(region => !string.IsNullOrWhiteSpace(region))
            .Select(region => region.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static KillfeedConfigData? LoadConfigForRuntimeSave()
    {
        var result = JsonConfigStore<KillfeedConfigData>.Load(
            FullPath,
            () => new KillfeedConfigData(),
            SerializerOptions,
            Normalize,
            message => Plugin.Logger.LogWarning(message));

        return result.ShouldApplyValue ? result.Value : null;
    }

    private static void WriteConfig(KillfeedConfigData config)
    {
        FileWriteCoordinator.WriteAllText(
            FullPath,
            WriteCommentedConfig(config),
            message => Plugin.Logger.LogWarning(message));
    }

    private static void AddMissingFieldsToConfigFile()
    {
        JsonConfigFieldPatcher.EnsureFields(
            FullPath,
            CreateMissingFieldPatches(),
            message => Plugin.Logger.LogWarning(message));
    }

    private static IReadOnlyList<JsonConfigFieldPatch> CreateMissingFieldPatches()
    {
        var defaults = new KillfeedConfigData();
        Normalize(defaults);

        return new List<JsonConfigFieldPatch>
        {
            new(
                Array.Empty<string>(),
                "ClanMemberStats",
                "{\n    // Minimum days a clan member must be in a clan before they count toward clan stats.\n    \"ClanMemberMinDaysForStats\": 7\n  }",
                new[] { "// Clan membership settings used when grouping player stats by clan." }),
            new(
                new[] { "ClanMemberStats" },
                "ClanMemberMinDaysForStats",
                defaults.ClanMemberStats.ClanMemberMinDaysForStats.ToString(),
                new[] { "// Minimum days a clan member must be in a clan before they count toward clan stats." }),

            new(
                Array.Empty<string>(),
                "Discord",
                "{\n    // Webhook URL for individual kill messages.\n    \"WebhookURL\": \"\",\n    // Webhook URL used by the .posttop command.\n    \"StatsWebhookURL\": \"\",\n    // Webhook URL for the auto-updated top 25 leaderboard.\n    \"LeaderboardWebhookURL\": \"\",\n    // Message id for the auto-updated leaderboard. Leave blank; BlackVeinKill fills this after creating the message.\n    \"LeaderboardWebhookMessageId\": \"\"\n  }",
                new[] { "// Discord webhook settings. Leave URLs blank to disable those posts." }),
            new(new[] { "Discord" }, "WebhookURL", JsonString(defaults.Discord.WebhookURL), new[] { "// Webhook URL for individual kill messages." }),
            new(new[] { "Discord" }, "StatsWebhookURL", JsonString(defaults.Discord.StatsWebhookURL), new[] { "// Webhook URL used by the .posttop command." }),
            new(new[] { "Discord" }, "LeaderboardWebhookURL", JsonString(defaults.Discord.LeaderboardWebhookURL), new[] { "// Webhook URL for the auto-updated top 25 leaderboard." }),
            new(new[] { "Discord" }, "LeaderboardWebhookMessageId", JsonString(defaults.Discord.LeaderboardWebhookMessageId), new[] { "// Message id for the auto-updated leaderboard. Leave blank; BlackVeinKill fills this after creating the message." }),

            new(Array.Empty<string>(), "LevelTrackingMode", defaults.LevelTrackingMode.ToString(), new[] { "// 1 = use max logged gearscore, 2 = use live gearscore at the time of the kill." }),

            new(
                Array.Empty<string>(),
                "TopCommandAccess",
                "{\n    \"RestrictDamageToAdmin\": false,\n    \"RestrictKillsToAdmin\": false,\n    \"RestrictDeathsToAdmin\": true,\n    \"RestrictAssistsToAdmin\": false,\n    \"RestrictMaxStreakToAdmin\": false\n  }",
                new[] { "// Optional admin-only restrictions for leaderboard command categories." }),
            new(new[] { "TopCommandAccess" }, "RestrictDamageToAdmin", JsonBool(defaults.TopCommandAccess.RestrictDamageToAdmin)),
            new(new[] { "TopCommandAccess" }, "RestrictKillsToAdmin", JsonBool(defaults.TopCommandAccess.RestrictKillsToAdmin)),
            new(new[] { "TopCommandAccess" }, "RestrictDeathsToAdmin", JsonBool(defaults.TopCommandAccess.RestrictDeathsToAdmin)),
            new(new[] { "TopCommandAccess" }, "RestrictAssistsToAdmin", JsonBool(defaults.TopCommandAccess.RestrictAssistsToAdmin)),
            new(new[] { "TopCommandAccess" }, "RestrictMaxStreakToAdmin", JsonBool(defaults.TopCommandAccess.RestrictMaxStreakToAdmin)),

            new(
                Array.Empty<string>(),
                "KillfeedColors",
                "{\n    \"KillerNameColor\": \"#ffffff\",\n    \"VictimNameColor\": \"#ffffff\",\n    \"ClanTagColor\": \"#888888\",\n    // Level color used when the kill is allowed by the level rules.\n    \"AllowedLevelColor\": \"#55ff55\",\n    // Level color used when the kill is outside the allowed level rules.\n    \"ForbiddenLevelColor\": \"#ff5555\"\n  }",
                new[] { "// Chat color settings. Use hex colors like #55ff55." }),
            new(new[] { "KillfeedColors" }, "KillerNameColor", JsonString(defaults.KillfeedColors.KillerNameColor)),
            new(new[] { "KillfeedColors" }, "VictimNameColor", JsonString(defaults.KillfeedColors.VictimNameColor)),
            new(new[] { "KillfeedColors" }, "ClanTagColor", JsonString(defaults.KillfeedColors.ClanTagColor)),
            new(new[] { "KillfeedColors" }, "AllowedLevelColor", JsonString(defaults.KillfeedColors.AllowedLevelColor), new[] { "// Level color used when the kill is allowed by the level rules." }),
            new(new[] { "KillfeedColors" }, "ForbiddenLevelColor", JsonString(defaults.KillfeedColors.ForbiddenLevelColor), new[] { "// Level color used when the kill is outside the allowed level rules." }),

            new(
                Array.Empty<string>(),
                "LevelRestrictions",
                "{\n    // Used when the killer is below level 91.\n    \"MaxLevelGapNormal\": 15,\n    // Used when the killer is level 91 or higher.\n    \"MaxLevelGapHigh\": 21\n  }",
                new[] { "// Maximum allowed killer-over-victim level gap outside FFA zones." }),
            new(new[] { "LevelRestrictions" }, "MaxLevelGapNormal", defaults.LevelRestrictions.MaxLevelGapNormal.ToString(), new[] { "// Used when the killer is below level 91." }),
            new(new[] { "LevelRestrictions" }, "MaxLevelGapHigh", defaults.LevelRestrictions.MaxLevelGapHigh.ToString(), new[] { "// Used when the killer is level 91 or higher." }),

            new(
                Array.Empty<string>(),
                "FfaZones",
                "{\n    \"EnableFfaZones\": false,\n    // Treat active major rift zones as FFA hot zones.\n    \"EnableT2RiftFfaZones\": true,\n    // Seconds to keep the FFA visual marker after a player leaves while PvP combat-tagged. Use 0 to remove it immediately.\n    \"VisualBuffRetentionSeconds\": 30,\n    // Map-zone hashes that should count as FFA.\n    \"MapZoneIds\": [\n      692515833\n    ],\n    // WorldRegionType names that should count as FFA.\n    \"RegionNames\": [\n      \"SilverlightHills\",\n      \"RuinsOfMortium\",\n      \"Strongblade\",\n      \"Gloomrot_North\",\n      \"Gloomrot_South\"\n    ]\n  }",
                new[] { "// FFA hot zones ignore level restrictions and mark kills as FFA." }),
            new(new[] { "FfaZones" }, "EnableFfaZones", JsonBool(defaults.FfaZones.EnableFfaZones)),
            new(new[] { "FfaZones" }, "EnableT2RiftFfaZones", JsonBool(defaults.FfaZones.EnableT2RiftFfaZones), new[] { "// Treat active major rift zones as FFA hot zones." }),
            new(new[] { "FfaZones" }, "VisualBuffRetentionSeconds", defaults.FfaZones.VisualBuffRetentionSeconds.ToString(), new[] { "// Seconds to keep the FFA visual marker after a player leaves while PvP combat-tagged. Use 0 to remove it immediately." }),
            new(new[] { "FfaZones" }, "MapZoneIds", BuildIntArrayJson(defaults.FfaZones.MapZoneIds, 4), new[] { "// Map-zone hashes that should count as FFA." }),
            new(new[] { "FfaZones" }, "RegionNames", BuildStringArrayJson(defaults.FfaZones.RegionNames, 4), new[] { "// WorldRegionType names that should count as FFA." }),

            new(
                Array.Empty<string>(),
                "LevelRuleZones",
                "{\n    \"EnableLevelRuleZones\": false,\n    // Ordered rules for zones that are not FFA but use a custom killer-over-victim level gap.\n    \"Rules\": [\n      {\n        \"Name\": \"Example 15 Gap Zone\",\n        \"MaxLevelGap\": 15,\n        \"MapZoneIds\": [],\n        \"RegionNames\": []\n      }\n    ]\n  }",
                new[] { "// Non-FFA zones with custom killfeed level-difference rules. First matching rule wins." }),
            new(new[] { "LevelRuleZones" }, "EnableLevelRuleZones", JsonBool(defaults.LevelRuleZones.EnableLevelRuleZones)),
            new(
                new[] { "LevelRuleZones" },
                "Rules",
                "[\n      {\n        \"Name\": \"Example 15 Gap Zone\",\n        \"MaxLevelGap\": 15,\n        \"MapZoneIds\": [],\n        \"RegionNames\": []\n      }\n    ]",
                new[] { "// Ordered rules for zones that are not FFA but use a custom killer-over-victim level gap." }),
        };
    }

    private static string WriteCommentedConfig(KillfeedConfigData config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  // Clan membership settings used when grouping player stats by clan.");
        sb.AppendLine("  \"ClanMemberStats\": {");
        sb.AppendLine("    // Minimum days a clan member must be in a clan before they count toward clan stats.");
        sb.AppendLine($"    \"ClanMemberMinDaysForStats\": {config.ClanMemberStats.ClanMemberMinDaysForStats}");
        sb.AppendLine("  },");
        sb.AppendLine();
        sb.AppendLine("  // Discord webhook settings. Leave URLs blank to disable those posts.");
        sb.AppendLine("  \"Discord\": {");
        sb.AppendLine("    // Webhook URL for individual kill messages.");
        sb.AppendLine($"    \"WebhookURL\": {JsonString(config.Discord.WebhookURL)},");
        sb.AppendLine("    // Webhook URL used by the .posttop command.");
        sb.AppendLine($"    \"StatsWebhookURL\": {JsonString(config.Discord.StatsWebhookURL)},");
        sb.AppendLine("    // Webhook URL for the auto-updated top 25 leaderboard.");
        sb.AppendLine($"    \"LeaderboardWebhookURL\": {JsonString(config.Discord.LeaderboardWebhookURL)},");
        sb.AppendLine("    // Message id for the auto-updated leaderboard. Leave blank; BlackVeinKill fills this after creating the message.");
        sb.AppendLine($"    \"LeaderboardWebhookMessageId\": {JsonString(config.Discord.LeaderboardWebhookMessageId)}");
        sb.AppendLine("  },");
        sb.AppendLine();
        sb.AppendLine("  // 1 = use max logged gearscore, 2 = use live gearscore at the time of the kill.");
        sb.AppendLine($"  \"LevelTrackingMode\": {config.LevelTrackingMode},");
        sb.AppendLine();
        sb.AppendLine("  // Optional admin-only restrictions for leaderboard command categories.");
        sb.AppendLine("  \"TopCommandAccess\": {");
        sb.AppendLine($"    \"RestrictDamageToAdmin\": {JsonBool(config.TopCommandAccess.RestrictDamageToAdmin)},");
        sb.AppendLine($"    \"RestrictKillsToAdmin\": {JsonBool(config.TopCommandAccess.RestrictKillsToAdmin)},");
        sb.AppendLine($"    \"RestrictDeathsToAdmin\": {JsonBool(config.TopCommandAccess.RestrictDeathsToAdmin)},");
        sb.AppendLine($"    \"RestrictAssistsToAdmin\": {JsonBool(config.TopCommandAccess.RestrictAssistsToAdmin)},");
        sb.AppendLine($"    \"RestrictMaxStreakToAdmin\": {JsonBool(config.TopCommandAccess.RestrictMaxStreakToAdmin)}");
        sb.AppendLine("  },");
        sb.AppendLine();
        sb.AppendLine("  // Chat color settings. Use hex colors like #55ff55.");
        sb.AppendLine("  \"KillfeedColors\": {");
        sb.AppendLine($"    \"KillerNameColor\": {JsonString(config.KillfeedColors.KillerNameColor)},");
        sb.AppendLine($"    \"VictimNameColor\": {JsonString(config.KillfeedColors.VictimNameColor)},");
        sb.AppendLine($"    \"ClanTagColor\": {JsonString(config.KillfeedColors.ClanTagColor)},");
        sb.AppendLine("    // Level color used when the kill is allowed by the level rules.");
        sb.AppendLine($"    \"AllowedLevelColor\": {JsonString(config.KillfeedColors.AllowedLevelColor)},");
        sb.AppendLine("    // Level color used when the kill is outside the allowed level rules.");
        sb.AppendLine($"    \"ForbiddenLevelColor\": {JsonString(config.KillfeedColors.ForbiddenLevelColor)}");
        sb.AppendLine("  },");
        sb.AppendLine();
        sb.AppendLine("  // Maximum allowed killer-over-victim level gap outside FFA zones.");
        sb.AppendLine("  \"LevelRestrictions\": {");
        sb.AppendLine("    // Used when the killer is below level 91.");
        sb.AppendLine($"    \"MaxLevelGapNormal\": {config.LevelRestrictions.MaxLevelGapNormal},");
        sb.AppendLine("    // Used when the killer is level 91 or higher.");
        sb.AppendLine($"    \"MaxLevelGapHigh\": {config.LevelRestrictions.MaxLevelGapHigh}");
        sb.AppendLine("  },");
        sb.AppendLine();
        sb.AppendLine("  // FFA hot zones ignore level restrictions and mark kills as FFA.");
        sb.AppendLine("  \"FfaZones\": {");
        sb.AppendLine($"    \"EnableFfaZones\": {JsonBool(config.FfaZones.EnableFfaZones)},");
        sb.AppendLine("    // Treat active major rift zones as FFA hot zones.");
        sb.AppendLine($"    \"EnableT2RiftFfaZones\": {JsonBool(config.FfaZones.EnableT2RiftFfaZones)},");
        sb.AppendLine("    // Seconds to keep the FFA visual marker after a player leaves while PvP combat-tagged. Use 0 to remove it immediately.");
        sb.AppendLine($"    \"VisualBuffRetentionSeconds\": {config.FfaZones.VisualBuffRetentionSeconds},");
        sb.AppendLine("    // Map-zone hashes that should count as FFA.");
        AppendIntArray(sb, "MapZoneIds", config.FfaZones.MapZoneIds, 4, true);
        sb.AppendLine("    // WorldRegionType names that should count as FFA.");
        AppendStringArray(sb, "RegionNames", config.FfaZones.RegionNames, 4, false);
        sb.AppendLine("  },");
        sb.AppendLine();
        sb.AppendLine("  // Non-FFA zones with custom killfeed level-difference rules. First matching rule wins.");
        sb.AppendLine("  \"LevelRuleZones\": {");
        sb.AppendLine($"    \"EnableLevelRuleZones\": {JsonBool(config.LevelRuleZones.EnableLevelRuleZones)},");
        sb.AppendLine("    // Ordered rules for zones that are not FFA but use a custom killer-over-victim level gap.");
        AppendLevelRuleZoneArray(sb, config.LevelRuleZones.Rules, 4, false);
        sb.AppendLine("  }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string JsonString(string? value)
    {
        return JsonSerializer.Serialize(value ?? string.Empty);
    }

    private static string JsonBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string BuildIntArrayJson(IReadOnlyList<int> values, int indent)
    {
        var sb = new StringBuilder();
        AppendIntArrayValue(sb, values, indent);
        return sb.ToString();
    }

    private static string BuildStringArrayJson(IReadOnlyList<string> values, int indent)
    {
        var sb = new StringBuilder();
        AppendStringArrayValue(sb, values, indent);
        return sb.ToString();
    }

    private static void AppendIntArray(StringBuilder sb, string propertyName, IReadOnlyList<int> values, int indent, bool addComma)
    {
        var padding = new string(' ', indent);
        sb.AppendLine($"{padding}\"{propertyName}\": [");
        AppendIntArrayItems(sb, values, indent);
        sb.AppendLine($"{padding}]{(addComma ? "," : string.Empty)}");
    }

    private static void AppendStringArray(StringBuilder sb, string propertyName, IReadOnlyList<string> values, int indent, bool addComma)
    {
        var padding = new string(' ', indent);
        sb.AppendLine($"{padding}\"{propertyName}\": [");
        AppendStringArrayItems(sb, values, indent);
        sb.AppendLine($"{padding}]{(addComma ? "," : string.Empty)}");
    }

    private static void AppendLevelRuleZoneArray(StringBuilder sb, IReadOnlyList<LevelRuleZoneConfig> rules, int indent, bool addComma)
    {
        var padding = new string(' ', indent);
        sb.AppendLine($"{padding}\"Rules\": [");
        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            var comma = index == rules.Count - 1 ? string.Empty : ",";
            sb.AppendLine($"{padding}  {{");
            sb.AppendLine($"{padding}    \"Name\": {JsonString(rule.Name)},");
            sb.AppendLine($"{padding}    \"MaxLevelGap\": {rule.MaxLevelGap},");
            AppendIntArray(sb, "MapZoneIds", rule.MapZoneIds, indent + 4, true);
            AppendStringArray(sb, "RegionNames", rule.RegionNames, indent + 4, false);
            sb.AppendLine($"{padding}  }}{comma}");
        }

        sb.AppendLine($"{padding}]{(addComma ? "," : string.Empty)}");
    }

    private static void AppendIntArrayValue(StringBuilder sb, IReadOnlyList<int> values, int indent)
    {
        var padding = new string(' ', indent);
        sb.AppendLine("[");
        AppendIntArrayItems(sb, values, indent);
        sb.Append($"{padding}]");
    }

    private static void AppendStringArrayValue(StringBuilder sb, IReadOnlyList<string> values, int indent)
    {
        var padding = new string(' ', indent);
        sb.AppendLine("[");
        AppendStringArrayItems(sb, values, indent);
        sb.Append($"{padding}]");
    }

    private static void AppendIntArrayItems(StringBuilder sb, IReadOnlyList<int> values, int indent)
    {
        var padding = new string(' ', indent);
        for (var index = 0; index < values.Count; index++)
        {
            var comma = index == values.Count - 1 ? string.Empty : ",";
            sb.AppendLine($"{padding}  {values[index]}{comma}");
        }
    }

    private static void AppendStringArrayItems(StringBuilder sb, IReadOnlyList<string> values, int indent)
    {
        var padding = new string(' ', indent);
        for (var index = 0; index < values.Count; index++)
        {
            var comma = index == values.Count - 1 ? string.Empty : ",";
            sb.AppendLine($"{padding}  {JsonString(values[index])}{comma}");
        }
    }
}

public sealed class KillfeedConfigData
{
    public ClanMemberStatsConfig ClanMemberStats { get; set; } = new();
    public DiscordConfig Discord { get; set; } = new();
    public int LevelTrackingMode { get; set; } = 1;
    public TopCommandAccessConfig TopCommandAccess { get; set; } = new();
    public KillfeedColorsConfig KillfeedColors { get; set; } = new();
    public LevelRestrictionsConfig LevelRestrictions { get; set; } = new();
    public FfaZonesConfig FfaZones { get; set; } = new();
    public LevelRuleZonesConfig LevelRuleZones { get; set; } = new();
}

public sealed class ClanMemberStatsConfig
{
    public int ClanMemberMinDaysForStats { get; set; } = 7;
}

public sealed class DiscordConfig
{
    public string WebhookURL { get; set; } = string.Empty;
    public string StatsWebhookURL { get; set; } = string.Empty;
    public string LeaderboardWebhookURL { get; set; } = string.Empty;
    public string LeaderboardWebhookMessageId { get; set; } = string.Empty;
}

public sealed class TopCommandAccessConfig
{
    public bool RestrictDamageToAdmin { get; set; } = false;
    public bool RestrictKillsToAdmin { get; set; } = false;
    public bool RestrictDeathsToAdmin { get; set; } = true;
    public bool RestrictAssistsToAdmin { get; set; } = false;
    public bool RestrictMaxStreakToAdmin { get; set; } = false;
}

public sealed class KillfeedColorsConfig
{
    public string KillerNameColor { get; set; } = "#ffffff";
    public string VictimNameColor { get; set; } = "#ffffff";
    public string ClanTagColor { get; set; } = "#888888";
    public string AllowedLevelColor { get; set; } = "#55ff55";
    public string ForbiddenLevelColor { get; set; } = "#ff5555";
}

public sealed class LevelRestrictionsConfig
{
    public int MaxLevelGapNormal { get; set; } = 15;
    public int MaxLevelGapHigh { get; set; } = 21;
}

public sealed class LevelRuleZonesConfig
{
    public bool EnableLevelRuleZones { get; set; } = false;
    public List<LevelRuleZoneConfig> Rules { get; set; } = new()
    {
        new LevelRuleZoneConfig()
    };
}

public sealed class LevelRuleZoneConfig
{
    public string Name { get; set; } = "Example 15 Gap Zone";
    public int MaxLevelGap { get; set; } = 15;
    public List<int> MapZoneIds { get; set; } = new();
    public List<string> RegionNames { get; set; } = new();
}

public sealed class FfaZonesConfig
{
    public bool EnableFfaZones { get; set; } = false;
    public bool EnableT2RiftFfaZones { get; set; } = true;
    public int VisualBuffRetentionSeconds { get; set; } = 30;
    public List<int> MapZoneIds { get; set; } = new() { 692515833 };
    public List<string> RegionNames { get; set; } = new()
    {
        "SilverlightHills",
        "RuinsOfMortium",
        "Strongblade",
        "Gloomrot_North",
        "Gloomrot_South",
    };
}
