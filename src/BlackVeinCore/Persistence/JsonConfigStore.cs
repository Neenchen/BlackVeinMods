using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace BlackVeinCore.Persistence;

public enum JsonConfigLoadStatus
{
    Loaded,
    MissingFile,
    RecoveredWithDefaults,
    Failed
}

public sealed record JsonConfigLoadResult<T>(T? Value, bool NeedsRewrite, JsonConfigLoadStatus Status) where T : class
{
    public bool ShouldApplyValue => Value != null && Status != JsonConfigLoadStatus.Failed;
}

public static class JsonConfigStore<T> where T : class
{
    public static JsonConfigLoadResult<T> Load(
        string path,
        Func<T> factory,
        JsonSerializerOptions options,
        Action<T>? normalize = null,
        Action<string>? logWarning = null)
    {
        var normalizedPath = Path.GetFullPath(path);
        EnsureDirectory(normalizedPath);

        if (!File.Exists(normalizedPath))
        {
            return new JsonConfigLoadResult<T>(factory(), true, JsonConfigLoadStatus.MissingFile);
        }

        try
        {
            var jsonData = FileWriteCoordinator.ReadAllTextWithRetry(normalizedPath, logWarning);
            var usedRecoveryMerge = false;
            var loadFailed = false;
            var value = TryDeserialize(jsonData, factory, options, logWarning, normalizedPath, out usedRecoveryMerge, out loadFailed);

            if (loadFailed || value == null)
            {
                logWarning?.Invoke($"Skipping rewrite for '{normalizedPath}' because the config could not be loaded safely.");
                return new JsonConfigLoadResult<T>(null, false, JsonConfigLoadStatus.Failed);
            }

            normalize?.Invoke(value);

            var rewrittenJson = JsonSerializer.Serialize(value, options);
            var needsRewrite = usedRecoveryMerge || NeedsRewrite(jsonData, rewrittenJson, options);
            var status = usedRecoveryMerge ? JsonConfigLoadStatus.RecoveredWithDefaults : JsonConfigLoadStatus.Loaded;
            return new JsonConfigLoadResult<T>(value, needsRewrite, status);
        }
        catch (Exception ex)
        {
            logWarning?.Invoke($"Failed to load '{normalizedPath}'. Keeping the existing in-memory config. Error: {ex}");
            return new JsonConfigLoadResult<T>(null, false, JsonConfigLoadStatus.Failed);
        }
    }

    public static bool Save(string path, T value, JsonSerializerOptions options, Action<string>? logWarning = null)
    {
        var normalizedPath = Path.GetFullPath(path);
        EnsureDirectory(normalizedPath);
        var jsonData = JsonSerializer.Serialize(value, options);
        return FileWriteCoordinator.WriteAllText(normalizedPath, jsonData, logWarning);
    }

    private static T? TryDeserialize(
        string jsonData,
        Func<T> factory,
        JsonSerializerOptions options,
        Action<string>? logWarning,
        string normalizedPath,
        out bool usedRecoveryMerge,
        out bool loadFailed)
    {
        usedRecoveryMerge = false;
        loadFailed = false;

        try
        {
            return JsonSerializer.Deserialize<T>(jsonData, options) ?? factory();
        }
        catch (Exception ex)
        {
            logWarning?.Invoke($"Failed to fully deserialize '{normalizedPath}'. Attempting to recover compatible values. Error: {ex.Message}");
        }

        try
        {
            using var document = JsonDocument.Parse(jsonData, GetJsonDocumentOptions(options));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return factory();
            }

            var recoveredValue = factory();
            MergeObject(recoveredValue, document.RootElement, options);
            usedRecoveryMerge = true;
            return recoveredValue;
        }
        catch (Exception ex)
        {
            logWarning?.Invoke($"Failed to recover compatible config values from '{normalizedPath}': {ex.Message}");
            loadFailed = true;
            return null;
        }
    }

    private static bool NeedsRewrite(string currentJson, string rewrittenJson, JsonSerializerOptions options)
    {
        try
        {
            var documentOptions = GetJsonDocumentOptions(options);
            using var currentDocument = JsonDocument.Parse(currentJson, documentOptions);
            using var rewrittenDocument = JsonDocument.Parse(rewrittenJson, documentOptions);
            return !JsonElementsEqual(currentDocument.RootElement, rewrittenDocument.RootElement);
        }
        catch
        {
            return true;
        }
    }

    private static JsonDocumentOptions GetJsonDocumentOptions(JsonSerializerOptions options)
    {
        return new JsonDocumentOptions
        {
            AllowTrailingCommas = options.AllowTrailingCommas,
            CommentHandling = options.ReadCommentHandling == JsonCommentHandling.Skip
                ? JsonCommentHandling.Skip
                : JsonCommentHandling.Disallow
        };
    }

    private static bool JsonElementsEqual(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => ObjectsEqual(left, right),
            JsonValueKind.Array => ArraysEqual(left, right),
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            _ => string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal)
        };
    }

    private static bool ObjectsEqual(JsonElement left, JsonElement right)
    {
        var leftProperties = left.EnumerateObject().ToArray();
        var rightProperties = right.EnumerateObject().ToArray();
        if (leftProperties.Length != rightProperties.Length)
        {
            return false;
        }

        foreach (var leftProperty in leftProperties)
        {
            if (!right.TryGetProperty(leftProperty.Name, out var rightValue) ||
                !JsonElementsEqual(leftProperty.Value, rightValue))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ArraysEqual(JsonElement left, JsonElement right)
    {
        var leftItems = left.EnumerateArray().ToArray();
        var rightItems = right.EnumerateArray().ToArray();
        if (leftItems.Length != rightItems.Length)
        {
            return false;
        }

        for (var index = 0; index < leftItems.Length; index++)
        {
            if (!JsonElementsEqual(leftItems[index], rightItems[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static void MergeObject(object target, JsonElement source, JsonSerializerOptions options)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var sourceProperties = source.EnumerateObject()
            .GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

        foreach (var property in target.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0 ||
                !sourceProperties.TryGetValue(property.Name, out var sourceValue))
            {
                continue;
            }

            ApplyPropertyValue(target, property, sourceValue, options);
        }
    }

    private static void ApplyPropertyValue(object target, PropertyInfo property, JsonElement sourceValue, JsonSerializerOptions options)
    {
        var propertyType = property.PropertyType;
        if (sourceValue.ValueKind == JsonValueKind.Null)
        {
            if (!propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) != null)
            {
                property.SetValue(target, null);
            }

            return;
        }

        if (IsMergeableObjectType(propertyType) && sourceValue.ValueKind == JsonValueKind.Object)
        {
            var nestedTarget = property.GetValue(target) ?? Activator.CreateInstance(propertyType);
            if (nestedTarget != null)
            {
                MergeObject(nestedTarget, sourceValue, options);
                property.SetValue(target, nestedTarget);
                return;
            }
        }

        try
        {
            var deserializedValue = JsonSerializer.Deserialize(sourceValue.GetRawText(), propertyType, options);
            if (deserializedValue != null || propertyType.IsValueType)
            {
                property.SetValue(target, deserializedValue);
            }
        }
        catch
        {
            // Keep the factory default for mismatched properties.
        }
    }

    private static bool IsMergeableObjectType(Type type)
    {
        return type != typeof(string) &&
            !type.IsArray &&
            !type.IsAbstract &&
            !type.IsInterface &&
            type.IsClass &&
            !typeof(IEnumerable).IsAssignableFrom(type);
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
