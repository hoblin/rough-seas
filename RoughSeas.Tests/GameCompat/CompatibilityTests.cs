using GameCompat;
using Mono.Cecil.Cil;
using Newtonsoft.Json.Linq;
using Xunit;

namespace RoughSeas.Tests.GameCompat;

public class CompatibilityTests
{
    private static JToken Boat() => JToken.Parse("""{ "behaviors": [{ "code": "a" }], "attributes": { "weight": 1 } }""");

    [Fact]
    public void AnAddUnderAMissingParentFails() =>
        Assert.Equal("its parent is missing", AssetPatchCheck.Apply(Boat(), "add", "/server/behaviors/-", new JValue(1)));

    [Fact]
    public void AnAppendToAnArrayPasses() =>
        Assert.Null(AssetPatchCheck.Apply(Boat(), "add", "/behaviors/-", new JValue(1)));

    [Fact]
    public void AnIndexPastTheEndFails() =>
        Assert.NotNull(AssetPatchCheck.Apply(Boat(), "add", "/behaviors/5", new JValue(1)));

    [Fact]
    public void AReplaceOfAMissingKeyFails() =>
        Assert.Equal("it is missing", AssetPatchCheck.Apply(Boat(), "replace", "/attributes/durability", new JValue(1)));

    [Fact]
    public void ALaterPatchSeesWhatAnEarlierOneAdded()
    {
        var boat = Boat();
        AssetPatchCheck.Apply(boat, "add", "/behaviorConfigs", new JObject());
        Assert.Null(AssetPatchCheck.Apply(boat, "add", "/behaviorConfigs/drift", new JValue(1)));
    }

    [Fact]
    public void TheFloorIsTheLowestVersionAboveTheHighestBrokenOne()
    {
        var compatible = new[] { "1.22.0", "1.22.4", "1.22.5" }.Select(Version.Parse);
        var incompatible = new[] { "1.22.3" }.Select(Version.Parse);
        Assert.Equal(Version.Parse("1.22.4"), Walk.Floor(compatible, incompatible));
    }

    [Fact]
    public void ThereIsNoFloorWhenNothingAboveTheBrokenVersionPasses() =>
        Assert.Null(Walk.Floor([], [Version.Parse("1.22.7")]));

    [Fact]
    public void LocalNumberingIsNotAChange()
    {
        Assert.Null(BehaviourCheck.Token(OpCodes.Ldloc_0, null));
        Assert.Null(BehaviourCheck.Token(OpCodes.Stloc_3, null));
    }

    [Fact]
    public void ABranchIsComparedWithoutItsOffset() =>
        Assert.Equal(BehaviourCheck.Token(OpCodes.Br, Instruction.Create(OpCodes.Nop)), BehaviourCheck.Token(OpCodes.Br_S, Instruction.Create(OpCodes.Ret)));

    [Fact]
    public void AChangedConstantIsAChange() =>
        Assert.NotEqual(BehaviourCheck.Token(OpCodes.Ldc_R4, 1.25f), BehaviourCheck.Token(OpCodes.Ldc_R4, 1.5f));
}
