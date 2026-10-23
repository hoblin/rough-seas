using RoughSeas.SeaAndWeather;
using Vintagestory.API.Util;

namespace RoughSeas.Tests.SeaAndWeather;

public class SeaMapSnapshotTests
{
    private static readonly SeaSample Gale = new(-18.37, 7.21, 0.836, 1.21, 4.37, 8.9, 0.412, -0.077, 14.62);

    [Fact]
    public void CarriesEveryCellToTheClient()
    {
        var samples = Enumerable.Range(0, 16 * 16).Select(i => Gale with { WaveHeight = i * 0.01 }).ToArray();
        var sent = SerializerUtil.Deserialize<SeaMapSnapshot>(SerializerUtil.Serialize(SeaMapSnapshot.Of(3, -2, 1234.5, 11.2, 16, samples)));
        var received = sent.Samples();

        Assert.Equal((3, -2, 1234.5, 16), (sent.RegionX, sent.RegionZ, sent.TotalHours, sent.CellsPerSide));
        Assert.Equal(11.2, sent.RegionMps, 4);
        Assert.Equal(samples.Length, received.Length);
        Assert.All(Enumerable.Range(0, samples.Length), i => Assert.Equal(samples[i].WaveHeight, received[i].WaveHeight, 2));
    }

    [Fact]
    public void CarriesTheWavesOfEachSlotAroundTheSnapshot()
    {
        WaveGeneration[] generations = [new(0.3, 6.5), default, new(-2.1, 9.25)];
        var sent = SerializerUtil.Deserialize<SeaMapSnapshot>(SerializerUtil.Serialize(SeaMapSnapshot.Of(0, 0, 0, 0, 1, [Gale], 386, generations)));

        Assert.Equal(386, sent.FirstWaveSlot);
        Assert.Equal(generations, sent.Generations());
    }

    [Fact]
    public void KeepsEachValueToThePrecisionItNeeds()
    {
        var received = SeaMapSnapshot.Of(0, 0, 0, 0, 1, [Gale]).Samples()[0];

        Assert.Equal(Gale.WindX, received.WindX, 2);
        Assert.Equal(Gale.WindZ, received.WindZ, 2);
        Assert.Equal(Gale.StormPresence, received.StormPresence, 3);
        Assert.Equal(Gale.RainCloudness, received.RainCloudness, 3);
        Assert.Equal(Gale.WavePeriod, received.WavePeriod, 2);
        Assert.Equal(Gale.CurrentX, received.CurrentX, 3);
        Assert.Equal(Gale.CurrentZ, received.CurrentZ, 3);
        Assert.Equal(Gale.WaterCelsius, received.WaterCelsius, 2);
    }

    [Fact]
    public void StaysWithinAFewKilobytesARegion()
    {
        var samples = Enumerable.Repeat(Gale, 16 * 16).ToArray();

        Assert.True(SerializerUtil.Serialize(SeaMapSnapshot.Of(0, 0, 0, 0, 16, samples)).Length < 5000);
    }
}
