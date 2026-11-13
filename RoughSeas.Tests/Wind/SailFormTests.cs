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
    private static readonly SailForm Form = new(Tack, Tack, Peak, Clew, Tack, Fixtures.Wind.Camber);

    [Fact]
    public void HerCornersStayOnHerSparsHoweverSheFills()
    {
        Assert.Equal(0, (Peak - Form.At(Full, 1, Untrimmed, 1)).Length, Precision);
        Assert.Equal(0, (Clew - Form.At(Full, 0, Untrimmed, -1)).Length, Precision);
        Assert.Equal(0, (Tack - Form.At(0, 0.5, Untrimmed, 1)).Length, Precision);
    }

    [Fact]
    public void SheBelliesToTheSideTheWindPresses()
    {
        Assert.True(Form.At(0.8, 0.5, Untrimmed, 1).Across > 0);
        Assert.True(Form.At(0.8, 0.5, Untrimmed, -1).Across < 0);
        Assert.Equal(0, Form.At(0.8, 0.5, Untrimmed, Flat).Across, Precision);
    }

    [Fact]
    public void TrimmedToStarboardHerLeechSwingsOutToStarboardAboutHerTack()
    {
        var trimmed = Form.At(Full, 0.5, Math.PI / 4, Flat);
        Assert.True(trimmed.Across > 0);
        Assert.Equal((Form.At(Full, 0.5, Untrimmed, Flat) - Tack).Length, (trimmed - Tack).Length, Precision);
    }

    [Fact]
    public void ReefedHerCentreOfEffortComesInTowardHerTack()
    {
        Assert.True((Form.Centre((Math.Sqrt(0.5), Full), Untrimmed, Flat) - Tack).Length < (Form.Centre(SailForm.Whole, Untrimmed, Flat) - Tack).Length);
    }
}
