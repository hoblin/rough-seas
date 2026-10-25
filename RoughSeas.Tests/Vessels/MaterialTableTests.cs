using Newtonsoft.Json;
using RoughSeas.Vessels;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Vessels;

public class MaterialTableTests
{
    private static readonly string[] VanillaWoods =
        ["birch", "oak", "maple", "pine", "acacia", "kapok", "baldcypress", "larch", "redwood", "ebony", "walnut", "purpleheart"];

    private readonly CountingLogger logger = new();
    private readonly MaterialTable table;

    public MaterialTableTests()
    {
        table = new MaterialTable(new Dictionary<string, Material>
        {
            ["wood"] = new() { Strength = 90, Density = 610, Toughness = 60000 },
            ["wood-oak"] = new() { Strength = 100, Density = 600, Toughness = 75000 },
            ["wood-ebony"] = new() { Strength = 160, Density = 1200 },
            ["hide"] = new() { Density = 1000 },
            ["wood-elm"] = new() { Strength = 65 },
            ["wood-wildcrafttree:cedar"] = new() { Strength = 50 },
            ["wood-cedar"] = new() { Strength = 80 },
            ["wood-walnut"] = new() { Strength = 110 },
            ["wood-game:walnut"] = new() { Strength = 120 },
            ["rope-flax"] = new() { Strength = 11 },
            ["rope-bast"] = new() { Strength = 5.5 }
        }, logger);
    }

    [Fact]
    public void ScalesTheCapacityByEveryPartAgainstItsReference()
    {
        var parts = new[] { Part("wood-ebony", "wood-oak"), Part("rope-bast", "rope-flax") };

        Assert.Equal(0.8, table.StrengthFactor(parts), 6);
    }

    [Fact]
    public void KnowsAnotherModsWoodByItsSpecies()
    {
        Assert.Equal(0.65, table.StrengthFactor([Part("wood-wildcrafttree-elm", "wood-oak")]), 6);
    }

    [Fact]
    public void KnowsAWoodByItsDomainWhereItsSpeciesNameMeansAnotherTree()
    {
        Assert.Equal(0.5, table.StrengthFactor([Part("wood-wildcrafttree-cedar", "wood-oak")]), 6);
        Assert.Equal(0.8, table.StrengthFactor([Part("wood-othermod-cedar", "wood-oak")]), 6);
    }

    [Fact]
    public void KnowsAVanillaWoodByItsDomainBeforeItsSpecies()
    {
        Assert.Equal(1.2, table.StrengthFactor([Part("wood-walnut", "wood-oak")]), 6);
    }

    [Fact]
    public void GivesAWoodItDoesNotKnowTheStrengthOfAnUnknownWood()
    {
        Assert.Equal(0.9, table.StrengthFactor([Part("wood-wildcrafttree-saxaul", "wood-oak")]), 6);
    }

    [Fact]
    public void WarnsOnceForEachWoodItDoesNotKnow()
    {
        table.StrengthFactor([Part("wood-wildcrafttree-saxaul", "wood-oak")]);
        table.StrengthFactor([Part("wood-wildcrafttree-saxaul", "wood-oak")]);
        table.StrengthFactor([Part("wood-wildcrafttree-ginkgo", "wood-oak")]);

        Assert.Equal(2, logger.Warnings);
    }

    [Fact]
    public void ShipsTheMedianOfTheVanillaWoodsAsTheStrengthOfAnUnknownWood()
    {
        var shipped = JsonConvert.DeserializeObject<Dictionary<string, Material>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "materials.json")))!;
        var vanilla = VanillaWoods.Select(wood => shipped["wood-" + wood].Strength!.Value).Order().ToArray();

        Assert.Equal((vanilla[5] + vanilla[6]) / 2, shipped["wood"].Strength!.Value, tolerance: 0.1);
    }

    [Fact]
    public void ShipsTheMedianOfTheVanillaWoodsAsTheDensityOfAnUnknownWood()
    {
        var shipped = JsonConvert.DeserializeObject<Dictionary<string, Material>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "materials.json")))!;
        var vanilla = VanillaWoods.Select(wood => shipped["wood-" + wood].Density!.Value).Order().ToArray();

        Assert.Equal((vanilla[5] + vanilla[6]) / 2, shipped["wood"].Density!.Value, tolerance: 0.1);
    }

    [Fact]
    public void KnowsAWoodsToughness()
    {
        Assert.Equal(75000, table.Toughness("wood-oak"));
    }

    [Fact]
    public void GivesAWoodNotMeasuredForToughnessThatOfAnUnknownWood()
    {
        Assert.Equal(60000, table.Toughness("wood-ebony"));
    }

    [Fact]
    public void SourcesEveryShippedToughness()
    {
        var shipped = JsonConvert.DeserializeObject<Dictionary<string, Material>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "materials.json")))!;

        Assert.All(shipped.Where(row => row.Value.Toughness != null), row => Assert.False(string.IsNullOrEmpty(row.Value.ToughnessSource), row.Key));
        Assert.NotNull(shipped["wood"].Toughness);
    }

    [Fact]
    public void GivesEveryShippedWoodADensityWithItsSource()
    {
        var shipped = JsonConvert.DeserializeObject<Dictionary<string, Material>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "materials.json")))!;

        Assert.All(shipped, row =>
        {
            Assert.True(row.Value.Density > 0, row.Key);
            Assert.False(string.IsNullOrEmpty(row.Value.DensitySource), row.Key);
        });
    }

    [Fact]
    public void WeighsAHullOfAnotherWoodByItsDensityInTheSameShape()
    {
        var ebony = table.Build([Part("wood-ebony", "wood-oak")], 400);
        var oak = table.Build([Part("wood-oak", "wood-oak")], 400);

        Assert.Equal(400 * 1200.0 / 600, ebony.Kilograms, 6);
        Assert.Equal(oak.Litres, ebony.Litres, 6);
        Assert.Equal(400 / 600.0 * 1000, oak.Litres, 6);
    }

    [Fact]
    public void BuildsAHullOfSeveralMaterialsByTheirShares()
    {
        var build = table.Build([Part("hide", "hide", 2), Part("wood-oak", "wood-oak", 3)], 100);

        Assert.Equal(100, build.Kilograms, 6);
        Assert.Equal((40 / 1000.0 + 60 / 600.0) * 1000, build.Litres, 6);
    }

    [Fact]
    public void WeighsAHullWithNoPartsAsAnUnknownWood()
    {
        var build = table.Build([], 61);

        Assert.Equal(61, build.Kilograms, 6);
        Assert.Equal(100, build.Litres, 6);
    }

    [Fact]
    public void WeighsAMaterialItDoesNotKnowAsAnUnknownWoodAndWarnsOnce()
    {
        table.Build([Part("hide-modded", "hide-modded")], 61);
        table.Build([Part("hide-modded", "hide-modded")], 61);

        Assert.Equal(100, table.Build([Part("hide-modded", "hide-modded")], 61).Litres, 6);
        Assert.Equal(1, logger.Warnings);
    }

    [Fact]
    public void CountsAnUnknownMaterialAsItsReference()
    {
        Assert.Equal(1, table.StrengthFactor([Part("hide-modded", "wood-oak")]));
    }

    [Fact]
    public void GivesARopesBreakingLoad()
    {
        Assert.Equal(11, table.BreakingKilonewtons("rope-flax"));
    }

    [Fact]
    public void CountsARopeItDoesNotKnowAsUnbreakable()
    {
        Assert.Equal(double.PositiveInfinity, table.BreakingKilonewtons("rope-modded"));
    }

    private static VesselPart Part(string material, string reference, double share = 1) => new() { Material = material, Reference = reference, Share = share };

    [Fact]
    public void WoodNotYetCutOutWeighsAsTheHullItBelongsTo()
    {
        var blank = new HullBuild(400, 500).WithUncut(450);

        Assert.Equal(950, blank.Litres);
        Assert.Equal(400.0 / 500, blank.Kilograms / blank.Litres, 9);
    }
}
