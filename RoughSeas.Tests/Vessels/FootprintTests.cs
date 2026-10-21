using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class FootprintTests
{
    private static readonly HullShape Hull = new(HalfLength: 2, HalfBeam: 0.5, Depth: 0.6, CentreAlong: 0, CentreAcross: 0, Keel: 0);

    [Fact]
    public void TouchesAHullLyingAlongside()
    {
        Assert.True(At(0, 0, yaw: 0).Touches(At(0.9, 0.5, yaw: 0)));
    }

    [Fact]
    public void MissesAHullLyingClearAlongside()
    {
        Assert.False(At(0, 0, yaw: 0).Touches(At(1.1, 0.5, yaw: 0)));
    }

    [Fact]
    public void TurnsWithTheHeading()
    {
        var ahead = At(0, 2.3, yaw: Math.PI / 2);

        Assert.True(At(0, 0, yaw: 0).Touches(ahead));
        Assert.False(At(0, 0, yaw: Math.PI / 2).Touches(ahead));
    }

    [Fact]
    public void MissesAHullCrossingClearOfTheOthersCorner()
    {
        Assert.False(At(0, 0, yaw: 0).Touches(At(2.2, 3.5, yaw: Math.PI / 4)));
    }

    [Fact]
    public void TouchesAHullLaidAcrossTheOther()
    {
        Assert.True(At(0, 0, yaw: 0).Touches(At(0.5, 2.3, yaw: Math.PI / 2)));
    }

    private static Footprint At(double x, double z, double yaw) => Footprint.Of(Hull, x, z, yaw);
}
