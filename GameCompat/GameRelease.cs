using System.Formats.Tar;
using System.IO.Compression;
using Mono.Cecil;
using Newtonsoft.Json.Linq;

namespace GameCompat;

internal sealed class GameRelease : IDisposable
{
    private static readonly string[] AssemblyFolders = ["", "Mods", "Lib"];
    private static readonly string[] ContentAssemblies = ["VintagestoryAPI", "VintagestoryLib", "VSEssentials", "VSSurvivalMod", "VSCreativeMod"];

    private readonly DefaultAssemblyResolver resolver = new();

    public GameRelease(string version, string directory)
    {
        Version = version;
        Directory = directory;
        foreach (var searched in resolver.GetSearchDirectories()) resolver.RemoveSearchDirectory(searched);
        foreach (var folder in AssemblyFolders) resolver.AddSearchDirectory(Path.Combine(directory, folder));
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
    }

    public string Version { get; }

    public string Directory { get; }

    public IAssemblyResolver Resolver => resolver;

    public string RuntimeFramework =>
        (string)JObject.Parse(File.ReadAllText(Path.Combine(Directory, "VintagestoryServer.runtimeconfig.json")))["runtimeOptions"]!["tfm"]!;

    public bool Ships(string assemblyName) =>
        AssemblyFolders.Any(folder => File.Exists(Path.Combine(Directory, folder, assemblyName + ".dll")));

    public TypeDefinition? FindType(string fullName)
    {
        foreach (var name in ContentAssemblies.Where(Ships))
        {
            var type = resolver.Resolve(AssemblyNameReference.Parse(name)).MainModule.GetType(fullName);
            if (type != null) return type;
        }
        return null;
    }

    public string? FindAsset(string path) =>
        new[] { "survival", "creative", "game" }
            .Select(origin => Path.Combine(Directory, "assets", origin, path))
            .FirstOrDefault(File.Exists);

    public static async Task<GameRelease> Fetch(string version)
    {
        var directory = Path.Combine(CacheRoot, "releases", version);
        if (!System.IO.Directory.Exists(directory)) await Download(version, directory);
        return new GameRelease(version, directory);
    }

    public static string CacheRoot =>
        Path.Combine(Environment.GetEnvironmentVariable("XDG_CACHE_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache"), "rough-seas-compat");

    public void Dispose() => resolver.Dispose();

    private static async Task Download(string version, string directory)
    {
        var partial = directory + ".partial";
        if (System.IO.Directory.Exists(partial)) System.IO.Directory.Delete(partial, recursive: true);

        using var http = new HttpClient();
        await using var download = await http.GetStreamAsync($"https://cdn.vintagestory.at/gamefiles/stable/vs_server_linux-x64_{version}.tar.gz");
        await using var unzipped = new GZipStream(download, CompressionMode.Decompress);
        await using var tar = new TarReader(unzipped);

        while (await tar.GetNextEntryAsync() is { } entry)
        {
            if (entry.EntryType != TarEntryType.RegularFile || !IsNeeded(entry.Name)) continue;
            var target = Path.Combine(partial, entry.Name.TrimStart('.', '/'));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await entry.ExtractToFileAsync(target, overwrite: true);
        }

        System.IO.Directory.Move(partial, directory);
    }

    private static bool IsNeeded(string entry) => entry.EndsWith(".dll") || entry.EndsWith(".json");
}
