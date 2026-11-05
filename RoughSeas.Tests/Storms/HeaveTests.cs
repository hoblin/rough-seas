using RoughSeas.Storms;
using RoughSeas.Vessels;

namespace RoughSeas.Tests.Storms;

public class HeaveTests
{
    private const double ClientSeconds = 1 / 60.0;
    private const double ServerSeconds = 1 / 30.0;

    private readonly ResponseConfig config = Fixtures.Config().Response;

    private Heave Laden(Hull hull, Aboard aboard) =>
        new(aboard.Total, AddedKg(hull), hull.Flotation.KgPerMetre, hull.Float(aboard).DraftMetres, hull.Flotation.DepthMetres, config);

    private static double AddedKg(Hull hull)
    {
        var shape = Fixtures.Shape(hull.Vessel);
        double squareMetres = hull.Flotation.HasHold ? hull.Flotation.KgPerMetre / 1025 : 4 * shape.HalfLength * shape.HalfBeam;
        return HeaveAddedMass.OfWaterplane(squareMetres, shape.HalfBeam);
    }

    private static List<double> Immersions(Heave heave, double from, double seconds, double step = ClientSeconds, double rising = 0)
    {
        var immersions = new List<double>();
        double immersion = from, speed = rising;
        bool wet = immersion > 0;
        for (double t = 0; t < seconds; t += step)
        {
            if (immersion > 0 && !wet) speed = heave.Entering(speed);
            wet = immersion > 0;
            speed += (wet ? heave.Acceleration(immersion, speed) : -HullResponse.Gravity) * step;
            immersion -= speed * step;
            immersions.Add(immersion);
        }
        return immersions;
    }

    [Theory]
    [InlineData("barkcanoe")]
    [InlineData("coracle")]
    [InlineData("currach")]
    public void ALightHullThrownClearOfTheWaterComesToRest(string vessel)
    {
        var hull = Fixtures.Hull(vessel);
        var empty = Laden(hull, hull.Crewed(bodies: 0, kit: 0));

        var immersions = Immersions(empty, 0.0013, 20, ServerSeconds, rising: 0.164);

        Assert.True(immersions.Min() < 0, "she never left the water");
        Assert.Equal(empty.DraftMetres, immersions[^1], 4);
        Assert.Equal(1, immersions.Count(immersion => immersion < 0));
    }

    [Theory]
    [InlineData("barkcanoe")]
    [InlineData("coracle")]
    [InlineData("currach")]
    [InlineData("dugout")]
    [InlineData("outrigger")]
    [InlineData("reedboat")]
    public void ARiderBoardingAtOnceSinksHerWithoutLiftingHerAboveWhereSheFloated(string vessel)
    {
        var hull = Fixtures.Hull(vessel);
        double empty = hull.Float(hull.Crewed(bodies: 0, kit: 0)).DraftMetres;
        var boarded = Laden(hull, hull.Crewed());

        var immersions = Immersions(boarded, empty, 6);

        Assert.True(immersions.Min() >= empty, $"she rose to {immersions.Min():F4} m from {empty:F4} m");
        Assert.Equal(boarded.DraftMetres, immersions[^1], 3);
    }

    [Fact]
    public void AHeavyHullSinksUnderTheSameLoadLessAndMoreSlowlyThanALightOne()
    {
        var canoe = Fixtures.Hull("barkcanoe");
        var dugout = Fixtures.Hull("dugout");

        var canoeImmersions = Immersions(Laden(canoe, canoe.Crewed(containers: 20)), canoe.Float(canoe.Crewed()).DraftMetres, 3);
        var dugoutImmersions = Immersions(Laden(dugout, dugout.Crewed(containers: 20)), dugout.Float(dugout.Crewed()).DraftMetres, 3);

        double canoeSank = canoeImmersions.Max() - canoe.Float(canoe.Crewed()).DraftMetres;
        double dugoutSank = dugoutImmersions.Max() - dugout.Float(dugout.Crewed()).DraftMetres;
        Assert.True(dugoutSank < canoeSank);
        Assert.True(dugoutImmersions.IndexOf(dugoutImmersions.Max()) > canoeImmersions.IndexOf(canoeImmersions.Max()));
    }

    [Theory]
    [InlineData("barkcanoe")]
    [InlineData("coracle")]
    [InlineData("reedboat")]
    public void SheComesToRestAtHerDraftOnTheServersSlowerTicksToo(string vessel)
    {
        var hull = Fixtures.Hull(vessel);
        var empty = Laden(hull, hull.Crewed(bodies: 0, kit: 0));

        var immersions = Immersions(empty, hull.Float(hull.Crewed()).DraftMetres, 6, ServerSeconds);

        Assert.Equal(empty.DraftMetres, immersions[^1], 4);
    }

    [Fact]
    public void SheBobsAtTheHeavePeriodTheDrawnHullRidesTheSwellOn()
    {
        var hull = Fixtures.Hull("currach");
        var heave = Laden(hull, hull.Crewed());
        var undamped = heave with { Config = new ResponseConfig() };

        var immersions = Immersions(undamped, heave.DraftMetres + 0.01, 4);
        var peaks = Enumerable.Range(1, immersions.Count - 2)
            .Where(i => immersions[i] > immersions[i - 1] && immersions[i] >= immersions[i + 1])
            .ToList();

        Assert.Equal(HullResponse.HeavePeriod(heave.Kg, heave.AddedKg, heave.KgPerMetre), (peaks[1] - peaks[0]) * ClientSeconds, 1);
    }

    [Fact]
    public void AHullLadenAwashSettlesAtHerSideNotBelowIt()
    {
        var reedboat = Fixtures.Hull("reedboat");
        var awash = Laden(reedboat, reedboat.Crewed(bodies: 260, kit: 40));

        var immersions = Immersions(awash, reedboat.Float(reedboat.Crewed(bodies: 0, kit: 0)).DraftMetres, 8);

        Assert.Equal(awash.DepthMetres, awash.DraftMetres);
        Assert.Equal(awash.DepthMetres, immersions[^1], 3);
    }

    [Fact]
    public void AHullPressedUnderPastHerSideIsLiftedNoHarderThanHerWholeDepthLifts()
    {
        var hull = Fixtures.Hull("currach");
        var heave = Laden(hull, hull.Crewed());

        Assert.Equal(heave.Acceleration(heave.DepthMetres, 0), heave.Acceleration(heave.DepthMetres + 1, 0));
    }
}
