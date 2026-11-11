using RoughSeas.Person;
using RoughSeas.Tests.Storms;

namespace RoughSeas.Tests.Person;

public class BodyTests
{
    private const double Gravity = 9.81;
    private const double Sea = 1025;
    private const double Fresh = 998;

    private static readonly Body Man = Fixtures.Person.Body;

    [Fact]
    public void WeighsTheVesselsCrew()
    {
        Assert.Equal(Fixtures.Mass.CrewKg, Man.MassKg);
        Assert.Equal(75, Man.MassKg);
    }

    [Fact]
    public void HasTissueOfItsFatsDensity()
    {
        Assert.InRange(Man.MassKg / Man.TissueLitres, 1.055, 1.06);
    }

    [Fact]
    public void BreathesAtAQuietBreathAboveFunctionalResidualCapacity()
    {
        Assert.Equal(3.66, Man.BreathingLitres, 2);
    }

    [Fact]
    public void FloatsInTheSeaAndSinksInFreshWaterAfterABreathOut()
    {
        var whole = Man.Litres(Man.BreathingLitres);

        Assert.True(whole * Sea / 1000 > Man.MassKg);
        Assert.True(whole * Fresh / 1000 < Man.MassKg);
    }

    [Fact]
    public void DisplacesAllOfItselfOverItsHead()
    {
        Assert.Equal(Man.Litres(3), Man.LitresUnder(Man.HeightMetres, 3), 6);
        Assert.Equal(0, Man.LitresUnder(0, 3));
    }

    [Fact]
    public void HoldsItsLungsInItsChest()
    {
        Assert.Equal(Man.LitresUnder(1.2, 0), Man.LitresUnder(1.2, 3), 6);
        Assert.Equal(Man.LitresUnder(1.5, 0) + 3, Man.LitresUnder(1.5, 3), 6);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10.08, 0.5)]
    [InlineData(30.2, 0.25)]
    public void SqueezesHeldAirByBoylesLaw(double metres, double share)
    {
        Assert.Equal(share * 3, Man.Squeezed(3, metres, Sea, Gravity), 1);
    }

    [Fact]
    public void TurnsNegativeBelowAboutSixMetres()
    {
        double Net(double depth) => Sea * Man.Litres(Man.Squeezed(Man.BreathingLitres, depth, Sea, Gravity)) / 1000 - Man.MassKg;

        Assert.True(Net(5.5) > 0, $"{Net(5.5):F2} kg at 5.5 m");
        Assert.True(Net(7.5) < 0, $"{Net(7.5):F2} kg at 7.5 m");
    }

    private static double Displaced => Sea * Man.Litres(Man.BreathingLitres) / Body.LitresPerCubicMetre;

    [Fact]
    public void HeavesWithTheWaterplanesDiscWhileTheSurfaceCutsIt()
    {
        var chest = 0.77 * Man.HeightMetres;
        var section = Man.LitresUnder(chest + 0.005, Man.BreathingLitres) - Man.LitresUnder(chest - 0.005, Man.BreathingLitres);
        var radius = Math.Sqrt(section / Body.LitresPerCubicMetre / 0.01 / Math.PI);

        Assert.Equal(4.0 / 3 * Sea * Math.Pow(radius, 3), Man.AddedKg(chest, Man.BreathingLitres, Sea), 6);
    }

    [Fact]
    public void DragsLittleWaterAlongItsLengthOnceUnder()
    {
        Assert.InRange(Man.AddedKg(2 * Man.HeightMetres, Man.BreathingLitres, Sea) / Displaced, 0.03, 0.06);
    }

    [Fact]
    public void GoesUnderWithNoJumpInTheWaterItDrags()
    {
        var awash = Man.AddedKg(Man.HeightMetres - 0.01, Man.BreathingLitres, Sea);
        var under = Man.AddedKg(Man.HeightMetres + 0.01, Man.BreathingLitres, Sea);

        Assert.InRange(Math.Abs(under - awash) / Man.MassKg, 0, 0.05);
    }

    [Fact]
    public void DragsAboutItsOwnWaterAcrossWhenUnder()
    {
        Assert.InRange(Man.AddedAcrossKg(2 * Man.HeightMetres, Man.BreathingLitres, Sea) / Displaced, 0.85, 1);
        Assert.Equal(0, Man.AddedAcrossKg(0, Man.BreathingLitres, Sea));
    }

    [Fact]
    public void MovesWithWaterOfItsOwnDensity()
    {
        var litres = Man.MassKg / Sea * 1000;
        const double acceleration = 0.7;
        var added = Man.AddedKg(1.6, Man.BreathingLitres, Sea);

        Assert.Equal(acceleration, Body.Lift(litres, Sea, Man.MassKg, added, Gravity, acceleration), 6);
    }

    [Fact]
    public void DragNeverTurnsAMotionBack()
    {
        var after = Body.Dragged(100, Sea, 0.25, 80, 1);

        Assert.InRange(after, 0, 100);
        Assert.Equal(-after, Body.Dragged(-100, Sea, 0.25, 80, 1), 6);
    }
}
