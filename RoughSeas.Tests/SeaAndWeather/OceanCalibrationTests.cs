using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class OceanCalibrationTests
{
    private static readonly OceanConfig Ocean = Fixtures.Config().Ocean;

    private static OceanCalibration Built(int coastValue)
    {
        var calibration = new OceanCalibration(Ocean);
        for (var i = 0; i < Ocean.CalibrationSamples * 30; i++)
        {
            var value = i * 37 % 256;
            calibration.Add(value, value >= coastValue);
        }
        return calibration;
    }

    [Fact]
    public void ReadsTheDefaultUntilTheBuiltWorldHasShownBothSeaAndLand()
    {
        var calibration = new OceanCalibration(Ocean);
        for (var i = 0; i < Ocean.CalibrationSamples * 4; i++) calibration.Add(200, water: true);

        Assert.False(calibration.Settled);
        Assert.Equal(Ocean.DefaultSeaFrom, calibration.SeaFrom);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(25)]
    public void FindsWhereTheBuiltWorldHasItsSea(int coastValue)
    {
        var calibration = Built(coastValue);

        Assert.True(calibration.Settled);
        Assert.InRange(calibration.SeaFrom, coastValue - 16, coastValue + 16);
    }

    [Fact]
    public void HoldsItsReadingOnceSettled()
    {
        var calibration = Built(25);
        var settled = calibration.SeaFrom;
        for (var i = 0; i < 10000; i++) calibration.Add(100, water: false);

        Assert.Equal(settled, calibration.SeaFrom);
    }

    [Fact]
    public void KeepsItsReadingInTheSave()
    {
        var saved = Built(25);
        var loaded = new OceanCalibration(Ocean);
        loaded.Load(saved.ToBytes());

        Assert.True(loaded.Settled);
        Assert.Equal(saved.SeaFrom, loaded.SeaFrom);
    }
}
