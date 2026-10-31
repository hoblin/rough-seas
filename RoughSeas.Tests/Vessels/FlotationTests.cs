using RoughSeas.Tests.Storms;
using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class FlotationTests
{
    [Fact]
    public void AnOakDugoutWithItsPaddlerShowsTheSideItWasBuiltWith()
    {
        var dugout = Fixtures.Hull("dugout");

        Assert.Equal(0.41, dugout.Float(dugout.Crewed()).FreeboardMetres, 0.01);
    }

    [Fact]
    public void ALoadedBoatSitsLower()
    {
        var dugout = Fixtures.Hull("dugout");

        Assert.True(dugout.Float(dugout.Crewed(bodies: 130, kit: 20)).DraftMetres > dugout.Float(dugout.Crewed()).DraftMetres);
    }

    [Fact]
    public void ALightWoodFloatsHigherThanAHeavyOneInTheSameShape()
    {
        var pine = Fixtures.Hull("dugout", "pine");
        var ebony = Fixtures.Hull("dugout", "ebony");

        Assert.True(pine.Float(pine.Crewed()).FreeboardMetres > ebony.Float(ebony.Crewed()).FreeboardMetres);
    }

    [Fact]
    public void AFloodedLightBoatFloatsAwash()
    {
        var currach = Fixtures.Hull("currach");
        var afloat = currach.Float(currach.Crewed(litres: 1500), litres: 1500);

        Assert.False(afloat.Sunk);
        Assert.Equal(0, afloat.FreeboardMetres);
    }

    [Fact]
    public void AFloodedBoatLoadedHeavierThanHerOwnMaterialHoldsUpGoesDown()
    {
        var currach = Fixtures.Hull("currach");

        Assert.True(currach.Float(currach.Crewed(containers: 60, litres: 1500), litres: 1500).Sunk);
    }

    [Fact]
    public void AFloodedBallastedSailboatGoesDown()
    {
        var sailboat = Fixtures.Hull("boat-sailed");

        Assert.False(sailboat.Float(sailboat.Crewed()).Sunk);
        Assert.True(sailboat.Float(sailboat.Crewed(litres: 1500), litres: 1500).Sunk);
    }

    [Fact]
    public void AFloodedBoatOfWoodHeavierThanWaterGoesDown()
    {
        var leadwood = Fixtures.Hull("dugout", "leadwood");

        Assert.True(leadwood.Float(leadwood.Crewed(litres: 1156), litres: 1156).Sunk);
    }

    [Fact]
    public void AHeavyBoatLoadedHardShipsWaterOverHerGunwaleWithNoDamage()
    {
        var ebony = Fixtures.Hull("dugout", "ebony");
        var oak = Fixtures.Hull("dugout");

        Assert.True(ebony.Float(ebony.Crewed(bodies: 130, kit: 70, litres: 900)).FreeboardMetres <= 0);
        Assert.True(oak.Float(oak.Crewed(bodies: 130, kit: 70, litres: 900)).FreeboardMetres > 0);
    }

    [Fact]
    public void AHullWithNoDepthGivenNeverSinks()
    {
        var unmeasured = new Flotation(0, 0, 10);

        Assert.False(unmeasured.Float(new Aboard(100, 75, 200, 60, 500, 0), flooded: true).Sunk);
    }

    [Fact]
    public void ARaftPressedUnderByItsRiderStillHoldsThemUp()
    {
        var raft = Fixtures.Hull("boat-raft");
        var afloat = raft.Float(raft.Crewed());

        Assert.False(afloat.Sunk);
        Assert.Equal(raft.Flotation.DepthMetres, afloat.DraftMetres, 6);
    }

    [Fact]
    public void ARaftOfLightWoodCarriesItsRiderAboveTheWater()
    {
        var raft = Fixtures.Hull("boat-raft", "pine");

        Assert.True(raft.Float(raft.Crewed()).FreeboardMetres > 0);
    }

    [Fact]
    public void AReedBoatSoakedForThreeDaysLiesOnTheBottom()
    {
        var reed = Fixtures.Hull("reedboat");
        var soak = Fixtures.IntegrityConfig("reedboat")["soak"]!;
        double perDay = (double)soak["kgPerDay"]!;

        Assert.False(reed.Float(reed.Crewed(soaked: perDay * 2.5)).Sunk);
        Assert.True(reed.Float(reed.Crewed(bodies: 0, kit: 0, soaked: perDay * 3.5)).Sunk);
    }
}
