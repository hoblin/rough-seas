using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class ReedsTests
{
    [Fact]
    public void ABundleLyingAloneIsOfItsOwnPlant() =>
        Assert.Equal(["papyrustops"], Reeds.Of(new Dictionary<string, string> { [Reeds.Port] = "papyrustops", [Reeds.Starboard] = Reeds.None }));

    [Fact]
    public void TwoBundlesOfOnePlantAreOfThatPlantOnce() =>
        Assert.Equal(["thatch"], Reeds.Of(new Dictionary<string, string> { [Reeds.Port] = "thatch", [Reeds.Starboard] = "thatch" }));

    [Fact]
    public void TwoBundlesOfDifferentPlantsAreOfBothPortFirst() =>
        Assert.Equal(["cattailtops", "papyrustops"], Reeds.Of(new Dictionary<string, string> { [Reeds.Port] = "cattailtops", [Reeds.Starboard] = "papyrustops" }));

    [Fact]
    public void AVesselOfNoBundlesIsOfNoPlant() =>
        Assert.Empty(Reeds.Of(new Dictionary<string, string> { ["wood"] = "oak" }));
}
