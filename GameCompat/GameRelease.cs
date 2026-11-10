using System.Formats.Tar;
using System.IO.Compression;
using Mono.Cecil;
using Newtonsoft.Json.Linq;

namespace GameCompat;

internal sealed class GameRelease : IDisposable
{
    private static readonly string[] AssemblyFolders = ["", "Mods", "Lib"];
    private static readonly string[] ContentAssemblies = ["VintagestoryAPI", "VintagestoryLib", "VSEssentials", "VSSurvivalMod", "VSCreativeMod"];

    private const string AssetListName = "asset-files.txt";
    private const string ArchiveRoot = "vintagestory/";

    private readonly DefaultAssemblyResolver resolver = new();
    private string[]? assetFiles;

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
        (string)JObject.Parse(File.ReadAllText(Path.Combine(Directory, "Vintagestory.runtimeconfig.json")))["runtimeOptions"]!["tfm"]!;

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

    public IReadOnlyList<string> AssetFiles => assetFiles ??= File.ReadAllLines(Path.Combine(Directory, AssetListName));

    public string? FindAsset(string path) =>
        new[] { "survival", "creative", "game" }
            .Select(origin => Path.Combine(Directory, "assets", origin, path))
            .FirstOrDefault(File.Exists);

    public static async Task<GameRelease> Fetch(string version)
    {
        var directory = Path.Combine(CacheRoot, "releases", version);
        if (!File.Exists(Path.Combine(directory, AssetListName)))
        {
            if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, recursive: true);
            await Download(version, directory);
        }
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
        await using var download = await http.GetStreamAsync($"https://cdn.vintagestory.at/gamefiles/stable/vs_client_linux-x64_{version}.tar.gz");
        await using var unzipped = new GZipStream(download, CompressionMode.Decompress);
        await using var tar = new TarReader(unzipped);

        var assets = new List<string>();
        while (await tar.GetNextEntryAsync() is { } entry)
        {
            if (entry.EntryType != TarEntryType.RegularFile) continue;
            var name = entry.Name.TrimStart('.', '/');
            if (name.StartsWith(ArchiveRoot)) name = name[ArchiveRoot.Length..];
            if (AssetPath(name) is { } asset) assets.Add(asset);
            if (!IsNeeded(name)) continue;
            var target = Path.Combine(partial, name);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await entry.ExtractToFileAsync(target, overwrite: true);
        }

        await File.WriteAllLinesAsync(Path.Combine(partial, AssetListName), assets);
        System.IO.Directory.Move(partial, directory);
    }

    private static string? AssetPath(string entry)
    {
        var parts = entry.Split('/', 3);
        return parts.Length == 3 && parts[0] == "assets" ? parts[2] : null;
    }

    private static bool IsNeeded(string entry) => entry.EndsWith(".dll") || entry.EndsWith(".json") || entry.EndsWith(".vsh") || entry.EndsWith(".fsh");
}
