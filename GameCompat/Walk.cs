using Newtonsoft.Json.Linq;

namespace GameCompat;

internal sealed class Walk(string repository, string currentVersion)
{
    private const string ReleaseIndex = "https://api.vintagestory.at/stable-unstable.json";

    public async Task<int> Run()
    {
        var checker = await Checker.Start(repository, currentVersion);
        var compatible = new SortedSet<Version>();
        var incompatible = new SortedSet<Version>();

        foreach (var minor in Minors(await StableReleases()))
        {
            var broke = false;
            foreach (var version in minor)
            {
                await Decompiler.Run(version.ToString());
                var report = await checker.Check(version.ToString());
                report.Print();
                (report.Compatible ? compatible : incompatible).Add(version);
                broke |= !report.Compatible;
            }
            if (broke) break;
        }

        Console.WriteLine(Floor(compatible, incompatible) is { } floor ? $"Floor: {floor}" : "No version from the current one down is compatible.");
        return 0;
    }

    internal static Version? Floor(IEnumerable<Version> compatible, IEnumerable<Version> incompatible)
    {
        var worstBroken = incompatible.DefaultIfEmpty().Max();
        return compatible.Where(version => worstBroken == null || version > worstBroken).DefaultIfEmpty().Min();
    }

    private IEnumerable<IGrouping<(int, int), Version>> Minors(IEnumerable<Version> stable) =>
        stable.Where(version => version <= Version.Parse(currentVersion))
            .OrderBy(version => version)
            .GroupBy(version => (version.Major, version.Minor))
            .OrderByDescending(minor => minor.Key);

    private static async Task<IEnumerable<Version>> StableReleases()
    {
        using var http = new HttpClient();
        var index = JObject.Parse(await http.GetStringAsync(ReleaseIndex));
        return index.Properties().Select(release => release.Name).Where(name => !name.Contains('-')).Select(Version.Parse).ToList();
    }
}
