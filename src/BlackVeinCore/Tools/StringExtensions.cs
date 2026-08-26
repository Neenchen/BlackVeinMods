using System;
using System.Text;
using UnityEngine;

namespace BlackVeinCore.Tools;

public static class StringExtensions
{
    public static string Bold(this string text)
    {
        return $"<b>{text}</b>";
    }

    public static string Italic(this string text)
    {
        return $"<i>{text}</i>";
    }

    public static string Colorify(this string text, Color color)
    {
        var color32 = (Color32)color;
        return text.Colorify(ToHexString(color32));
    }

    public static string Colorify(this string text, Color32 color)
    {
        return text.Colorify(ToHexString(color));
    }

    public static string Colorify(this string text, string hex)
    {
        if (hex.StartsWith("#", StringComparison.Ordinal))
        {
            hex = hex.Substring(1);
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.Append("<color=#");
        stringBuilder.Append(hex);
        stringBuilder.Append('>');
        stringBuilder.Append(text);
        stringBuilder.Append("</color>");
        return stringBuilder.ToString();
    }

    public static string Size(this string text, int size)
    {
        return $"<size={size}>{text}</size>";
    }

    public static string Emphasize(this string text)
    {
        return text.Colorify(ExtendedColor.AntiqueWhite);
    }

    public static string BaseColor(this string text)
    {
        return text.Colorify(ExtendedColor.White);
    }

    public static string White(this string text)
    {
        return text.Colorify(ExtendedColor.LightGray);
    }

    public static string Error(this string text)
    {
        return text.Colorify(ExtendedColor.Crimson);
    }

    public static string Warning(this string text)
    {
        return text.Colorify(ExtendedColor.ReunoYellow);
    }

    public static string Success(this string text)
    {
        return text.Colorify(ExtendedColor.MediumSeaGreen);
    }

    public static string FriendlyTeam(this string text)
    {
        return text.Success();
    }

    public static string EnemyTeam(this string text)
    {
        return text.Error();
    }

    public static string NeutralTeam(this string text)
    {
        return text.Warning();
    }

    private static string ToHexString(Color32 color)
    {
        return $"{color.r:X2}{color.g:X2}{color.b:X2}{color.a:X2}";
    }
}
