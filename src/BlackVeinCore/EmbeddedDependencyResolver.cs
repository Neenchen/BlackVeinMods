using System.Reflection;
using System.Runtime.Loader;

namespace BlackVeinCore;

internal static class EmbeddedDependencyResolver
{
    private const string LiteDbAssemblyName = "LiteDB";
    private const string LiteDbResourceName = "BlackVeinCore.Dependencies.LiteDB.dll";

    private static readonly object SyncRoot = new();
    private static bool _initialized;
    private static Assembly? _liteDbAssembly;

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromEmbeddedResource;
            var loadContext = AssemblyLoadContext.GetLoadContext(typeof(EmbeddedDependencyResolver).Assembly);
            if (loadContext != null)
            {
                loadContext.Resolving += ResolveFromEmbeddedResource;
            }

            _initialized = true;
        }
    }

    private static Assembly? ResolveFromEmbeddedResource(object? sender, ResolveEventArgs args)
    {
        return ResolveFromEmbeddedResource(new AssemblyName(args.Name));
    }

    private static Assembly? ResolveFromEmbeddedResource(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        return ResolveFromEmbeddedResource(assemblyName);
    }

    private static Assembly? ResolveFromEmbeddedResource(AssemblyName assemblyName)
    {
        if (!string.Equals(assemblyName.Name, LiteDbAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        lock (SyncRoot)
        {
            if (_liteDbAssembly != null)
            {
                return _liteDbAssembly;
            }

            var ownerAssembly = typeof(EmbeddedDependencyResolver).Assembly;
            using var stream = ownerAssembly.GetManifestResourceStream(LiteDbResourceName);
            if (stream == null)
            {
                Plugin.Logger?.LogError($"Embedded dependency resource '{LiteDbResourceName}' was not found.");
                return null;
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            _liteDbAssembly = Assembly.Load(memoryStream.ToArray());
            return _liteDbAssembly;
        }
    }
}
