using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class WindOnCoordinatesTests
{
    private static SeaState WindFrom(double degrees, double mps, int beaufort)
    {
        var (towardX, towardZ) = Compass.Toward((degrees + 180) * Math.PI / 180);
        return new SeaState(towardX * mps, towardZ * mps, beaufort, 0, 0, 0, null);
    }

    [Theory]
    [InlineData(255, 11.2, 6, "WSW 6 Bft · 11 m/s")]
    [InlineData(358, 3.4, 2, "N 2 Bft · 3 m/s")]
    [InlineData(90, 25, 10, "E 10 Bft · 25 m/s")]
    public void NamesTheWindByItsPointForceAndSpeed(double fromDegrees, double mps, int beaufort, string line)
    {
        Assert.Equal(line, WindOnCoordinates.Describe(WindFrom(fromDegrees, mps, beaufort)));
    }
}
