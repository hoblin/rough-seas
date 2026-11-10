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
        report.Add("References", ReferenceCheck.Run(mod, release, current));
        report.Add("Harmony and reflection", new ReachCheck(release).Run(reach));
        var patches = new AssetPatchCheck(release, current, repository).Run();
        report.Add("Asset patches", patches.Reasons);
        report.Add("Text anchors", TextAnchorCheck.Run(mod, release));
        var references = new GameReferenceCheck(release, repository).Run(mod);
        report.Add("Game references", references.Reasons);
        var read = ReadChanges.Load(repository);
        bool Unread(BehaviourChange change) => !read.Holds(version, change);

        if (version == current.Version)
        {
            report.Add("Declared reliances", BehaviourCheck.Unknown(reliances, current));
            report.Add("Declared reliances", BehaviourCheck.Undeclared(reach, reliances));
        }
        else if (report.NoHardFailures)
        {
            report.ToRead = (await WithSource(BehaviourCheck.Changes(reliances, current, release).Where(Unread).ToList(), release)).Concat(patches.Changes.Where(Unread)).ToList();
        }

        if (report.NoHardFailures) report.ToRead = report.ToRead.Concat(references.Changes.Where(Unread)).ToList();
        return report;
    }

    private async Task<IReadOnlyList<BehaviourChange>> WithSource(IReadOnlyList<BehaviourChange> changes, GameRelease release)
    {
        if (changes.Count == 0) return [];
        var (from, to) = (await Decompiler.Run(release.Version), await Decompiler.Run(current.Version));
        return changes.Select(change => change with { SourceDiff = MethodSource.Diff(change.Reliance, from, to) }).ToList();
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
