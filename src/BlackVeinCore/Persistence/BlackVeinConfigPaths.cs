using BepInEx;

namespace BlackVeinCore.Persistence;

public static class BlackVeinConfigPaths
{
    public static string ConfigDirectory => Path.Combine(
        Paths.ConfigPath,
        "BlackVeinMods");
}
