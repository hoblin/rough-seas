using RoughSeas.Tests.Storms;
using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class HeaveAddedMassTests
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
        Assert.Equal(expected, HeaveAddedMass.SecondKind(eccentricity), 9);
    }

    [Fact]
    public void ARoundPlateCarriesTwoThirdsOfLambsDiscInTheOpenWater()
    {
        Assert.Equal(4.0 / 3 * KgPerCubicMetre, HeaveAddedMass.OfPlate(1, 1), 6);
    }

    [Fact]
    public void ALongPlateCarriesWhatItsStripsCarry()
    {
        double strips = 2.0 / 3 * Math.PI * KgPerCubicMetre * 20 * 0.5 * 0.5;

        Assert.Equal(strips, HeaveAddedMass.OfPlate(20, 0.5), strips * 0.002);
    }

    [Fact]
    public void AWideShallowHullDragsSeveralTimesHerOwnMassOfWater()
    {
        var canoe = Fixtures.Hull("barkcanoe");
        var shape = Fixtures.Shape("barkcanoe");
        var aboard = canoe.Crewed();

        double added = HeaveAddedMass.OfWaterplane(canoe.Flotation.KgPerMetre / KgPerCubicMetre, shape.HalfBeamMetres);

        Assert.InRange(added / aboard.Total, 2, 10);
    }
}
