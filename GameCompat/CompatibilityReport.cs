using Mono.Cecil;

namespace GameCompat;

internal sealed class CompatibilityReport
{
    private readonly List<(string Section, string Reason)> reasons = [];

    private CompatibilityReport(string version) => Version = version;

    public string Version { get; }

    public IReadOnlyList<BehaviourChange> ToRead { get; private set; } = [];

    public bool LoadsAndBuilds => reasons.Count == 0;

    public bool Compatible => LoadsAndBuilds && ToRead.Count == 0;

    public static async Task<CompatibilityReport> For(GameRelease release, ModBuild build, GameRelease current, ModBuild currentBuild, string repository)
    {
        var report = new CompatibilityReport(release.Version);

        using var mod = ModuleDefinition.ReadModule(currentBuild.Dll, new ReaderParameters { AssemblyResolver = release.Resolver });
        var reach = ModReach.Read(mod);
        var reliances = BehaviourCheck.Declared(mod);

        report.Add("Runtime", RuntimeReason(mod, release));
        report.Add("Build", build.Errors);
        report.Add("References", ReferenceCheck.Run(mod, release));
        report.Add("Harmony and reflection", new ReachCheck(release).Run(reach));
        report.Add("Asset patches", new AssetPatchCheck(release, repository).Run());

        if (release.Version == current.Version)
        {
            report.Add("Declared reliances", BehaviourCheck.Unknown(reliances, current));
            report.Add("Declared reliances", BehaviourCheck.Undeclared(reach, reliances));
        }
        else if (report.LoadsAndBuilds)
        {
            var read = ReadChanges.Load(repository);
            var unread = BehaviourCheck.Changes(reliances, current, release).Where(change => !read.Holds(release.Version, change)).ToList();
            if (unread.Count > 0)
            {
                var (from, to) = (await Decompiler.Run(release.Version), await Decompiler.Run(current.Version));
                report.ToRead = unread.Select(change => change with { SourceDiff = MethodSource.Diff(change.Reliance, from, to) }).ToList();
            }
        }

        return report;
    }

    public void Print()
    {
        Console.WriteLine($"{Version}: {(!LoadsAndBuilds ? "incompatible" : ToRead.Count > 0 ? "needs reading" : "compatible")}");
        foreach (var group in reasons.GroupBy(reason => reason.Section))
        {
            Console.WriteLine($"  {group.Key}:");
            foreach (var (_, reason) in group) Console.WriteLine($"    {reason}");
        }

        foreach (var change in ToRead) Print(change);
    }

    private void Print(BehaviourChange change)
    {
        Console.WriteLine($"  {Version} {change.Reliance.Target} {change.Stamp}");
        Console.WriteLine($"    we rely on: {change.Reliance.What}");
        if (change.Gone) Console.WriteLine("    the method does not exist in this version");
        Console.WriteLine(change.SourceDiff);
    }

    private void Add(string section, IEnumerable<string> found) => reasons.AddRange(found.Select(reason => (section, reason)));

    private static IEnumerable<string> RuntimeReason(ModuleDefinition mod, GameRelease release)
    {
        var target = mod.Assembly.CustomAttributes
            .First(attribute => attribute.AttributeType.Name == "TargetFrameworkAttribute")
            .ConstructorArguments[0].Value.ToString()!;
        var wanted = System.Version.Parse(target[(target.IndexOf('v') + 1)..]);
        var available = System.Version.Parse(release.RuntimeFramework["net".Length..]);
        if (available < wanted) yield return $"the game runs on .NET {available}, the mod targets .NET {wanted}";
    }
}
