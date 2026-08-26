namespace BlackVeinCore.Persistence;

public sealed record JsonConfigFieldPatch(
    IReadOnlyList<string> Path,
    string Name,
    string ValueJson,
    IReadOnlyList<string>? CommentLines = null);
