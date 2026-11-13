using RoughSeas.Tests.Storms;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class SailFormTests
{
    private const double Full = 1;
    private const double Untrimmed = 0;
    private const double Flat = 0;
    private const double Precision = 9;
    private static readonly HullPoint Tack = new(-1, 0.7, 0);
    private static readonly HullPoint Peak = new(0.3, 3.2, 0);
    private static readonly HullPoint Clew = new(1.6, 0.9, 0);
    private static readonly HullPoint Masthead = new(-0.4, 3.4, 0.3);
    private static readonly SailForm Form = new(Tack, Tack, Peak, Clew, Tack, Masthead - Tack, Fixtures.Wind.Camber);

    [Fact]
    public void HerCornersStayOnHerSparsHoweverSheFills()
    {
        Assert.Equal(0, (Peak - Form.At(Full, 1, Untrimmed, 1)).Length, Precision);
        Assert.Equal(0, (Clew - Form.At(Full, 0, Untrimmed, -1)).Length, Precision);
        Assert.Equal(0, (Tack - Form.At(0, 0.5, Untrimmed, 1)).Length, Precision);
    }

    [Fact]
    public void SheBelliesOutOfHerPlaneToWhicheverSideTheWindPresses()
    {
        var normal = Form.Normal(Untrimmed);
        double Off(double depth) => (Form.At(0.8, 0.5, Untrimmed, depth) - Tack).Dot(normal);
        Assert.True(Off(1) > 0);
        Assert.True(Off(-1) < 0);
        Assert.Equal(0, Off(Flat), Precision);
    }

    [Fact]
    public void TrimmedSheTurnsAboutTheLineFromHerTackToTheMastheadAndKeepsHerDistanceFromIt()
    {
        var axis = (Masthead - Tack).Unit;
        HullPoint Radial(HullPoint p) => p - Tack - (p - Tack).Dot(axis) * axis;
        var (rest, trimmed) = (Form.At(Full, 0.5, Untrimmed, Flat), Form.At(Full, 0.5, Math.PI / 3, Flat));
        Assert.Equal((rest - Tack).Dot(axis), (trimmed - Tack).Dot(axis), Precision);
        Assert.Equal(Radial(rest).Length, Radial(trimmed).Length, Precision);
        Assert.Equal(Math.PI / 3, Math.Acos(Radial(rest).Unit.Dot(Radial(trimmed).Unit)), 6);
    }

    [Fact]
    public void RolledDownOnHerBoomHerCentreOfEffortRisesTowardHerYard()
    {
        Assert.True(Form.Centre((Full, 0.5), Untrimmed, Flat).Height > Form.Centre(SailForm.Whole, Untrimmed, Flat).Height);
    }
}
