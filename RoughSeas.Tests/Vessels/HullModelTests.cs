using Newtonsoft.Json.Linq;
using RoughSeas.Tests.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Vessels;

public class HullModelTests
{
    private const double BucketLitresPerSecond = 10 / 3.0;

    private readonly TreeAttribute tree = new();
    private int changes;

    [Fact]
    public void AKnockCracksOnlyThePlaceItLands()
    {
        var hull = Planked("dugout");

        hull.Take(HullPlace.PortSide, 0.3);

        Assert.Equal(0.3, hull.Crack(HullPlace.PortSide), 6);
        Assert.All(HullShape.Places.Where(place => place != HullPlace.PortSide), place => Assert.Equal(0, hull.Crack(place)));
        Assert.True(changes > 0);
    }

    [Fact]
    public void ACrackLeaksAndACrackedPlaceIsShownApartFromTheRest()
    {
        var hull = Planked("dugout");
        hull.Take(HullPlace.Bottom, 0.3);

        Assert.True(hull.LitresPerSecond(Draft(0.2), 0.41) > 0);
        Assert.Equal(2, hull.Stage(HullPlace.Bottom));
        Assert.Equal(0, hull.Stage(HullPlace.PortBow));
    }

    [Fact]
    public void ACrackHighOnTheSideLeaksOnlyOnceSheSitsLowEnough()
    {
        var hull = Planked("dugout");
        hull.Take(HullPlace.StarboardSide, 0.3);

        Assert.Equal(0, hull.LitresPerSecond(Draft(0.05), 0.41));
        Assert.True(hull.LitresPerSecond(Draft(0.3), 0.41) > hull.LitresPerSecond(Draft(0.2), 0.41));
    }

    [Fact]
    public void OneHardKnockLeaksLessThanABucketBailsButEnoughCracksOutpaceIt()
    {
        var hull = Planked("dugout");
        hull.Take(HullPlace.Bottom, 0.3);
        Assert.True(hull.LitresPerSecond(Draft(0.21), 0.41) < BucketLitresPerSecond);

        foreach (var place in HullShape.Places) hull.Take(place, 0.6);
        Assert.True(hull.LitresPerSecond(Draft(0.21), 0.41) > BucketLitresPerSecond);
    }

    [Fact]
    public void OnlyAReallySevereBlowBreaksAHullUp()
    {
        var hull = Planked("dugout");

        Assert.Equal(Knock.Harmed, hull.Take(HullPlace.PortBow, 0.9));
        Assert.Equal(Knock.Harmed, hull.Take(HullPlace.PortBow, 0.9));
        Assert.Equal(Knock.BrokenUp, hull.Take(HullPlace.StarboardQuarter, 1.2));
    }

    [Fact]
    public void AMendPatchesThePlaceLookedAtOrElseTheWorstCrack()
    {
        var hull = Planked("dugout");
        var patch = new HullRepair { Restores = 0.4f };
        hull.Take(HullPlace.PortSide, 0.5);
        hull.Take(HullPlace.PortBow, 0.8);

        hull.Mend(patch, HullPlace.PortSide);
        Assert.Equal(0.1, hull.Crack(HullPlace.PortSide), 6);

        hull.Mend(patch, HullPlace.StarboardSide);
        Assert.Equal(0.4, hull.Crack(HullPlace.PortBow), 6);
    }

    [Fact]
    public void AnUngreasedHideSeepsAndGreaseStopsIt()
    {
        var hull = Planked("currach");
        Assert.Equal(0, hull.Inflow(1, 1, Draft(0.05), 0.45));

        hull.Pass(3, afloat: true, sun: 0);
        Assert.True(hull.Inflow(1, 1, Draft(0.05), 0.45) > 0);

        hull.Mend(new HullRepair { Restores = 0.5f, Greases = true }, HullPlace.Bottom);
        Assert.Equal(0, hull.Inflow(1, 1, Draft(0.05), 0.45));
    }

    [Fact]
    public void AWoodenHullDoesNotWearAfloat()
    {
        var hull = Planked("dugout");

        hull.Pass(30, afloat: true, sun: 0);

        Assert.Equal(1, hull.Condition);
        Assert.Equal(0, hull.Inflow(1, 1, Draft(0.2), 0.41));
    }

    [Fact]
    public void AKnockWorksTheRaftsLashingsLooseUntilItComesApart()
    {
        var raft = new LashedHull(() => tree, () => changes++);

        Assert.Equal(Knock.Harmed, raft.Take(HullPlace.PortSide, 0.6));
        Assert.Equal(0.4f, raft.Condition, 6);
        Assert.Equal(Knock.BrokenUp, raft.Take(HullPlace.PortBow, 0.6));
    }

    [Fact]
    public void ReedSoaksAfloatAndDriesInTheSunLeavingSomeRot()
    {
        var reed = Reed();

        reed.Pass(2, afloat: true, sun: 0);
        double soaked = reed.SoakedKg;
        reed.Pass(10, afloat: false, sun: 1);

        Assert.True(soaked > 0);
        Assert.InRange(reed.SoakedKg, 0.01, soaked / 2);
        Assert.Equal(reed.RotKg, reed.SoakedKg, 6);
    }

    [Fact]
    public void ReedAshoreInTheDarkOrRainKeepsItsWater()
    {
        var reed = Reed();
        reed.Pass(2, afloat: true, sun: 0);
        double soaked = reed.SoakedKg;

        reed.Pass(2, afloat: false, sun: 0);

        Assert.Equal(soaked, reed.SoakedKg);
    }

    [Fact]
    public void ReedPastAFewCyclesIsPastSaving()
    {
        var reed = Reed();
        var hull = Fixtures.Hull("reedboat");
        for (int cycle = 0; cycle < 5; cycle++)
        {
            reed.Pass(3, afloat: true, sun: 0);
            reed.Pass(10, afloat: false, sun: 1);
        }

        Assert.True(hull.Float(hull.Crewed(bodies: 0, kit: 0, soaked: reed.SoakedKg)).Sunk);
    }

    private PlankedHull Planked(string vessel) =>
        new(() => tree, () => changes++, new JsonObject(Fixtures.IntegrityConfig(vessel)), Fixtures.HullConfig);

    private ReedHull Reed() => new(() => tree, () => changes++, new JsonObject(Fixtures.IntegrityConfig("reedboat")));

    private static Afloat Draft(double metres) => new(metres, 0, false);
}
