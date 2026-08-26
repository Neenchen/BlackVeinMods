using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BlackVeinCore.Tools;

public static class RichTextMessageFormatter
{
    private static readonly Regex PlaceholderPattern = new Regex(
        "\\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\\.(?<formatter>[A-Za-z_][A-Za-z0-9_]*)\\(\\))?\\}",
        RegexOptions.Compiled);

    public static string Format(string template, IReadOnlyDictionary<string, object> values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return PlaceholderPattern.Replace(template, match =>
        {
            string name = match.Groups["name"].Value;
            if (!values.TryGetValue(name, out object? value))
            {
                return match.Value;
            }

            string formattedValue = value?.ToString() ?? string.Empty;
            string formatter = match.Groups["formatter"].Success ? match.Groups["formatter"].Value : string.Empty;
            return ApplyFormatter(formattedValue, formatter);
        });
    }

    private static string ApplyFormatter(string value, string formatter)
    {
        if (string.IsNullOrEmpty(formatter))
        {
            return value;
        }

        return formatter.ToLowerInvariant() switch
        {
            "bold" => value.Bold(),
            "italic" => value.Italic(),
            "emphasize" => value.Emphasize(),
            "basecolor" => value.BaseColor(),
            "white" => value.White(),
            "error" => value.Error(),
            "warning" => value.Warning(),
            "success" => value.Success(),
            "friendlyteam" => value.FriendlyTeam(),
            "enemyteam" => value.EnemyTeam(),
            "neutralteam" => value.NeutralTeam(),
            _ => value
        };
    }
}
