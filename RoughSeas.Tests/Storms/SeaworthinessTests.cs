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
        var dugout = Of("dugout");
        Assert.Equal(dugout, dugout.Laden(0, swamping));
    }

    [Fact]
    public void AFullBilgeLeavesHerAwashSoEveryCrestComesAboard()
    {
        var full = Of("currach").Laden(1, swamping);
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

    private static Seaworthiness Of(string vessel) => Fixtures.Seaworthiness(vessel);
}
