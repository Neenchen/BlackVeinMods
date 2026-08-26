using System.Text;
using System.Text.Json;

namespace BlackVeinCore.Persistence;

public static class JsonConfigFieldPatcher
{
    public static bool EnsureFields(string path, IReadOnlyList<JsonConfigFieldPatch> fields, Action<string>? logWarning = null)
    {
        if (fields.Count == 0 || !File.Exists(path))
        {
            return false;
        }

        var json = FileWriteCoordinator.ReadAllTextWithRetry(path, logWarning);
        var updatedJson = json;
        var changed = false;

        foreach (var field in fields)
        {
            if (TryEnsureField(updatedJson, field, out var patchedJson))
            {
                updatedJson = patchedJson;
                changed = true;
            }
        }

        return changed && FileWriteCoordinator.WriteAllText(path, updatedJson, logWarning);
    }

    public static bool SetFieldValue(string path, JsonConfigFieldPatch field, Action<string>? logWarning = null)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var json = FileWriteCoordinator.ReadAllTextWithRetry(path, logWarning);
        if (!TryFindObject(json, field.Path, out var objectStart, out var objectEnd))
        {
            return false;
        }

        string updatedJson;
        if (TryFindProperty(json, objectStart, objectEnd, field.Name, out _, out var valueStart, out var valueEnd))
        {
            updatedJson = json[..valueStart] + field.ValueJson + json[valueEnd..];
        }
        else if (TryInsertField(json, objectStart, objectEnd, field, out updatedJson))
        {
        }
        else
        {
            return false;
        }

        return updatedJson != json && FileWriteCoordinator.WriteAllText(path, updatedJson, logWarning);
    }

    private static bool TryEnsureField(string json, JsonConfigFieldPatch field, out string updatedJson)
    {
        updatedJson = json;
        if (!TryFindObject(json, field.Path, out var objectStart, out var objectEnd))
        {
            return false;
        }

        if (TryFindProperty(json, objectStart, objectEnd, field.Name, out _, out _, out _))
        {
            return false;
        }

        return TryInsertField(json, objectStart, objectEnd, field, out updatedJson);
    }

    private static bool TryInsertField(string json, int objectStart, int objectEnd, JsonConfigFieldPatch field, out string updatedJson)
    {
        updatedJson = json;
        var hasProperties = ObjectHasProperties(json, objectStart, objectEnd);
        var baseIndent = GetLineIndent(json, objectStart);
        var propertyIndent = baseIndent + "  ";
        var propertyBlock = BuildPropertyBlock(field, propertyIndent);
        var prefix = hasProperties ? "," + Environment.NewLine : Environment.NewLine;
        var suffix = Environment.NewLine + baseIndent;

        updatedJson = json[..objectEnd] + prefix + propertyBlock + suffix + json[objectEnd..];
        return true;
    }

    private static string BuildPropertyBlock(JsonConfigFieldPatch field, string indent)
    {
        var builder = new StringBuilder();
        if (field.CommentLines != null)
        {
            foreach (var comment in field.CommentLines)
            {
                builder.Append(indent);
                builder.AppendLine(comment);
            }
        }

        builder.Append(indent);
        builder.Append(JsonSerializer.Serialize(field.Name));
        builder.Append(": ");
        builder.Append(field.ValueJson);
        return builder.ToString();
    }

    private static bool TryFindObject(string json, IReadOnlyList<string> path, out int objectStart, out int objectEnd)
    {
        objectStart = SkipTrivia(json, 0);
        objectEnd = -1;

        if (objectStart >= json.Length || json[objectStart] != '{' ||
            !TryFindMatchingCompositeEnd(json, objectStart, out objectEnd))
        {
            return false;
        }

        foreach (var segment in path)
        {
            if (!TryFindProperty(json, objectStart, objectEnd, segment, out _, out var valueStart, out _))
            {
                return false;
            }

            valueStart = SkipTrivia(json, valueStart);
            if (valueStart >= json.Length || json[valueStart] != '{' ||
                !TryFindMatchingCompositeEnd(json, valueStart, out var nestedEnd))
            {
                return false;
            }

            objectStart = valueStart;
            objectEnd = nestedEnd - 1;
        }

        return true;
    }

    private static bool TryFindProperty(
        string json,
        int objectStart,
        int objectEnd,
        string propertyName,
        out int nameStart,
        out int valueStart,
        out int valueEnd)
    {
        nameStart = -1;
        valueStart = -1;
        valueEnd = -1;
        var position = objectStart + 1;

        while (position < objectEnd)
        {
            position = SkipTrivia(json, position);
            if (position >= objectEnd)
            {
                break;
            }

            if (json[position] == ',')
            {
                position++;
                continue;
            }

            if (json[position] != '"')
            {
                position++;
                continue;
            }

            nameStart = position;
            if (!TryReadString(json, position, out var currentName, out position))
            {
                return false;
            }

            position = SkipTrivia(json, position);
            if (position >= objectEnd || json[position] != ':')
            {
                continue;
            }

            valueStart = SkipTrivia(json, position + 1);
            valueEnd = SkipValue(json, valueStart);

            if (string.Equals(currentName, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            position = valueEnd;
        }

        return false;
    }

    private static bool ObjectHasProperties(string json, int objectStart, int objectEnd)
    {
        var position = SkipTrivia(json, objectStart + 1);
        return position < objectEnd;
    }

    private static int SkipValue(string json, int position)
    {
        position = SkipTrivia(json, position);
        if (position >= json.Length)
        {
            return position;
        }

        return json[position] switch
        {
            '"' => SkipString(json, position),
            '{' or '[' => TryFindMatchingCompositeEnd(json, position, out var end) ? end : json.Length,
            _ => SkipPrimitive(json, position)
        };
    }

    private static int SkipPrimitive(string json, int position)
    {
        while (position < json.Length)
        {
            var current = json[position];
            if (current is ',' or '}' or ']')
            {
                break;
            }

            position++;
        }

        while (position > 0 && char.IsWhiteSpace(json[position - 1]))
        {
            position--;
        }

        return position;
    }

    private static bool TryFindMatchingCompositeEnd(string json, int start, out int end)
    {
        end = -1;
        var stack = new Stack<char>();
        stack.Push(json[start] == '{' ? '}' : ']');
        var position = start + 1;

        while (position < json.Length)
        {
            var current = json[position];
            if (current == '"')
            {
                position = SkipString(json, position);
                continue;
            }

            if (current == '/' && position + 1 < json.Length)
            {
                var next = json[position + 1];
                if (next == '/')
                {
                    position = SkipLineComment(json, position);
                    continue;
                }

                if (next == '*')
                {
                    position = SkipBlockComment(json, position);
                    continue;
                }
            }

            if (current is '{' or '[')
            {
                stack.Push(current == '{' ? '}' : ']');
            }
            else if (stack.Count > 0 && current == stack.Peek())
            {
                stack.Pop();
                if (stack.Count == 0)
                {
                    end = position + 1;
                    return true;
                }
            }

            position++;
        }

        return false;
    }

    private static int SkipTrivia(string json, int position)
    {
        while (position < json.Length)
        {
            if (char.IsWhiteSpace(json[position]))
            {
                position++;
                continue;
            }

            if (json[position] == '/' && position + 1 < json.Length)
            {
                if (json[position + 1] == '/')
                {
                    position = SkipLineComment(json, position);
                    continue;
                }

                if (json[position + 1] == '*')
                {
                    position = SkipBlockComment(json, position);
                    continue;
                }
            }

            break;
        }

        return position;
    }

    private static bool TryReadString(string json, int position, out string value, out int nextPosition)
    {
        nextPosition = SkipString(json, position);
        value = string.Empty;

        try
        {
            value = JsonSerializer.Deserialize<string>(json[position..nextPosition]) ?? string.Empty;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int SkipString(string json, int position)
    {
        position++;
        while (position < json.Length)
        {
            if (json[position] == '\\')
            {
                position += 2;
                continue;
            }

            if (json[position] == '"')
            {
                return position + 1;
            }

            position++;
        }

        return json.Length;
    }

    private static int SkipLineComment(string json, int position)
    {
        position += 2;
        while (position < json.Length && json[position] is not '\r' and not '\n')
        {
            position++;
        }

        return position;
    }

    private static int SkipBlockComment(string json, int position)
    {
        position += 2;
        while (position + 1 < json.Length)
        {
            if (json[position] == '*' && json[position + 1] == '/')
            {
                return position + 2;
            }

            position++;
        }

        return json.Length;
    }

    private static string GetLineIndent(string json, int position)
    {
        var lineStart = position;
        while (lineStart > 0 && json[lineStart - 1] is not '\r' and not '\n')
        {
            lineStart--;
        }

        var builder = new StringBuilder();
        while (lineStart < json.Length && json[lineStart] is ' ' or '\t')
        {
            builder.Append(json[lineStart]);
            lineStart++;
        }

        return builder.ToString();
    }
}
