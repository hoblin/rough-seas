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
    public void LyingAlongVanillasYawItsNoseGoesDownWithItsPitch()
    {
        var lying = Posture.Lying(Math.PI / 2, 0.5);
        Assert.Equal(Math.Cos(0.5), lying.X, 9);
        Assert.Equal(-Math.Sin(0.5), lying.Y, 9);
        Assert.Equal(0, lying.Z, 9);
    }

    [Fact]
    public void AWayAlongItsSlantMeetsOnlyItsLengthsDrag()
    {
        var (slant, speed, step) = (Posture.Along(1, -2, 0), 9.0, 1 / 60.0);
        var through = (X: speed / Math.Sqrt(5), Y: -2 * speed / Math.Sqrt(5), Z: 0.0);
        for (var t = 0; t < 30; t++) through = slant.Dragged(through, Body, Seawater, 1, Kg, Kg, step);

        var rate = 0.5 * Seawater * Body.DragAreaSwimming / Kg;
        Assert.Equal(speed / (1 + rate * speed * 0.5), Math.Sqrt(through.X * through.X + through.Y * through.Y), 1);
    }

    [Fact]
    public void AWayAlongItsSlantKeepsItsDirection()
    {
        var slant = Posture.Along(1, -2, 0);
        var (x, y, _) = slant.Dragged((3, -6, 0), Body, Seawater, 1, Kg, Kg, 0.1);
        Assert.Equal(-2, y / x, 9);
    }
}
