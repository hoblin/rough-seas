using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class OceanCurrentsTests
{
    private const int North = -1;
    private const int South = 1;
    private const double Air = 15;

    private static readonly OceanCurrents Currents = new(Fixtures.Config().Currents);
    private static readonly double Reach = Fixtures.Config().Currents.BasinReachBlocks;

    private static Basin Ocean(double fromWest) => new(2 * Reach * fromWest, 2 * Reach * (1 - fromWest));

    [Fact]
    public void RunsAWarmCurrentPolewardAlongTheWestOfASubtropicalOcean()
    {
        var gulfStream = Currents.At(35, North, Ocean(0.01), Air);

        Assert.InRange(-gulfStream.Z, 1.5, 2.5);
        Assert.True(gulfStream.WaterCelsius > Air + 4, $"water {gulfStream.WaterCelsius} °C");
    }

    [Fact]
    public void RunsAColdCurrentTowardsTheEquatorAlongTheEastOfASubtropicalOcean()
    {
        var canary = Currents.At(30, North, Ocean(0.99), Air);

        Assert.InRange(canary.Z, 0.1, 0.3);
        Assert.True(canary.WaterCelsius < Air - 3, $"water {canary.WaterCelsius} °C");
    }

    [Fact]
    public void RunsACurrentFromThePoleAlongTheWestOfASubpolarOcean()
    {
        var labrador = Currents.At(58, North, Ocean(0.01), Air);

        Assert.True(labrador.Z > 1, $"towards south at {labrador.Z} m/s");
        Assert.True(labrador.WaterCelsius < Air - 3, $"water {labrador.WaterCelsius} °C");
    }

    [Fact]
    public void RunsAWarmCurrentPolewardAlongTheEastOfASubpolarOcean()
    {
        var norwegian = Currents.At(60, North, Ocean(0.99), Air);

        Assert.True(norwegian.Z < 0, "towards north");
        Assert.True(norwegian.WaterCelsius > Air + 3, $"water {norwegian.WaterCelsius} °C");
    }

    [Fact]
    public void FlowsWestUnderTheTradesAndEastUnderTheWesterlies()
    {
        Assert.True(Currents.At(15, North, Ocean(0.5), Air).X < -0.1, "trades");
        Assert.True(Currents.At(45, North, Ocean(0.5), Air).X > 0.1, "westerlies");
    }

    [Fact]
    public void MirrorsTheGyresInTheSouth()
    {
        var north = Currents.At(35, North, Ocean(0.01), Air);
        var brazil = Currents.At(-35, South, Ocean(0.01), Air);

        Assert.Equal(north.X, brazil.X, 6);
        Assert.Equal(-north.Z, brazil.Z, 6);
        Assert.Equal(north.WaterCelsius, brazil.WaterCelsius, 6);
    }

    [Fact]
    public void RunsWeakerCurrentsInSmallerSeas()
    {
        var ocean = Currents.At(35, North, Ocean(0.01), Air);
        var sea = Currents.At(35, North, new Basin(0.01 * Reach / 4, 0.99 * Reach / 4), Air);

        Assert.True(sea.Speed < ocean.Speed / 4, $"{sea.Speed} m/s in the sea, {ocean.Speed} m/s in the ocean");
    }

    [Fact]
    public void LeavesLandStillAtTheAirsTemperature()
    {
        Assert.Equal(new SeaCurrent(0, 0, Air, 0), Currents.At(35, North, default, Air));
    }

    [Fact]
    public void NeverCoolsSeawaterBelowItsFreezingPoint()
    {
        var freezing = Fixtures.Config().Currents.SeawaterFreezingCelsius;

        Assert.Equal(freezing, Currents.At(58, North, Ocean(0.01), -20).WaterCelsius);
    }
}
