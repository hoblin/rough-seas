using RoughSeas.Tests.Storms;
using RoughSeas.Wind;

namespace RoughSeas.Tests.Wind;

public class SailFormTests
{
    private const double Full = 1;
    private const double Flat = 0;
    private const double Precision = 9;
    private static readonly HullPoint Tack = new(-1, 0.7, 0);
    private static readonly HullPoint Peak = new(0.3, 3.2, 0);
    private static readonly HullPoint Clew = new(1.6, 0.9, 0);
    private static readonly SailForm Form = new(Tack, Tack, Peak, Clew, Fixtures.Wind.Camber);

    [Fact]
    public void HerCornersStayOnHerSparsHoweverSheFills()
    {
        Assert.Equal(0, (Peak - Form.At(Full, 1, 1)).Length, Precision);
        Assert.Equal(0, (Clew - Form.At(Full, 0, -1)).Length, Precision);
        Assert.Equal(0, (Tack - Form.At(0, 0.5, 1)).Length, Precision);
    }

    [Fact]
    public void SheBelliesOutOfHerPlaneToWhicheverSideTheWindPresses()
    {
        double Off(double depth) => (Form.At(0.8, 0.5, depth) - Tack).Dot(Form.Normal);
        Assert.True(Off(1) > 0);
        Assert.True(Off(-1) < 0);
        Assert.Equal(0, Off(Flat), Precision);
    }

    [Fact]
    public void PosedAnewSheKeepsHerShapeBetweenHerNewCorners()
    {
        var clew = new HullPoint(1.2, 1.9, -0.8);
        var posed = Form.Posed(Tack, Tack, Peak, clew);
        Assert.Equal(0, (clew - posed.At(Full, 0, Flat)).Length, Precision);
        Assert.Equal(0, (Peak - posed.At(Full, 1, Flat)).Length, Precision);
        Assert.Equal(0, (posed.At(0.5, 0.5, Flat) - Tack).Dot(posed.Normal), Precision);
    }

    [Fact]
    public void RolledDownOnHerBoomHerCentreOfEffortRisesTowardHerYard()
    {
        Assert.True(Form.Centre((Full, 0.5), Flat).Height > Form.Centre(SailForm.Whole, Flat).Height);
    }
}
