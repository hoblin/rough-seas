using Newtonsoft.Json.Linq;
using RoughSeas.Storms;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Storms;

public class SeaworthinessTests
{
    private readonly Waves waves = new(Fixtures.Config().Waves);
    private readonly SwampingConfig swamping = Fixtures.Config().Swamping;

    [Fact]
    public void AMetreAndAHalfSwellComesOverTheDugoutButNotTheCurrachOrTheOutrigger()
    {
        var crest = waves.Crest(1.5, breaking: false);
        Assert.True(Of("dugout").ShippedShare(crest) > 0);
        Assert.Equal(0, Of("currach").ShippedShare(crest));
        Assert.Equal(0, Of("outrigger").ShippedShare(crest));
    }

    [Fact]
    public void TheCurrachRidesOverABreakerThatBoardsTheDugoutAndWashesOverTheRaft()
    {
        var crest = waves.Crest(2, breaking: true);
        Assert.Equal(0, Of("currach").ShippedShare(crest));
        Assert.True(Of("dugout").ShippedShare(crest) > 0);
        Assert.True(Of("boat-raft").ShippedShare(crest) > 0);
    }

    [Fact]
    public void TheHigherACrestOverTopsTheSideTheMoreOfItStaysAboard()
    {
        var dugout = Of("dugout");
        Assert.True(dugout.ShippedShare(waves.Crest(3, breaking: true)) > dugout.ShippedShare(waves.Crest(1.5, breaking: true)));
        Assert.InRange(dugout.ShippedShare(waves.Crest(20, breaking: true)), 0, 1);
    }

    [Fact]
    public void ABreakerThatRollsTheDugoutLeavesTheCurrachAndTheOutriggerUpright()
    {
        Assert.True(Of("dugout").Capsizes(1));
        Assert.False(Of("currach").Capsizes(1));
        Assert.False(Of("outrigger").Capsizes(1));
    }

    [Fact]
    public void TheDugoutRollsHarderThanTheCurrachAndTheCurrachHarderThanTheOutrigger()
    {
        Assert.True(Of("dugout").RollResponse > Of("currach").RollResponse);
        Assert.True(Of("currach").RollResponse > Of("outrigger").RollResponse);
    }

    [Fact]
    public void ACraftWithNoSeaworthinessDataNeverCapsizesAndRollsAsVanillaDoes()
    {
        var unknown = Seaworthiness.From(new JsonObject(new JObject()));
        Assert.False(unknown.Capsizes(100));
        Assert.Equal(1, unknown.RollResponse);
        Assert.Equal(1, unknown.PitchResponse);
    }

    [Fact]
    public void AnEmptyBilgeLeavesHerAsSheIs()
    {
        var dugout = Fixtures.Seaworthiness("dugout");
        Assert.Equal(dugout, dugout.Laden(0, swamping));
    }

    [Fact]
    public void AFullBilgeLeavesHerAwashSoEveryCrestComesAboard()
    {
        var full = Fixtures.Afloat("currach", litres: 1500);
        Assert.Equal(0, full.FreeboardMetres);
        Assert.Equal(1, full.ShippedShare(waves.Crest(0.5, breaking: false)));
    }

    [Fact]
    public void WaterAboardMakesHerRollMoreAndGoOverToASmallerBreaker()
    {
        var dry = Of("dugout");
        var half = dry.Laden(0.5, swamping);
        Assert.True(half.RollResponse > dry.RollResponse);
        Assert.True(half.CapsizeBreakerMetres < dry.CapsizeBreakerMetres);
        Assert.False(Of("currach").Capsizes(1.5));
        Assert.True(Of("currach").Laden(1, swamping).Capsizes(1.5));
    }

    [Fact]
    public void WaterAboardSlowsALightHullMoreThanAHeavyOne()
    {
        Assert.Equal(1, swamping.SpeedFactor(470, 470));
        Assert.InRange(swamping.SpeedFactor(470, 695), 0.9, 0.95);
        Assert.InRange(swamping.SpeedFactor(85, 210), 0.8, 0.85);
    }

    [Fact]
    public void ALoadedBoatIsSlowerWithADryBilge()
    {
        var dugout = Fixtures.Hull("dugout");
        var loaded = dugout.Crewed(bodies: 130, kit: 70).Total;

        Assert.True(swamping.SpeedFactor(dugout.ReferenceKg, loaded) < 0.95);
    }

    [Fact]
    public void WaterAboardLowersHerSideUntilAFullBilgeBringsItToTheWater()
    {
        Assert.True(Fixtures.Afloat("dugout", litres: 225).FreeboardMetres < Of("dugout").FreeboardMetres);
        Assert.InRange(Fixtures.Afloat("dugout", litres: 449).FreeboardMetres, 0, 0.01);
    }

    private static Seaworthiness Of(string vessel) => Fixtures.Afloat(vessel);
}
