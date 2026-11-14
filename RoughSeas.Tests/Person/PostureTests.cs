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
}
