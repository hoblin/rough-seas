using RoughSeas.Person;
using RoughSeas.Tests.Storms;

namespace RoughSeas.Tests.Person;

public class PostureTests
{
    private const double Seawater = 1025;
    private const double Kg = 80;
    private static readonly BodyConfig Body = Fixtures.PersonConfig.Body;
    private static readonly Posture Flat = Posture.Along(1, 0, 0);

    private static double Falling(Posture posture) => -posture.Dragged((0, -9, 0), Body, Seawater, 1, Kg, Kg, 0.1).Y;

    [Fact]
    public void AFallAlongItsLengthMeetsLessDragThanAFallFlat() =>
        Assert.True(Falling(Posture.Upright) > 1.5 * Falling(Flat), $"{Falling(Posture.Upright):F2} m/s upright against {Falling(Flat):F2} m/s flat");

    [Fact]
    public void AWayAlongItsSlantKeepsItsDirection()
    {
        var slant = Posture.Along(1, -2, 0);
        var (x, y, _) = slant.Dragged((3, -6, 0), Body, Seawater, 1, Kg, Kg, 0.1);
        Assert.Equal(-2, y / x, 9);
    }

    [Fact]
    public void ItTurnsTowardHowItIsDrawnNoFasterThanItsPace()
    {
        var turned = Posture.Upright.TurnedToward(Flat, 0.5);
        Assert.Equal(Math.Cos(0.5), turned.Y, 9);
        Assert.Equal(Math.Sin(0.5), turned.X, 9);
    }

    [Fact]
    public void ItReachesHowItIsDrawnOnceThatIsWithinItsPace() =>
        Assert.Equal(Flat, Posture.Along(1, 0.1, 0).TurnedToward(Flat, 0.5));

    [Fact]
    public void ItsAxisHasNoHeadOrFootSoItTurnsTheShortWay() =>
        Assert.Equal(1, Posture.Along(1, 0.1, 0).TurnedToward(Posture.Along(-1, 0, 0), 0.5).X, 9);
}
