using System.Text.RegularExpressions;

namespace RoughSeas.Tests.Soundscape;

public class SoundscapeConfigTests
{
    private static readonly string Assets = Path.Combine(Repository(), "RoughSeas", "assets", "roughseas");

    public static TheoryData<string> OurSounds()
    {
        var config = File.ReadAllText(Path.Combine(Assets, "config", "soundscape.json"));
        var sounds = new TheoryData<string>();
        foreach (var path in Regex.Matches(config, "\"roughseas:(sounds/[^\"]+)\"").Select(match => match.Groups[1].Value).Distinct()) sounds.Add(path);
        return sounds;
    }

    [Theory]
    [MemberData(nameof(OurSounds))]
    public void PlaysOnlySoundsTheModShips(string path)
    {
        var found = path.EndsWith('*')
            ? Directory.Exists(Path.Combine(Assets, Path.GetDirectoryName(path)!)) && Directory.EnumerateFiles(Path.Combine(Assets, Path.GetDirectoryName(path)!), "*.ogg").Any()
            : File.Exists(Path.Combine(Assets, path + ".ogg"));
        Assert.True(found, $"config/soundscape.json plays roughseas:{path}, which RoughSeas/assets/roughseas does not ship");
    }

    private static string Repository()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "RoughSeas", "assets"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("the repository above the test binaries");
    }
}
