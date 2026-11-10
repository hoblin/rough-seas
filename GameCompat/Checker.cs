using Mono.Cecil;

namespace GameCompat;

internal sealed class Checker
{
    private readonly string repository;
    private readonly GameRelease current;
    private readonly ModBuild currentBuild;

    private Checker(string repository, GameRelease current, ModBuild currentBuild)
    {
        this.repository = repository;
        this.current = current;
        this.currentBuild = currentBuild;
    }

    public static async Task<Checker> Start(string repository, string currentVersion)
    {
        var current = await GameRelease.Fetch(currentVersion);
        var build = ModBuild.Against(current, repository);
        if (!build.Succeeded) throw new InvalidOperationException($"The mod doesn't build against the current game {currentVersion}:\n{string.Join("\n", build.Errors)}");
        return new Checker(repository, current, build);
    }

    public async Task<CompatibilityReport> Check(string version)
    {
        using var release = await GameRelease.Fetch(version);
        var build = version == current.Version ? currentBuild : ModBuild.Against(release, repository);

        var report = new CompatibilityReport(version);
        using var mod = ModuleDefinition.ReadModule(currentBuild.Dll, new ReaderParameters { AssemblyResolver = release.Resolver });
        var reach = ModReach.Read(mod);
        var reliances = BehaviourCheck.Declared(mod);

        report.Add("Runtime", RuntimeReason(mod, release));
        report.Add("Build", build.Errors);
        report.Add("References", ReferenceCheck.Run(mod, release));
        report.Add("Harmony and reflection", new ReachCheck(release).Run(reach));
        report.Add("Asset patches", new AssetPatchCheck(release, repository).Run());

        if (version == current.Version)
        {
            report.Add("Declared reliances", BehaviourCheck.Unknown(reliances, current));
            report.Add("Declared reliances", BehaviourCheck.Undeclared(reach, reliances));
        }
        else if (report.NoHardFailures)
        {
            report.ToRead = await Unread(reliances, release);
        }

        return report;
    }

    private async Task<IReadOnlyList<BehaviourChange>> Unread(IReadOnlyList<Reliance> reliances, GameRelease release)
    {
        var read = ReadChanges.Load(repository);
        var unread = BehaviourCheck.Changes(reliances, current, release).Where(change => !read.Holds(release.Version, change)).ToList();
        if (unread.Count == 0) return [];

        var (from, to) = (await Decompiler.Run(release.Version), await Decompiler.Run(current.Version));
        return unread.Select(change => change with { SourceDiff = MethodSource.Diff(change.Reliance, from, to) }).ToList();
    }

    private static IEnumerable<string> RuntimeReason(ModuleDefinition mod, GameRelease release)
    {
        var target = mod.Assembly.CustomAttributes
            .First(attribute => attribute.AttributeType.Name == "TargetFrameworkAttribute")
            .ConstructorArguments[0].Value.ToString()!;
        var wanted = Version.Parse(target[(target.IndexOf('v') + 1)..]);
        var available = Version.Parse(release.RuntimeFramework["net".Length..]);
        if (available < wanted) yield return $"the game runs on .NET {available}, the mod targets .NET {wanted}";
    }
}
