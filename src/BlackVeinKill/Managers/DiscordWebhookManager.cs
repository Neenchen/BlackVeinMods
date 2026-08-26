using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlackVeinKill.Database;
using BlackVeinKill.Services;

namespace BlackVeinKill.Managers;

public static class DiscordWebhookManager
{
    private static readonly HttpClient HttpClient = new();
    private static readonly Regex WebhookRegex = new(@"^(?<base>https://(?:ptb\.|canary\.)?discord(?:app)?\.com/api/webhooks/(?<id>\d+)/(?<token>[^/?#]+))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void SendKillMessage(string message)
    {
        var webhookUrl = KillfeedSettings.WebhookURL;
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        _ = Task.Run(() => SendPayloadAsync(webhookUrl, new
        {
            username = "BlackVeinKill",
            content = message,
            allowed_mentions = new { parse = Array.Empty<string>() }
        }, "killfeed webhook"));
    }

    public static void UpdateLeaderboard()
    {
        var webhookUrl = KillfeedSettings.LeaderboardWebhookURL;
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        _ = Task.Run(() => UpdateLeaderboardAsync(webhookUrl));
    }

    public static async Task PostStatsSnapshotAsync(string webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        await SendPayloadAsync(webhookUrl, new
        {
            username = "BlackVeinKill",
            content = BuildLeaderboardContent(10),
            allowed_mentions = new { parse = Array.Empty<string>() }
        }, "stats webhook");
    }

    private static async Task UpdateLeaderboardAsync(string webhookUrl)
    {
        var content = BuildLeaderboardContent(25);
        var payload = new
        {
            username = "BlackVeinKill",
            content,
            allowed_mentions = new { parse = Array.Empty<string>() }
        };

        var messageId = KillfeedSettings.LeaderboardWebhookMessageId;
        if (!string.IsNullOrWhiteSpace(messageId) &&
            TryBuildWebhookMessageUrl(webhookUrl, messageId, out var messageUrl) &&
            await SendPayloadAsync(messageUrl, payload, "leaderboard webhook edit", HttpMethod.Patch))
        {
            return;
        }

        if (!TryBuildWebhookCreateUrl(webhookUrl, out var createUrl))
        {
            Plugin.Logger.LogWarning("[BlackVeinKill] LeaderboardWebhookURL is not a Discord webhook URL.");
            return;
        }

        var responseJson = await SendPayloadForResponseAsync(createUrl, payload, "leaderboard webhook create");
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            if (document.RootElement.TryGetProperty("id", out var idElement))
            {
                KillfeedSettings.SetLeaderboardWebhookMessageId(idElement.GetString() ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] Could not parse leaderboard webhook response: {ex.Message}");
        }
    }

    private static string BuildLeaderboardContent(int count)
    {
        var topPlayers = PvPStatsService.GetAllStats().Values
            .Where(stats => (stats.Kills ?? 0) > 0)
            .OrderByDescending(stats => stats.Kills ?? 0)
            .ThenByDescending(stats => stats.Assists ?? 0)
            .ThenByDescending(stats => stats.Damage ?? 0)
            .ThenBy(stats => stats.Name, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"**Top {count} PvP Leaderboard**");
        sb.AppendLine($"Updated <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>");

        if (topPlayers.Count == 0)
        {
            sb.AppendLine("No kills recorded yet.");
            return sb.ToString();
        }

        for (var index = 0; index < topPlayers.Count; index++)
        {
            var stats = topPlayers[index];
            sb.AppendLine(FormatLeaderboardLine(index + 1, stats));
        }

        return sb.ToString();
    }

    private static string FormatLeaderboardLine(int rank, PlayerStats stats)
    {
        var ffa = (stats.FfaKills ?? 0) > 0 ? $" | FFA {stats.FfaKills}" : string.Empty;
        return $"{rank}. **{EscapeDiscordMarkdown(stats.Name)}** - K {stats.Kills ?? 0} / D {stats.Deaths ?? 0} / A {stats.Assists ?? 0} | Streak {stats.KillStreak ?? 0} | Max {stats.MaxStreak ?? 0}{ffa}";
    }

    private static async Task<bool> SendPayloadAsync(string url, object payload, string context, HttpMethod? method = null)
    {
        var response = await SendPayloadForResponseAsync(url, payload, context, method);
        return response != null;
    }

    private static async Task<string?> SendPayloadForResponseAsync(string url, object payload, string context, HttpMethod? method = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(method ?? HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var response = await HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Plugin.Logger.LogWarning($"[BlackVeinKill] {context} failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                return null;
            }

            return body;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[BlackVeinKill] {context} error: {ex.Message}");
            return null;
        }
    }

    private static bool TryBuildWebhookCreateUrl(string webhookUrl, out string createUrl)
    {
        createUrl = string.Empty;
        var match = WebhookRegex.Match(webhookUrl);
        if (!match.Success)
        {
            return false;
        }

        createUrl = $"{match.Groups["base"].Value}?wait=true";
        return true;
    }

    private static bool TryBuildWebhookMessageUrl(string webhookUrl, string messageId, out string messageUrl)
    {
        messageUrl = string.Empty;
        var match = WebhookRegex.Match(webhookUrl);
        if (!match.Success || string.IsNullOrWhiteSpace(messageId))
        {
            return false;
        }

        messageUrl = $"{match.Groups["base"].Value}/messages/{messageId}";
        return true;
    }

    public static string EscapeDiscordMarkdown(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("`", "\\`")
            .Replace("~", "\\~")
            .Replace("|", "\\|")
            .Replace(">", "\\>");
    }
}
