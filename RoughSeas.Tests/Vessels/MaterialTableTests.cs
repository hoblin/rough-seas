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
            ["wood"] = new() { Strength = 90 },
            ["wood-oak"] = new() { Strength = 100 },
            ["wood-ebony"] = new() { Strength = 160 },
            ["wood-elm"] = new() { Strength = 65 },
            ["wood-wildcrafttree:cedar"] = new() { Strength = 50 },
            ["wood-cedar"] = new() { Strength = 80 },
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
        var vanilla = VanillaWoods.Select(wood => shipped["wood-" + wood].Strength).Order().ToArray();

        Assert.Equal((vanilla[5] + vanilla[6]) / 2, shipped["wood"].Strength, tolerance: 0.1);
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

    private static VesselPart Part(string material, string reference) => new() { Material = material, Reference = reference };

    private class CountingLogger : LoggerBase
    {
        public int Warnings { get; private set; }

        protected override void LogImpl(EnumLogType logType, string format, params object[] args)
        {
            if (logType == EnumLogType.Warning) Warnings++;
        }
    }
}
