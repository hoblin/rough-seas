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
        return await CompatibilityReport.For(release, build, current, currentBuild, repository);
    }
}
