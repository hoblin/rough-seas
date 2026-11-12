using RoughSeas.Storms;
using RoughSeas.Tests.Storms;
using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class HullDragCurrentTests
{
    private const double FrameSeconds = 1 / 60.0;
    private const float Westward = -(float)Math.PI / 2;

    private static HullDrag Dugout(double cargoKg = 0) =>
        Fixtures.DragOf("dugout", Fixtures.Hull("dugout").Crewed(containers: cargoKg));

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.6)]
    [InlineData(1.2)]
    public void APullAlongALineChangesHerSpeedAlongItByItOverTheMassItMeets(double angle)
    {
        var drag = Dugout();
        var (unitX, unitZ) = (Math.Cos(angle), Math.Sin(angle));
        const double change = 0.4;
        var kilograms = drag.KilogramsToward(unitX, unitZ, 0.3);
        var (x, z) = drag.Acceleration(change * kilograms * unitX, change * kilograms * unitZ, 0.3);

        Assert.Equal(change, x * unitX + z * unitZ, 9);
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
        for (var t = 0.0; t < 240; t += FrameSeconds)
        {
            carried = 2 - drag.Settle(2 - carried, 0, 0, FrameSeconds).X;
            Assert.True(carried <= 2);
        }
        Assert.InRange(carried, 1.9, 2);
    }

    [Fact]
    public void ASecondsStepInOneGoAndInManyTakeHerAboutAsFar()
    {
        var drag = Dugout();
        var stepped = 2.0;
        for (var i = 0; i < 60; i++) stepped = drag.Settle(stepped, 0, 0, FrameSeconds).X;
        Assert.Equal(stepped, drag.Settle(2, 0, 0, 1).X, 2);
    }

    [Fact]
    public void ALoadedHullTakesOnTheCurrentSlowerThanAnEmptyOne()
    {
        Assert.True(Carried(Dugout(cargoKg: 300), 0, seconds: 2) < Carried(Dugout(), 0, seconds: 2));
    }

    [Fact]
    public void AHullLyingAcrossTheStreamIsCarriedSoonerThanOneLyingAlongIt()
    {
        Assert.True(Carried(Dugout(), Westward, seconds: 2) < Carried(Dugout(), 0, seconds: 2));
    }

    [Fact]
    public void TheCurrentPullsWithTheSquareOfItsSpeedAcrossTheHull()
    {
        var drag = Dugout();
        Assert.Equal(44.4, drag.Pull(2, 0, 0).X / drag.Pull(0.3, 0, 0).X, 0.1);
    }

    [Fact]
    public void AHeldHullLyingAlongTheStreamFeelsLittleOfIt()
    {
        var drag = Dugout();
        Assert.True(Math.Abs(drag.Pull(2, 0, Westward).X) < Math.Abs(drag.Pull(2, 0, 0).X) / 10);
    }

    [Fact]
    public void TheWaterPullsAHullTheWayItRuns()
    {
        var drag = Dugout();
        Assert.True(drag.Pull(1, 0, 0).X > 0);
        Assert.True(drag.Pull(1, 0, Westward).X > 0);
        Assert.True(drag.Pull(-1, 0, Westward).X < 0);
    }

    [Fact]
    public void AnOutriggersFloatResistsTheCurrentBesideHerHull()
    {
        var aboard = Fixtures.Hull("outrigger").Crewed();
        Assert.True(Fixtures.DragOf("outrigger", aboard).Pull(1, 0, 0).X > Dugout().Pull(1, 0, 0).X);
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
    [InlineData("boat-raft")]
    [InlineData("boat-sailed")]
    [InlineData("coracle")]
    public void EveryHullSwingsToLieAlongTheStreamFromARoughStep(string vessel)
    {
        var drag = Fixtures.DragOf(vessel, Fixtures.Hull(vessel).Crewed());
        var yaw = 0.0;
        var turn = 0.0;
        for (var t = 0.0; t < 600; t += 0.2)
        {
            turn = drag.Swing(2, 0, (float)yaw, turn, 0.2);
            yaw += turn * 0.2;
            Assert.InRange(turn, -2, 2);
        }
        Assert.True(-Math.Sin(yaw) > 0.9, $"{vessel}: yaw {yaw:F2}");
    }

    [Fact]
    public void TheBowLiesWhereABoatMoves()
    {
        var shape = Fixtures.Shape("boat-sailed");
        var (x, z) = shape.Bow(0);
        Assert.True(z > 0, $"bow at {x:F2} {z:F2}");
        Assert.Equal(shape.HalfLengthMetres - shape.CentreAlongMetres, z, 6);
    }

    private static double Carried(HullDrag drag, float yaw, double seconds)
    {
        var carried = 0.0;
        for (var t = 0.0; t < seconds; t += FrameSeconds) carried = 1 - drag.Settle(1 - carried, 0, yaw, FrameSeconds).X;
        return carried;
    }
}
