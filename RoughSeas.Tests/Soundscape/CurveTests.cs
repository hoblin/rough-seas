using Newtonsoft.Json;
using RoughSeas.Soundscape;

namespace RoughSeas.Tests.Soundscape;

public class CurveTests
{
    private static readonly SoundscapeConfig Shipped =
        JsonConvert.DeserializeObject<SoundscapeConfig>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "soundscape.json")))!;

    [Fact]
    public void ReadsBetweenItsPointsInAStraightLine()
    {
        double[][] curve = [[0, 0], [2, 1], [4, 0]];
        Assert.Equal(0.5, Curve.At(curve, 1), 6);
        Assert.Equal(0.25, Curve.At(curve, 3.5), 6);
    }

    [Fact]
    public void HoldsItsEndsBeyondItsPoints()
    {
        double[][] curve = [[1, 0.2], [3, 0.8]];
        Assert.Equal(0.2, Curve.At(curve, -5));
        Assert.Equal(0.8, Curve.At(curve, 10));
    }

    [Fact]
    public void IsSilentWithoutPoints() => Assert.Equal(0, Curve.At([], 1));

    [Theory]
    [InlineData(0)]
    [InlineData(1.2)]
    [InlineData(2)]
    [InlineData(3.5)]
    [InlineData(8)]
    public void KeepsTheBedsAtTheirLoudnessThroughEveryCrossfade(double waveHeight)
    {
        var volumes = Curve.Mix(Shipped.Beds, waveHeight);
        var power = Math.Sqrt(volumes.Sum(volume => volume * volume));
        Assert.Equal(Curve.At(Shipped.Beds.Volume, waveHeight), power, 6);
    }

    [Fact]
    public void FadesFromTheCalmBedToTheStormBedAsTheSeaRises()
    {
        var calm = Curve.Mix(Shipped.Beds, 0);
        var storm = Curve.Mix(Shipped.Beds, 6);
        Assert.True(calm[0] > calm[^1]);
        Assert.True(storm[^1] > storm[0]);
    }

    [Fact]
    public void LeavesTheBedsSilentWhenNoLoopIsMeantToPlay()
    {
        var beds = new BedsConfig { Volume = [[0, 1]], Loops = [new BedConfig { Share = [[0, 0]] }] };
        Assert.Equal([0.0], Curve.Mix(beds, 2));
    }

    [Theory]
    [InlineData("cracking", "wood")]
    [InlineData("cracking", "hide")]
    [InlineData("cracking", "bark")]
    [InlineData("lashed", "wood")]
    [InlineData("reed", "reed")]
    public void GivesEveryHullModelAndItsMaterialsAVoice(string model, string material)
    {
        var voice = Shipped.Hull.Models[model][material];
        Assert.False(string.IsNullOrEmpty(voice.Knock));
    }
}
