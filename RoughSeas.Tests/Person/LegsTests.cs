using RoughSeas.Person;
using RoughSeas.Tests.Storms;

namespace RoughSeas.Tests.Person;

public class LegsTests
{
    private static readonly LegsConfig Config = Fixtures.PersonConfig.Body.Legs;
    private static readonly SwimStroke Up = new(0, 1, 0, Jump: true);
    private static readonly (double X, double Y, double Z) Still = default;

    private static Legs NewLegs() => new(Fixtures.Person.Body, Config);

    private static double Upward((double X, double Y, double Z) force) => force.Y;

    [Fact]
    public void PushOnlyWhileJumpIsHeld()
    {
        Assert.Equal(0, Upward(NewLegs().Push(0, 0, 0, Still, true, new SwimStroke(0, 1, 0))));
        Assert.True(Upward(NewLegs().Push(0, 0, 0, Still, true, Up)) > 0);
    }

    [Fact]
    public void PushHardestFromAStandstillAndNotAtAllAtTheSpeedTheyStraightenWithNothing()
    {
        Assert.Equal(NewLegs().MaxNewtons, Upward(NewLegs().Push(0, 0, 0, Still, true, Up)), 6);
        Assert.Equal(0, Upward(NewLegs().Push(0, 0, 0, (0, Config.MetresPerSecond, 0), true, Up)), 6);
    }

    [Fact]
    public void StopOnceTheyHaveStretchedTheirPush()
    {
        var legs = NewLegs();
        legs.Push(0, 0, 0, Still, true, Up);

        Assert.True(Upward(legs.Push(0, Config.PushMetres / 2, 0, Still, false, Up)) > 0);
        Assert.Equal(0, Upward(legs.Push(0, Config.PushMetres, 0, Still, false, Up)));
    }

    [Fact]
    public void StopWhenTheBodyGainsNothingOnThePush()
    {
        var legs = NewLegs();
        legs.Push(0, 0, 0, Still, true, Up);

        Assert.Equal(0, Upward(legs.Push(0, 0, 0, Still, true, Up)));
    }

    [Fact]
    public void PushAgainOnlyOnceTheFeetHaveLeftTheBedAndLanded()
    {
        var legs = NewLegs();
        legs.Push(0, 0, 0, Still, true, Up);
        legs.Push(0, Config.PushMetres, 0, Still, true, Up);

        Assert.Equal(0, Upward(legs.Push(0, 0, 0, Still, true, Up)));
        legs.Push(0, 1, 0, Still, false, Up);
        Assert.True(Upward(legs.Push(0, 0, 0, Still, true, Up)) > 0);
    }
}
