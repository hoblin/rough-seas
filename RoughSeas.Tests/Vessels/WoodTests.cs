using RoughSeas.Vessels;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Vessels;

public class WoodTests
{
    [Fact]
    public void ReadsAVanillaWoodFromItsBareSpecies()
    {
        Assert.Equal(new Wood("game", "oak"), Wood.Parse("oak"));
    }

    [Fact]
    public void ReadsAnotherModsWoodWithItsDomain()
    {
        Assert.Equal(new Wood("wildcrafttree", "elm"), Wood.Parse("wildcrafttree-elm"));
    }

    [Theory]
    [InlineData("oak")]
    [InlineData("wildcrafttree-elm")]
    public void WritesTheCodeItWasReadFrom(string code)
    {
        Assert.Equal(code, Wood.Parse(code).Code);
    }

    [Fact]
    public void FindsItsOwnLogAndPlankInItsOwnDomain()
    {
        var elm = Wood.Parse("wildcrafttree-elm");

        Assert.Equal(new AssetLocation("wildcrafttree:log-placed-elm-ud"), elm.Locate(new AssetLocation("{log}")));
        Assert.Equal(new AssetLocation("wildcrafttree:plank-elm"), elm.Locate(new AssetLocation("{plank}")));
    }

    [Fact]
    public void LeavesACodeThatNamesNoPartOfItAsItIs()
    {
        Assert.Equal(new AssetLocation("game:firewood"), Wood.Parse("oak").Locate(new AssetLocation("game:firewood")));
    }

    [Fact]
    public void LooksItselfUpByItsDomainBeforeItsSpecies()
    {
        Assert.Equal(["wildcrafttree:cedar", "cedar"], Wood.Parse("wildcrafttree-cedar").Keys);
    }
}
