using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GameCompat;

internal sealed record ModBuild(string Dll, IReadOnlyList<string> Errors)
{
    private static readonly Regex CompilerError = new(@"[^/\s]+\.cs\(\d+,\d+\): error \w+: [^\[]+", RegexOptions.Compiled);

    public bool Succeeded => Errors.Count == 0;

    public static ModBuild Against(GameRelease release, string repository)
    {
        var source = Path.Combine(GameRelease.CacheRoot, "builds", Path.GetFileName(repository), release.Version);
        Mirror(Path.Combine(repository, "RoughSeas"), source);

        var start = new ProcessStartInfo("dotnet", ["build", source, "--nologo", "-v", "q", "-c", "Release", $"-p:VINTAGE_STORY={release.Directory}"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var build = Process.Start(start)!;
        var stderr = build.StandardError.ReadToEndAsync();
        var output = build.StandardOutput.ReadToEnd();
        build.WaitForExit();

        var errors = CompilerError.Matches(output).Select(match => match.Value.Trim()).Distinct().Order().ToList();
        if (build.ExitCode != 0 && errors.Count == 0) errors.Add((output + stderr.Result).Trim());
        return new ModBuild(Path.Combine(source, "bin", "Release", "Mods", "mod", "RoughSeas.dll"), errors);
    }

    private static void Mirror(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var stale in Directory.EnumerateFileSystemEntries(to).Where(entry => !IsBuildOutput(entry)))
        {
            if (Directory.Exists(stale)) Directory.Delete(stale, recursive: true);
            else File.Delete(stale);
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, file);
            if (IsBuildOutput(relative.Split(Path.DirectorySeparatorChar)[0])) continue;
            var target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static bool IsBuildOutput(string entry) => Path.GetFileName(entry) is "bin" or "obj";
}
