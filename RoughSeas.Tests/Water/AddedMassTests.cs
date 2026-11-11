using RoughSeas.Tests.Storms;
using RoughSeas.Water;

namespace RoughSeas.Tests.Water;

public class AddedMassTests
{
    private const double KgPerCubicMetre = 1025;

    [Theory]
    [InlineData(0, Math.PI / 2)]
    [InlineData(0.5, 1.4674622093394272)]
    [InlineData(0.9, 1.1716970527816141)]
    [InlineData(0.999, 1.0039944099655043)]
    [InlineData(1, 1)]
    public void TheEllipticIntegralIsLegendres(double eccentricity, double expected)
    {
        Assert.Equal(expected, AddedMass.SecondKind(eccentricity), 9);
    }

    [Fact]
    public void ARoundPlateCarriesTwoThirdsOfLambsDiscInTheOpenWater()
    {
        Assert.Equal(4.0 / 3 * KgPerCubicMetre, AddedMass.OfPlate(1, 1, KgPerCubicMetre), 6);
    }

    [Fact]
    public void ALongPlateCarriesWhatItsStripsCarry()
    {
        double strips = 2.0 / 3 * Math.PI * KgPerCubicMetre * 20 * 0.5 * 0.5;

        Assert.Equal(strips, AddedMass.OfPlate(20, 0.5, KgPerCubicMetre), strips * 0.002);
    }

    [Fact]
    public void AWideShallowHullDragsSeveralTimesHerOwnMassOfWater()
    {
        var canoe = Fixtures.Hull("barkcanoe");
        var shape = Fixtures.Shape("barkcanoe");
        var aboard = canoe.Crewed();

        double added = AddedMass.OfWaterplane(canoe.Flotation.KgPerMetre / KgPerCubicMetre, shape.HalfBeamMetres, KgPerCubicMetre);

        Assert.InRange(added / aboard.Total, 2, 10);
    }

    [Fact]
    public void ASphereCarriesHalfItsWaterEveryWay()
    {
        var cubicMetres = 4.0 / 3 * Math.PI;

        Assert.Equal((KgPerCubicMetre * cubicMetres / 2, KgPerCubicMetre * cubicMetres / 2), AddedMass.OfSpheroid(2, cubicMetres, KgPerCubicMetre));
    }

    [Theory]
    [InlineData(1.99, 0.209, 0.702)]
    [InlineData(4.99, 0.059, 0.895)]
    [InlineData(9.97, 0.021, 0.960)]
    public void AProlateSpheroidCarriesWhatLambsTableGives(double aspect, double along, double across)
    {
        const double minor = 0.15;
        var cubicMetres = 4.0 / 3 * Math.PI * aspect * minor * minor * minor;
        var displaced = KgPerCubicMetre * cubicMetres;

        var added = AddedMass.OfSpheroid(2 * aspect * minor, cubicMetres, KgPerCubicMetre);

        Assert.InRange(added.Along / displaced, along - 0.003, along + 0.003);
        Assert.InRange(added.Across / displaced, across - 0.003, across + 0.003);
    }
}
