using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class RamsTests
{
    private const double PerTick = 1 / 60.0;
    private const long Other = 7;

    private readonly Rams rams = new(give: 1);

    [Fact]
    public void RamsABodyItRunsIntoAtTheClosingSpeed()
    {
        var knocked = Assert.Single(rams.Step([Touching(towardX: 2, closingX: 3)]));

        Assert.Equal(3, knocked.SpeedLost, 6);
        Assert.Equal(Other, knocked.Against);
    }

    [Fact]
    public void CountsOnlyTheSpeedAlongTheLineBetweenThem()
    {
        var knocked = Assert.Single(rams.Step([new Contact(Other, 3, 4, 0, 5 * PerTick)]));

        Assert.Equal(4, knocked.SpeedLost, 6);
    }

    [Fact]
    public void PointsTheKnockTowardTheBodyRammed()
    {
        var knocked = Assert.Single(rams.Step([new Contact(Other, 0, -2, 0, -3 * PerTick)]));

        Assert.Equal(0, knocked.TowardX, 6);
        Assert.Equal(-3, knocked.TowardZ, 6);
    }

    [Fact]
    public void CarriesTheWeightOfTheHullRammedWithTheRam()
    {
        var knocked = Assert.Single(rams.Step([new Contact(Other, 2, 0, 3 * PerTick, 0, TheirKg: 400)]));

        Assert.Equal(400, knocked.TheirKg);
    }

    [Fact]
    public void IgnoresABodyDriftingAgainstTheHullUnderItsGive()
    {
        Assert.Empty(rams.Step([Touching(towardX: 2, closingX: 0.8)]));
    }

    [Fact]
    public void IgnoresABodyDrawingAway()
    {
        Assert.Empty(rams.Step([Touching(towardX: 2, closingX: -3)]));
    }

    [Fact]
    public void RamsOncePerContact()
    {
        rams.Step([Touching(towardX: 2, closingX: 3)]);

        Assert.Empty(rams.Step([Touching(towardX: 1.5, closingX: 3)]));
    }

    [Fact]
    public void RamsAgainOnceTheyHaveParted()
    {
        rams.Step([Touching(towardX: 2, closingX: 3)]);
        rams.Step([]);

        Assert.Single(rams.Step([Touching(towardX: 2, closingX: 3)]));
    }

    [Fact]
    public void RamsAfreshAfterItStoppedMeasuring()
    {
        rams.Step([Touching(towardX: 2, closingX: 3)]);
        rams.Forget();

        Assert.Single(rams.Step([Touching(towardX: 2, closingX: 3)]));
    }

    [Fact]
    public void TwoEqualHullsShareTheEnergyTheirContactTakesOut()
    {
        Assert.Equal(0.5 * 100 * (9 - 1) / 2, rams.Joules(kilograms: 200, theirKilograms: 200, closingSpeed: 3), 6);
    }

    [Fact]
    public void AgainstAFarHeavierHullTheContactTakesThisHullsOwnEnergy()
    {
        Assert.Equal(0.5 * 200 * (9 - 1) / 2, rams.Joules(kilograms: 200, theirKilograms: 1e9, closingSpeed: 3), 3);
    }

    private static Contact Touching(double towardX, double closingX) => new(Other, towardX, 0, closingX * PerTick, 0);
}
