using System.Text.RegularExpressions;
using Newtonsoft.Json;
using RoughSeas.Soundscape;

namespace RoughSeas.Tests.Soundscape;

public class SoundscapeConfigTests
{
    private static readonly string Assets = Path.Combine(Repository(), "RoughSeas", "assets", "roughseas");
    private static readonly SoundscapeConfig Shipped =
        JsonConvert.DeserializeObject<SoundscapeConfig>(File.ReadAllText(Path.Combine(Assets, "config", "soundscape.json")))!;

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

    [Fact]
    public void ReadsEverySoundItNamesWithItsLoudness()
    {
        var water = Shipped.Water;
        Cue[] cues = [Shipped.Crests.Hiss, Shipped.Crests.Plunge, water.Slap, water.StrongSlap, water.Slam, water.Pour, water.Slosh, water.Leak, water.Splash];
        Assert.All(cues, cue => Assert.False(string.IsNullOrEmpty(cue.Sound) || cue.Volume.Length == 0, $"a cue reads as {cue.Sound}, {cue.Volume.Length} points"));
        Assert.All(Shipped.Beds.Loops, loop => Assert.False(string.IsNullOrEmpty(loop.Sound) || loop.Share.Length == 0 || loop.Lufs >= 0));
        Assert.NotEmpty(Shipped.Shore.Surf);
        Assert.NotEmpty(Shipped.Shore.BreakingStrength);
    }

    [Theory]
    [InlineData("cracking", "wood")]
    [InlineData("cracking", "hide")]
    [InlineData("cracking", "bark")]
    [InlineData("lashed", "wood")]
    public void GivesAHullThatBreaksAKnockACrackAndABreakUp(string model, string material)
    {
        var voice = Shipped.Hull.Models[model][material];
        Assert.False(string.IsNullOrEmpty(voice.Knock));
        Assert.False(string.IsNullOrEmpty(voice.Harm));
        Assert.NotEmpty(voice.Break);
    }

    [Fact]
    public void GivesTheReedBoatACrunchAndAHardOne()
    {
        var voice = Shipped.Hull.Models["reed"]["reed"];
        Assert.False(string.IsNullOrEmpty(voice.Knock));
        Assert.False(string.IsNullOrEmpty(voice.Hard));
    }

    [Fact]
    public void HandsTheSurfFromBandToBandAsTheWavesRise()
    {
        var from = Shipped.Shore.Surf.Select(band => band.FromMetres).ToArray();
        Assert.Equal(from.Order(), from);
    }

    [Fact]
    public void NeverAsksTheSurfForMoreThanItsFileCanPlay()
    {
        for (var height = 0.0; height <= 10; height += 0.05)
        {
            var band = Shipped.Shore.Surf.LastOrDefault(band => band.FromMetres <= height);
            var strength = Curve.At(Shipped.Shore.BreakingStrength, height) * (band?.Gain ?? 1);
            Assert.True(strength <= 1 + 1e-9, $"surf in {height:F2} m waves asks for {strength:F2}");
        }
    }

    private static string Repository()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "RoughSeas", "assets"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("the repository above the test binaries");
    }
}
