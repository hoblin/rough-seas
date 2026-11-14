using RoughSeas.Person;
using RoughSeas.Tests.Storms;

namespace RoughSeas.Tests.Person;

public class LegsTests
{
    private const double Gravity = 9.81;
    private static readonly LegsConfig Config = Fixtures.PersonConfig.Body.Legs;
    private static readonly double MassKg = Fixtures.Person.Body.MassKg;
    private static readonly SwimStroke Up = new(0, 1, 0, Jump: true);

    private static Legs NewLegs() => new(Fixtures.Person.Body, Config);

    private static double Upward(Legs legs, double y, bool onBed, SwimStroke? stroke = null) =>
        legs.Push(0, y, 0, onBed, stroke ?? Up, MassKg, 0).Y;

    private static double TakeOff(double newtons, double heldBack) => Math.Sqrt(2 * Config.PushMetres * (newtons - heldBack) / MassKg);

    [Fact]
    public void PushOnlyWhileJumpIsHeld()
    {
        Assert.Equal(0, Upward(NewLegs(), 0, true, new SwimStroke(0, 1, 0)));
        Assert.True(Upward(NewLegs(), 0, true) > 0);
    }

    [Fact]
    public void HaveNothingToPushOffAboveTheBed()
    {
        Assert.Equal(0, Upward(NewLegs(), 0, false));
    }

    [Fact]
    public void PushWithTheForceTheirProfileGivesTheMeanSpeedOfThatPush()
    {
        var newtons = NewLegs().Steady(MassKg, 0);

        Assert.Equal(NewLegs().MaxNewtons * (1 - TakeOff(newtons, 0) / 2 / Config.MetresPerSecond), newtons, 3);
    }

    [Fact]
    public void PushHarderAgainstTheirWeightOnLandThanInTheWater()
    {
        var land = NewLegs().Steady(MassKg, MassKg * Gravity);
        var water = NewLegs().Steady(MassKg, 0);

        Assert.True(land > water, $"{land:F0} N on land, {water:F0} N in the water");
        Assert.True(TakeOff(land, MassKg * Gravity) < TakeOff(water, 0));
    }

    [Fact]
    public void StopOnceTheyHaveStretchedTheirPush()
    {
        var legs = NewLegs();
        Upward(legs, 0, true);

        Assert.True(Upward(legs, Config.PushMetres / 2, false) > 0);
        Assert.Equal(0, Upward(legs, Config.PushMetres, false));
    }

    [Fact]
    public void StopWhenTheBodyGainsNothingOnThePush()
    {
        var legs = NewLegs();
        Upward(legs, 0, true);

        Assert.Equal(0, Upward(legs, 0, true));
    }

    [Fact]
    public void PushAgainOnlyOnceTheFeetHaveLeftTheBedAndLanded()
    {
        var legs = NewLegs();
        Upward(legs, 0, true);
        Upward(legs, Config.PushMetres, true);

        Assert.Equal(0, Upward(legs, 0, true));
        Upward(legs, 1, false);
        Assert.True(Upward(legs, 0, true) > 0);
    }
}
