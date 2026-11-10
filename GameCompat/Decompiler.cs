using System.Diagnostics;

namespace GameCompat;

internal static class Decompiler
{
    private static readonly string[] Assemblies = ["VintagestoryAPI", "VintagestoryLib", "Mods/VSEssentials", "Mods/VSSurvivalMod"];

    public static async Task<string> Run(string version)
    {
        var folder = Path.Combine(GameRelease.CacheRoot, "decompiled", version);
        if (Directory.Exists(folder)) return folder;

        using var release = await GameRelease.Fetch(version);
        var partial = GameRelease.PartialOf(folder);

        foreach (var assembly in Assemblies)
        {
            var output = Path.Combine(partial, Path.GetFileName(assembly));
            Directory.CreateDirectory(output);
            var start = new ProcessStartInfo("dotnet", ["ilspycmd", "--project", "-o", output, Path.Combine(release.Directory, assembly + ".dll"),
                "-r", release.Directory, "-r", Path.Combine(release.Directory, "Lib"), "-r", Path.Combine(release.Directory, "Mods")])
            {
                RedirectStandardOutput = true,
            };
            using var decompile = Process.Start(start)!;
            await decompile.StandardOutput.ReadToEndAsync();
            await decompile.WaitForExitAsync();
            if (decompile.ExitCode != 0) throw new InvalidOperationException($"ilspycmd failed on {assembly}");
        }

        GameRelease.Publish(partial, folder);
        return folder;
    }
}
