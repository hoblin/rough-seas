using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class HullDragTests
{
    private const double FrameSeconds = 1 / 60.0;
    private const float AlongX = (float)(-Math.PI / 2);

    private static HullDrag Dugout(double cargoKg = 0)
    {
        var hull = Fixtures.Hull("dugout");
        return Fixtures.Drag("dugout", hull.Crewed(containers: cargoKg));
    }

    [Fact]
    public void AHullAtRestInStillWaterStaysAtRest()
    {
        Assert.Equal((0.0, 0.0), Dugout().Settle(0, 0, 0, 1));
    }

    [Fact]
    public void TheCurrentCarriesAHullUpToItsOwnSpeedAndNoFurther()
    {
        var drag = Dugout();
        var carried = 0.0;
        for (var t = 0.0; t < 120; t += FrameSeconds)
        {
            carried = 2 - drag.Settle(2 - carried, 0, 0, FrameSeconds).X;
            Assert.True(carried <= 2);
        }
        Assert.InRange(carried, 1.95, 2);
    }

    [Fact]
    public void ALoadedHullTakesOnTheCurrentSlowerThanAnEmptyOne()
    {
        Assert.True(Carried(Dugout(cargoKg: 300), 0, seconds: 2) < Carried(Dugout(), 0, seconds: 2));
    }

    [Fact]
    public void AHullLyingAcrossTheStreamIsCarriedSoonerThanOneLyingAlongIt()
    {
        Assert.True(Carried(Dugout(), AlongX, seconds: 2) < Carried(Dugout(), 0, seconds: 2));
    }

    [Fact]
    public void TheCurrentPullsWithTheSquareOfItsSpeed()
    {
        var drag = Dugout();
        Assert.Equal(44.4, drag.Pull(2, 0, 0).X / drag.Pull(0.3, 0, 0).X, 0.1);
    }

    [Fact]
    public void AHeldHullLyingAlongTheStreamFeelsLittleOfIt()
    {
        var drag = Dugout();
        Assert.True(Math.Abs(drag.Pull(2, 0, AlongX).X) < Math.Abs(drag.Pull(2, 0, 0).X) / 10);
    }

    [Fact]
    public void AnOutriggersFloatResistsTheCurrentBesideHerHull()
    {
        var aboard = Fixtures.Hull("outrigger").Crewed();
        Assert.True(Fixtures.Drag("outrigger", aboard).Pull(1, 0, 0).X > Fixtures.Drag("dugout", Fixtures.Hull("dugout").Crewed()).Pull(1, 0, 0).X);
    }

    [Fact]
    public void AHeldHullSwingsSternDownstreamUntilSheLiesAlongTheStream()
    {
        var drag = Dugout();
        var yaw = 0.0;
        var turn = 0.0;
        for (var t = 0.0; t < 300; t += 0.05)
        {
            turn = drag.Swing(1, 0, (float)yaw, turn, 0.05);
            yaw += turn * 0.05;
        }
        var (sternX, sternZ) = (-Math.Sin(yaw), -Math.Cos(yaw));
        Assert.True(sternX > 0.98, $"stern points ({sternX:F2}, {sternZ:F2})");
    }

    [Theory]
    [InlineData(1, 0.5)]
    [InlineData(2, 0.210)]
    [InlineData(10, 0.021)]
    public void TheWaterAHullDragsAlongHerMatchesLambsSpheroid(double slenderness, double share)
    {
        Assert.Equal(share, HullDrag.SurgeAddedShare(slenderness), 3);
    }

    private static double Carried(HullDrag drag, float yaw, double seconds)
    {
        var carried = 0.0;
        for (var t = 0.0; t < seconds; t += FrameSeconds) carried = 1 - drag.Settle(1 - carried, 0, yaw, FrameSeconds).X;
        return carried;
    }
}
