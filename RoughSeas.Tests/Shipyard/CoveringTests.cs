using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Shipyard;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Shipyard;

public class CoveringTests
{
    private static readonly JToken Entity = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vessel", "coracle.json")));
    private static readonly JObject Shape = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "coracle.json")));

    private readonly TreeAttribute tree = new();

    private Framing Coracle()
    {
        var config = JsonConvert.DeserializeObject<CoveringConfig>(Entity["behaviorConfigs"]![EntityBehaviorCovering.Code]!.ToString())!;
        config.Measure(Shape);
        tree.SetInt(config.Stages[0].Group, config.Stages[0].Pieces);
        tree.SetInt(config.Stages[1].Group, 1);
        return new Framing(() => tree, () => { }, config.Stages);
    }

    private static void LayUpTo(Framing coracle, string group)
    {
        while (coracle.Current is { } stage && stage.Group != group) coracle.Lay("oak", 0);
    }

    [Fact]
    public void HerLatticeLiesFlatUntilHerGunwaleBendsItUp()
    {
        var coracle = Coracle();
        LayUpTo(coracle, "Gunwale");

        Assert.DoesNotContain("Lattice1", coracle.Hidden());
        Assert.Contains("LatticeBent1", coracle.Hidden());

        coracle.Lay("oak", 0);

        Assert.Contains("Lattice1", coracle.Hidden());
        Assert.DoesNotContain("LatticeBent1", coracle.Hidden());
    }

    [Fact]
    public void AHideCoversAsManySectionsAsItIsWorthAndNoMoreThanAreLeft()
    {
        var coracle = Coracle();
        LayUpTo(coracle, "Cover");
        var cover = coracle.Current!;

        coracle.Lay(null, 0, pieces: 5);

        Assert.Equal(cover.Pieces, coracle.Laid(cover));
        Assert.Equal("Lacing", coracle.Current?.Group);
    }

    [Theory]
    [InlineData(0, new string[0])]
    [InlineData(1, new[] { "small" })]
    [InlineData(3, new[] { "large" })]
    [InlineData(4, new[] { "large", "small" })]
    public void WhatAHideHasLeftOverComesBackAsTheHidesItStillMakes(int sections, string[] hides) =>
        Assert.Equal(hides, HideLaying.Remainder(sections));

    [Fact]
    public void ACoracleFromBeforeTheBuildHasHerLatticeBent()
    {
        var finished = new Framing(() => null, () => { }, Coracle().Stages);

        Assert.True(finished.Finished(0));
        Assert.Contains("Lattice1", finished.Hidden());
        Assert.DoesNotContain("LatticeBent1", finished.Hidden());
    }
}
