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
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(13)]
    [InlineData(30)]
    public void KeepsTheSeaAtItsLevelThroughEveryCrossfade(double windMps)
    {
        var volumes = Curve.Mix(Shipped.Beds, windMps);
        var power = Math.Sqrt(volumes.Select((volume, i) => Math.Pow(volume * Math.Pow(10, Shipped.Beds.Loops[i].Lufs / 20), 2)).Sum());
        Assert.Equal(Curve.At(Shipped.Beds.Lufs, windMps), 20 * Math.Log10(power), 6);
    }

    [Fact]
    public void FadesFromTheModerateBedToTheStormBedAsTheWindRises()
    {
        var moderate = Curve.Mix(Shipped.Beds, 8);
        var storm = Curve.Mix(Shipped.Beds, 20);
        Assert.True(moderate[0] > moderate[^1]);
        Assert.True(storm[^1] > storm[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void PlaysNoBedInACalm(double windMps) => Assert.All(Curve.Mix(Shipped.Beds, windMps), volume => Assert.Equal(0, volume));

    [Fact]
    public void LeavesTheBedsSilentWhenNoLoopIsMeantToPlay()
    {
        var beds = new BedsConfig { Lufs = [[0, -20]], Loops = [new BedConfig { Share = [[0, 0]] }] };
        Assert.Equal([0.0], Curve.Mix(beds, 2));
    }

    [Fact]
    public void NeverAsksALoopForMoreThanItsFileCanPlay()
    {
        for (var wind = 0.0; wind <= 55; wind += 0.5)
        {
            Assert.All(Curve.Mix(Shipped.Beds, wind), volume => Assert.True(volume <= 1 + 1e-9, $"a bed loop asks for {volume:F2} in a {wind} m/s wind"));
        }
    }
}
